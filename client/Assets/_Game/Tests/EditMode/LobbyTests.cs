using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class LobbyTests
    {
        [Test]
        public void EveryCodeTheLobbyCanReturnHasAName()
        {
            // The list is taken from server/crates/lobby/src/error.rs. A code that fell
            // through to Unknown would leave a screen unable to tell a stale listing from
            // a dead end.
            Assert.That(LobbyFailures.FromCode("invalid_name"), Is.EqualTo(LobbyFailure.InvalidName));
            Assert.That(LobbyFailures.FromCode("invalid_player_count"), Is.EqualTo(LobbyFailure.InvalidPlayerCount));
            Assert.That(LobbyFailures.FromCode("match_not_found"), Is.EqualTo(LobbyFailure.MatchNotFound));
            Assert.That(LobbyFailures.FromCode("match_full"), Is.EqualTo(LobbyFailure.MatchFull));
            Assert.That(LobbyFailures.FromCode("match_already_started"), Is.EqualTo(LobbyFailure.MatchAlreadyStarted));
            Assert.That(LobbyFailures.FromCode("no_capacity"), Is.EqualTo(LobbyFailure.NoCapacity));
            Assert.That(LobbyFailures.FromCode("client_too_old"), Is.EqualTo(LobbyFailure.ClientTooOld));
            Assert.That(LobbyFailures.FromCode("invalid_version"), Is.EqualTo(LobbyFailure.InvalidVersion));
            Assert.That(LobbyFailures.FromCode("unauthorized"), Is.EqualTo(LobbyFailure.Unauthorized));
            Assert.That(LobbyFailures.FromCode("rate_limited"), Is.EqualTo(LobbyFailure.RateLimited));
            Assert.That(LobbyFailures.FromCode("too_many_matches"), Is.EqualTo(LobbyFailure.TooManyMatches));
        }

        [Test]
        public void ACodeFromANewerLobbyIsUnknownRatherThanAGuess()
        {
            Assert.That(LobbyFailures.FromCode("teapot"), Is.EqualTo(LobbyFailure.Unknown));
            Assert.That(LobbyFailures.FromCode(""), Is.EqualTo(LobbyFailure.Unknown));
            Assert.That(LobbyFailures.FromCode(null), Is.EqualTo(LobbyFailure.Unknown));
        }

        [Test]
        public void AListingThatWentStaleIsToldApartFromADeadEnd()
        {
            // The distinction the screens are built on: one means refresh and pick again,
            // the other means stop and say something.
            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.MatchFull), Is.True);
            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.MatchAlreadyStarted), Is.True);
            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.MatchNotFound), Is.True);

            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.ClientTooOld), Is.False);
            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.NoCapacity), Is.False);
            Assert.That(LobbyFailures.MeansTheListingWentStale(LobbyFailure.Unreachable), Is.False);
        }

        [Test]
        public void WhatThePlayerCanFixIsToldApartFromWhatTheyCannot()
        {
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.InvalidName), Is.True);
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.InvalidPlayerCount), Is.True);
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.TooManyMatches), Is.True, "closing one is something they can do");
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.RateLimited), Is.False, "waiting is not a fix");
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.ClientTooOld), Is.False);
            Assert.That(LobbyFailures.IsTheirsToFix(LobbyFailure.Unreachable), Is.False);
        }

        [Test]
        public void ANameIsJudgedTheWayTheLobbyJudgesIt()
        {
            Assert.That(DisplayName.IsAcceptable("Bryan"), Is.True);
            Assert.That(DisplayName.IsAcceptable("a-b_c 1"), Is.True);

            Assert.That(DisplayName.IsAcceptable("a"), Is.False, "one character got through");
            Assert.That(DisplayName.IsAcceptable(new string('a', 17)), Is.False, "seventeen got through");
            Assert.That(DisplayName.IsAcceptable(new string('a', 16)), Is.True, "sixteen was refused");
            Assert.That(DisplayName.IsAcceptable("<script>"), Is.False, "markup got through");
            Assert.That(DisplayName.IsAcceptable("bad\nname"), Is.False, "a control character got through");
            Assert.That(DisplayName.IsAcceptable(null), Is.False);
        }

        [Test]
        public void ANameIsTrimmedBeforeItIsJudged()
        {
            // The lobby trims first, so "  ab  " is a legal two-character name and the
            // client must not refuse it before sending.
            Assert.That(DisplayName.IsAcceptable("  ab  "), Is.True);
            Assert.That(DisplayName.Clean("  ab  "), Is.EqualTo("ab"));
            Assert.That(DisplayName.IsAcceptable("   "), Is.False);
        }

        [Test]
        public void LengthIsCountedInCharactersNotBytes()
        {
            // The server counts chars(), so sixteen accented letters is a legal name even
            // though it is thirty-two bytes. Counting bytes here would refuse a name the
            // lobby accepts, which is the one direction this must never fail in.
            Assert.That(DisplayName.IsAcceptable(new string('é', 16)), Is.True);
            Assert.That(DisplayName.IsAcceptable(new string('é', 17)), Is.False);
        }

        [Test]
        public void AResultCarriesEitherAValueOrAReason()
        {
            LobbyResult<int> ok = LobbyResult<int>.Success(7);
            LobbyResult<int> bad = LobbyResult<int>.Failed(LobbyFailure.MatchFull);

            Assert.That(ok.Ok, Is.True);
            Assert.That(ok.Value, Is.EqualTo(7));
            Assert.That(bad.Ok, Is.False);
            Assert.That(bad.Failure, Is.EqualTo(LobbyFailure.MatchFull));
        }

        [Test]
        public void OnlyAnUnansweredCallIsWorthRetrying()
        {
            // A refusal is refused for a reason: retrying match_full just asks the lobby
            // the same question again. Not reaching it at all is the opposite case.
            Assert.That(LobbyResult<int>.Failed(LobbyFailure.Unreachable).WorthRetrying, Is.True);
            Assert.That(LobbyResult<int>.Failed(LobbyFailure.Unreadable).WorthRetrying, Is.True);

            Assert.That(LobbyResult<int>.Failed(LobbyFailure.MatchFull).WorthRetrying, Is.False);
            Assert.That(LobbyResult<int>.Failed(LobbyFailure.ClientTooOld).WorthRetrying, Is.False);
            Assert.That(LobbyResult<int>.Success(1).WorthRetrying, Is.False);
        }

        [Test]
        public void AFullListingKnowsItIsFull()
        {
            var full = new MatchListing("1", "Night raid", "Bryan", 4, 4);
            var open = new MatchListing("2", "Dust bowl", "Sam", 2, 4);

            Assert.That(full.IsFull, Is.True);
            Assert.That(open.IsFull, Is.False);
            Assert.That(open.Occupancy, Is.EqualTo("2/4"));
        }
    }
}
