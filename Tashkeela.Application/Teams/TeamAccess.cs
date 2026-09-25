using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Teams;

namespace Tashkeela.Application.Teams;

internal sealed record CallerMembership(Guid MembershipId, TeamRole Role);

/// <summary>
/// Resource-based authorization (NFR-SEC-3). Permission depends on the caller's membership row in *this* team, so it
/// can't be a role claim in the token or a static [Authorize] policy. Every team-scoped use case calls one of these
/// first. Non-members get 404, so team ids can't be probed (NFR-SEC-4); members without the role get 403.
/// </summary>
internal static class TeamAccess
{
    public static Task<CallerMembership?> FindMembershipAsync(this IAppDbContext db, Guid teamId, Guid userId, CancellationToken ct) =>
        db.TeamMemberships
            .Where(m => m.TeamId == teamId && m.UserId == userId && m.LeftAtUtc == null)
            .Select(m => new CallerMembership(m.Id, m.Role))
            .SingleOrDefaultAsync(ct);

    public static async Task<CallerMembership> RequireMemberAsync(this IAppDbContext db, Guid teamId, Guid userId, CancellationToken ct) =>
        await db.FindMembershipAsync(teamId, userId, ct) ?? throw new NotFoundException("Team", teamId);

    public static async Task<CallerMembership> RequireManagerAsync(this IAppDbContext db, Guid teamId, Guid userId, CancellationToken ct) =>
        EnsureManager(await db.RequireMemberAsync(teamId, userId, ct));

    public static CallerMembership EnsureManager(CallerMembership caller) =>
        caller.Role == TeamRole.Manager ? caller : throw new ForbiddenException("Only team managers can do this.");

    /// <summary>Loads the <see cref="Team"/> aggregate with its active roster, tracked, for roster changes.</summary>
    public static async Task<Team> LoadRosterAsync(this IAppDbContext db, Guid teamId, CancellationToken ct) =>
        await db.Teams
            .Include(t => t.Memberships.Where(m => m.LeftAtUtc == null))
            .SingleOrDefaultAsync(t => t.Id == teamId, ct)
        ?? throw new NotFoundException("Team", teamId);

    public static TeamMembership RequireActiveMember(this Team team, Guid membershipId) =>
        team.FindActiveMember(membershipId) ?? throw new NotFoundException("Member", membershipId);
}
