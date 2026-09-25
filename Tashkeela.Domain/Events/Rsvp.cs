namespace Tashkeela.Domain.Events;

/// <summary>One member's answer to one event. Created as NoReply; changed only through <see cref="TeamEvent"/>.</summary>
public sealed class Rsvp
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid MembershipId { get; private set; }
    public RsvpStatus Status { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>The member, or a Manager overriding (FR-EVT-4).</summary>
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>NFR-REL-3: concurrent answers to the same RSVP can't silently overwrite each other.</summary>
    public byte[] RowVersion { get; private set; } = [];

    private Rsvp()
    {
        // For EF Core.
    }

    internal Rsvp(Guid eventId, Guid membershipId)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        MembershipId = membershipId;
        Status = RsvpStatus.NoReply;
    }

    internal void Set(RsvpStatus status, Guid actorUserId, DateTimeOffset nowUtc)
    {
        Status = status;
        UpdatedAtUtc = nowUtc;
        UpdatedByUserId = actorUserId;
    }
}
