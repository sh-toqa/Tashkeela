using Tashkeela.Domain;
using Tashkeela.Domain.Events;

namespace Tashkeela.UnitTests;

/// <summary>FR-EVT-1..4/7 and BR-2/3/4.</summary>
public sealed class TeamEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Kickoff = Now.AddDays(3);
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static EventDetails Details(DateTimeOffset? deadline = null) =>
        new(EventType.Game, " Friendly ", Kickoff, Kickoff.AddHours(2), "Club pitch", null, null, deadline, 0);

    private static TeamEvent NewEvent(DateTimeOffset? deadline = null) =>
        TeamEvent.Schedule(Guid.NewGuid(), Details(deadline), [Member, Member], Now);

    [Fact]
    public void Scheduling_gives_each_member_one_NoReply_rsvp_and_defaults_the_deadline_to_the_start()
    {
        var ev = NewEvent();

        ev.Rsvps.ShouldHaveSingleItem().Status.ShouldBe(RsvpStatus.NoReply);
        ev.RsvpDeadlineUtc.ShouldBe(Kickoff);
        ev.Title.ShouldBe("Friendly");
    }

    [Fact]
    public void Times_with_any_offset_are_stored_as_utc()
    {
        var cairo = TimeSpan.FromHours(3);
        var start = new DateTimeOffset(2026, 10, 1, 20, 0, 0, cairo);
        var ev = TeamEvent.Schedule(Guid.NewGuid(), Details() with { StartsAt = start, EndsAt = start.AddHours(1) }, [], Now);

        ev.StartsAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        ev.StartsAtUtc.ShouldBe(start);
    }

    [Fact]
    public void A_member_answers_before_the_deadline()
    {
        var ev = NewEvent();

        var rsvp = ev.Respond(Member, RsvpStatus.In, Actor, Now);

        (rsvp.Status, rsvp.UpdatedByUserId, rsvp.UpdatedAtUtc).ShouldBe((RsvpStatus.In, Actor, Now));
    }

    [Fact]
    public void After_the_deadline_only_a_manager_override_works()
    {
        var ev = NewEvent(deadline: Now.AddHours(1));
        var later = Now.AddHours(2);

        Should.Throw<DomainException>(() => ev.Respond(Member, RsvpStatus.In, Actor, later));
        ev.Override(Member, RsvpStatus.Out, Actor, later).Status.ShouldBe(RsvpStatus.Out);
    }

    [Fact]
    public void Cancelled_events_accept_no_answers_or_edits()
    {
        var ev = NewEvent();
        ev.Cancel(Now);

        Should.Throw<DomainException>(() => ev.Respond(Member, RsvpStatus.In, Actor, Now));
        Should.Throw<DomainException>(() => ev.Override(Member, RsvpStatus.In, Actor, Now));
        Should.Throw<DomainException>(() => ev.Update(Details()));
    }

    [Fact]
    public void Cancelling_is_idempotent()
    {
        var ev = NewEvent();
        ev.Cancel(Now);

        ev.Cancel(Now.AddHours(1));

        ev.CancelledAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void NoReply_is_not_an_answer()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewEvent().Respond(Member, RsvpStatus.NoReply, Actor, Now));
    }

    [Fact]
    public void Invariants_hold_even_for_callers_that_skip_request_validation()
    {
        Should.Throw<ArgumentException>(() => TeamEvent.Schedule(Guid.NewGuid(), Details() with { EndsAt = Kickoff }, [], Now));
        Should.Throw<ArgumentException>(() => TeamEvent.Schedule(Guid.NewGuid(), Details(Kickoff.AddMinutes(1)), [], Now));
        Should.Throw<ArgumentException>(() => TeamEvent.Schedule(Guid.NewGuid(), Details() with { MinPlayers = -1 }, [], Now));
    }

    [Fact]
    public void A_new_member_is_added_once()
    {
        var ev = NewEvent();
        var newcomer = Guid.NewGuid();

        ev.AddAttendee(newcomer);
        ev.AddAttendee(newcomer);

        ev.Rsvps.Count(r => r.MembershipId == newcomer).ShouldBe(1);
    }
}
