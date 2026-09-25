namespace Tashkeela.Domain.Events;

/// <summary>
/// The editable description of an event (FR-EVT-1), shared by scheduling and editing.
/// Times may carry any offset; they are stored as UTC.
/// </summary>
public sealed record EventDetails(
    EventType Type,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Location,
    string? Opponent,
    string? Notes,
    DateTimeOffset? RsvpDeadline,
    int MinPlayers);

/// <summary>
/// A game, practice or other team event, with its members' RSVPs (SRS "Event"; renamed to avoid clashing with the C#
/// keyword). The aggregate root for RSVPs because the RSVP rules — deadline (BR-2), cancelled events (BR-3), Manager
/// override (FR-EVT-4) — all depend on the event's own state.
/// </summary>
public sealed class TeamEvent
{
    public const int TitleMaxLength = 100;
    public const int LocationMaxLength = 200;
    public const int OpponentMaxLength = 100;
    public const int NotesMaxLength = 2000;
    public const int MaxMinPlayers = 100;

    private readonly List<Rsvp> _rsvps = [];

    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public EventType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public string Location { get; private set; } = null!;
    public string? Opponent { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset RsvpDeadlineUtc { get; private set; }

    /// <summary>FR-EVT-6: 0 means no minimum.</summary>
    public int MinPlayers { get; private set; }

    public EventStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>The loaded RSVPs. The application loads only the ones it changes.</summary>
    public IReadOnlyCollection<Rsvp> Rsvps => _rsvps;

    public bool IsCancelled => Status == EventStatus.Cancelled;

    private TeamEvent()
    {
        // For EF Core.
    }

    /// <summary>FR-EVT-1/2: every current member starts with a NoReply RSVP.</summary>
    public static TeamEvent Schedule(Guid teamId, EventDetails details, IEnumerable<Guid> membershipIds, DateTimeOffset nowUtc)
    {
        var ev = new TeamEvent { Id = Guid.NewGuid(), TeamId = teamId, Status = EventStatus.Scheduled, CreatedAtUtc = nowUtc };
        ev.Apply(details);
        foreach (var membershipId in membershipIds.Distinct())
        {
            ev._rsvps.Add(new Rsvp(ev.Id, membershipId));
        }

        return ev;
    }

    /// <summary>FR-EVT-7.</summary>
    public void Update(EventDetails details)
    {
        EnsureNotCancelled("edited");
        Apply(details);
    }

    /// <summary>FR-EVT-7. Idempotent.</summary>
    public void Cancel(DateTimeOffset nowUtc)
    {
        if (IsCancelled)
        {
            return;
        }

        Status = EventStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    /// <summary>BR-4: someone who joined after the event was scheduled.</summary>
    public void AddAttendee(Guid membershipId)
    {
        if (_rsvps.All(r => r.MembershipId != membershipId))
        {
            _rsvps.Add(new Rsvp(Id, membershipId));
        }
    }

    /// <summary>FR-EVT-3 / BR-2 / BR-3: a member answers for themselves before the deadline.</summary>
    public Rsvp Respond(Guid membershipId, RsvpStatus answer, Guid actorUserId, DateTimeOffset nowUtc)
    {
        EnsureNotCancelled("answered");
        if (nowUtc >= RsvpDeadlineUtc)
        {
            throw new DomainException("The RSVP deadline has passed. Ask a team manager to update your RSVP.");
        }

        return SetRsvp(membershipId, answer, actorUserId, nowUtc);
    }

    /// <summary>FR-EVT-4 / BR-2: a Manager sets any member's RSVP, even after the deadline (not on cancelled events).</summary>
    public Rsvp Override(Guid membershipId, RsvpStatus answer, Guid managerUserId, DateTimeOffset nowUtc)
    {
        EnsureNotCancelled("answered");
        return SetRsvp(membershipId, answer, managerUserId, nowUtc);
    }

    private Rsvp SetRsvp(Guid membershipId, RsvpStatus answer, Guid actorUserId, DateTimeOffset nowUtc)
    {
        if (answer is not (RsvpStatus.In or RsvpStatus.Out or RsvpStatus.Maybe))
        {
            throw new ArgumentOutOfRangeException(nameof(answer), "An RSVP must be In, Out or Maybe.");
        }

        var rsvp = _rsvps.SingleOrDefault(r => r.MembershipId == membershipId)
            ?? throw new ArgumentException($"Membership {membershipId} has no loaded RSVP for event {Id}.", nameof(membershipId));
        rsvp.Set(answer, actorUserId, nowUtc);
        return rsvp;
    }

    // Guards, not user-facing validation: the API rejects these inputs with a 400 first. They stay here so no caller
    // (a future league fixture creating events, a seeder) can create an event that ends before it starts.
    private void Apply(EventDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Location);
        ArgumentOutOfRangeException.ThrowIfNegative(details.MinPlayers);
        if (details.EndsAt <= details.StartsAt)
        {
            throw new ArgumentException("An event must end after it starts.", nameof(details));
        }

        var deadline = details.RsvpDeadline ?? details.StartsAt;
        if (deadline > details.StartsAt)
        {
            throw new ArgumentException("The RSVP deadline can't be after the event starts.", nameof(details));
        }

        Type = details.Type;
        Title = details.Title.Trim();
        StartsAtUtc = details.StartsAt.ToUniversalTime();
        EndsAtUtc = details.EndsAt.ToUniversalTime();
        Location = details.Location.Trim();
        Opponent = string.IsNullOrWhiteSpace(details.Opponent) ? null : details.Opponent.Trim();
        Notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        RsvpDeadlineUtc = deadline.ToUniversalTime();
        MinPlayers = details.MinPlayers;
    }

    private void EnsureNotCancelled(string action)
    {
        if (IsCancelled)
        {
            throw new DomainException($"This event is cancelled and can't be {action}.");
        }
    }
}
