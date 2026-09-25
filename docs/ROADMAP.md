# Roadmap

Every item below comes from the [SRS](SRS.md). IDs match the SRS so each one can be traced back to its requirement.
The current version covers **Teams** and **Events**. The other requirements are grouped into future iterations,
ordered by what each one depends on.

```text
Implemented in the current version
├── Teams and Rosters       FR-TEAM-1 … FR-TEAM-8, BR-1
└── Events and Attendance   FR-EVT-1 … FR-EVT-7, BR-2, BR-3, BR-4

Future iterations
├── 1. Accounts and Authentication     FR-AUTH-1 … 7
├── 2. Notifications and Reminders     FR-NOT-1 … 7 (+ the "notify" half of FR-EVT-7)
├── 3. Fees and Payments               FR-FEE-1 … 14, Paymob (PAY-IF-1 … 8)
├── 4. Real-time and Team Chat         FR-EVT-8, FR-CHAT-1 … 6, RT-1 … 3
├── 5. Leagues and Standings           FR-LG-1 … 11
├── 6. Operations and platform         observability, health, CI/CD, deployment, demo data
└── 7. Stretch features                SRS §8.4 (Should / Could)
```

---

## Implemented in the current version

### Teams and Rosters (SRS §4.2)

| ID | Requirement | Status |
| --- | --- | --- |
| FR-TEAM-1 | Create a team; the creator becomes Manager | Done |
| FR-TEAM-2 | Invite by email or shareable link; tokens expire after 7 days | Done (email is logged, not sent: see iteration 2) |
| FR-TEAM-3 | Accept an invite and join as Player | Done for signed-in users. "Registering first if needed" arrives with iteration 1 |
| FR-TEAM-4 | Member type (Regular/Spare), jersey number, position | Done |
| FR-TEAM-5 | Promote to Manager, remove a member | Done |
| FR-TEAM-6 | Last Manager can't be removed or demoted | Done (BR-1, also protected against concurrent changes) |
| FR-TEAM-7 | A member can leave | Done |
| FR-TEAM-8 | List my teams with my role | Done |

### Events and Attendance (SRS §4.3)

| ID | Requirement | Status |
| --- | --- | --- |
| FR-EVT-1 | Create an event: type, start and end, location, opponent, notes, RSVP deadline, minimum players | Done |
| FR-EVT-2 | NoReply RSVP for every current member | Done |
| FR-EVT-3 | Member sets In/Out/Maybe until the deadline | Done |
| FR-EVT-4 | Manager overrides any RSVP at any time | Done |
| FR-EVT-5 | Counts and names per status | Done |
| FR-EVT-6 | Flag events below minimum players | Done (`isBelowMinimum`) |
| FR-EVT-7 | Edit or cancel an event **and notify all members** | Edit/cancel done. Notifying members is FR-NOT-4 (iteration 2) |

### Business rules, quality attributes and interfaces covered

- **BR-1** A team always has at least one Manager. **BR-2** Deadline applies to members, not Managers.
  **BR-3** Cancelled events accept no RSVPs. **BR-4** Late joiners get NoReply RSVPs for upcoming events.
- **NFR-SEC-2** Invitation codes stored hashed, single-purpose and expiring. **NFR-SEC-3** Authorization is
  checked against the caller's membership on every request. **NFR-SEC-4** Non-members get 404, not 403.
  **NFR-SEC-10** Phone and email are visible only to members of the same team.
- **NFR-REL-3** Optimistic concurrency on RSVPs (the fee and score parts come with their features).
- **NFR-PERF-4** Reads use no-tracking projections, and lookups are indexed. **NFR-USE-2** Field-level validation
  errors in ProblemDetails. **NFR-USE-4** Times in ISO 8601 UTC, and each team exposes its time zone.
- **API-1** `/api/v1`. **API-3** ProblemDetails with traceId. **API-4** `page`/`pageSize` paging. **API-5** / **UI-1**
  Swagger UI at `/swagger`, with a JWT Authorize button (**UI-4**).
- **NFR-MNT-1** Domain / Application / Infrastructure / API with dependencies pointing inward.
  **NFR-MNT-5** Integration tests run the full HTTP pipeline against SQL Server in Testcontainers.
- Authorization matrix (§7): the Player and Manager columns for *view team, roster, schedule*, *RSVP*,
  *create/edit/cancel events* and *invite/promote/remove members*.

### Deliberate deviation from the SRS

- **NFR-MNT-2** asks for use cases as MediatR commands/queries with FluentValidation. This version uses plain
  application services and DataAnnotations instead. The README's *Architectural decisions* section explains why.
  Neither choice changes any endpoint, so it can be revisited without touching the API contract.
- `POST /invitations/{token}/accept` (SRS §3.2) became `POST /invitations/accept` with the code in the body, so the
  secret never appears in URLs or access logs.

---

## Future iterations

### 1. Accounts and Authentication (SRS §4.1)

Replaces the development-only `POST /api/v1/dev/token` endpoint.

- FR-AUTH-1 Register with name, email, phone and password
- FR-AUTH-2 Email confirmation before first login
- FR-AUTH-3 JWT access token (15 min) and refresh token (7 days)
- FR-AUTH-4 Refresh-token rotation with replay detection (revoke the token family)
- FR-AUTH-5 Password reset with a single-use, 1-hour emailed token
- FR-AUTH-6 Edit profile, language (EN/AR) and notification preferences
- FR-AUTH-7 Revoke the refresh token on logout
- NFR-SEC-1 ASP.NET Core Identity hashing, and a 15-minute lockout after 5 failed logins
- NFR-SEC-6 Rate-limit auth endpoints to 10 requests/minute/IP
- API-2 Rotating refresh tokens, and UI-4 Authorize with the token from `/auth/login`
- FR-TEAM-3 (remaining part): an invitee can register first, then accept

