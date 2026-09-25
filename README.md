# Tashkeela

**Tashkeela** (تشكيلة, "lineup") is a backend API that helps adult amateur sports teams in Egypt manage rosters
and attendance. It replaces WhatsApp groups and spreadsheets.

Built with ASP.NET Core (.NET 10), EF Core and SQL Server. The API is explored through Swagger UI.

## Current version

This version implements the two core features of the [SRS](docs/SRS.md) end to end: the domain model, the
database, the REST API, validation, authorization and tests.

| Feature | What works | SRS |
| --- | --- | --- |
| **Teams** | Create a team (creator becomes Manager). Invite by email or shareable code (7-day expiry). Join with a code. Set member type, jersey number and position. Promote/demote. Remove members. Leave. List my teams. A team can never lose its last Manager. | FR-TEAM-1 … 8, BR-1 |
| **Events** | Schedule games, practices and other events. Every member starts as NoReply. Members RSVP In/Out/Maybe until the deadline, and Managers can override at any time. Counts and names per status, with a below-minimum flag. Edit and cancel. Late joiners get RSVPs for upcoming events. | FR-EVT-1 … 7, BR-2, BR-3, BR-4 |

The remaining requirements (accounts, notifications, fees and payments, chat, leagues) are **planned future
iterations**. Each one is listed with its SRS ID in the **[roadmap](docs/ROADMAP.md)**.

## Run it

Requirements: .NET 10 SDK and Docker.

```bash
cp .env.example .env                    # then set a strong MSSQL_SA_PASSWORD
docker compose up -d                    # SQL Server on localhost:1433

dotnet tool restore                     # dotnet-ef
dotnet user-secrets set "ConnectionStrings:Tashkeela" \
  "Server=localhost,1433;Database=TashkeelaV1;User Id=sa;Password=<your password>;TrustServerCertificate=True" \
  --project Tashkeela.API
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project Tashkeela.API

dotnet ef database update --project Tashkeela.Infrastructure --startup-project Tashkeela.API
dotnet run --project Tashkeela.API      # http://localhost:5280/swagger
```

Secrets live in User Secrets and are never stored in `appsettings.json`. If another SQL Server already uses port
1433, set `SQL_PORT` in `.env` (e.g. `14330`) and use that port in the connection string.

**Trying it in Swagger:** call `POST /api/v1/dev/token` with an email and display name. Click **Authorize** and paste
the `accessToken`. Create a team, invite a second user (sign in again with another email to get their token),
schedule an event and RSVP.

> `POST /api/v1/dev/token` is development scaffolding that stands in for the Accounts feature (FR-AUTH-*, future
> work). It issues a real signed JWT, so every other endpoint runs exactly as it will in production. It is mapped
> only when `DevAuth:Enabled` is true, which `appsettings.Development.json` sets. It does not exist in production.

### Tests

```bash
dotnet test        # 22 unit tests + 32 integration tests (integration tests need Docker)
```

## API

All endpoints are under `/api/v1` and need a bearer token. Errors are RFC 7807 `ProblemDetails` with a `traceId`.

| Method | Route | Who | Notes |
| --- | --- | --- | --- |
| `POST` | `/teams` | any user | 201. The caller becomes Manager |
| `GET` | `/teams` | any user | Teams I belong to, with my role (paged) |
| `GET` | `/teams/{teamId}` | member | |
| `GET` | `/teams/{teamId}/members` | member | Managers first (paged) |
| `PUT` | `/teams/{teamId}/members/{membershipId}` | Manager | Member type, jersey number, position |
| `PUT` | `/teams/{teamId}/members/{membershipId}/role` | Manager | 409 when demoting the last Manager |
| `DELETE` | `/teams/{teamId}/members/{membershipId}` | Manager | 409 when removing the last Manager |
| `POST` | `/teams/{teamId}/leave` | member | 409 for the last Manager |
| `POST` | `/teams/{teamId}/invitations` | Manager | With `email`: single use and emailed. Without: shareable code |
| `POST` | `/invitations/accept` | any user | Code in the body. Joins as Player |
| `GET` | `/teams/{teamId}/events` | member | Upcoming first. `from`, `to`, `includeCancelled`, paging |
| `POST` | `/teams/{teamId}/events` | Manager | 201. Must start in the future |
| `GET` | `/events/{eventId}` | member | Counts and names per RSVP status |
| `PUT` | `/events/{eventId}` | Manager | 409 if cancelled |
| `POST` | `/events/{eventId}/cancel` | Manager | Idempotent |
| `PUT` | `/events/{eventId}/rsvps/me` | member | In/Out/Maybe. 409 after the deadline or if cancelled |
| `PUT` | `/events/{eventId}/rsvps/{membershipId}` | Manager | Override, even after the deadline |

Status codes follow one rule per case. A malformed request gets **400** with field errors. A team you don't belong
to gets **404**, so team ids can't be probed. A member without the Manager role gets **403**. A request that breaks
a business rule or loses a concurrency race gets **409**.

## Architecture

Four projects, and dependencies point inward:

```text
Tashkeela.API ──────────► Tashkeela.Application ──────► Tashkeela.Domain
      │                            ▲
      └──► Tashkeela.Infrastructure┘   (implements Application's two interfaces)
```

