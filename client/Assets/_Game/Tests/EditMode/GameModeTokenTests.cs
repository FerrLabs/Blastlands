using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class GameModeTokenTests
    {
        [Test]
        public void EveryModeSurvivesTheWireBothWays()
        {
            foreach (GameMode mode in GameModeTokens.All)
            {
                Assert.That(GameModeTokens.TryRead(GameModeTokens.Write(mode), out GameMode read), Is.True, mode.ToString());
                Assert.That(read, Is.EqualTo(mode));
            }
        }

        [Test]
        public void TheTokensAreTheOnesTheLobbyUses()
        {
            Assert.That(GameModeTokens.Write(GameMode.Arena), Is.EqualTo("arena"));
            Assert.That(GameModeTokens.Write(GameMode.Classic), Is.EqualTo("classic"));
            Assert.That(GameModeTokens.Write(GameMode.ClassicBlinded), Is.EqualTo("classic_blinded"));
        }

        [Test]
        public void AnUnknownTokenIsNotRead()
        {
            Assert.That(GameModeTokens.TryRead("Classic", out _), Is.False);
            Assert.That(GameModeTokens.TryRead(null, out _), Is.False);
        }

        [Test]
        public void SteppingWrapsBothWays()
        {
            Assert.That(GameModeTokens.Step(GameMode.Classic, 1), Is.EqualTo(GameMode.ClassicBlinded));
            Assert.That(GameModeTokens.Step(GameMode.Classic, -1), Is.EqualTo(GameMode.Arena));
            Assert.That(GameModeTokens.Step(GameMode.Arena, 1), Is.EqualTo(GameMode.Classic));
        }

        [Test]
        public void EachModePlaysOnItsOwnBoard()
        {
            Assert.That(ArenaSettings.For(GameMode.Arena).Board, Is.EqualTo(BoardKind.Island));
            Assert.That(ArenaSettings.For(GameMode.Classic).Board, Is.EqualTo(BoardKind.Lattice));
            Assert.That(ArenaSettings.For(GameMode.ClassicBlinded).Board, Is.EqualTo(BoardKind.Lattice));
        }
    }
}
