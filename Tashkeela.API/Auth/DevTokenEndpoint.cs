using System.Net.Mail;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Tashkeela.Application.Users;

namespace Tashkeela.API.Auth;

public sealed record DevTokenRequest(string Email, string DisplayName, string? PhoneNumber);

public sealed record DevTokenResponse(Guid UserId, string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// DEVELOPMENT SCAFFOLDING standing in for Accounts (FR-AUTH-*, a future iteration): finds or creates a user by
/// email and returns a real signed JWT, so every other endpoint is exercised exactly as it will be in production.
/// Mapped only when DevAuth:Enabled is true (Development and tests), never in production.
/// </summary>
internal static class DevTokenEndpoint
{
    public static void MapDevTokenEndpoint(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/dev/token", IssueTokenAsync)
            .AllowAnonymous()
            .WithTags("Dev sign-in")
            .WithSummary("Get a token for a test user (development only). Creates the user on first use.");

    private static async Task<IResult> IssueTokenAsync(
        DevTokenRequest request, UserService users, IOptions<JwtOptions> jwtOptions, TimeProvider time, CancellationToken ct)
    {
        if (!MailAddress.TryCreate(request.Email, out _) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email and a display name are required."] });
        }

        var user = await users.FindOrCreateAsync(request.Email, request.DisplayName, request.PhoneNumber, ct);

        var jwt = jwtOptions.Value;
        var now = time.GetUtcNow();
        var expires = now + jwt.AccessTokenLifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            ]),
            SigningCredentials = new SigningCredentials(jwt.Key(), SecurityAlgorithms.HmacSha256),
        });

        return Results.Ok(new DevTokenResponse(user.Id, token, expires));
    }
}