| Project | Responsibility | Depends on |
| --- | --- | --- |
| **Domain** | Entities and business rules: `Team` (roster and last-Manager rule), `Invitation`, `TeamEvent` (RSVP rules). Plain C#, with no packages. | nothing |
| **Application** | Use cases: authorize the caller, load entities, call domain methods, save. Also the request/response contracts, validation and read queries. | Domain, EF Core (core package only) |
| **Infrastructure** | SQL Server `AppDbContext`, entity configurations, migrations, and the email adapter. | Application |
| **API** | Thin controllers, JWT authentication, exception-to-ProblemDetails mapping, OpenAPI/Swagger, and the composition root (`Program.cs`). | Application, Infrastructure |

Code is organized by feature inside each project:

```text
Tashkeela.Domain/        Teams/  Team, TeamMembership, Invitation, enums
                         Events/ TeamEvent (+ EventDetails), Rsvp, enums
                         Users/  User          DomainException.cs
Tashkeela.Application/   Teams/  TeamService, InvitationService, TeamContracts, TeamAccess
                         Events/ EventService, RsvpService, EventContracts, EventAccess
                         Users/  UserService   Common/ IAppDbContext, IEmailSender, paging, exceptions
Tashkeela.Infrastructure/Persistence/ AppDbContext, Configurations/, Migrations/    Email/
Tashkeela.API/           Controllers/  Auth/  Errors/  OpenApi/  Program.cs
tests/                   Tashkeela.UnitTests  Tashkeela.IntegrationTests
```

### One request

```text
PUT /api/v1/teams/{teamId}/members/{membershipId}/role   {"role":"Player"}
  → [ApiController] validates ChangeRoleRequest (DataAnnotations)            → 400
  → TeamsController.ChangeRole → TeamService.ChangeRoleAsync(callerId, …)
      db.RequireManagerAsync(teamId, callerId)   caller's membership        → 404 / 403
      db.LoadRosterAsync(teamId)                 Team + active memberships
      team.ChangeRole(member, Player)            last-Manager rule (BR-1)    → 409
      db.SaveChangesAsync()                      Team.Version check          → 409
  → 204 No Content
```

The flow has three hops: controller, service, domain. Go-to-definition works on every one of them.

## Architectural decisions

The rule for this version: **every pattern must solve a problem these two features actually have.**
### EF Core

- Fluent API configuration, one class per entity. Enums are stored as strings, and max lengths come from domain
  constants.
- **Constraints back up the rules.** A filtered unique index `(TeamId, UserId) WHERE LeftAtUtc IS NULL` allows one
  active membership per user per team, which also makes two concurrent joins fail cleanly. Check constraints
  enforce that an event ends after it starts, that the deadline is not after the start, and the jersey number range.
- Indexes back the hot lookups: the membership check on every request, "my teams", and a team's schedule.
- Memberships are never deleted. Leaving sets `LeftAtUtc`, so RSVP history keeps a valid foreign key.
- Reads are projections (nothing tracked). Writes load the entity with a filtered `Include`: the active roster, or
  only the one RSVP being changed.
- Times are `DateTimeOffset` stored in UTC. Requests accept any offset. Each team carries an IANA time zone
  (default `Africa/Cairo`) for display.
- Integration tests migrate a real SQL Server container with the same migrations, so the migrations are tested too.

### Concurrency

Only two places need it, and each one uses the simplest EF Core mechanism:

1. **The Team roster (BR-1).** Two Managers demote each other at the same moment. Each request loads the roster,
   sees that "another Manager remains", and passes the domain check. Without protection, both saves succeed and
   the team has no Manager. `Team.Version` is a concurrency token that every role or membership change increments.
   The second save hits a stale version, gets `DbUpdateConcurrencyException`, and returns 409. Adding a player
   doesn't increment it, so concurrent joins don't conflict.
2. **RSVPs (NFR-REL-3).** A SQL Server `rowversion` on each RSVP. A member and a Manager changing the same RSVP at
   once can't silently overwrite each other.

Event edits are deliberately last-write-wins: only Managers edit them, and a lost edit breaks no invariant. Both
races are covered by tests against SQL Server.

## Testing

- **Unit tests (22):** domain rules with no I/O, including the last-Manager rule, the version bumps, the invitation
  lifecycle, and the RSVP deadline/cancel/override rules. Also architecture checks: the Domain has no
  dependencies, Application doesn't reference Infrastructure, SQL Server or ASP.NET Core, and entities have no
  public setters.
- **Integration tests (32):** the real HTTP pipeline (`WebApplicationFactory`) against SQL Server in Testcontainers.
  They cover the user stories, validation field names, 401/403/404/409 behavior (the Player and Manager rows of
  the authorization matrix), both concurrency races, and the generated OpenAPI document. Only email is faked.

## Known limitations of this version

- Sign-in uses the development token endpoint until Accounts is built (see the roadmap).
- Invitation emails are written to the log. SMTP delivery and event-change notifications come with
  Notifications.
- Someone who joins after an event has started gets no RSVP for it. BR-4 covers future events only.
