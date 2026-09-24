using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ClassicItemTests
    {
        private static readonly MatchSettings Classic = MatchSettings.Classic.WithSuddenDeath(SuddenDeathSettings.Off);

        private static MatchState Open(params GridPos[] spawns)
        {
            var state = new MatchState(new Arena(11, 5), Classic, 3u);
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

        private static ActiveBomb Plant(MatchState state, GridPos tile, int owner)
        {
            var bomb = new ActiveBomb(new Bomb(tile, owner, 1), 1000);
            state.AddBomb(bomb);
            return bomb;
        }

        [Test]
        public void WalkingIntoABombWithKickSendsItSlidingUntilSomethingStopsIt()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));
            state.Players[0].CanKick = true;
            state.Arena[new GridPos(8, 2)] = TileKind.HardBlock;
            ActiveBomb bomb = Plant(state, new GridPos(3, 2), 1);

            Run(state, 60, PlayerInput.Moving(Direction.Right), PlayerInput.None);

            Assert.That(bomb.Position, Is.EqualTo(new GridPos(7, 2)), "it stops against the wall");
            Assert.That(state.Bombs[0], Is.SameAs(bomb));
            Assert.That(bomb.Id, Is.Not.Zero, "the view tells a sliding bomb from a new one by this, not by its tile");
            Assert.That(bomb.Sliding, Is.EqualTo(Direction.None));
        }

        [Test]
        public void WithoutKickABombIsJustAWall()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));
            ActiveBomb bomb = Plant(state, new GridPos(3, 2), 1);

            Run(state, 60, PlayerInput.Moving(Direction.Right), PlayerInput.None);

            Assert.That(bomb.Position, Is.EqualTo(new GridPos(3, 2)));
            Assert.That(state.Players[0].Tile, Is.EqualTo(new GridPos(2, 2)));
        }

        [Test]
        public void ASlidingBombStopsShortOfAPlayer()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(6, 2));
            state.Players[0].CanKick = true;
            ActiveBomb bomb = Plant(state, new GridPos(3, 2), 1);

            Run(state, 60, PlayerInput.Moving(Direction.Right), PlayerInput.None);

            Assert.That(bomb.Position, Is.EqualTo(new GridPos(5, 2)));
        }

        [Test]
        public void ARemoteBombWaitsForItsOwnerAndGoesOffOnThePress()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));
            state.Players[0].HasRemote = true;
            state.Players[0].BombsHeld = 1;

            Run(state, 1, PlayerInput.Dropping(), PlayerInput.None);
            Run(state, 20, PlayerInput.Moving(Direction.Right), PlayerInput.None);
            Run(state, Classic.FuseTicks * 3, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Bombs.Count, Is.EqualTo(1), "a remote bomb does not burn down on its own");
            Assert.That(state.Bombs[0].Remote, Is.True);

            Run(state, 1, PlayerInput.UsingAbility(), PlayerInput.None);
            Run(state, 1, PlayerInput.None, PlayerInput.None);

            Assert.That(state.Bombs.Count, Is.Zero, "the press sets it off");
        }

        [Test]
        public void ARemoteBombBurnsDownOnceItsOwnerIsGone()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));
            ActiveBomb bomb = Plant(state, new GridPos(5, 2), 0);
            bomb.Remote = true;
            state.Players[0].Alive = false;

            Assert.That(ClassicItems.FuseBurns(state, bomb), Is.True, "or it would sit there for the rest of the round");
        }

        [Test]
        public void EveryCurseWearsOffAndTheControlsComeBack()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));
            PlayerState player = state.Players[0];
            player.Curse = CurseKind.Reversed;
            player.CurseTicksRemaining = 2;

            Run(state, 1, PlayerInput.Moving(Direction.Right), PlayerInput.None);
            Assert.That(player.Position.X, Is.LessThan(SubPos.CentreOf(1)), "reversed controls walk the other way");

            Run(state, 1, PlayerInput.None, PlayerInput.None);
            Assert.That(player.Curse, Is.EqualTo(CurseKind.None));
        }

        [Test]
        public void TheCursesDoWhatTheySay()
        {
            PlayerInput walk = PlayerInput.Moving(Direction.Right);

            Assert.That(ClassicItems.Cursed(CurseKind.Leaky, walk).DropBomb, Is.True);
            Assert.That(ClassicItems.Cursed(CurseKind.Dry, PlayerInput.Dropping()).DropBomb, Is.False);
            Assert.That(ClassicItems.Cursed(CurseKind.Reversed, walk).MoveX, Is.LessThan(0));

            var player = new PlayerState(0, SubPos.AtTileCentre(new GridPos(1, 1)), Classic);
            int normal = ClassicItems.Speed(player, Classic);
            player.Curse = CurseKind.Slow;
            Assert.That(ClassicItems.Speed(player, Classic), Is.LessThan(normal));
            player.Curse = CurseKind.Hasty;
            Assert.That(ClassicItems.Speed(player, Classic), Is.GreaterThan(normal));
        }

        [Test]
        public void TheSkullPicksACurseAndATimer()
        {
            MatchState state = Open(new GridPos(1, 2), new GridPos(9, 0));

            ClassicItems.Apply(state, state.Players[0], PowerUpKind.Skull);

            Assert.That(state.Players[0].Curse, Is.Not.EqualTo(CurseKind.None));
            Assert.That(state.Players[0].CurseTicksRemaining, Is.EqualTo(Classic.TicksPerSecond * ClassicItems.CurseSeconds));
        }

        [Test]
        public void OnlyTheClassicModesHideTheNewItemsAndOnlyUnderOtherwiseEmptyBlocks()
        {
            var arena = new Arena(40, 40);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    arena[new GridPos(x, y)] = TileKind.SoftBlock;
                }
            }

            var classic = PowerUpPlacer.Place(arena, 50, 9u, true);
            var arenaMode = PowerUpPlacer.Place(arena, 50, 9u, false);

            Assert.That(classic.ContainsValue(PowerUpKind.Kick), Is.True);
            Assert.That(classic.ContainsValue(PowerUpKind.Remote), Is.True);
            Assert.That(classic.ContainsValue(PowerUpKind.Skull), Is.True);
            Assert.That(arenaMode.ContainsValue(PowerUpKind.Kick) || arenaMode.ContainsValue(PowerUpKind.Remote)
                || arenaMode.ContainsValue(PowerUpKind.Skull), Is.False);
            foreach (var pair in arenaMode)
            {
                Assert.That(classic[pair.Key], Is.EqualTo(pair.Value), "the bread and butter pickups stay where they were");
            }

            Assert.That(RuleSet.Classic.ClassicItems && RuleSet.ClassicBlinded.ClassicItems && !RuleSet.Arena.ClassicItems, Is.True);
        }
    }
}
