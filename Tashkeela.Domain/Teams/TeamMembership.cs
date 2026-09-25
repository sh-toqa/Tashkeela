namespace Tashkeela.Domain.Teams;

/// <summary>
/// A user's membership in a team. Never deleted: leaving sets <see cref="LeftAtUtc"/>, so RSVPs keep pointing at a
/// real record. Rejoining creates a new membership. Created and changed only through <see cref="Team"/>.
/// </summary>
public sealed class TeamMembership
{
    public const int MaxJerseyNumber = 99;
    public const int PositionMaxLength = 50;

    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamRole Role { get; private set; }
    public MemberType MemberType { get; private set; }
    public int? JerseyNumber { get; private set; }
    public string? Position { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset? LeftAtUtc { get; private set; }

    public bool IsActive => LeftAtUtc is null;
    public bool IsManager => IsActive && Role == TeamRole.Manager;

    private TeamMembership()
    {
        // For EF Core.
    }

    internal TeamMembership(Guid teamId, Guid userId, TeamRole role, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        TeamId = teamId;
        UserId = userId;
        Role = role;
        MemberType = MemberType.Regular;
        JoinedAtUtc = nowUtc;
    }

    internal void ChangeRole(TeamRole role) => Role = role;

    internal void UpdateDetails(MemberType memberType, int? jerseyNumber, string? position)
    {
        if (jerseyNumber is < 0 or > MaxJerseyNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), $"A jersey number must be between 0 and {MaxJerseyNumber}.");
        }

        MemberType = memberType;
        JerseyNumber = jerseyNumber;
        Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim();
    }

    internal void End(DateTimeOffset nowUtc) => LeftAtUtc = nowUtc;
}
