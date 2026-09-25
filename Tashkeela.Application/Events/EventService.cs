using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Application.Teams;
using Tashkeela.Domain.Events;

namespace Tashkeela.Application.Events;

/// <summary>
/// Team schedules (FR-EVT-1, 2, 5, 6, 7). Writes load the <see cref="TeamEvent"/> and let it decide; reads project
/// into response DTOs in one SQL statement, nothing tracked.
/// </summary>
public sealed class EventService(IAppDbContext db, TimeProvider time)
{
    /// <summary>A team's schedule, soonest first. Default: events that haven't ended yet, cancelled ones excluded.</summary>
    public async Task<PagedResult<EventResponse>> ListForTeamAsync(
        Guid callerId, Guid teamId, DateTimeOffset? from, DateTimeOffset? to, bool includeCancelled, PageQuery page, CancellationToken ct)
    {
        var caller = await db.RequireMemberAsync(teamId, callerId, ct);
        var fromUtc = (from ?? time.GetUtcNow()).ToUniversalTime();

        var events = db.Events.Where(e => e.TeamId == teamId && e.EndsAtUtc >= fromUtc);
        if (to is { } until)
        {
            var toUtc = until.ToUniversalTime();
            events = events.Where(e => e.StartsAtUtc < toUtc);
        }

        if (!includeCancelled)
        {
            events = events.Where(e => e.Status == EventStatus.Scheduled);
        }

        return await Summaries(events.OrderBy(e => e.StartsAtUtc).ThenBy(e => e.Id), caller.MembershipId).ToPagedResultAsync(page, ct);
    }

    /// <summary>FR-EVT-5: the event with counts, plus the names of active members per status.</summary>
    public async Task<EventDetailsResponse> GetAsync(Guid callerId, Guid eventId, CancellationToken ct)
    {
        var caller = await db.RequireEventMemberAsync(eventId, callerId, ct);
        var summary = await SummaryAsync(eventId, caller.Membership.MembershipId, ct);

        var attendees = await (
                from r in db.Rsvps
                join m in db.TeamMemberships on r.MembershipId equals m.Id
                join u in db.Users on m.UserId equals u.Id
                where r.EventId == eventId && m.LeftAtUtc == null
                orderby u.DisplayName, m.Id
                select new { r.Status, Attendee = new AttendeeResponse(m.Id, u.DisplayName, r.UpdatedAtUtc) })
            .ToListAsync(ct);

        List<AttendeeResponse> With(RsvpStatus status) => [.. attendees.Where(a => a.Status == status).Select(a => a.Attendee)];
        return new EventDetailsResponse(summary, new AttendanceResponse(
            With(RsvpStatus.In), With(RsvpStatus.Out), With(RsvpStatus.Maybe), With(RsvpStatus.NoReply)));
    }

    /// <summary>FR-EVT-1/2: a Manager schedules an event; every current member starts as NoReply.</summary>
    public async Task<EventResponse> CreateAsync(Guid callerId, Guid teamId, EventRequest request, CancellationToken ct)
    {
        var caller = await db.RequireManagerAsync(teamId, callerId, ct);
        var now = time.GetUtcNow();
        if (request.StartsAt <= now)
        {
            throw new InvalidRequestException("startsAt", "Must be in the future."); // editing a past event stays allowed
        }

        var memberIds = await db.TeamMemberships
            .Where(m => m.TeamId == teamId && m.LeftAtUtc == null)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var ev = TeamEvent.Schedule(teamId, request.ToDetails(), memberIds, now);
        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return await SummaryAsync(ev.Id, caller.MembershipId, ct);
    }

    /// <summary>FR-EVT-7: a Manager edits an event (409 once cancelled).</summary>
    public async Task<EventResponse> UpdateAsync(Guid callerId, Guid eventId, EventRequest request, CancellationToken ct)
    {
        var caller = await db.RequireEventManagerAsync(eventId, callerId, ct);
        var ev = await db.Events.SingleAsync(e => e.Id == eventId, ct);

        ev.Update(request.ToDetails());
        await db.SaveChangesAsync(ct);

        return await SummaryAsync(ev.Id, caller.Membership.MembershipId, ct);
    }

    /// <summary>FR-EVT-7: a Manager cancels an event. Idempotent.</summary>
    public async Task CancelAsync(Guid callerId, Guid eventId, CancellationToken ct)
    {
        await db.RequireEventManagerAsync(eventId, callerId, ct);
        var ev = await db.Events.SingleAsync(e => e.Id == eventId, ct);

        ev.Cancel(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    private Task<EventResponse> SummaryAsync(Guid eventId, Guid callerMembershipId, CancellationToken ct) =>
        Summaries(db.Events.Where(e => e.Id == eventId), callerMembershipId).SingleAsync(ct);

    // One SQL statement with a COUNT subquery per status. Order `events` before calling: EF can't order a
    // constructor projection.
    private IQueryable<EventResponse> Summaries(IQueryable<TeamEvent> events, Guid callerMembershipId)
    {
        var activeRsvps = db.Rsvps.Where(r => db.TeamMemberships.Any(m => m.Id == r.MembershipId && m.LeftAtUtc == null));

        return events.Select(e => new EventResponse(
            e.Id, e.TeamId, e.Type, e.Title, e.StartsAtUtc, e.EndsAtUtc, e.Location, e.Opponent, e.Notes,
            e.RsvpDeadlineUtc, e.MinPlayers, e.Status,
            new RsvpCounts(
                activeRsvps.Count(r => r.EventId == e.Id && r.Status == RsvpStatus.In),
                activeRsvps.Count(r => r.EventId == e.Id && r.Status == RsvpStatus.Out),
                activeRsvps.Count(r => r.EventId == e.Id && r.Status == RsvpStatus.Maybe),
                activeRsvps.Count(r => r.EventId == e.Id && r.Status == RsvpStatus.NoReply)),
            db.Rsvps.Where(r => r.EventId == e.Id && r.MembershipId == callerMembershipId)
                .Select(r => (RsvpStatus?)r.Status)
                .FirstOrDefault()));
    }
}
