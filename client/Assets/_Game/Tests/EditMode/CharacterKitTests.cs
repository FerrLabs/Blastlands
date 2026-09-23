using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class CharacterKitTests
    {
        private static readonly MatchSettings Arena = MatchSettings.Default.WithSuddenDeath(SuddenDeathSettings.Off);

        private static PlayerState Spawned(CharacterKind kind, MatchSettings settings)
        {
            return new PlayerState(0, SubPos.AtTileCentre(new GridPos(1, 1)), settings, kind);
        }

        [Test]
        public void EachKitIsTheHeadStartItNames()
        {
            PlayerState plain = Spawned(CharacterKind.None, Arena);

            Assert.That(Spawned(CharacterKind.Demolisher, Arena).FireRange, Is.EqualTo(plain.FireRange + 1));
            Assert.That(Spawned(CharacterKind.Runner, Arena).SpeedSteps, Is.EqualTo(plain.SpeedSteps + 2));
            Assert.That(Spawned(CharacterKind.Sapper, Arena).NextBombKind, Is.EqualTo(BombKind.Pierce));
            Assert.That(Spawned(CharacterKind.Grenadier, Arena).NextBombKind, Is.EqualTo(BombKind.Cluster));
        }

        // Starting with room for one bomb is what stops a player having two blasts live
        // at once and walling themselves in. It is withheld on purpose and earned through
        // BombUp, so no kit may hand it out, now or when the roster grows.
        [Test]
        public void NoKitStartsAPlayerWithTwoBombsToHand()
        {
            PlayerState plain = Spawned(CharacterKind.None, Arena);

            foreach (CharacterKind kind in CharacterKits.All)
            {
                PlayerState kitted = Spawned(kind, Arena);
                Assert.That(kitted.CarryCapacity, Is.EqualTo(plain.CarryCapacity), kind.ToString());
                Assert.That(kitted.BombsHeld, Is.EqualTo(plain.BombsHeld), kind.ToString());
            }
        }

        // One head start each, and nothing else. A kit that quietly moved a second stat
        // would be the strongest one without anybody having decided it should be.
        [Test]
        public void AKitTouchesOnlyItsOwnStat()
        {
            PlayerState plain = Spawned(CharacterKind.None, Arena);
            PlayerState demolisher = Spawned(CharacterKind.Demolisher, Arena);

            Assert.That(demolisher.SpeedSteps, Is.EqualTo(plain.SpeedSteps));
            Assert.That(demolisher.CarryCapacity, Is.EqualTo(plain.CarryCapacity));
            Assert.That(demolisher.BombsHeld, Is.EqualTo(plain.BombsHeld));
            Assert.That(demolisher.NextBombKind, Is.EqualTo(plain.NextBombKind));
        }

        // A head start on a pickup, never a way past what the pickups can reach.
        [Test]
        public void AKitStopsAtTheSameCeilingThePickupsDo()
        {
            PlayerState player = Spawned(CharacterKind.None, Arena);
            player.FireRange = Arena.MaxFireRange;
            player.SpeedSteps = Arena.MaxSpeedSteps;

            CharacterKits.Apply(player, CharacterKind.Demolisher, Arena);
            CharacterKits.Apply(player, CharacterKind.Runner, Arena);

            Assert.That(player.FireRange, Is.EqualTo(Arena.MaxFireRange));
            Assert.That(player.SpeedSteps, Is.EqualTo(Arena.MaxSpeedSteps));
        }

        [Test]
        public void AFullMatchSeatsOneOfEach()
        {
            var seen = new HashSet<CharacterKind>();
            for (int seat = 0; seat < CharacterKits.All.Count; seat++)
            {
                seen.Add(CharacterKits.ForSeat(seat));
            }

            Assert.That(seen.Count, Is.EqualTo(CharacterKits.All.Count));
            Assert.That(seen, Has.No.Member(CharacterKind.None));
            Assert.That(CharacterKits.ForSeat(CharacterKits.All.Count), Is.EqualTo(CharacterKits.ForSeat(0)));
        }

        [Test]
        public void TheArenaHandsEachSeatItsKit()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Arena, 4, 3u, CharacterKits.ForSeat);

            for (int seat = 0; seat < 4; seat++)
            {
                Assert.That(state.Players[seat].Character, Is.EqualTo(CharacterKits.ForSeat(seat)));
            }

            Assert.That(state.Players[0].FireRange, Is.EqualTo(Arena.StartingFireRange + 1), "seat 0 is the Demolisher");
        }

        // Classic is one verb and the same tools for everybody. Passing it characters
        // changes nothing, so no caller can hand it a kit by mistake.
        [Test]
        public void ClassicHandsOutNoKitsEvenWhenAsked()
        {
            MatchSettings classic = MatchSettings.Classic.WithSuddenDeath(SuddenDeathSettings.Off);
            MatchState state = MatchFactory.Create(ArenaSettings.Classic, classic, 4, 3u, CharacterKits.ForSeat);

            foreach (PlayerState player in state.Players)
            {
                Assert.That(player.Character, Is.EqualTo(CharacterKind.None));
                Assert.That(player.FireRange, Is.EqualTo(classic.StartingFireRange));
                Assert.That(player.SpeedSteps, Is.EqualTo(0));
                Assert.That(player.NextBombKind, Is.EqualTo(BombKind.Standard));
            }
        }

        // Every existing caller builds matches through the old overload, and every
        // balance bar in the suite was measured against players with no kit.
        [Test]
        public void TheOverloadWithoutCharactersStaysKitless()
        {
            MatchState state = MatchFactory.Create(ArenaSettings.Default, Arena, 4, 3u);

            foreach (PlayerState player in state.Players)
            {
                Assert.That(player.Character, Is.EqualTo(CharacterKind.None));
            }
        }
    }
}
