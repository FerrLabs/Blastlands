using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class StandInsTests
    {
        private static readonly MatchSettings Settings =
            MatchSettings.Default.WithSuddenDeath(SuddenDeathSettings.Off).WithTilesPerLooseBomb(0);

        private static readonly GridPos Left = new GridPos(1, 5);
        private static readonly GridPos Right = new GridPos(9, 5);

        private static MatchState TwoPlayers()
        {
            var state = new MatchState(new Arena(11, 11), Settings, 7u);
            state.AddPlayer(Left);
            state.AddPlayer(Right);
            return state;
        }

        private static PlayerInput[] Idle()
        {
            return new[] { PlayerInput.None, PlayerInput.None };
        }

        [Test]
        public void AnEmptySeatIsPlayedRatherThanLeftStanding()
        {
            MatchState state = TwoPlayers();
            var standIns = new StandIns(2, BotSettings.Normal);

            for (int tick = 0; tick < 90 && state.Outcome == RoundOutcome.Running; tick++)
            {
                PlayerInput[] inputs = Idle();
                standIns.Fill(state, inputs, seat => seat == 0);
                MatchSim.Tick(state, inputs);
            }

            Assert.That(state.Players[1].Tile, Is.Not.EqualTo(Right), "the abandoned player never moved");
        }

        [Test]
        public void ASeatedPlayersInputIsNeverTouched()
        {
            MatchState state = TwoPlayers();
            var standIns = new StandIns(2, BotSettings.Normal);
            var sent = new PlayerInput(1, 0, false, false);

            for (int tick = 0; tick < 30; tick++)
            {
                PlayerInput[] inputs = { sent, PlayerInput.None };
                standIns.Fill(state, inputs, seat => seat == 0);

                Assert.That(inputs[0].MoveX, Is.EqualTo(1));
                Assert.That(inputs[0].MoveY, Is.EqualTo(0));
                Assert.That(inputs[0].DropBomb, Is.False);
                MatchSim.Tick(state, inputs);
            }
        }

        [Test]
        public void AReconnectedPlayerGetsTheirCharacterBack()
        {
            MatchState state = TwoPlayers();
            var standIns = new StandIns(2, BotSettings.Normal);
            bool secondSeated = false;

            for (int tick = 0; tick < 60; tick++)
            {
                PlayerInput[] inputs = Idle();
                standIns.Fill(state, inputs, seat => seat == 0 || secondSeated);
                MatchSim.Tick(state, inputs);
            }

            secondSeated = true;
            for (int tick = 0; tick < 30; tick++)
            {
                PlayerInput[] inputs = Idle();
                standIns.Fill(state, inputs, seat => seat == 0 || secondSeated);

                Assert.That(inputs[1].IsMoving || inputs[1].DropBomb || inputs[1].Dash, Is.False,
                    $"the bot was still driving at tick {tick} after the player came back");
            }
        }

        [Test]
        public void ADeadPlayerIsNotDriven()
        {
            MatchState state = TwoPlayers();
            state.Players[1].Alive = false;
            var standIns = new StandIns(2, BotSettings.Normal);

            PlayerInput[] inputs = Idle();
            standIns.Fill(state, inputs, seat => seat == 0);

            Assert.That(inputs[1].IsMoving || inputs[1].DropBomb, Is.False);
        }
    }
}
