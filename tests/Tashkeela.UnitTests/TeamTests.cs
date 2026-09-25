using Tashkeela.Domain;
using Tashkeela.Domain.Teams;

namespace Tashkeela.UnitTests;

/// <summary>BR-1 (a team always has a Manager) and FR-TEAM-1, 3–7.</summary>
public sealed class TeamTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Creator = Guid.NewGuid();

    private static Team NewTeam() => Team.Create(" Sharks ", Sport.Football, null, null, Creator, Now);

    private static TeamMembership Creators(Team team) => team.ActiveMembershipOf(Creator)!;

    [Fact]
    public void Creator_becomes_the_first_manager_and_defaults_apply()
    {
        var team = NewTeam();

        team.Name.ShouldBe("Sharks");
        team.TimeZoneId.ShouldBe(Team.DefaultTimeZoneId);
        Creators(team).IsManager.ShouldBeTrue();
        Creators(team).MemberType.ShouldBe(MemberType.Regular);
    }

    [Fact]
    public void A_user_cannot_join_twice()
    {
        var team = NewTeam();
        var player = Guid.NewGuid();
        team.AddPlayer(player, Now);

        Should.Throw<DomainException>(() => team.AddPlayer(player, Now));
    }

    [Fact]
    public void The_last_manager_cannot_be_demoted_or_removed()
    {
        var team = NewTeam();
        team.AddPlayer(Guid.NewGuid(), Now);

        Should.Throw<DomainException>(() => team.ChangeRole(Creators(team), TeamRole.Player));
        Should.Throw<DomainException>(() => team.EndMembership(Creators(team), Now));
        Creators(team).IsManager.ShouldBeTrue();
    }

    [Fact]
    public void A_manager_can_step_down_once_another_manager_exists()
    {
        var team = NewTeam();
        var other = team.AddPlayer(Guid.NewGuid(), Now);

        team.ChangeRole(other, TeamRole.Manager);
        team.EndMembership(Creators(team), Now);

        team.Memberships.Count(m => m.IsManager).ShouldBe(1);
        other.IsManager.ShouldBeTrue();
    }

    [Fact]
    public void A_removed_player_is_no_longer_active_and_can_rejoin_as_a_new_membership()
    {
        var team = NewTeam();
        var userId = Guid.NewGuid();
        var first = team.AddPlayer(userId, Now);

        team.EndMembership(first, Now);
        var second = team.AddPlayer(userId, Now.AddDays(1));

        first.IsActive.ShouldBeFalse();
        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public void Only_changes_that_can_affect_the_manager_rule_bump_the_version()
    {
        var team = NewTeam();

        var player = team.AddPlayer(Guid.NewGuid(), Now);
        team.UpdateMemberDetails(player, MemberType.Spare, 7, "Winger");
        team.Version.ShouldBe(0); // concurrent joins and profile edits must not conflict with each other

        team.ChangeRole(player, TeamRole.Manager);
        team.EndMembership(player, Now);
        team.Version.ShouldBe(2);
    }

    [Fact]
    public void Member_details_are_set_and_trimmed()
    {
        var team = NewTeam();
        var player = team.AddPlayer(Guid.NewGuid(), Now);

        team.UpdateMemberDetails(player, MemberType.Spare, 10, "  Goalkeeper ");

        (player.MemberType, player.JerseyNumber, player.Position).ShouldBe((MemberType.Spare, 10, "Goalkeeper"));
        Should.Throw<ArgumentOutOfRangeException>(() => team.UpdateMemberDetails(player, MemberType.Regular, 100, null));
    }

    [Fact]
    public void Members_of_another_team_are_rejected()
    {
        var team = NewTeam();
        var stranger = NewTeam().AddPlayer(Guid.NewGuid(), Now);

        Should.Throw<ArgumentException>(() => team.ChangeRole(stranger, TeamRole.Manager));
        Should.Throw<ArgumentException>(() => team.EndMembership(stranger, Now));
    }
}
