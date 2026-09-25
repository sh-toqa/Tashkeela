using System.ComponentModel.DataAnnotations;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Events;

namespace Tashkeela.Application.Events;

/// <summary>
/// Body for scheduling or editing an event. Times are ISO 8601 with any offset; responses are UTC (NFR-USE-4).
/// One request type for create and update because the fields and their rules are identical.
/// </summary>
public sealed record EventRequest : IValidatableObject
{
    [Required]
    public EventType? Type { get; init; }

    [Required, StringLength(TeamEvent.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? StartsAt { get; init; }

    [Required]
    public DateTimeOffset? EndsAt { get; init; }

    [Required, StringLength(TeamEvent.LocationMaxLength)]
    public string Location { get; init; } = string.Empty;

    [StringLength(TeamEvent.OpponentMaxLength)]
    public string? Opponent { get; init; }

    [StringLength(TeamEvent.NotesMaxLength)]
    public string? Notes { get; init; }

    /// <summary>Defaults to <see cref="StartsAt"/>; can't be after it.</summary>
    public DateTimeOffset? RsvpDeadline { get; init; }

    /// <summary>FR-EVT-6: the event is flagged when fewer members are In. 0 means no minimum.</summary>
    [Range(0, TeamEvent.MaxMinPlayers)]
    public int MinPlayers { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsAt <= StartsAt)
        {
            yield return ValidationError.For(nameof(EndsAt), "Must be after startsAt.");
        }

        if (RsvpDeadline > StartsAt)
        {
            yield return ValidationError.For(nameof(RsvpDeadline), "Can't be after startsAt.");
        }
    }

    internal EventDetails ToDetails() =>
        new(Type!.Value, Title, StartsAt!.Value, EndsAt!.Value, Location, Opponent, Notes, RsvpDeadline, MinPlayers);
}

public sealed record RsvpRequest
{
    /// <summary>In, Out or Maybe (NoReply is the starting state, not an answer).</summary>
    [Required, AllowedValues(RsvpStatus.In, RsvpStatus.Out, RsvpStatus.Maybe, ErrorMessage = "Must be In, Out or Maybe.")]
    public RsvpStatus? Status { get; init; }
}

/// <summary>FR-EVT-5: counts of *active* members per status (former members' RSVPs are kept as history only).</summary>
public sealed record RsvpCounts(int In, int Out, int Maybe, int NoReply);

public sealed record EventResponse(
    Guid Id,
    Guid TeamId,
    EventType Type,
    string Title,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Location,
    string? Opponent,
    string? Notes,
    DateTimeOffset RsvpDeadlineUtc,
    int MinPlayers,
    EventStatus Status,
    RsvpCounts Counts,
    RsvpStatus? MyRsvp)
{
    /// <summary>FR-EVT-6.</summary>
    public bool IsBelowMinimum => Counts.In < MinPlayers;
}

public sealed record AttendeeResponse(Guid MembershipId, string DisplayName, DateTimeOffset? RespondedAtUtc);

public sealed record AttendanceResponse(
    IReadOnlyList<AttendeeResponse> In,
    IReadOnlyList<AttendeeResponse> Out,
    IReadOnlyList<AttendeeResponse> Maybe,
    IReadOnlyList<AttendeeResponse> NoReply);

public sealed record EventDetailsResponse(EventResponse Event, AttendanceResponse Attendance);

public sealed record RsvpResponse(Guid EventId, Guid MembershipId, RsvpStatus Status, DateTimeOffset? UpdatedAtUtc);
