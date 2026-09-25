using System;
using System.IO;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class UpdateLayoutTests
    {
        private static readonly string Parent = Path.Combine(Path.GetTempPath(), "Games");

        private static readonly GameVersion Version = new GameVersion(26, 9, 20);

        [Test]
        public void TheNewBuildAndTheOldOneLiveBesideTheInstall()
        {
            var layout = new UpdateLayout(Path.Combine(Parent, "Blastlands"), Version);

            Assert.That(layout.Install, Is.EqualTo(Path.Combine(Parent, "Blastlands")));
            Assert.That(layout.Staged, Is.EqualTo(Path.Combine(Parent, "Blastlands.update-26.9.20")));
            Assert.That(layout.Previous, Is.EqualTo(Path.Combine(Parent, "Blastlands.previous")));
        }

        [Test]
        public void ABuildThatFitsBesideTheInstallIsNotTooDeep()
        {
            var layout = new UpdateLayout(Path.Combine(Parent, "Blastlands"), Version);

            Assert.That(layout.TooDeepFor(new[] { "Blastlands.exe", Path.Combine("Blastlands_Data", "Managed", "Core.dll") }), Is.Null);
        }

        [Test]
        public void TheLimitIsTheLongestPathTheStagedCopyNeeds()
        {
            var layout = new UpdateLayout(Path.Combine(Parent, "Blastlands"), Version);
            int room = UpdateLayout.LongestPath - layout.Staged.Length - 1;

            string fits = new string('a', room);
            string over = new string('a', room + 1);

            Assert.That(layout.TooDeepFor(new[] { "Blastlands.exe", fits }), Is.Null, "259 characters is still a path Windows takes");
            Assert.That(layout.TooDeepFor(new[] { "Blastlands.exe", over }), Does.Contain("260 characters, the limit is 259"));
            Assert.That(layout.TooDeepFor(new[] { "Blastlands.exe", over }), Does.Contain("shorter folder"));
        }

        [Test]
        public void ATrailingSeparatorDoesNotMoveTheSiblings()
        {
            string install = Path.Combine(Parent, "Blastlands") + Path.DirectorySeparatorChar;

            var layout = new UpdateLayout(install, Version);

            Assert.That(layout.Install, Is.EqualTo(Path.Combine(Parent, "Blastlands")));
            Assert.That(layout.Staged, Is.EqualTo(Path.Combine(Parent, "Blastlands.update-26.9.20")));
        }

        [Test]
        public void AnInstallWithNoParentHasNowhereToStage()
        {
            TestDelegate atRoot = () => new UpdateLayout(Path.GetPathRoot(Parent), Version);

            Assert.Throws<ArgumentException>(atRoot);
        }
    }
}
