using Microsoft.EntityFrameworkCore;
using Tashkeela.Domain.Events;
using Tashkeela.Domain.Teams;
using Tashkeela.Domain.Users;

namespace Tashkeela.Application.Common;

/// <summary>
/// The application's only data-access abstraction. It exists because Application can't reference Infrastructure
/// (where the SQL Server DbContext and migrations live), not to hide EF Core: a DbSet already is a repository and
/// SaveChangesAsync already is a unit of work, so services use them directly — Include, projections and all.
/// Aggregate rule: change rosters through <see cref="Team"/> and RSVPs through <see cref="TeamEvent"/>. The child sets
/// are exposed for reads (projections); the domain's internal constructors stop them being created directly.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMembership> TeamMemberships { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<TeamEvent> Events { get; }
    DbSet<Rsvp> Rsvps { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
