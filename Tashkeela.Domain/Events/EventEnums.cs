namespace Tashkeela.Domain.Events;

public enum EventType
{
    Game,
    Practice,
    Other,
}

public enum EventStatus
{
    Scheduled,
    Cancelled,
}

/// <summary>A member's attendance answer (SRS §1.3). NoReply is the starting state, never a valid answer.</summary>
public enum RsvpStatus
{
    NoReply,
    In,
    Out,
    Maybe,
}
