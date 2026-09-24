using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The invite decides whether the client dials anything at all, so a half-filled one
    // has to read as unusable rather than as localhost on port zero.
    public class MatchInviteTests
    {
        [Test]
        public void AnInviteNeedsAHostAPortAndATicket()
        {
            Assert.That(new MatchInvite("m", "10.0.0.5", 7777, "t", GameMode.Arena).CanConnect, Is.True);
            Assert.That(new MatchInvite("m", "", 7777, "t", GameMode.Arena).CanConnect, Is.False);
            Assert.That(new MatchInvite("m", "10.0.0.5", 0, "t", GameMode.Arena).CanConnect, Is.False);
            Assert.That(new MatchInvite("m", "10.0.0.5", 70000, "t", GameMode.Arena).CanConnect, Is.False);
            Assert.That(new MatchInvite("m", "10.0.0.5", 7777, "", GameMode.Arena).CanConnect, Is.False);
            Assert.That(default(MatchInvite).CanConnect, Is.False);
        }

        [Test]
        public void HostingTakesBothTickets()
        {
            var listing = new MatchListing("m", "Name", "Host", 1, 0, 4, GameMode.Arena);
            var invite = new MatchInvite("m", "10.0.0.5", 7777, "game", GameMode.Arena);

            Assert.That(new MatchHosting(listing, invite, "host").CanStart, Is.True);
            Assert.That(new MatchHosting(listing, invite, "").CanStart, Is.False);
            Assert.That(new MatchHosting(listing, default(MatchInvite), "host").CanStart, Is.False);
        }

        // An unknown state has to read as not running: dialling a game server that was
        // never told to start is the failure that looks like a hung client.
        [Test]
        public void OnlyTheStateTheLobbyActuallySendsMeansRunning()
        {
            Assert.That(MatchProgress.Reads("in_progress"), Is.True);
            Assert.That(MatchProgress.Reads("waiting_for_players"), Is.False);
            Assert.That(MatchProgress.Reads("InProgress"), Is.False);
            Assert.That(MatchProgress.Reads(null), Is.False);
        }
    }
}
