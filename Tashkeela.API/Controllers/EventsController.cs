using Microsoft.AspNetCore.Mvc;
using Tashkeela.API.Auth;
using Tashkeela.Application.Common;
using Tashkeela.Application.Events;

namespace Tashkeela.API.Controllers;

/// <summary>Team schedules and attendance. Events of teams you don't belong to return 404.</summary>
[ApiController]
[Route("api/v1")]
[Tags("Events")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class EventsController(EventService events, RsvpService rsvps) : ControllerBase
{
    /// <summary>A team's events, soonest first. Defaults to events that haven't ended, excluding cancelled ones.</summary>
    [HttpGet("teams/{teamId:guid}/events")]
    public Task<PagedResult<EventResponse>> List(
        Guid teamId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] PageQuery page,
        CancellationToken ct,
        [FromQuery] bool includeCancelled = false) =>
        events.ListForTeamAsync(User.UserId(), teamId, from, to, includeCancelled, page, ct);

    /// <summary>Schedule a game, practice or other event. Managers only. Every member starts as NoReply.</summary>
    [HttpPost("teams/{teamId:guid}/events")]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create(Guid teamId, EventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(User.UserId(), teamId, request, ct);
        return CreatedAtAction(nameof(Get), new { eventId = created.Id }, created);
    }

    /// <summary>An event with RSVP counts and who is In, Out, Maybe or hasn't replied.</summary>
    [HttpGet("events/{eventId:guid}")]
    public Task<EventDetailsResponse> Get(Guid eventId, CancellationToken ct) =>
        events.GetAsync(User.UserId(), eventId, ct);

    /// <summary>Edit an event. Managers only; 409 once cancelled.</summary>
    [HttpPut("events/{eventId:guid}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<EventResponse> Update(Guid eventId, EventRequest request, CancellationToken ct) =>
        events.UpdateAsync(User.UserId(), eventId, request, ct);

    /// <summary>Cancel an event. Managers only. Cancelled events accept no RSVPs. Idempotent.</summary>
    [HttpPost("events/{eventId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cancel(Guid eventId, CancellationToken ct)
    {
        await events.CancelAsync(User.UserId(), eventId, ct);
        return NoContent();
    }

    /// <summary>Set your RSVP: In, Out or Maybe. 409 after the deadline or for a cancelled event.</summary>
    [HttpPut("events/{eventId:guid}/rsvps/me")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RsvpResponse> Respond(Guid eventId, RsvpRequest request, CancellationToken ct) =>
        rsvps.RespondAsync(User.UserId(), eventId, request, ct);

    /// <summary>Set any member's RSVP, even after the deadline. Managers only.</summary>
    [HttpPut("events/{eventId:guid}/rsvps/{membershipId:guid}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RsvpResponse> Override(Guid eventId, Guid membershipId, RsvpRequest request, CancellationToken ct) =>
        rsvps.OverrideAsync(User.UserId(), eventId, membershipId, request, ct);
}
