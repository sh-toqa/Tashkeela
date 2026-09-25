using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tashkeela.API.Auth;
using Tashkeela.Application.Common;
using Tashkeela.Application.Events;
using Tashkeela.Application.Teams;

namespace Tashkeela.IntegrationTests;

/// <summary>A signed-in test user: an HTTP client carrying their token.</summary>
public sealed record TestUser(HttpClient Client, Guid UserId, string Email);

/// <summary>Drives the real endpoints to arrange test data, so tests read like user stories.</summary>
public static class TestApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestUser> SignInAsync(this ApiFactory factory, string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/dev/token", new { email, displayName = $"Player {email[..13]}" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await response.Content.ReadFromJsonAsync<DevTokenResponse>(Json, Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return new TestUser(client, token.UserId, email);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    public static async Task<TeamResponse> CreateTeamAsync(this TestUser user, string name = "Sharks")
    {
        var response = await user.Client.PostAsJsonAsync("/api/v1/teams", new { name, sport = "Football" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsync<TeamResponse>();
    }

    public static async Task<string> InviteCodeAsync(this TestUser manager, Guid teamId, string? email = null)
    {
        var response = await manager.Client.PostAsJsonAsync($"/api/v1/teams/{teamId}/invitations", new { email }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<InvitationResponse>()).Code;
    }

    public static Task<HttpResponseMessage> AcceptAsync(this TestUser user, string code) =>
        user.Client.PostAsJsonAsync("/api/v1/invitations/accept", new { code }, Ct);

    /// <summary>A new user who joined the team through a link invitation; returns them and their membership id.</summary>
    public static async Task<(TestUser Player, Guid MembershipId)> AddPlayerAsync(this ApiFactory factory, TestUser manager, Guid teamId)
    {
        var player = await factory.SignInAsync();
        var response = await player.AcceptAsync(await manager.InviteCodeAsync(teamId));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (player, (await response.ReadAsync<TeamResponse>()).MyMembershipId);
    }

    public static async Task<PagedResult<MemberResponse>> MembersAsync(this TestUser user, Guid teamId) =>
        (await user.Client.GetFromJsonAsync<PagedResult<MemberResponse>>($"/api/v1/teams/{teamId}/members", Json, Ct))!;

    public static object NewEvent(DateTimeOffset? startsAt = null, int minPlayers = 0)
    {
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(3);
        return new { type = "Game", title = "Friendly", startsAt = start, endsAt = start.AddHours(2), location = "Club pitch", minPlayers };
    }

    public static async Task<EventResponse> CreateEventAsync(this TestUser manager, Guid teamId, object? body = null)
    {
        var response = await manager.Client.PostAsJsonAsync($"/api/v1/teams/{teamId}/events", body ?? NewEvent(), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsync<EventResponse>();
    }

    public static async Task<EventDetailsResponse> EventAsync(this TestUser user, Guid eventId) =>
        (await user.Client.GetFromJsonAsync<EventDetailsResponse>($"/api/v1/events/{eventId}", Json, Ct))!;

    public static Task<HttpResponseMessage> RespondAsync(this TestUser user, Guid eventId, string status) =>
        user.Client.PutAsJsonAsync($"/api/v1/events/{eventId}/rsvps/me", new { status }, Ct);

    /// <summary>The field names in a 400 ValidationProblemDetails body.</summary>
    public static async Task<IReadOnlyList<string>> ErrorFieldsAsync(this HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<JsonElement>();
        return [.. problem.GetProperty("errors").EnumerateObject().Select(p => p.Name)];
    }
}
