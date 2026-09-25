namespace Tashkeela.Domain.Teams;

public enum RedeemResult
{
    Redeemed,
    Expired,
    AlreadyUsed,
    WrongRecipient,
}

/// <summary>
/// FR-TEAM-2: an invitation identified by a secret code; only the code's SHA-256 hash is stored (NFR-SEC-2).
/// Email invitations are single-use and only for the invited address; link invitations (no email) can be shared
/// and used by anyone until they expire.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }

    /// <summary>Set for email invitations; null for shareable links.</summary>
    public string? Email { get; private set; }

    public string TokenHash { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }

    public bool IsSingleUse => Email is not null;

    private Invitation()
    {
        // For EF Core.
    }

    public static Invitation Create(Guid teamId, string? email, string tokenHash, Guid createdByUserId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new Invitation
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            TokenHash = tokenHash,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + Lifetime,
        };
    }

    /// <summary>Uses the invitation if it is still valid for this person; says why not otherwise.</summary>
    public RedeemResult Redeem(Guid userId, string userEmail, DateTimeOffset nowUtc)
    {
        if (nowUtc >= ExpiresAtUtc)
        {
            return RedeemResult.Expired;
        }

        if (!IsSingleUse)
        {
            return RedeemResult.Redeemed; // link invitations aren't consumed
        }

        if (AcceptedAtUtc is not null)
        {
            return RedeemResult.AlreadyUsed;
        }

        if (!string.Equals(Email, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return RedeemResult.WrongRecipient;
        }

        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = userId;
        return RedeemResult.Redeemed;
    }
}
