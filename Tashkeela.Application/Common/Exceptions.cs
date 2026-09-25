namespace Tashkeela.Application.Common;

// Expected failures a use case reports. The API turns each into one HTTP status (ProblemDetails); business-rule
// conflicts use the domain's DomainException (409). Deliberately flat: one exception per status code, no hierarchy.

/// <summary>404. Also used when the caller isn't allowed to know the resource exists (NFR-SEC-4).</summary>
public sealed class NotFoundException(string resource, Guid id) : Exception($"{resource} '{id}' was not found.");

/// <summary>403: the caller can see the resource but their role doesn't allow this action.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>
/// 400 for input that is only invalid in context ("must start in the future", an unknown invitation code).
/// Checks that need nothing but the request body belong on the request DTO instead.
/// </summary>
public sealed class InvalidRequestException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
