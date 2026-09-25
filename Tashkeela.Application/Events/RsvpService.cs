using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;

namespace Tashkeela.Application.Events;

/// <summary>
/// Attendance (FR-EVT-3/4, BR-2/3). Both use cases load the event with the one RSVP being changed and let
/// <see cref="Domain.Events.TeamEvent"/> decide; the RSVP's rowversion turns a concurrent overwrite into a 409 (NFR-REL-3).
/// </summary>
public sealed class RsvpService(IAppDbContext db, TimeProvider time)
{
    /// <summary>FR-EVT-3 / BR-2 / BR-3: set your own RSVP before the deadline.</summary>
    public async Task<RsvpResponse> RespondAsync(Guid callerId, Guid eventId, RsvpRequest request, CancellationToken ct)
    {
        var caller = await db.RequireEventMemberAsync(eventId, callerId, ct);
        var membershipId = caller.Membership.MembershipId;
        var ev = await db.LoadWithRsvpOfAsync(eventId, membershipId, ct);

        var rsvp = ev.Respond(membershipId, request.Status!.Value, callerId, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new RsvpResponse(eventId, membershipId, rsvp.Status, rsvp.UpdatedAtUtc);
    }

    /// <summary>FR-EVT-4: a Manager sets any member's RSVP, even after the deadline.</summary>
    public async Task<RsvpResponse> OverrideAsync(Guid callerId, Guid eventId, Guid membershipId, RsvpRequest request, CancellationToken ct)
    {
        var caller = await db.RequireEventManagerAsync(eventId, callerId, ct);
        var isActiveMember = await db.TeamMemberships.AnyAsync(
            m => m.Id == membershipId && m.TeamId == caller.TeamId && m.LeftAtUtc == null, ct);
        if (!isActiveMember)
        {
            throw new NotFoundException("Member", membershipId); // ids from the URL must belong to this team
        }

        var ev = await db.LoadWithRsvpOfAsync(eventId, membershipId, ct);
        var rsvp = ev.Override(membershipId, request.Status!.Value, callerId, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new RsvpResponse(eventId, membershipId, rsvp.Status, rsvp.UpdatedAtUtc);
    }
}
