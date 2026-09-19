using System.IO;
using System.Text;
using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class DigestTests
    {
        private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        private static Stream Content(string text)
        {
            return new MemoryStream(Encoding.ASCII.GetBytes(text));
        }

        [Test]
        public void TheDigestOfTheDownloadedBytesIsAccepted()
        {
            Assert.That(Digest.Matches(Content("abc"), AbcSha256), Is.True);
        }

        [Test]
        public void ADownloadWhoseBytesDifferIsRefused()
        {
            Assert.That(Digest.Matches(Content("abd"), AbcSha256), Is.False);
        }

        [Test]
        public void AnExpectedDigestThatIsNotASha256RefusesEverything()
        {
            Assert.That(Digest.Matches(Content("abc"), ""), Is.False);
            Assert.That(Digest.Matches(Content("abc"), null), Is.False);
            Assert.That(Digest.Matches(Content("abc"), AbcSha256.ToUpperInvariant()), Is.False);
        }

        [Test]
        public void OnlyLowercaseSixtyFourCharacterHexIsASha256()
        {
            Assert.That(Digest.IsSha256(AbcSha256), Is.True);
            Assert.That(Digest.IsSha256(AbcSha256.Substring(1)), Is.False);
            Assert.That(Digest.IsSha256(AbcSha256 + "a"), Is.False);
            Assert.That(Digest.IsSha256(AbcSha256.Replace('b', 'g')), Is.False);
            Assert.That(Digest.IsSha256(null), Is.False);
        }
    }
}
