using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // These read the numbers against what they physically mean, which almost nothing
    // else does. The rest of the suite computes its expectations out of the same
    // settings it is testing, so `wallFace - Settings.PlayerRadius - 1` agrees with
    // itself whatever the radius happens to be.
    //
    // Written after a transposition in `Default` shipped and shifted the last three
    // values by one. It was caught, but by a sudden death test failing, which says
    // nothing about a settings constructor. Each of these three would have named it.
    public class MatchSettingsTests
    {
        private static readonly MatchSettings Settings = MatchSettings.Default;

        [Test]
        public void APlayerIsABodyRatherThanAPoint()
        {
            // Smaller than half a tile so two bodies pass in a corridor, and not so
            // small that one stands in the middle of a lane touching neither wall, which
            // is what the corner assist is there to work against.
            Assert.That(Settings.PlayerRadius, Is.LessThan(SubPos.UnitsPerTile / 2));
            Assert.That(Settings.PlayerRadius, Is.GreaterThan(SubPos.UnitsPerTile / 4));
        }

        [Test]
        public void TheCornerAssistIsANudgeAndNotAJump()
        {
            // It pushes a body onto an open lane when it walks into the edge of one. At
            // more than the body's own half-width it stops being a correction and starts
            // moving the player somewhere they did not aim.
            Assert.That(Settings.CornerAssist, Is.GreaterThan(0), "zero makes corridor mouths sticky");
            Assert.That(Settings.CornerAssist, Is.LessThan(Settings.PlayerRadius));
        }

        [Test]
        public void ABombLyingInFireGoesOffFasterThanOneJustPlaced()
        {
            // The whole point of the shorter fuse is that it reads as a chain reaction.
            // At or above the normal fuse there is no chain, just a second bomb.
            Assert.That(Settings.LooseBombFuseTicks, Is.LessThan(Settings.FuseTicks));
            Assert.That(Settings.LooseBombFuseTicks, Is.GreaterThan(0), "it still has to be worth reacting to");
        }

        [Test]
        public void TheFuseIsTheTwoAndAHalfSecondsTheFileClaims()
        {
            // The header of MatchSettings says a fuse of 75 ticks is two and a half
            // seconds, which is only true while the tick rate agrees with it.
            Assert.That(Settings.FuseTicks * 2, Is.EqualTo(Settings.TicksPerSecond * 5));
        }

        [Test]
        public void EverySpeedPickupIsWorthTaking()
        {
            for (int steps = 1; steps <= Settings.MaxSpeedSteps; steps++)
            {
                Assert.That(
                    Settings.SpeedFor(steps),
                    Is.GreaterThan(Settings.SpeedFor(steps - 1)),
                    $"pickup {steps} does nothing");
            }
        }

        [Test]
        public void ACopiedSettingChangesOneThingAndLeavesTheRest()
        {
            // The copy constructor the With helpers share. A field it forgot would not
            // compile, but one it took from the wrong place would.
            MatchSettings changed = Settings
                .WithRules(RuleSet.Classic)
                .WithTilesPerLooseBomb(0)
                .WithSuddenDeath(SuddenDeathSettings.Off);

            // Read against Default rather than against Classic, or the first line would
            // be asserting that Classic equals Classic.
            Assert.That(Settings.Rules.BombsReturn, Is.False, "the fixture stopped distinguishing the two");
            Assert.That(changed.Rules.BombsReturn, Is.True);
            Assert.That(Settings.TilesPerLooseBomb, Is.GreaterThan(0));
            Assert.That(changed.TilesPerLooseBomb, Is.EqualTo(0));
            Assert.That(Settings.SuddenDeath.Enabled, Is.True);
            Assert.That(changed.SuddenDeath.Enabled, Is.False);

            Assert.That(changed.PlayerRadius, Is.EqualTo(Settings.PlayerRadius));
            Assert.That(changed.CornerAssist, Is.EqualTo(Settings.CornerAssist));
            Assert.That(changed.LooseBombFuseTicks, Is.EqualTo(Settings.LooseBombFuseTicks));
            Assert.That(changed.FuseTicks, Is.EqualTo(Settings.FuseTicks));
            Assert.That(changed.TicksPerSecond, Is.EqualTo(Settings.TicksPerSecond));
            Assert.That(changed.BombRespawnTicks, Is.EqualTo(Settings.BombRespawnTicks));
            Assert.That(changed.MaxFireRange, Is.EqualTo(Settings.MaxFireRange));
            Assert.That(changed.DashSpeed, Is.EqualTo(Settings.DashSpeed));
        }
    }
}
