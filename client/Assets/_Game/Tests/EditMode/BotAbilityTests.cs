using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class BotAbilityTests
    {
        private static readonly MatchSettings Arena = MatchSettings.Default
            .WithTilesPerLooseBomb(0)
            .WithSuddenDeath(SuddenDeathSettings.Off);

        private static MatchState Match(GridPos bot, GridPos rival)
        {
            var state = new MatchState(new Arena(15, 15), Arena, 3u);
            state.AddPlayer(bot, CharacterKind.Demolisher);
            state.AddPlayer(rival, CharacterKind.None);
            return state;
        }

        private static void Plant(MatchState state, GridPos tile, int owner)
        {
            state.AddBomb(new ActiveBomb(new Bomb(tile, owner, 2, BombKind.Standard), 60));
        }

        [Test]
        public void ItTriggersWhenARivalItCanSeeStandsInTheBlast()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(8, 7));
            Plant(state, new GridPos(7, 7), 0);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.True);
        }

        [Test]
        public void ItHoldsWhenItWouldBeInTheBlastItself()
        {
            MatchState state = Match(new GridPos(6, 7), new GridPos(8, 7));
            Plant(state, new GridPos(7, 7), 0);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.False);
        }

        [Test]
        public void ItHoldsWhenNobodyIsInReach()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.False);
        }

        [Test]
        public void ItDoesNotTriggerOnARivalHiddenFromIt()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(8, 7));
            state.Arena[new GridPos(8, 7)] = TileKind.Bush;
            Plant(state, new GridPos(7, 7), 0);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.False);
        }

        [Test]
        public void ItCountsTheChainItWouldStart()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(11, 7));
            Plant(state, new GridPos(7, 7), 0);
            Plant(state, new GridPos(9, 7), 1);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.True);
        }

        [Test]
        public void ARivalsBombIsNotItsToTrigger()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(8, 7));
            Plant(state, new GridPos(7, 7), 1);

            Assert.That(BotAbilities.ShouldUse(state, state.Players[0]), Is.False);
        }

        [Test]
        public void TheBrainActsOnIt()
        {
            MatchState state = Match(new GridPos(1, 1), new GridPos(8, 7));
            Plant(state, new GridPos(7, 7), 0);
            var brain = new BotBrain(0, BotSettings.Hard);

            Assert.That(brain.Think(state).Ability, Is.True);
        }
    }
}
