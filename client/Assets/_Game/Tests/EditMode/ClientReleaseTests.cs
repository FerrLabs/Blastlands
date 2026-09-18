using Blastlands.Core.Lobby;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // What the lobby says about the build a player should be running, and whether that
    // answer is complete enough to act on.
    public class ClientReleaseTests
    {
        private static ClientRelease Release(string url, string sha)
        {
            return new ClientRelease("26.9.6", "26.8.0", url, sha);
        }

        [Test]
        public void AReleaseWithSomewhereToFetchAndSomethingToCheckIsUsable()
        {
            Assert.That(Release("https://example.test/b.zip", "abc").CanBeFetched, Is.True);
        }

        [Test]
        public void AReleaseWithNoDownloadIsNotFetchable()
        {
            // Real, not hypothetical: the release publishes the archive and its hash
            // together, so there is a window between a version being cut and those being
            // written where the lobby knows a build is out of date and not where it is.
            Assert.That(Release(null, "abc").CanBeFetched, Is.False);
            Assert.That(Release("", "abc").CanBeFetched, Is.False);
        }

        [Test]
        public void AReleaseWithNothingToCheckAgainstIsNotFetchable()
        {
            // An update that installs whatever it downloaded is a remote code execution
            // vector. Without a hash there is nothing to verify against, so the download
            // is refused rather than trusted.
            Assert.That(Release("https://example.test/b.zip", null).CanBeFetched, Is.False);
            Assert.That(Release("https://example.test/b.zip", "").CanBeFetched, Is.False);
        }

        [Test]
        public void APlainHttpDownloadIsRefused()
        {
            // The hash and the URL arrive over the same connection, so anybody able to
            // rewrite one can rewrite the other. Requiring TLS is what makes the hash
            // worth checking rather than a formality.
            Assert.That(Release("http://example.test/b.zip", "abc").CanBeFetched, Is.False);
            Assert.That(Release("ftp://example.test/b.zip", "abc").CanBeFetched, Is.False);
            Assert.That(Release("example.test/b.zip", "abc").CanBeFetched, Is.False);

            // Schemes are case-insensitive per RFC 3986, so this is a real URL and
            // refusing it would refuse a perfectly good release.
            Assert.That(Release("HTTPS://example.test/b.zip", "abc").CanBeFetched, Is.True);
        }

        [Test]
        public void TheVersionsAreCarriedThroughUntouched()
        {
            // Parsed by GameVersion rather than here, so this only has to not lose them.
            ClientRelease release = Release("https://example.test/b.zip", "abc");

            Assert.That(release.Latest, Is.EqualTo("26.9.6"));
            Assert.That(release.Minimum, Is.EqualTo("26.8.0"));
        }
    }
}
