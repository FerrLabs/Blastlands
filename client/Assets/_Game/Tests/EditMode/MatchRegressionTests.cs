using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // Replicates configurations seen in a live match rather than a hand-built arena,
    // because the generated arena is where spawn clearing and blast shape interact.
    public class MatchRegressionTests
    {
        [Test]
        public void APlayerIdleOnTheirOwnClusterBombDiesInAGeneratedArena()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, 4, 20260813u);
            PlayerState player = state.Players[0];
            player.NextBombKind = BombKind.Cluster;
            player.FireRange = 3;

            GridPos spawn = player.Tile;

            var inputs = new PlayerInput[state.Players.Count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = PlayerInput.None;
            }

            inputs[0] = PlayerInput.Dropping();
            MatchSim.Tick(state, inputs);
            inputs[0] = PlayerInput.None;

            Assert.That(state.Bombs.Count, Is.EqualTo(1), "the bomb was placed");

            for (int i = 0; i < MatchSettings.Default.FuseTicks + 5 && state.Flames.Count == 0; i++)
            {
                MatchSim.Tick(state, inputs);
            }

            Assert.That(state.Flames.Count, Is.GreaterThan(0), "the bomb detonated");
            Assert.That(state.HasFlameAt(spawn), Is.True, "the blast covers the tile the bomb sat on");
            Assert.That(player.Tile, Is.EqualTo(spawn), "an idle player does not move");
            Assert.That(player.Alive, Is.False, "a player idle on their own bomb dies");
        }
    }
}
