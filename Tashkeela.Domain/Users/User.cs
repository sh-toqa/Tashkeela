namespace Tashkeela.Domain.Users;

/// <summary>
/// A person's profile. It has no behaviour in this version: registration and sign-in (FR-AUTH-*) are future work.
/// It lives in the domain because memberships, invitations and RSVPs refer to it.
/// </summary>
public sealed class User
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User()
    {
        // For EF Core.
    }

    public static User Create(string email, string displayName, string phoneNumber, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }
}
