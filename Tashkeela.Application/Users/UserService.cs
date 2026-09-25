using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Users;

namespace Tashkeela.Application.Users;

/// <summary>
/// Stand-in for the Accounts feature (FR-AUTH-*, future work): finds or creates a user by email so the development
/// sign-in endpoint can issue a real JWT. Registration, email confirmation and passwords are not implemented.
/// </summary>
public sealed class UserService(IAppDbContext db, TimeProvider time)
{
    public async Task<User> FindOrCreateAsync(string email, string displayName, string? phoneNumber, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = User.Create(email, displayName, phoneNumber ?? string.Empty, time.GetUtcNow());
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        return user;
    }
}
