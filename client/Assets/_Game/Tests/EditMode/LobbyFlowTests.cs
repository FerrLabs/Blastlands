using System.Collections.Generic;
using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class LobbyFlowTests
    {
        private static MatchInvite Invite(string id)
        {
            return new MatchInvite(id, "10.0.0.5", 7777, "ticket");
        }

        private static LobbyFlow Browsing()
        {
            var flow = new LobbyFlow();
            Assert.That(flow.Named("Bryan"), Is.True);
            flow.Listed(new List<MatchListing>
            {
                new MatchListing("a", "First", "Ana", 1, 4),
                new MatchListing("b", "Second", "Ben", 3, 4),
            });
            return flow;
        }

        [Test]
        public void ANameTheLobbyWouldRefuseNeverLeavesTheFirstScreen()
        {
            var flow = new LobbyFlow();

            Assert.That(flow.Named("x"), Is.False);
            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Name));
            Assert.That(flow.Notice, Is.EqualTo(LobbyFailure.InvalidName));

            Assert.That(flow.Named("  Bryan  "), Is.True);
            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Browse));
            Assert.That(flow.Player, Is.EqualTo("Bryan"));
        }

        // The case the issue calls the normal one: the row was fine when it was drawn
        // and is gone by the time it is clicked.
        [Test]
        public void AMatchThatFilledUpIsDroppedAndThePlayerGoesBackToTheList()
        {
            LobbyFlow flow = Browsing();

            flow.Refused(LobbyFailure.MatchFull, "b");

            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Browse));
            Assert.That(flow.Matches.Count, Is.EqualTo(1));
            Assert.That(flow.Matches[0].Id, Is.EqualTo("a"));
            Assert.That(flow.Notice, Is.EqualTo(LobbyFailure.MatchFull));
        }

        [Test]
        public void AMatchThatStartedOrVanishedIsTreatedTheSameWay()
        {
            foreach (LobbyFailure failure in new[] { LobbyFailure.MatchAlreadyStarted, LobbyFailure.MatchNotFound })
            {
                LobbyFlow flow = Browsing();
                flow.Joined(Invite("b"));

                flow.Refused(failure, "b");

                Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Browse), failure.ToString());
                Assert.That(flow.Matches.Count, Is.EqualTo(1), failure.ToString());
            }
        }

        // The refusals that are not about the listing leave the player where they are:
        // sending a host back to the list because the lobby is briefly unreachable
        // would throw away the match they are hosting.
        [Test]
        public void ARefusalThatIsNotAboutTheListingKeepsTheScreen()
        {
            LobbyFlow flow = Browsing();
            flow.Created(Invite("mine"), "host-ticket");

            flow.Refused(LobbyFailure.NotEnoughPlayers);

            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Host));
            Assert.That(flow.HasNotice, Is.True);

            flow.Refused(LobbyFailure.Unreachable);
            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Host));
        }

        [Test]
        public void ANameTheLobbyRefusesSendsThePlayerBackToTypeAnother()
        {
            LobbyFlow flow = Browsing();

            flow.Refused(LobbyFailure.InvalidName);

            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Name));
        }

        [Test]
        public void TheMatchOnlyStartsWithSomewhereToConnectTo()
        {
            LobbyFlow flow = Browsing();

            Assert.That(flow.Running(), Is.False, "browsing is not waiting on a match");

            flow.Joined(new MatchInvite("b", string.Empty, 0, string.Empty));
            Assert.That(flow.Running(), Is.False, "an invite without an endpoint connects nowhere");
            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Wait));

            flow.Joined(Invite("b"));
            Assert.That(flow.Running(), Is.True);
            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Play));
            Assert.That(flow.Invite.Port, Is.EqualTo(7777));
        }

        [Test]
        public void OnlyTheListRefreshesAndOnlyOnTheTimer()
        {
            LobbyFlow flow = Browsing();

            Assert.That(flow.ShouldRefresh(LobbyFlow.SecondsBetweenRefreshes - 0.1f), Is.False);
            Assert.That(flow.ShouldRefresh(0.2f), Is.True);
            Assert.That(flow.ShouldRefresh(0.2f), Is.False, "the timer starts again after a refresh");

            flow.Created(Invite("mine"), "host-ticket");
            Assert.That(flow.ShouldRefresh(60f), Is.False, "a host is not browsing");
        }

        // Listing again while the player has a row half-clicked must not leave a stale
        // count on screen, and must not carry the previous page's rows.
        [Test]
        public void ListingReplacesWhatWasThereRatherThanAddingToIt()
        {
            LobbyFlow flow = Browsing();

            flow.Listed(new List<MatchListing> { new MatchListing("c", "Third", "Cleo", 2, 8) });

            Assert.That(flow.Matches.Count, Is.EqualTo(1));
            Assert.That(flow.Matches[0].Id, Is.EqualTo("c"));

            flow.Listed(null);
            Assert.That(flow.Matches.Count, Is.EqualTo(0));
        }

        [Test]
        public void LeavingAMatchForgetsTheWayBackIntoIt()
        {
            LobbyFlow flow = Browsing();
            flow.Created(Invite("mine"), "host-ticket");

            flow.Left();

            Assert.That(flow.Screen, Is.EqualTo(LobbyScreen.Browse));
            Assert.That(flow.Invite.CanConnect, Is.False);
            Assert.That(flow.HostTicket, Is.Empty);
            Assert.That(flow.Running(), Is.False);
        }
    }
}
