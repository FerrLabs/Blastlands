using Blastlands.Core.Net;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class AbilityTests
    {
        private static readonly MatchSettings Arena = MatchSettings.Default
            .WithTilesPerLooseBomb(0)
            .WithSuddenDeath(SuddenDeathSettings.Off);

        private static MatchState Match(MatchSettings settings, CharacterKind first, params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(15, 15), settings, 3u);
            for (int i = 0; i < spawns.Length; i++)
            {
                state.AddPlayer(spawns[i], i == 0 ? first : CharacterKind.None);
            }

            return state;
        }

        private static void Plant(MatchState state, GridPos tile, int owner, int fuse)
        {
            state.AddBomb(new ActiveBomb(new Bomb(tile, owner, 2, BombKind.Standard), fuse));
        }

        private static void Tick(MatchState state, params PlayerInput[] inputs)
        {
            MatchSim.Tick(state, inputs);
        }

        [Test]
        public void TheTriggerSetsOffTheOldestOfYourBombsOnThisTick()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);
            Plant(state, new GridPos(3, 11), 0, 70);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasFlameAt(new GridPos(7, 7)), Is.True, "the oldest went off");
            Assert.That(state.HasBombAt(new GridPos(3, 11)), Is.True, "the newer one is still waiting");
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.EqualTo(Arena.Abilities.TriggerCooldownTicks));
        }

        [Test]
        public void SomebodyElsesBombIsNotYoursToTrigger()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 1, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.True);
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.Zero, "nothing happened, so nothing was spent");
        }

        [Test]
        public void TheCooldownHoldsTheNextTriggerBack()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);
            Plant(state, new GridPos(3, 11), 0, 70);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);
            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(3, 11)), Is.True);
        }

        [Test]
        public void TheCooldownRunsDownAndTheTriggerComesBack()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);
            for (int i = 0; i < Arena.Abilities.TriggerCooldownTicks; i++)
            {
                Tick(state, PlayerInput.None, PlayerInput.None);
            }

            Plant(state, new GridPos(3, 11), 0, 400);
            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasFlameAt(new GridPos(3, 11)), Is.True);
        }

        [Test]
        public void UsingItGivesAHiderAway()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            state.Arena[new GridPos(1, 1)] = TileKind.Bush;
            Plant(state, new GridPos(7, 7), 0, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(Vision.IsHidden(state, state.Players[0]), Is.False);
        }

        [Test]
        public void AStunnedPlayerCannotTrigger()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);
            state.Players[0].StunTicksRemaining = 10;

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.True);
        }

        [Test]
        public void ClassicHasNoAbilities()
        {
            MatchSettings classic = MatchSettings.Classic.WithSuddenDeath(SuddenDeathSettings.Off);
            MatchState state = Match(classic, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.True);
        }

        [Test]
        public void TheRunnerVanishesOnOpenGround()
        {
            MatchState state = Match(Arena, CharacterKind.Runner, new GridPos(4, 7), new GridPos(10, 7));
            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.False);
            Assert.That(Vision.CanSee(state, state.Players[0], state.Players[1]), Is.True, "vanishing does not blind");
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.EqualTo(Arena.Abilities.VanishCooldownTicks));
        }

        [Test]
        public void TheRunnerComesBackIntoViewWhenItRunsOut()
        {
            MatchState state = Match(Arena, CharacterKind.Runner, new GridPos(4, 7), new GridPos(10, 7));

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);
            for (int i = 0; i < Arena.Abilities.VanishTicks; i++)
            {
                Tick(state, PlayerInput.None, PlayerInput.None);
            }

            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True);
        }

        [Test]
        public void ActingWhileVanishedGivesTheRunnerAway()
        {
            MatchState state = Match(Arena, CharacterKind.Runner, new GridPos(4, 7), new GridPos(10, 7));

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);
            Tick(state, PlayerInput.Dashing(Direction.Up), PlayerInput.None);

            Assert.That(Vision.CanSee(state, state.Players[1], state.Players[0]), Is.True);
        }

        [Test]
        public void VanishingWipesOutTheGiveawayOfWhatCameBefore()
        {
            MatchState state = Match(Arena, CharacterKind.Runner, new GridPos(4, 7), new GridPos(10, 7));
            state.Players[0].RevealTicksRemaining = 20;

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(Vision.IsHidden(state, state.Players[0]), Is.True);
        }

        private static MatchState Grenadier(Direction facing)
        {
            MatchState state = Match(Arena, CharacterKind.Grenadier, new GridPos(4, 7), new GridPos(13, 13));
            state.Players[0].Facing = facing;
            return state;
        }

        [Test]
        public void TheGrenadierThrowsItsBombThreeTilesAhead()
        {
            MatchState state = Grenadier(Direction.Right);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.True);
            Assert.That(state.HasBombAt(new GridPos(4, 7)), Is.False);
            Assert.That(state.Players[0].BombsHeld, Is.Zero, "the thrown bomb came out of the pocket");
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.EqualTo(Arena.Abilities.ThrowCooldownTicks));
        }

        [Test]
        public void AThrowStopsShortOfAWall()
        {
            MatchState state = Grenadier(Direction.Right);
            state.Arena[new GridPos(6, 7)] = TileKind.SoftBlock;

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(5, 7)), Is.True);
            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.False, "nothing goes over a wall");
        }

        [Test]
        public void AThrowStopsShortOfAnotherBomb()
        {
            MatchState state = Grenadier(Direction.Right);
            Plant(state, new GridPos(6, 7), 1, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(5, 7)), Is.True);
        }

        [Test]
        public void WithTheWayAheadBlockedTheThrowIsNotSpent()
        {
            MatchState state = Grenadier(Direction.Right);
            state.Arena[new GridPos(5, 7)] = TileKind.HardBlock;

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.Bombs.Count, Is.Zero);
            Assert.That(state.Players[0].BombsHeld, Is.EqualTo(1));
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.Zero);
        }

        [Test]
        public void WithNothingInHandThereIsNothingToThrow()
        {
            MatchState state = Grenadier(Direction.Right);
            state.Players[0].BombsHeld = 0;

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.Bombs.Count, Is.Zero);
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.Zero);
        }

        [Test]
        public void AThrownBombKeepsTheThrowersReachAndKind()
        {
            MatchState state = Grenadier(Direction.Up);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Bomb thrown = state.Bombs[0].Bomb;
            Assert.That(thrown.Position, Is.EqualTo(new GridPos(4, 4)));
            Assert.That(thrown.Kind, Is.EqualTo(BombKind.Cluster), "the Grenadier's kit");
            Assert.That(thrown.FireRange, Is.EqualTo(state.Players[0].FireRange));
            Assert.That(state.Bombs[0].FuseRemaining, Is.EqualTo(Arena.FuseTicks - 1));
        }

        [Test]
        public void ThrowIsOnlyReadyWithABombInHandAndSomewhereToLand()
        {
            MatchState ready = Grenadier(Direction.Right);
            MatchState emptyHanded = Grenadier(Direction.Right);
            emptyHanded.Players[0].BombsHeld = 0;
            MatchState blocked = Grenadier(Direction.Right);
            blocked.Arena[new GridPos(5, 7)] = TileKind.HardBlock;

            Assert.That(Abilities.CanUseNow(ready, ready.Players[0]), Is.True);
            Assert.That(Abilities.CanUseNow(emptyHanded, emptyHanded.Players[0]), Is.False);
            Assert.That(Abilities.CanUseNow(blocked, blocked.Players[0]), Is.False);
        }

        [Test]
        public void TriggerIsOnlyReadyWithABombOfYoursOnTheBoard()
        {
            MatchState state = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 1, 60);
            Assert.That(Abilities.CanUseNow(state, state.Players[0]), Is.False);

            Plant(state, new GridPos(3, 11), 0, 60);
            Assert.That(Abilities.CanUseNow(state, state.Players[0]), Is.True);

            state.Players[0].AbilityCooldownRemaining = 5;
            Assert.That(Abilities.CanUseNow(state, state.Players[0]), Is.False, "cooling down");
        }

        [Test]
        public void ACharacterWithoutAnAbilityPressesForNothing()
        {
            MatchState state = Match(Arena, CharacterKind.None, new GridPos(1, 1), new GridPos(13, 13));
            Plant(state, new GridPos(7, 7), 0, 60);

            Tick(state, PlayerInput.UsingAbility(), PlayerInput.None);

            Assert.That(state.HasBombAt(new GridPos(7, 7)), Is.True);
            Assert.That(state.Players[0].AbilityCooldownRemaining, Is.Zero);
        }

        [Test]
        public void TheAbilityTravelsInTheInputPacket()
        {
            var buffer = new byte[InputCodec.Size];
            Assert.That(InputCodec.TryWrite(buffer, 0, 12, PlayerInput.UsingAbility()), Is.True);

            Assert.That(InputCodec.TryRead(buffer, 0, out int tick, out PlayerInput read), Is.True);
            Assert.That(tick, Is.EqualTo(12));
            Assert.That(read.Ability, Is.True);
            Assert.That(read.DropBomb || read.Dash || read.Push, Is.False);
        }

        [Test]
        public void TheSnapshotCarriesTheCharacterAndItsCooldown()
        {
            MatchState server = Match(Arena, CharacterKind.Demolisher, new GridPos(1, 1), new GridPos(13, 13));
            server.Players[0].AbilityCooldownRemaining = 42;
            server.Players[0].VanishTicksRemaining = 17;
            MatchState client = Match(Arena, CharacterKind.None, new GridPos(1, 1), new GridPos(13, 13));

            var buffer = new byte[SnapshotCodec.MaxSize];
            int used = SnapshotCodec.Write(server, buffer);

            Assert.That(SnapshotCodec.TryApply(buffer, used, client), Is.True);
            Assert.That(client.Players[0].Character, Is.EqualTo(CharacterKind.Demolisher));
            Assert.That(client.Players[0].AbilityCooldownRemaining, Is.EqualTo(42));
            Assert.That(client.Players[0].VanishTicksRemaining, Is.EqualTo(17));
        }
    }
}
