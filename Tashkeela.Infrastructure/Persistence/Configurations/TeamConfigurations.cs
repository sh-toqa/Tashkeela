using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tashkeela.Domain.Teams;
using Tashkeela.Domain.Users;

namespace Tashkeela.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength);
        builder.Property(t => t.Sport).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.LogoUrl).HasMaxLength(Team.LogoUrlMaxLength);
        builder.Property(t => t.TimeZoneId).HasMaxLength(Team.TimeZoneIdMaxLength);
        builder.Property(t => t.Version).IsConcurrencyToken(); // BR-1 guard against concurrent roster changes (see Team.Version)

        builder.HasMany(t => t.Memberships).WithOne().HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("TeamMemberships", t =>
            t.HasCheckConstraint("CK_TeamMemberships_JerseyNumber", $"[JerseyNumber] BETWEEN 0 AND {TeamMembership.MaxJerseyNumber}"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.MemberType).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Position).HasMaxLength(TeamMembership.PositionMaxLength);

        // One *active* membership per user per team, enforced by the database so concurrent joins can't duplicate.
        // Also the index behind every authorization check.
        builder.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique().HasFilter("[LeftAtUtc] IS NULL");
        builder.HasIndex(m => m.UserId); // "my teams"

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.HasOne<Team>().WithMany().HasForeignKey(i => i.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
