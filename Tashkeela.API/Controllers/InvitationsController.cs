using Microsoft.AspNetCore.Mvc;
using Tashkeela.API.Auth;
using Tashkeela.Application.Teams;

namespace Tashkeela.API.Controllers;

[ApiController]
[Route("api/v1/invitations")]
[Tags("Teams")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class InvitationsController(InvitationService invitations) : ControllerBase
{
    /// <summary>Join a team as a player. The code travels in the body so it never appears in URLs or access logs.</summary>
    [HttpPost("accept")]
    [ProducesResponseType<TeamResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<TeamResponse> Accept(AcceptInvitationRequest request, CancellationToken ct) =>
        invitations.AcceptAsync(User.UserId(), request, ct);
}
