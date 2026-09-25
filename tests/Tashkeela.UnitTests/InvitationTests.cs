using Tashkeela.Domain.Teams;

namespace Tashkeela.UnitTests;

/// <summary>FR-TEAM-2/3: email invitations are single-use and addressed; link invitations are shareable.</summary>
public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private static Invitation New(string? email) => Invitation.Create(Guid.NewGuid(), email, "HASH", Guid.NewGuid(), Now);

    [Fact]
    public void A_link_invitation_can_be_used_by_anyone_until_it_expires()
    {
        var invitation = New(email: null);

        invitation.Redeem(Guid.NewGuid(), "a@example.com", Now).ShouldBe(RedeemResult.Redeemed);
        invitation.Redeem(Guid.NewGuid(), "b@example.com", Now).ShouldBe(RedeemResult.Redeemed);
        invitation.Redeem(Guid.NewGuid(), "c@example.com", Now + Invitation.Lifetime).ShouldBe(RedeemResult.Expired);
    }

    [Fact]
    public void An_email_invitation_is_single_use_and_only_for_that_address()
    {
        var invitation = New("Player@Example.com");
        var player = Guid.NewGuid();

        invitation.Redeem(Guid.NewGuid(), "someone@example.com", Now).ShouldBe(RedeemResult.WrongRecipient);
        invitation.Redeem(player, "player@example.com", Now).ShouldBe(RedeemResult.Redeemed); // case-insensitive
        invitation.AcceptedByUserId.ShouldBe(player);
        invitation.Redeem(player, "player@example.com", Now).ShouldBe(RedeemResult.AlreadyUsed);
    }
}
