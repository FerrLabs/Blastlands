using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ArchivePathTests
    {
        [Test]
        public void EntriesInsideTheBuildAreExtracted()
        {
            Assert.That(ArchivePath.IsSafe("Blastlands.exe"), Is.True);
            Assert.That(ArchivePath.IsSafe("Blastlands_Data/level0"), Is.True);
            Assert.That(ArchivePath.IsSafe("Blastlands_Data\\Managed\\Assembly-CSharp.dll"), Is.True);
            Assert.That(ArchivePath.IsSafe("Blastlands_Data/"), Is.True);
        }

        [Test]
        public void AnEntryClimbingOutOfTheStagingDirectoryIsRefused()
        {
            Assert.That(ArchivePath.IsSafe("../Blastlands.exe"), Is.False);
            Assert.That(ArchivePath.IsSafe("Blastlands_Data/../../evil.dll"), Is.False);
            Assert.That(ArchivePath.IsSafe("Blastlands_Data\\..\\..\\evil.dll"), Is.False);
            Assert.That(ArchivePath.IsSafe(".."), Is.False);
        }

        [Test]
        public void AnAbsoluteEntryIsRefused()
        {
            Assert.That(ArchivePath.IsSafe("/etc/profile"), Is.False);
            Assert.That(ArchivePath.IsSafe("\\Windows\\System32\\evil.dll"), Is.False);
            Assert.That(ArchivePath.IsSafe("C:/Windows/evil.dll"), Is.False);
            Assert.That(ArchivePath.IsSafe("C:evil.dll"), Is.False);
        }

        [Test]
        public void AnAlternateDataStreamIsRefused()
        {
            Assert.That(ArchivePath.IsSafe("Blastlands.exe:hidden"), Is.False);
        }

        [Test]
        public void AnEmptyEntryIsRefused()
        {
            Assert.That(ArchivePath.IsSafe(""), Is.False);
            Assert.That(ArchivePath.IsSafe(null), Is.False);
        }
    }
}
