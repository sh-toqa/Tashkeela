using System.ComponentModel.DataAnnotations;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Teams;
using Tashkeela.Domain.Users;

namespace Tashkeela.Application.Teams;

// The Teams HTTP contract. Entities are never serialized: responses are shaped for the caller (e.g. "my role") and
// requests validate their own shape with DataAnnotations, which [ApiController] checks before any code runs.
// Request conventions:
// - properties, not positional parameters: MVC reports errors under the JSON name ("role") only for properties;
// - value types are nullable + [Required], so a missing value is a 400, not a silent default (a missing "role"
//   must not quietly mean Player).

public sealed record CreateTeamRequest : IValidatableObject
{
    [Required, StringLength(Team.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Sport? Sport { get; init; }

    /// <summary>An absolute https URL.</summary>
    [StringLength(Team.LogoUrlMaxLength)]
    public string? LogoUrl { get; init; }

    /// <summary>IANA time zone id; defaults to Africa/Cairo.</summary>
    [StringLength(Team.TimeZoneIdMaxLength)]
    public string? TimeZone { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(LogoUrl)
            && !(Uri.TryCreate(LogoUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return ValidationError.For(nameof(LogoUrl), "Must be an absolute https URL.");
        }

        if (!string.IsNullOrWhiteSpace(TimeZone) && !TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
        {
            yield return ValidationError.For(nameof(TimeZone), "Must be an IANA time zone, e.g. Africa/Cairo.");
        }
    }
}

public sealed record UpdateMemberRequest
{
    [Required]
    public MemberType? MemberType { get; init; }

    [Range(0, TeamMembership.MaxJerseyNumber)]
    public int? JerseyNumber { get; init; }

    [StringLength(TeamMembership.PositionMaxLength)]
    public string? Position { get; init; }
}

public sealed record ChangeRoleRequest
{
    [Required]
    public TeamRole? Role { get; init; }
}

public sealed record CreateInvitationRequest
{
    /// <summary>Omit for a shareable code anyone can use; set it for a single-use invitation to that address.</summary>
    [EmailAddress, StringLength(User.EmailMaxLength)]
    public string? Email { get; init; }
}

public sealed record AcceptInvitationRequest
{
    [Required]
    public string Code { get; init; } = string.Empty;
}

/// <summary>A team as seen by one of its members.</summary>
public sealed record TeamResponse(
    Guid Id,
    string Name,
    Sport Sport,
    string? LogoUrl,
    string TimeZone,
    int MemberCount,
    Guid MyMembershipId,
    TeamRole MyRole);

/// <summary>A roster entry. Contact details are included because only members can read a roster (NFR-SEC-10).</summary>
public sealed record MemberResponse(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string Email,
    string PhoneNumber,
    TeamRole Role,
    MemberType MemberType,
    int? JerseyNumber,
    string? Position,
    DateTimeOffset JoinedAtUtc);

/// <summary>The invitation code is shown only in this response: the server stores its hash.</summary>
public sealed record InvitationResponse(Guid Id, string? Email, string Code, DateTimeOffset ExpiresAtUtc);
