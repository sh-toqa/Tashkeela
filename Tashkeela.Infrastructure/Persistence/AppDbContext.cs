using Microsoft.EntityFrameworkCore;
using Tashkeela.Application.Common;
using Tashkeela.Domain.Events;
using Tashkeela.Domain.Teams;
using Tashkeela.Domain.Users;

namespace Tashkeela.Infrastructure.Persistence;

/// <summary>The SQL Server database. Mapping lives in <c>Configurations/</c>, one class per entity.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TeamEvent> Events => Set<TeamEvent>();
    public DbSet<Rsvp> Rsvps => Set<Rsvp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
