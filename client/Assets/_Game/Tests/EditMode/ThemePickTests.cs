using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ThemePickTests
    {
        [Test]
        public void EveryClientOfAMatchPicksTheSameTheme()
        {
            const string match = "05ebdca0-596e-4dc8-87a1-0a68aa901f17";

            Assert.That(ThemePick.For(match, 4), Is.EqualTo(3), "pinned: clients only agree while this number never moves");
        }

        [Test]
        public void MatchesSpreadAcrossEveryTheme()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < 64; i++)
            {
                int pick = ThemePick.For(System.Guid.NewGuid().ToString(), 4);
                Assert.That(pick, Is.InRange(0, 3));
                seen.Add(pick);
            }

            Assert.That(seen.Count, Is.EqualTo(4), "online matches used to all get the first theme");
        }

        [Test]
        public void WithNothingToGoOnItFallsBackToTheFirstTheme()
        {
            Assert.That(ThemePick.For(null, 4), Is.Zero);
            Assert.That(ThemePick.For(string.Empty, 4), Is.Zero);
            Assert.That(ThemePick.For("abc", 0), Is.Zero);
        }
    }
}
