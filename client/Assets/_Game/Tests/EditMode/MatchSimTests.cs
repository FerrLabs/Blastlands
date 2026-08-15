using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class MatchSimTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        private static MatchState OpenMatch(int width, int height, params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(width, height), Settings, 1u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        private static void Run(MatchState state, int ticks, params PlayerInput[] inputs)
        {
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        private static int RunUntilFlames(MatchState state, PlayerInput[] inputs)
        {
            for (int i = 1; i <= 400; i++)
            {
                MatchSim.Tick(state, inputs);
                if (state.Flames.Count > 0)
                {
                    return i;
                }
            }

            return -1;
        }

        [Test]
        public void APlayerWalksAcrossOpenFloor()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));

            Run(state, 10, PlayerInput.Moving(Direction.Right));

            Assert.That(state.Players[0].Tile.X, Is.EqualTo(2));
        }

        [Test]
        public void AHardBlockStopsAPlayerWithTheirBodyAgainstIt()
        {
            // Not at the centre of the last free tile any more: positions are continuous,
            // so a player walks right up to the wall and stops where their body meets it.
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            state.Arena[new GridPos(3, 1)] = TileKind.HardBlock;

            Run(state, 40, PlayerInput.Moving(Direction.Right));

            int wallFace = 3 * SubPos.UnitsPerTile;
            Assert.That(
                state.Players[0].Position.X,
                Is.EqualTo(wallFace - Settings.PlayerRadius - 1),
                "pressed against the block, not parked on a tile centre");
        }

        [Test]
        public void WalkingIntoTheEdgeOfAGapSlidesIntoIt()
        {
            // The old movement pulled the player onto their corridor centre on every
            // step, which hid this. Free positions need the corner assist instead, or
            // walking into a corridor mouth slightly off centre reads as sticky wall.
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            state.Arena[new GridPos(2, 2)] = TileKind.HardBlock;

            PlayerState player = state.Players[0];
            player.Position = player.Position.WithY(SubPos.CentreOf(1) + 70);

            Run(state, 30, PlayerInput.Moving(Direction.Right));

            Assert.That(player.Tile.X, Is.GreaterThan(1), "it got through rather than catching");
        }

        [Test]
        public void ABombIsRefusedOnceTheLastOneIsSpent()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));

            MatchSim.Tick(state, new[] { PlayerInput.Dropping() });
            Assert.That(state.Bombs.Count, Is.EqualTo(1));
            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(0), "placing spends it");

            Run(state, 12, PlayerInput.Moving(Direction.Right));
            MatchSim.Tick(state, new[] { PlayerInput.Dropping() });

            Assert.That(state.Bombs.Count, Is.EqualTo(1), "nothing left to place");
        }

        [Test]
        public void ABombDetonatesExactlyOnItsFuse()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));
            var inputs = new[] { PlayerInput.Dropping() };

            MatchSim.Tick(state, inputs);
            int ticks = RunUntilFlames(state, new[] { PlayerInput.None });

            Assert.That(ticks, Is.EqualTo(Settings.FuseTicks - 1));
            Assert.That(state.Bombs, Is.Empty);
        }

        [Test]
        public void DetonationDoesNotGiveTheBombBack()
        {
            // The point of the change: a bomb is spent when placed, so going off is not
            // a refill. A player who has thrown everything has to go and find more,
            // which is what puts them back in the open.
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1), new GridPos(7, 7));
            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });

            RunUntilFlames(state, new[] { PlayerInput.Moving(Direction.Right), PlayerInput.None });

            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(0));
        }

        [Test]
        public void APlayerWhoStaysOnTheirOwnBombDies()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1), new GridPos(7, 7));

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.Players[0].Alive, Is.False);
        }

        [Test]
        public void APlayerCanStepOffTheirOwnBombButNotBackOnto()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1));

            MatchSim.Tick(state, new[] { PlayerInput.Dropping() });
            Run(state, 12, PlayerInput.Moving(Direction.Right));
            Assert.That(state.Players[0].Tile.X, Is.EqualTo(2), "the player left the bomb tile");

            Run(state, 20, PlayerInput.Moving(Direction.Left));

            Assert.That(state.Players[0].Tile.X, Is.EqualTo(2));
            Assert.That(
                state.Players[0].Position.X,
                Is.EqualTo((2 * SubPos.UnitsPerTile) + Settings.PlayerRadius),
                "stopped with their body against the bomb they left");
        }

        [Test]
        public void ABlastTurnsASoftBlockIntoFloor()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1), new GridPos(7, 7));
            state.Arena[new GridPos(2, 1)] = TileKind.SoftBlock;

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.Arena[new GridPos(2, 1)], Is.EqualTo(TileKind.Floor));
        }

        [Test]
        public void TwoPlayersCaughtInTheSameBlastBothDieAndTheRoundIsADraw()
        {
            MatchState state = OpenMatch(9, 9, new GridPos(1, 1), new GridPos(2, 1));

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.Players[0].Alive, Is.False);
            Assert.That(state.Players[1].Alive, Is.False);
            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Draw));
        }

        [Test]
        public void TheLastPlayerAliveWinsTheRound()
        {
            MatchState state = OpenMatch(11, 11, new GridPos(1, 1), new GridPos(9, 9));

            MatchSim.Tick(state, new[] { PlayerInput.Dropping(), PlayerInput.None });
            RunUntilFlames(state, new[] { PlayerInput.None, PlayerInput.None });

            Assert.That(state.Outcome, Is.EqualTo(RoundOutcome.Winner));
            Assert.That(state.WinnerId, Is.EqualTo(1));
        }

        [Test]
        public void TheSameSeedAndInputsProduceTheSameState()
        {
            string first = RunScriptedMatch();
            string second = RunScriptedMatch();

            Assert.That(first, Is.EqualTo(second));
        }

        private static string RunScriptedMatch()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Settings, 4, 4242u);

            var script = new List<PlayerInput[]>
            {
                new[] { PlayerInput.Dropping(), PlayerInput.Moving(Direction.Down), PlayerInput.Moving(Direction.Left), PlayerInput.None },
                new[] { PlayerInput.Moving(Direction.Right), PlayerInput.Moving(Direction.Right), PlayerInput.Dropping(), PlayerInput.Moving(Direction.Up) },
                new[] { PlayerInput.Moving(Direction.Down), PlayerInput.None, PlayerInput.Moving(Direction.Down), PlayerInput.Dropping() },
            };

            for (int tick = 0; tick < 200; tick++)
            {
                MatchSim.Tick(state, script[tick % script.Count]);
            }

            return Digest(state);
        }

        private static string Digest(MatchState state)
        {
            var builder = new StringBuilder();
            builder.Append("t=").Append(state.Tick).Append(" outcome=").Append(state.Outcome);

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                builder.Append(" p").Append(player.Id)
                    .Append(player.Position)
                    .Append(player.Alive ? "A" : "D")
                    .Append(player.BombsHeld);
            }

            builder.Append(" bombs=").Append(state.Bombs.Count).Append(" flames=").Append(state.Flames.Count);

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    builder.Append((int)state.Arena[new GridPos(x, y)]);
                }
            }

            return builder.ToString();
        }
    }
}
