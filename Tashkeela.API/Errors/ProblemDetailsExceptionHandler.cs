using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Domain;

namespace Tashkeela.API.Errors;

/// <summary>
/// The one place exceptions become HTTP responses (API-3: RFC 7807 ProblemDetails with the request's traceId).
/// Controllers contain no try/catch. Unknown exceptions are logged and returned as a generic 500 that leaks nothing.
/// </summary>
internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            InvalidRequestException e => new ValidationProblemDetails(new Dictionary<string, string[]> { [e.Field] = [e.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
            },
            NotFoundException e => Problem(StatusCodes.Status404NotFound, e.Message),
            ForbiddenException e => Problem(StatusCodes.Status403Forbidden, e.Message),
            DomainException e => Problem(StatusCodes.Status409Conflict, e.Message),
            // Optimistic concurrency: someone changed the same row between our read and our write.
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "The data was changed by another request. Reload and try again."),
            // A unique index rejected a concurrent duplicate (e.g. joining the same team twice at once).
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                Problem(StatusCodes.Status409Conflict, "This conflicts with existing data."),
            _ => null,
        };

        if (problem is null)
        {
            LogUnhandled(logger, exception);
            problem = Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string detail) => new() { Status = status, Detail = detail };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing the request")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
