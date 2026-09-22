using System.Collections.Generic;
using System.Reflection;
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

        // The three the copy is asked to change. Everything else has to come through
        // untouched, including whatever is added to MatchSettings after this is written.
        private static readonly HashSet<string> Deliberate = new HashSet<string>
        {
            "Rules",
            "TilesPerLooseBomb",
            "SuddenDeath",
        };

        [Test]
        public void APlayerIsABodyRatherThanAPoint()
        {
            // Small enough that a body fits through a one-tile gap with room to spare,
            // which is what walking a corridor needs. Two bodies never pass each other
            // in one: at any radius above a quarter tile they are wider than the lane,
            // and the lower bound is what keeps a player a body rather than a dot the
            // corner assist would have nothing to nudge.
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

            // Every other property, read off the type rather than listed by hand: a
            // column of eight covers a third of what the constructor carries, misses a
            // field added later, and would pass a WallTelegraphTicks = from.WallRetryTicks
            // as long as neither appeared in the column.
            foreach (PropertyInfo property in typeof(MatchSettings).GetProperties())
            {
                if (Deliberate.Contains(property.Name))
                {
                    continue;
                }

                Assert.That(
                    property.GetValue(changed),
                    Is.EqualTo(property.GetValue(Settings)),
                    property.Name + " did not survive the copy");
            }
        }
    }
}
