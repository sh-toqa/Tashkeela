using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Events;
using Tashkeela.Domain.Events;
using Tashkeela.Domain.Teams;
using Tashkeela.Infrastructure.Persistence;

namespace Tashkeela.IntegrationTests;

/// <summary>FR-EVT-3/4/5, BR-2/3, NFR-REL-3. Authorization matrix: "RSVP" (own / any).</summary>
public sealed class RsvpTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_member_answers_and_everyone_sees_the_counts_and_names()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);
        var ev = await manager.CreateEventAsync(team.Id);

        var rsvp = await (await player.RespondAsync(ev.Id, "In")).ReadAsync<RsvpResponse>();
        (rsvp.MembershipId, rsvp.Status).ShouldBe((playerId, RsvpStatus.In));

        var details = await manager.EventAsync(ev.Id);
        details.Event.Counts.ShouldBe(new RsvpCounts(1, 0, 0, 1));
        details.Attendance.In.ShouldHaveSingleItem().MembershipId.ShouldBe(playerId);
        details.Attendance.NoReply.ShouldHaveSingleItem().MembershipId.ShouldBe(team.MyMembershipId);
    }

    [Theory]
    [InlineData("NoReply")]
    [InlineData(null)]
    public async Task NoReply_or_a_missing_status_is_not_an_answer(string? status)
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var ev = await manager.CreateEventAsync(team.Id);

        (await (await manager.RespondAsync(ev.Id, status!)).ErrorFieldsAsync()).ShouldBe(["status"]);
    }

    [Fact]
    public async Task After_the_deadline_members_get_409_but_a_manager_can_still_override()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);
        var ev = await manager.CreateEventAsync(team.Id);
        await factory.WithDbAsync(db => db.Events.Where(e => e.Id == ev.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RsvpDeadlineUtc, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));

        (await player.RespondAsync(ev.Id, "In")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var overridden = await manager.Client.PutAsJsonAsync($"/api/v1/events/{ev.Id}/rsvps/{playerId}", new { status = "Out" }, Ct);
        (await overridden.ReadAsync<RsvpResponse>()).Status.ShouldBe(RsvpStatus.Out);
        (await player.Client.PutAsJsonAsync($"/api/v1/events/{ev.Id}/rsvps/{team.MyMembershipId}", new { status = "In" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancelled_events_accept_no_answers()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var ev = await manager.CreateEventAsync(team.Id);
        await manager.Client.PostAsync($"/api/v1/events/{ev.Id}/cancel", null, Ct);

        (await manager.RespondAsync(ev.Id, "In")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Former_members_keep_their_rsvp_history_but_are_not_counted()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);
        var ev = await manager.CreateEventAsync(team.Id);
        await player.RespondAsync(ev.Id, "In");

        await player.Client.PostAsync($"/api/v1/teams/{team.Id}/leave", null, Ct);

        (await manager.EventAsync(ev.Id)).Event.Counts.ShouldBe(new RsvpCounts(0, 0, 0, 1));
        await factory.WithDbAsync(async db => (await db.Rsvps.SingleAsync(r => r.MembershipId == playerId, Ct)).Status.ShouldBe(RsvpStatus.In));
        (await manager.Client.PutAsJsonAsync($"/api/v1/events/{ev.Id}/rsvps/{playerId}", new { status = "Out" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

/// <summary>
/// The two optimistic-concurrency mechanisms, exercised against SQL Server. These races can't be staged reliably over
/// HTTP, so each test loads the same rows in two DbContexts and saves both, as two overlapping requests would.
/// </summary>
public sealed class ConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_managers_demoting_each_other_at_once_cannot_leave_the_team_without_a_manager()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (_, otherId) = await factory.AddPlayerAsync(manager, team.Id);
        await manager.Client.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{otherId}/role", new { role = "Manager" }, Ct);

        await factory.WithDbAsync(async first => await factory.WithDbAsync(async second =>
        {
            var a = await Roster(first, team.Id);
            var b = await Roster(second, team.Id);
            a.ChangeRole(a.FindActiveMember(team.MyMembershipId)!, TeamRole.Player); // each passes BR-1 on its own snapshot
            b.ChangeRole(b.FindActiveMember(otherId)!, TeamRole.Player);

            await first.SaveChangesAsync(Ct);
            await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        }));
    }

    [Fact]
    public async Task Two_simultaneous_answers_to_one_rsvp_cannot_silently_overwrite_each_other()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var ev = await manager.CreateEventAsync(team.Id);
        var now = DateTimeOffset.UtcNow;

        await factory.WithDbAsync(async first => await factory.WithDbAsync(async second =>
        {
            var a = await WithRsvp(first, ev.Id, team.MyMembershipId);
            var b = await WithRsvp(second, ev.Id, team.MyMembershipId);
            a.Respond(team.MyMembershipId, RsvpStatus.In, manager.UserId, now);
            b.Respond(team.MyMembershipId, RsvpStatus.Out, manager.UserId, now);

            await first.SaveChangesAsync(Ct);
            await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        }));
    }

    private static Task<Team> Roster(AppDbContext db, Guid teamId) =>
        db.Teams.Include(t => t.Memberships.Where(m => m.LeftAtUtc == null)).SingleAsync(t => t.Id == teamId, Ct);

    private static Task<TeamEvent> WithRsvp(AppDbContext db, Guid eventId, Guid membershipId) =>
        db.Events.Include(e => e.Rsvps.Where(r => r.MembershipId == membershipId)).SingleAsync(e => e.Id == eventId, Ct);
}
