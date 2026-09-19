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
