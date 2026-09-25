using Microsoft.AspNetCore.Mvc;
using Tashkeela.API.Auth;
using Tashkeela.Application.Common;
using Tashkeela.Application.Teams;

namespace Tashkeela.API.Controllers;

/// <summary>Teams, rosters and invitations. Teams you don't belong to return 404.</summary>
[ApiController]
[Route("api/v1/teams")]
[Tags("Teams")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class TeamsController(TeamService teams, InvitationService invitations) : ControllerBase
{
    /// <summary>Create a team. You become its manager.</summary>
    [HttpPost]
    [ProducesResponseType<TeamResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamResponse>> Create(CreateTeamRequest request, CancellationToken ct)
    {
        var team = await teams.CreateAsync(User.UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { teamId = team.Id }, team);
    }

    /// <summary>The teams you belong to, with your role in each.</summary>
    [HttpGet]
    public Task<PagedResult<TeamResponse>> ListMine([FromQuery] PageQuery page, CancellationToken ct) =>
        teams.ListMineAsync(User.UserId(), page, ct);

    /// <summary>A team you belong to.</summary>
    [HttpGet("{teamId:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TeamResponse> Get(Guid teamId, CancellationToken ct) =>
        teams.GetAsync(User.UserId(), teamId, ct);

    /// <summary>The team's current members, managers first.</summary>
    [HttpGet("{teamId:guid}/members")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResult<MemberResponse>> ListMembers(Guid teamId, [FromQuery] PageQuery page, CancellationToken ct) =>
        teams.ListMembersAsync(User.UserId(), teamId, page, ct);

    /// <summary>Set a member's type (Regular/Spare), jersey number and position. Managers only.</summary>
    [HttpPut("{teamId:guid}/members/{membershipId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(Guid teamId, Guid membershipId, UpdateMemberRequest request, CancellationToken ct)
    {
        await teams.UpdateMemberAsync(User.UserId(), teamId, membershipId, request, ct);
        return NoContent();
    }

    /// <summary>Promote a member to manager or demote a manager to player. Managers only; 409 for the last manager.</summary>
    [HttpPut("{teamId:guid}/members/{membershipId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(Guid teamId, Guid membershipId, ChangeRoleRequest request, CancellationToken ct)
    {
        await teams.ChangeRoleAsync(User.UserId(), teamId, membershipId, request, ct);
        return NoContent();
    }

    /// <summary>Remove a member. Managers only; 409 for the last manager.</summary>
    [HttpDelete("{teamId:guid}/members/{membershipId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid teamId, Guid membershipId, CancellationToken ct)
    {
        await teams.RemoveMemberAsync(User.UserId(), teamId, membershipId, ct);
        return NoContent();
    }

    /// <summary>Leave the team. 409 if you are the last manager.</summary>
    [HttpPost("{teamId:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid teamId, CancellationToken ct)
    {
        await teams.LeaveAsync(User.UserId(), teamId, ct);
        return NoContent();
    }

    /// <summary>
    /// Invite someone. Managers only. With an email: single use, for that address, and emailed. Without: a shareable
    /// code anyone can use. The code is shown only in this response and expires after 7 days.
    /// </summary>
    [HttpPost("{teamId:guid}/invitations")]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Invite(Guid teamId, CreateInvitationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await invitations.CreateAsync(User.UserId(), teamId, request, ct));
}
