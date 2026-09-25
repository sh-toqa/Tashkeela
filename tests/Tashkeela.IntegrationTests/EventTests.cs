using System.Net;
using System.Net.Http.Json;
using Tashkeela.Application.Common;
using Tashkeela.Application.Events;
using Tashkeela.Domain.Events;

namespace Tashkeela.IntegrationTests;

/// <summary>FR-EVT-1/2/5/6/7. Authorization matrix: "Create, edit, cancel team events", "View schedule".</summary>
public sealed class EventTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_manager_schedules_an_event_and_every_member_starts_as_NoReply()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        await factory.AddPlayerAsync(manager, team.Id);

        var ev = await manager.CreateEventAsync(team.Id, TestApi.NewEvent(minPlayers: 5));

        (ev.Counts, ev.MyRsvp, ev.Status).ShouldBe((new RsvpCounts(0, 0, 0, 2), RsvpStatus.NoReply, EventStatus.Scheduled));
        ev.IsBelowMinimum.ShouldBeTrue();
    }

    [Fact]
    public async Task Times_are_accepted_with_any_offset_and_returned_in_utc()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var day = DateTime.UtcNow.AddDays(5);
        var cairoKickoff = new DateTimeOffset(day.Year, day.Month, day.Day, 20, 0, 0, TimeSpan.FromHours(3));

        var ev = await manager.CreateEventAsync(team.Id, TestApi.NewEvent(cairoKickoff));

        ev.StartsAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        ev.StartsAtUtc.ShouldBe(cairoKickoff);
        ev.RsvpDeadlineUtc.ShouldBe(ev.StartsAtUtc); // defaults to the start
    }

    [Fact]
    public async Task Invalid_events_are_rejected_field_by_field()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var missing = await manager.Client.PostAsJsonAsync($"/api/v1/teams/{team.Id}/events", new { title = "No type or times" }, Ct);
        (await missing.ErrorFieldsAsync()).ShouldBe(["type", "startsAt", "endsAt", "location"], ignoreOrder: true);

        var backwards = await manager.Client.PostAsJsonAsync($"/api/v1/teams/{team.Id}/events", new
        {
            type = "Practice", title = "Backwards", startsAt = start, endsAt = start.AddHours(-1), location = "Pitch",
            rsvpDeadline = start.AddHours(1),
        }, Ct);
        (await backwards.ErrorFieldsAsync()).ShouldBe(["endsAt", "rsvpDeadline"], ignoreOrder: true);

        var past = await manager.Client.PostAsJsonAsync($"/api/v1/teams/{team.Id}/events", TestApi.NewEvent(DateTimeOffset.UtcNow.AddHours(-1)), Ct);
        (await past.ErrorFieldsAsync()).ShouldBe(["startsAt"]);
    }

    [Fact]
    public async Task Players_can_view_but_not_manage_events_and_outsiders_see_nothing()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, _) = await factory.AddPlayerAsync(manager, team.Id);
        var ev = await manager.CreateEventAsync(team.Id);
        var outsider = await factory.SignInAsync();

        (await player.EventAsync(ev.Id)).Event.Id.ShouldBe(ev.Id);
        (await player.Client.PostAsJsonAsync($"/api/v1/teams/{team.Id}/events", TestApi.NewEvent(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await player.Client.PostAsync($"/api/v1/events/{ev.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.Client.GetAsync($"/api/v1/events/{ev.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/v1/teams/{team.Id}/events", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_manager_edits_and_cancels_and_a_cancelled_event_is_frozen()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var ev = await manager.CreateEventAsync(team.Id);
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var edit = new { type = "Practice", title = "Moved training", startsAt = start, endsAt = start.AddHours(1), location = "Indoor hall" };

        var updated = await (await manager.Client.PutAsJsonAsync($"/api/v1/events/{ev.Id}", edit, Ct)).ReadAsync<EventResponse>();
        (updated.Title, updated.Type, updated.Location).ShouldBe(("Moved training", EventType.Practice, "Indoor hall"));

        (await manager.Client.PostAsync($"/api/v1/events/{ev.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.Client.PostAsync($"/api/v1/events/{ev.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.Client.PutAsJsonAsync($"/api/v1/events/{ev.Id}", edit, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_schedule_lists_upcoming_events_soonest_first_without_cancelled_ones()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var later = await manager.CreateEventAsync(team.Id, TestApi.NewEvent(DateTimeOffset.UtcNow.AddDays(9)));
        var sooner = await manager.CreateEventAsync(team.Id, TestApi.NewEvent(DateTimeOffset.UtcNow.AddDays(2)));
        var cancelled = await manager.CreateEventAsync(team.Id);
        await manager.Client.PostAsync($"/api/v1/events/{cancelled.Id}/cancel", null, Ct);

        var schedule = await manager.Client.GetFromJsonAsync<PagedResult<EventResponse>>($"/api/v1/teams/{team.Id}/events", TestApi.Json, Ct);
        schedule!.Items.Select(e => e.Id).ShouldBe([sooner.Id, later.Id]);

        var all = await manager.Client.GetFromJsonAsync<PagedResult<EventResponse>>(
            $"/api/v1/teams/{team.Id}/events?includeCancelled=true&pageSize=1", TestApi.Json, Ct);
        (all!.TotalCount, all.TotalPages, all.Items.Count).ShouldBe((3, 3, 1));
    }
}
