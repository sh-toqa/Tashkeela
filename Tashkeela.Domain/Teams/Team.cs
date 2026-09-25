namespace Tashkeela.Domain.Teams;

/// <summary>
/// A team and its roster. Team is the aggregate root for its memberships for one concrete reason: BR-1 ("a team
/// always has at least one Manager") can only be checked by looking at the whole active roster, so every role or
/// membership change goes through here.
/// </summary>
public sealed class Team
{
    public const int NameMaxLength = 100;
    public const int LogoUrlMaxLength = 2048;
    public const int TimeZoneIdMaxLength = 64;
    public const string DefaultTimeZoneId = "Africa/Cairo";

    private readonly List<TeamMembership> _memberships = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Sport Sport { get; private set; }
    public string? LogoUrl { get; private set; }

    /// <summary>IANA id (NFR-USE-4). Times are stored in UTC; the zone is for clients to display local times.</summary>
    public string TimeZoneId { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, bumped by every change that could affect BR-1. Two managers demoting each other
    /// at the same moment would each see "another manager remains"; the version check makes the second save fail.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>The loaded memberships (the application loads active ones only).</summary>
    public IReadOnlyCollection<TeamMembership> Memberships => _memberships;

    private Team()
    {
        // For EF Core.
    }

    /// <summary>FR-TEAM-1: the creator becomes the first Manager.</summary>
    public static Team Create(string name, Sport sport, string? logoUrl, string? timeZoneId, Guid creatorUserId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Sport = sport,
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim(),
            CreatedAtUtc = nowUtc,
        };
        team._memberships.Add(new TeamMembership(team.Id, creatorUserId, TeamRole.Manager, nowUtc));
        return team;
    }

    public TeamMembership? FindActiveMember(Guid membershipId) =>
        _memberships.SingleOrDefault(m => m.IsActive && m.Id == membershipId);

    public TeamMembership? ActiveMembershipOf(Guid userId) =>
        _memberships.SingleOrDefault(m => m.IsActive && m.UserId == userId);

    /// <summary>FR-TEAM-3. Doesn't bump <see cref="Version"/>: adding a Player can't break BR-1.</summary>
    public TeamMembership AddPlayer(Guid userId, DateTimeOffset nowUtc)
    {
        if (ActiveMembershipOf(userId) is not null)
        {
            throw new DomainException("You're already a member of this team.");
        }

        var membership = new TeamMembership(Id, userId, TeamRole.Player, nowUtc);
        _memberships.Add(membership);
        return membership;
    }

    /// <summary>FR-TEAM-4: member type, jersey number and position. Doesn't affect BR-1.</summary>
    public void UpdateMemberDetails(TeamMembership member, MemberType memberType, int? jerseyNumber, string? position)
    {
        EnsureActiveMemberOfThisTeam(member);
        member.UpdateDetails(memberType, jerseyNumber, position);
    }

    /// <summary>FR-TEAM-5 (promote) and demotion, guarded by BR-1 / FR-TEAM-6.</summary>
    public void ChangeRole(TeamMembership member, TeamRole role)
    {
        EnsureActiveMemberOfThisTeam(member);
        if (member.Role == role)
        {
            return;
        }

        if (member.IsManager)
        {
            EnsureAnotherManagerRemains(member, "demote");
        }

        member.ChangeRole(role);
        Version++;
    }

    /// <summary>FR-TEAM-5 (remove) and FR-TEAM-7 (leave), guarded by BR-1 / FR-TEAM-6.</summary>
    public void EndMembership(TeamMembership member, DateTimeOffset nowUtc)
    {
        EnsureActiveMemberOfThisTeam(member);
        if (member.IsManager)
        {
            EnsureAnotherManagerRemains(member, "remove");
        }

        member.End(nowUtc);
        Version++;
    }

    private void EnsureActiveMemberOfThisTeam(TeamMembership member)
    {
        if (member.TeamId != Id || !member.IsActive)
        {
            throw new ArgumentException("Not an active member of this team.", nameof(member));
        }
    }

    private void EnsureAnotherManagerRemains(TeamMembership leaving, string action)
    {
        if (!_memberships.Any(m => m.IsManager && m.Id != leaving.Id))
        {
            throw new DomainException($"Can't {action} the team's last manager. Promote another member to manager first.");
        }
    }
}