### 2. Notifications and Reminders (SRS §4.4, §3.5, §3.6)

- FR-NOT-1 Reminders to NoReply members 48 h and 24 h before an event (offsets configurable per team)
- FR-NOT-2 Signed one-click In/Out links in reminder emails, usable without login (NFR-USE-5)
- FR-NOT-3 Never send the same reminder twice (ReminderLog entity, §6.1)
- FR-NOT-4 Notify members when an event is created, changed or cancelled (completes FR-EVT-7)
- FR-NOT-5 Notify assigned members when a fee is created or becomes overdue
- FR-NOT-6 Opt out per notification category
- FR-NOT-7 WhatsApp delivery for opted-in users (Should, WA-1, WA-2)
- EM-1 SMTP sender behind the existing `IEmailSender` interface (Mailpit locally), EM-2 templates,
  EM-3 bilingual emails (Should)
- Background jobs: NFR-REL-1 idempotent jobs, NFR-REL-2 retries with exponential backoff

### 3. Fees and Payments (SRS §4.5, §3.4, §6.3, §6.4)

- FR-FEE-1 … FR-FEE-11 (Must): fees with due dates, equal split (BR-5), one assignment per member, Paymob
  card/wallet payments, cash/InstaPay recording, waivers, a daily overdue job, the finance dashboard, receipts,
  and an audit log of every status change
- FR-FEE-12 Fawry (Should), FR-FEE-13 recurring monthly fees (Should), FR-FEE-14 expense tracking (Could)
- PAY-IF-1 … PAY-IF-8 Paymob Intention API, Unified Checkout, HMAC-SHA512 callback verification, idempotency,
  amount checks, secrets handling, refund callbacks
- C-2 … C-6 payment constraints (card data never touches the server, `IPaymentProvider`, integer piasters,
  callback-only status changes, test mode)
- BR-5 leftover piasters in equal splits, BR-6 one in-flight payment per assignment
- NFR-SEC-5 HMAC-only callbacks, NFR-REL-4 reconciliation job, NFR-USE-3 money as piasters plus currency,
  NFR-REL-3 concurrency on fee assignments, NFR-OBS-3 callback logging
- Entities: Fee, FeeAssignment, Payment, ProcessedCallback, AuditLog

### 4. Real-time and Team Chat (SRS §4.6, §3.3)

- FR-EVT-8 Push RSVP changes to connected clients
- FR-CHAT-1 … FR-CHAT-5 One members-only channel per team, persisted real-time messages, cursor paging (50),
  typing indicators, edit/delete rules
- FR-CHAT-6 Polls and pinned bulletins (Should)
- RT-1 … RT-3 SignalR hub at `/hubs/team`, membership-checked groups, server events
- UI-6 Hub documentation and a sample client, since Swagger doesn't cover WebSockets

### 5. Leagues and Standings (SRS §4.7)

- FR-LG-1 … FR-LG-9 (Must): leagues and League Admins, seasons with points rules and divisions, team
  invitations accepted by the team's Manager, fixtures that appear as events on both teams' schedules, scores,
  standings with tie-breakers, recalculation on correction, league announcements
- FR-LG-10 Season registration fees through Paymob (Should), FR-LG-11 Live score updates (Could)
- BR-7 Home and away teams differ and share a division. BR-8 Only Final games count toward standings.
- NFR-PERF-5 Cached standings, NFR-REL-3 concurrency on game scores
- Authorization matrix: the League Admin column. Also the Event.LeagueGameId link (§6.1).

### 6. Operations and platform

- NFR-OBS-1 … NFR-OBS-4 Serilog structured logs, correlation IDs, sensitive-data rules, and an admin-only job
  dashboard
- NFR-REL-5 `/health/live` and `/health/ready`
- NFR-SEC-7 CORS allow-list, NFR-SEC-8 Azure Key Vault, NFR-SEC-9 HTTPS with HSTS (enabled outside Development)
- NFR-MNT-4 80 % Domain/Application coverage, NFR-MNT-6 GitHub Actions CI with a Docker image,
  NFR-MNT-7 ADRs
- NFR-PERF-1 … 3 performance targets verified under load
- NFR-USE-6 English/Arabic API errors (Should)
- UI-3 request/response examples for every endpoint, UI-5 demo accounts in the Swagger description
- System Admin role (§2.4, §7), §8.3 demo data seeder, §8.1 acceptance run on the deployed demo (AC-1 … AC-14)

### 7. Stretch features (SRS §8.4)

- FR-EVT-9 Weekly recurring events
- FR-EVT-10 Spare waitlist with automatic promotion
- FR-EVT-11 iCal feed per team
- WhatsApp notifications, Arabic emails and errors, Fawry, recurring fees, polls and bulletins, season
  registration fees, expense tracking, live scores, Stripe as a second `IPaymentProvider`, a frontend client

Out of scope for v1.0 altogether (SRS §8.5): custom frontends, youth teams, facility booking, public league
websites, refereeing/analytics, payouts, in-app refunds.
