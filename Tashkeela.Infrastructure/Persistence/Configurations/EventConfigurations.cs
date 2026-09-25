using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tashkeela.Domain.Events;
using Tashkeela.Domain.Teams;
using Tashkeela.Domain.Users;

namespace Tashkeela.Infrastructure.Persistence.Configurations;

internal sealed class TeamEventConfiguration : IEntityTypeConfiguration<TeamEvent>
{
    public void Configure(EntityTypeBuilder<TeamEvent> builder)
    {
        builder.ToTable("Events", t =>
        {
            // Database backstops for invariants TeamEvent enforces.
            t.HasCheckConstraint("CK_Events_EndsAfterStart", "[EndsAtUtc] > [StartsAtUtc]");
            t.HasCheckConstraint("CK_Events_DeadlineNotAfterStart", "[RsvpDeadlineUtc] <= [StartsAtUtc]");
            t.HasCheckConstraint("CK_Events_MinPlayers", "[MinPlayers] >= 0");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Title).HasMaxLength(TeamEvent.TitleMaxLength);
        builder.Property(e => e.Location).HasMaxLength(TeamEvent.LocationMaxLength);
        builder.Property(e => e.Opponent).HasMaxLength(TeamEvent.OpponentMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(TeamEvent.NotesMaxLength);
        builder.HasIndex(e => new { e.TeamId, e.StartsAtUtc }); // a team's schedule

        builder.HasOne<Team>().WithMany().HasForeignKey(e => e.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Rsvps).WithOne().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Rsvps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RsvpConfiguration : IEntityTypeConfiguration<Rsvp>
{
    public void Configure(EntityTypeBuilder<Rsvp> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.RowVersion).IsRowVersion(); // NFR-REL-3
        builder.HasIndex(r => new { r.EventId, r.MembershipId }).IsUnique();
        builder.HasIndex(r => r.MembershipId);

        // Restrict: RSVPs are attendance history and must outlive a membership ending.
        builder.HasOne<TeamMembership>().WithMany().HasForeignKey(r => r.MembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
