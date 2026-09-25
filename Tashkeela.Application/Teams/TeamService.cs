using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Teams;

namespace Tashkeela.Application.Teams;

/// <summary>
/// Team and roster use cases (FR-TEAM-1, 4–8). Each write follows the same three steps: authorize, load the
/// <see cref="Team"/> aggregate and let it enforce its rules, save. Reads project straight from SQL into response DTOs
/// (nothing tracked, NFR-PERF-4). Team.Version turns a conflicting concurrent roster change into a 409.
/// </summary>
public sealed class TeamService(IAppDbContext db, TimeProvider time)
{
    /// <summary>FR-TEAM-1: any user creates a team and becomes its Manager.</summary>
    public async Task<TeamResponse> CreateAsync(Guid callerId, CreateTeamRequest request, CancellationToken ct)
    {
        var team = Team.Create(request.Name, request.Sport!.Value, request.LogoUrl, request.TimeZone, callerId, time.GetUtcNow());
        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);

        return await GetAsync(callerId, team.Id, ct);
    }

    /// <summary>FR-TEAM-8: the teams the caller belongs to, with their role in each.</summary>
    public Task<PagedResult<TeamResponse>> ListMineAsync(Guid callerId, PageQuery page, CancellationToken ct) =>
        TeamsOf(db.TeamMemberships.Where(m => m.UserId == callerId && m.LeftAtUtc == null)).ToPagedResultAsync(page, ct);

    public async Task<TeamResponse> GetAsync(Guid callerId, Guid teamId, CancellationToken ct)
    {
        var caller = await db.RequireMemberAsync(teamId, callerId, ct);
        return await TeamsOf(db.TeamMemberships.Where(m => m.Id == caller.MembershipId)).SingleAsync(ct);
    }

    /// <summary>The active roster: managers first, then by name.</summary>
    public async Task<PagedResult<MemberResponse>> ListMembersAsync(Guid callerId, Guid teamId, PageQuery page, CancellationToken ct)
    {
        await db.RequireMemberAsync(teamId, callerId, ct);
        return await (
                from m in db.TeamMemberships
                join u in db.Users on m.UserId equals u.Id
                where m.TeamId == teamId && m.LeftAtUtc == null
                orderby m.Role == TeamRole.Manager descending, u.DisplayName, m.Id
                select new MemberResponse(
                    m.Id, u.Id, u.DisplayName, u.Email, u.PhoneNumber, m.Role, m.MemberType, m.JerseyNumber, m.Position, m.JoinedAtUtc))
            .ToPagedResultAsync(page, ct);
    }

    /// <summary>FR-TEAM-4: a Manager sets a member's type, jersey number and position.</summary>
    public async Task UpdateMemberAsync(Guid callerId, Guid teamId, Guid membershipId, UpdateMemberRequest request, CancellationToken ct)
    {
        await db.RequireManagerAsync(teamId, callerId, ct);
        var team = await db.LoadRosterAsync(teamId, ct);

        team.UpdateMemberDetails(team.RequireActiveMember(membershipId), request.MemberType!.Value, request.JerseyNumber, request.Position);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>FR-TEAM-5/6: a Manager promotes or demotes a member; the last Manager can't be demoted (409).</summary>
    public async Task ChangeRoleAsync(Guid callerId, Guid teamId, Guid membershipId, ChangeRoleRequest request, CancellationToken ct)
    {
        await db.RequireManagerAsync(teamId, callerId, ct);
        var team = await db.LoadRosterAsync(teamId, ct);

        team.ChangeRole(team.RequireActiveMember(membershipId), request.Role!.Value);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>FR-TEAM-5/6: a Manager removes a member; the last Manager can't be removed (409).</summary>
    public async Task RemoveMemberAsync(Guid callerId, Guid teamId, Guid membershipId, CancellationToken ct)
    {
        await db.RequireManagerAsync(teamId, callerId, ct);
        var team = await db.LoadRosterAsync(teamId, ct);

        team.EndMembership(team.RequireActiveMember(membershipId), time.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>FR-TEAM-7: a member leaves; the last Manager must promote someone first (409).</summary>
    public async Task LeaveAsync(Guid callerId, Guid teamId, CancellationToken ct)
    {
        var caller = await db.RequireMemberAsync(teamId, callerId, ct);
        var team = await db.LoadRosterAsync(teamId, ct);

        team.EndMembership(team.RequireActiveMember(caller.MembershipId), time.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    // Ordered before projecting: EF can't translate an OrderBy on a constructor-projected record.
    private IQueryable<TeamResponse> TeamsOf(IQueryable<TeamMembership> callerMemberships) =>
        from m in callerMemberships
        join t in db.Teams on m.TeamId equals t.Id
        orderby t.Name, t.Id
        select new TeamResponse(
            t.Id, t.Name, t.Sport, t.LogoUrl, t.TimeZoneId,
            db.TeamMemberships.Count(x => x.TeamId == t.Id && x.LeftAtUtc == null),
            m.Id, m.Role);
}
