using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Application.Events;
using Tashkeela.Domain.Teams;

namespace Tashkeela.Application.Teams;

/// <summary>FR-TEAM-2/3: invitations by email (single use) or shareable code, and joining a team with one.</summary>
public sealed class InvitationService(IAppDbContext db, IEmailSender email, TimeProvider time)
{
    /// <summary>FR-TEAM-2: a Manager invites by email (emailed, single use) or creates a shareable code (no email).</summary>
    public async Task<InvitationResponse> CreateAsync(Guid callerId, Guid teamId, CreateInvitationRequest request, CancellationToken ct)
    {
        await db.RequireManagerAsync(teamId, callerId, ct);

        var code = SecureToken.Generate();
        var invitation = Invitation.Create(teamId, request.Email, SecureToken.Hash(code), callerId, time.GetUtcNow());
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        if (invitation.Email is { } to)
        {
            var teamName = await db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).SingleAsync(ct);
            await email.SendAsync(InvitationEmail(to, teamName, code, invitation.ExpiresAtUtc), ct);
        }

        return new InvitationResponse(invitation.Id, invitation.Email, code, invitation.ExpiresAtUtc);
    }

    /// <summary>
    /// FR-TEAM-3: the caller joins as a Player. BR-4: they also get a NoReply RSVP for every upcoming event, saved in
    /// the same transaction. That cross-feature step is a direct call, not a domain event: one caller, visible here.
    /// </summary>
    public async Task<TeamResponse> AcceptAsync(Guid callerId, AcceptInvitationRequest request, CancellationToken ct)
    {
        var hash = SecureToken.Hash(request.Code);
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.TokenHash == hash, ct) ?? throw InvalidCode();
        var callerEmail = await db.Users.Where(u => u.Id == callerId).Select(u => u.Email).SingleAsync(ct);
        var now = time.GetUtcNow();

        switch (invitation.Redeem(callerId, callerEmail, now))
        {
            case RedeemResult.Expired or RedeemResult.AlreadyUsed:
                throw InvalidCode();
            case RedeemResult.WrongRecipient:
                throw new ForbiddenException("This invitation was sent to a different email address.");
        }

        var team = await db.LoadRosterAsync(invitation.TeamId, ct);
        var membership = team.AddPlayer(callerId, now); // 409 if already a member
        await UpcomingEvents.AddAttendeeAsync(db, team.Id, membership.Id, now, ct);
        await db.SaveChangesAsync(ct); // the filtered unique index catches a concurrent double join (409)

        return new TeamResponse(
            team.Id, team.Name, team.Sport, team.LogoUrl, team.TimeZoneId,
            team.Memberships.Count(m => m.IsActive), membership.Id, membership.Role);
    }

    private static InvalidRequestException InvalidCode() => new("code", "The invitation code is invalid or has expired.");

    private static EmailMessage InvitationEmail(string to, string teamName, string code, DateTimeOffset expiresAtUtc)
    {
        var expires = expiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        return new EmailMessage(
            to,
            $"You're invited to join {teamName} on Tashkeela",
            $"<p>You're invited to join <strong>{WebUtility.HtmlEncode(teamName)}</strong>. Accept with this code:</p>" +
            $"<pre>{WebUtility.HtmlEncode(code)}</pre><p>It expires on {expires}.</p>",
            $"You're invited to join {teamName}. Accept with this code:\n\nInvitation code: {code}\n\nIt expires on {expires}.");
    }
}
