using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tashkeela.Application.Common;
using Tashkeela.Application.Teams;
using Tashkeela.Domain.Teams;

namespace Tashkeela.IntegrationTests;

/// <summary>FR-TEAM-1/4/5/6/7/8, BR-1, NFR-SEC-3/4. Authorization matrix: "View team", "Promote, remove members".</summary>
public sealed class TeamTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_team_makes_you_its_manager()
    {
        var manager = await factory.SignInAsync();

        var team = await manager.CreateTeamAsync("Alex Sharks");

        (team.Name, team.MyRole, team.MemberCount, team.TimeZone).ShouldBe(("Alex Sharks", TeamRole.Manager, 1, "Africa/Cairo"));
        var mine = await manager.Client.GetFromJsonAsync<PagedResult<TeamResponse>>("/api/v1/teams", TestApi.Json, Ct);
        mine!.Items.ShouldHaveSingleItem().Id.ShouldBe(team.Id);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_field_by_field()
    {
        var user = await factory.SignInAsync();

        var missing = await user.Client.PostAsJsonAsync("/api/v1/teams", new { name = "" }, Ct);
        (await missing.ErrorFieldsAsync()).ShouldBe(["name", "sport"], ignoreOrder: true);

        // Cross-field/format rules (IValidatableObject) run once the attribute checks pass.
        var invalid = await user.Client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Sharks", sport = "Padel", logoUrl = "http://not-https.example", timeZone = "Mars/Olympus" }, Ct);
        (await invalid.ErrorFieldsAsync()).ShouldBe(["logoUrl", "timeZone"], ignoreOrder: true);
    }

    [Fact]
    public async Task Non_members_get_404_so_teams_cannot_be_probed()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var outsider = await factory.SignInAsync();

        (await outsider.Client.GetAsync($"/api/v1/teams/{team.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/v1/teams/{team.Id}/members", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Requests_without_a_token_get_401_problem_details()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/teams", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Players_get_403_for_manager_actions()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);

        (await player.Client.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{playerId}/role", new { role = "Manager" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await player.Client.DeleteAsync($"/api/v1/teams/{team.Id}/members/{team.MyMembershipId}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await player.Client.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{playerId}", new { memberType = "Spare" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await player.Client.PostAsJsonAsync($"/api/v1/teams/{team.Id}/invitations", new { }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_missing_role_is_a_400_not_a_silent_demotion()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        await factory.AddPlayerAsync(manager, team.Id);

        var response = await manager.Client.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{team.MyMembershipId}/role", new { }, Ct);

        (await response.ErrorFieldsAsync()).ShouldBe(["role"]);
    }

    [Fact]
    public async Task The_last_manager_cannot_be_demoted_removed_or_leave()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        await factory.AddPlayerAsync(manager, team.Id);
        var me = $"/api/v1/teams/{team.Id}/members/{team.MyMembershipId}";

        (await manager.Client.PutAsJsonAsync($"{me}/role", new { role = "Player" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await manager.Client.DeleteAsync(me, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await manager.Client.PostAsync($"/api/v1/teams/{team.Id}/leave", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_manager_can_leave_after_promoting_someone()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);

        (await manager.Client.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{playerId}/role", new { role = "Manager" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.Client.PostAsync($"/api/v1/teams/{team.Id}/leave", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await player.MembersAsync(team.Id)).Items.ShouldHaveSingleItem().Role.ShouldBe(TeamRole.Manager);
        (await manager.Client.GetAsync($"/api/v1/teams/{team.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_removed_member_loses_access()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);

        (await manager.Client.DeleteAsync($"/api/v1/teams/{team.Id}/members/{playerId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await player.Client.GetAsync($"/api/v1/teams/{team.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await manager.MembersAsync(team.Id)).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Membership_ids_from_another_team_are_not_found()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var otherTeam = await (await factory.SignInAsync()).CreateTeamAsync();

        (await manager.Client.DeleteAsync($"/api/v1/teams/{team.Id}/members/{otherTeam.MyMembershipId}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_manager_sets_member_details_and_invalid_values_are_rejected()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var (player, playerId) = await factory.AddPlayerAsync(manager, team.Id);
        var url = $"/api/v1/teams/{team.Id}/members/{playerId}";

        (await manager.Client.PutAsJsonAsync(url, new { memberType = "Spare", jerseyNumber = 9, position = "Striker" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var member = (await player.MembersAsync(team.Id)).Items.Single(m => m.MembershipId == playerId);
        (member.MemberType, member.JerseyNumber, member.Position).ShouldBe((MemberType.Spare, 9, "Striker"));

        var invalid = await manager.Client.PutAsJsonAsync(url, new { jerseyNumber = 100 }, Ct);
        (await invalid.ErrorFieldsAsync()).ShouldBe(["memberType", "jerseyNumber"], ignoreOrder: true);
        (await player.Client.PutAsJsonAsync(url, new { memberType = "Regular" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Errors_are_problem_details_with_a_trace_id()
    {
        var user = await factory.SignInAsync();

        var response = await user.Client.GetAsync($"/api/v1/teams/{Guid.NewGuid()}", Ct);

        var problem = await response.ReadAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }
}
