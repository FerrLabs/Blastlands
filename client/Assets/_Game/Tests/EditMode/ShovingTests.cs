using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ShovingTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default.WithTilesPerLooseBomb(0);

        private static MatchState OpenMatch(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), Settings, 4u);
            foreach (GridPos spawn in spawns)
            {
                state.AddPlayer(spawn);
            }

            return state;
        }

        // Shoving without walking. PlayerInput.Pushing carries a direction so the shover
        // faces the right way, which also makes them advance — fine in most tests, fatal
        // in the one where the thing they are shoving someone into is a blast.
        private static PlayerInput PushWhereFacing(MatchState state, int player, Direction facing)
        {
            state.Players[player].Facing = facing;
            return new PlayerInput(0, 0, false, false, true);
        }

        private static void Run(MatchState state, int ticks, params PlayerInput[] inputs)
        {
            for (int i = 0; i < ticks; i++)
            {
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void AShoveMovesSomeoneWithoutHurtingThem()
        {
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            PlayerState target = state.Players[1];
            int before = target.Position.X;

            Run(state, Settings.Push.Ticks + 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(target.Position.X, Is.GreaterThan(before), "it moved them");
            Assert.That(target.Alive, Is.True, "bombs are the only thing that kills");
        }

        [Test]
        public void AShoveIntoAWallStunsTheTarget()
        {
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            state.Arena[new GridPos(7, 7)] = TileKind.HardBlock;

            Run(state, Settings.Push.Ticks + 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Stunned, Is.True, "they hit something");
            Assert.That(state.Players[1].Alive, Is.True, "and are still alive");
        }

        [Test]
        public void AShoveIntoOpenGroundDoesNotStun()
        {
            // Only the impact punishes. A shove that lands nowhere is a reposition.
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));

            Run(state, Settings.Push.Ticks + 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Stunned, Is.False);
        }

        [Test]
        public void AStunnedPlayerCannotMoveOrPlaceBombs()
        {
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            PlayerState target = state.Players[1];
            target.StunTicksRemaining = 20;
            target.BombsHeld = 1;

            SubPos before = target.Position;
            Run(state, 5, PlayerInput.None, new PlayerInput(StickReader.Range, 0, true, false));

            Assert.That(target.Position, Is.EqualTo(before), "the controls are gone");
            Assert.That(state.Bombs, Is.Empty, "including the bomb button");
        }

        [Test]
        public void AStunWearsOff()
        {
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            state.Players[1].StunTicksRemaining = 6;

            Run(state, 8, PlayerInput.None, PlayerInput.None);
            Assert.That(state.Players[1].Stunned, Is.False);

            SubPos before = state.Players[1].Position;
            Run(state, 5, PlayerInput.None, PlayerInput.Moving(Direction.Right));

            Assert.That(state.Players[1].Position.X, Is.GreaterThan(before.X), "and they have control back");
        }

        [Test]
        public void AShoveOnlyReachesWhoIsInFront()
        {
            // Reaching behind the shover reads as a bug however good the range check is.
            MatchState state = OpenMatch(new GridPos(6, 7), new GridPos(5, 7));
            PlayerState behind = state.Players[1];
            int before = behind.Position.X;

            Run(state, Settings.Push.Ticks + 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(behind.Position.X, Is.EqualTo(before), "the one behind is untouched");
        }

        [Test]
        public void AShoveDoesNotReachAcrossTheArena()
        {
            MatchState state = OpenMatch(new GridPos(2, 7), new GridPos(12, 7));
            int before = state.Players[1].Position.X;

            Run(state, Settings.Push.Ticks + 1, PlayerInput.Pushing(Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Position.X, Is.EqualTo(before));
        }

        [Test]
        public void SomeoneCanBeShovedIntoABlast()
        {
            // The point of the mechanic. Push does no damage, so the only way to kill
            // with it is to put someone where a bomb already is: the kill still belongs
            // to the bomb.
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            state.AddBomb(new ActiveBomb(new Bomb(new GridPos(8, 7), 99, 2), 12));

            Run(state, 20, PushWhereFacing(state, 0, Direction.Right), PlayerInput.None);

            Assert.That(state.Players[1].Alive, Is.False, "the blast caught them where they landed");
            Assert.That(state.Players[0].Alive, Is.True, "and the shover kept their distance");
        }

        [Test]
        public void AStunnedPlayerCanStillBeShoved()
        {
            // Someone helpless is exactly who you want to be able to move. A stun that
            // made them immovable would turn the punish into protection.
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));
            PlayerState target = state.Players[1];
            target.StunTicksRemaining = 200;
            int before = target.Position.X;

            Run(state, Settings.Push.Ticks + 1, PushWhereFacing(state, 0, Direction.Right), PlayerInput.None);

            Assert.That(target.Position.X, Is.GreaterThan(before));
            Assert.That(target.Stunned, Is.True, "and they are still stunned afterwards");
        }

        [Test]
        public void ShovingHasACooldown()
        {
            MatchState state = OpenMatch(new GridPos(5, 7), new GridPos(6, 7));

            MatchSim.Tick(state, new[] { PushWhereFacing(state, 0, Direction.Right), PlayerInput.None });
            Assert.That(state.Players[0].CanPush, Is.False, "spent");

            Run(state, Settings.Push.CooldownTicks, PlayerInput.None, PlayerInput.None);
            Assert.That(state.Players[0].CanPush, Is.True, "recharged");
        }

        [Test]
        public void TwoPlayersShovingEachOtherBothLand()
        {
            // Decided against the same starting positions, so the loop order does not
            // quietly pick a winner.
            MatchState state = OpenMatch(new GridPos(6, 7), new GridPos(7, 7));
            int leftStart = state.Players[0].Position.X;
            int rightStart = state.Players[1].Position.X;

            Run(state, Settings.Push.Ticks + 1,
                PlayerInput.Pushing(Direction.Right), PlayerInput.Pushing(Direction.Left));

            Assert.That(state.Players[0].Position.X, Is.LessThan(leftStart), "each was pushed away");
            Assert.That(state.Players[1].Position.X, Is.GreaterThan(rightStart));
        }
    }
}
