using System.Net;
using Tashkeela.Application.Teams;
using Tashkeela.Domain.Events;
using Tashkeela.Domain.Teams;

namespace Tashkeela.IntegrationTests;

/// <summary>FR-TEAM-2/3, BR-4, NFR-SEC-2.</summary>
public sealed class InvitationTests(ApiFactory factory)
{
    [Fact]
    public async Task A_link_code_lets_several_people_join_as_players()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var code = await manager.InviteCodeAsync(team.Id);

        foreach (var player in new[] { await factory.SignInAsync(), await factory.SignInAsync() })
        {
            var joined = await (await player.AcceptAsync(code)).ReadAsync<TeamResponse>();
            (joined.Id, joined.MyRole).ShouldBe((team.Id, TeamRole.Player));
        }

        (await manager.MembersAsync(team.Id)).TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task An_email_invitation_is_emailed_and_works_once_for_that_address_only()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync("Email Invite FC");
        var invitee = await factory.SignInAsync();
        var code = await manager.InviteCodeAsync(team.Id, invitee.Email);

        factory.Emails.SentTo(invitee.Email).ShouldHaveSingleItem().TextBody.ShouldContain(code);
        (await (await factory.SignInAsync()).AcceptAsync(code)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await invitee.AcceptAsync(code)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await factory.SignInAsync()).AcceptAsync(code)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_codes_are_rejected_and_members_cannot_join_twice()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var code = await manager.InviteCodeAsync(team.Id);

        (await (await manager.AcceptAsync("not-a-real-code")).ErrorFieldsAsync()).ShouldBe(["code"]);
        (await manager.AcceptAsync(code)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_the_hash_of_a_code_is_stored()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var code = await manager.InviteCodeAsync(team.Id);

        await factory.WithDbAsync(async db =>
            db.Invitations.Where(i => i.TeamId == team.Id).ToList().ShouldAllBe(i => i.TokenHash != code && i.TokenHash.Length == 64));
    }

    [Fact]
    public async Task Someone_who_joins_later_gets_a_NoReply_rsvp_for_upcoming_events()
    {
        var manager = await factory.SignInAsync();
        var team = await manager.CreateTeamAsync();
        var ev = await manager.CreateEventAsync(team.Id);

        var (player, _) = await factory.AddPlayerAsync(manager, team.Id);

        var details = await player.EventAsync(ev.Id);
        details.Event.MyRsvp.ShouldBe(RsvpStatus.NoReply);
        details.Event.Counts.NoReply.ShouldBe(2);
    }
}
