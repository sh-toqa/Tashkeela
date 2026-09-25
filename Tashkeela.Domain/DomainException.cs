namespace Tashkeela.Domain;

/// <summary>
/// The request is valid, but the current state forbids it (e.g. removing a team's last Manager). Mapped to HTTP 409.
/// Programming errors and inputs that request validation should already have rejected use the standard
/// <see cref="ArgumentException"/> family instead.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
