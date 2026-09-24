using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SeatsTests
    {
        [Test]
        public void StepsStopAtTheEndsInsteadOfWrapping()
        {
            Assert.That(Seats.Step(Seats.Most, 1), Is.EqualTo(Seats.Most), "eight does not wrap round to two");
            Assert.That(Seats.Step(Seats.Fewest, -1), Is.EqualTo(Seats.Fewest));
            Assert.That(Seats.Step(4, 1), Is.EqualTo(5));
        }

        [Test]
        public void ASavedCountFromAnotherBuildIsPulledBackInRange()
        {
            Assert.That(Seats.Step(99, -1), Is.EqualTo(Seats.Most - 1));
            Assert.That(Seats.Clamp(0), Is.EqualTo(Seats.Fewest));
        }

        [Test]
        public void EverySeatTheHostCanOpenHasASpawnOnEveryBoard()
        {
            foreach (GameMode mode in GameModeTokens.All)
            {
                for (uint seed = 1; seed <= 6; seed++)
                {
                    MatchState state = MatchFactory.Create(ArenaSettings.For(mode), MatchSettings.For(mode), Seats.Most, seed);
                    Assert.That(state.Players.Count, Is.EqualTo(Seats.Most), $"{mode}, seed {seed}");
                }
            }
        }
    }
}
