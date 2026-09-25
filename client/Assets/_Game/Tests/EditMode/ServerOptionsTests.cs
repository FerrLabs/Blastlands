using System;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // A match started on the wrong numbers is worse than one refused: a wrong match id
    // releases somebody else's match, and a seat count the board cannot hold crashes the
    // slot rather than telling the lobby why. So most of these are about refusing, and
    // about saying which field of the assignment was wrong.
    public class ServerOptionsTests
    {
        private static bool Create(
            out ServerOptions options,
            out string error,
            string match = "abc123",
            string players = "4",
            string humans = null,
            string mode = null,
            string bots = null)
        {
            return ServerOptions.TryCreate(
                7001, "https://lobby.example.com", "shared-secret", match, players, humans, mode, bots, out options, out error);
        }

        [Test]
        public void AnAssignmentIsReadAlongsideWhatTheHostAlreadyKnows()
        {
            Assert.That(Create(out ServerOptions options, out string error), Is.True, error);

            Assert.That(options.ListenPort, Is.EqualTo(7001));
            Assert.That(options.MatchId, Is.EqualTo("abc123"));
            Assert.That(options.ExpectedPlayers, Is.EqualTo(4));
            Assert.That(options.LobbyUrl, Is.EqualTo("https://lobby.example.com"));
            Assert.That(options.InstanceToken, Is.EqualTo("shared-secret"));
        }

        [Test]
        public void AnAssignmentThatNamesNoModeOrBotsGetsArenaAndNormal()
        {
            Assert.That(Create(out ServerOptions options, out string error), Is.True, error);

            Assert.That(options.Mode, Is.EqualTo(GameMode.Arena), "a lobby from before modes");
            Assert.That(options.BotSkill, Is.EqualTo(BotSkill.Normal), "a lobby from before the choice existed");
        }

        [Test]
        public void TheModeAndTheBotsAreRead()
        {
            Assert.That(Create(out ServerOptions options, out string error, mode: "classic_blinded", bots: "hard"), Is.True, error);

            Assert.That(options.Mode, Is.EqualTo(GameMode.ClassicBlinded));
            Assert.That(options.BotSkill, Is.EqualTo(BotSkill.Hard));
        }

        [Test]
        public void AModeOrABotSkillNobodyKnowsIsRefusedRatherThanGuessed()
        {
            Assert.That(Create(out _, out string error, mode: "bomberman"), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.ModeField));

            Assert.That(Create(out _, out error, bots: "nightmare"), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.BotsField));
        }

        [Test]
        public void WithoutAHumanCountEverySeatIsWaitedFor()
        {
            Assert.That(Create(out ServerOptions options, out string error), Is.True, error);

            Assert.That(options.ExpectedHumans, Is.EqualTo(options.ExpectedPlayers));
        }

        [Test]
        public void TheHumanCountIsReadBesideTheSeats()
        {
            Assert.That(Create(out ServerOptions options, out string error, humans: "2"), Is.True, error);

            Assert.That(options.ExpectedPlayers, Is.EqualTo(4));
            Assert.That(options.ExpectedHumans, Is.EqualTo(2));
        }

        [Test]
        public void MoreHumansThanSeatsOrNoneAtAllIsRefused()
        {
            foreach (string humans in new[] { "5", "0", "two" })
            {
                Assert.That(Create(out _, out string error, humans: humans), Is.False, humans);
                Assert.That(error, Does.Contain(ServerOptions.HumansField));
            }
        }

        [Test]
        public void AnAssignmentWithoutAMatchOrSeatsSaysWhichFieldIsMissing()
        {
            Assert.That(Create(out _, out string error, match: " "), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.MatchField));

            Assert.That(Create(out _, out error, players: null), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.PlayersField));
        }

        [Test]
        public void AMatchWithNobodyInItIsRefused()
        {
            Assert.That(Create(out _, out string error, players: "0"), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.PlayersField));
        }

        [Test]
        public void TheCeilingOnPlayersIsLeftToTheArena()
        {
            // How many an arena seats is how many spawns it generated, which MatchFactory
            // knows and this does not. Restating a number here would be a second answer
            // to drift away from the first, so a high count is accepted and fails later
            // with the count the arena actually has.
            Assert.That(Create(out ServerOptions options, out string error, players: "99"), Is.True, error);
            Assert.That(options.ExpectedPlayers, Is.EqualTo(99));

            // Spelled TestDelegate rather than passed inline, the same way
            // ArenaGeneratorTests does and for the same reason: NUnit 4 overloads
            // Assert.Throws on both TestDelegate and Action, so a bare lambda is
            // ambiguous. Unity ships NUnit 3.5, which has only the TestDelegate
            // overload, so the editor compiles the lambda happily and the dotnet build
            // in CI does not. TestDelegate is the form both versions accept.
            TestDelegate tooManyForTheBoard = () =>
                MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, options.ExpectedPlayers, 1u);

            Assert.Throws<ArgumentOutOfRangeException>(tooManyForTheBoard);
        }

        [Test]
        public void SurroundingSpaceIsTrimmedRatherThanCarried()
        {
            // A match id with a trailing space is a different id to the lobby, and the
            // release call would quietly 404.
            Assert.That(Create(out ServerOptions options, out string error, match: " abc ", players: " 2 "), Is.True, error);

            Assert.That(options.MatchId, Is.EqualTo("abc"));
            Assert.That(options.ExpectedPlayers, Is.EqualTo(2));
        }
    }
}
