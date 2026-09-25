using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Application.Teams;
using Tashkeela.Domain.Events;

namespace Tashkeela.Application.Events;

internal sealed record EventCaller(Guid TeamId, CallerMembership Membership);

/// <summary>
/// /events/{id} routes carry no team id: find the event's team, then apply the team membership check. An unknown
/// event and a non-member both get "Event not found", so neither the event nor its team is revealed.
/// </summary>
internal static class EventAccess
{
    public static async Task<EventCaller> RequireEventMemberAsync(this IAppDbContext db, Guid eventId, Guid userId, CancellationToken ct)
    {
        var teamId = await db.Events.Where(e => e.Id == eventId).Select(e => (Guid?)e.TeamId).SingleOrDefaultAsync(ct);
        var membership = teamId is { } id ? await db.FindMembershipAsync(id, userId, ct) : null;
        return membership is null ? throw new NotFoundException("Event", eventId) : new EventCaller(teamId!.Value, membership);
    }

    public static async Task<EventCaller> RequireEventManagerAsync(this IAppDbContext db, Guid eventId, Guid userId, CancellationToken ct)
    {
        var caller = await db.RequireEventMemberAsync(eventId, userId, ct);
        TeamAccess.EnsureManager(caller.Membership);
        return caller;
    }

    /// <summary>Loads the event with only the one RSVP being changed (filtered Include), tracked.</summary>
    public static async Task<TeamEvent> LoadWithRsvpOfAsync(this IAppDbContext db, Guid eventId, Guid membershipId, CancellationToken ct)
    {
        var ev = await db.Events
            .Include(e => e.Rsvps.Where(r => r.MembershipId == membershipId))
            .SingleAsync(e => e.Id == eventId, ct);

        // Someone who joined after the event started has no RSVP for it (BR-4 covers future events only).
        return ev.Rsvps.Count == 1 ? ev : throw new NotFoundException("RSVP", membershipId);
    }
}

/// <summary>BR-4: someone who just joined gets a NoReply RSVP for every upcoming scheduled event. The caller saves.</summary>
internal static class UpcomingEvents
{
    public static async Task AddAttendeeAsync(IAppDbContext db, Guid teamId, Guid membershipId, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var upcoming = await db.Events
            .Where(e => e.TeamId == teamId && e.Status == EventStatus.Scheduled && e.StartsAtUtc > nowUtc)
            .ToListAsync(ct);

        foreach (var ev in upcoming)
        {
            ev.AddAttendee(membershipId); // a brand-new membership can't have an RSVP yet
        }
    }
}
