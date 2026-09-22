using Blastlands.Core;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ClientOptionsTests
    {
        [Test]
        public void AShippedBuildTalksToTheProductionLobby()
        {
            Assert.That(ClientOptions.Lobby(new[] { "Blastlands.exe" }), Is.EqualTo("https://api.blastlands.ferrlabs.com"));
        }

        [Test]
        public void TheLobbyFlagPointsTheClientElsewhere()
        {
            string[] arguments = { "Blastlands.exe", "--lobby", " http://127.0.0.1:8080/ " };

            Assert.That(ClientOptions.Lobby(arguments), Is.EqualTo("http://127.0.0.1:8080"));
        }

        [Test]
        public void ALobbyThatIsNotAnHttpUrlKeepsTheDefault()
        {
            Assert.That(
                ClientOptions.Lobby(new[] { "Blastlands.exe", "--lobby", "api.blastlands.ferrlabs.com" }),
                Is.EqualTo(ClientOptions.DefaultLobby));
            Assert.That(
                ClientOptions.Lobby(new[] { "Blastlands.exe", "--lobby", "file:///etc/passwd" }),
                Is.EqualTo(ClientOptions.DefaultLobby));
        }

        [Test]
        public void AFlagWithoutAValueKeepsTheDefault()
        {
            Assert.That(ClientOptions.Lobby(new[] { "Blastlands.exe", "--lobby" }), Is.EqualTo(ClientOptions.DefaultLobby));
            Assert.That(ClientOptions.Lobby(new[] { "Blastlands.exe", "--lobby", " " }), Is.EqualTo(ClientOptions.DefaultLobby));
        }

        [Test]
        public void ScreenShakeIsOnUnlessTurnedOff()
        {
            Assert.That(ClientOptions.ScreenShake(new[] { "Blastlands.exe" }), Is.True);
            Assert.That(ClientOptions.ScreenShake(new[] { "Blastlands.exe", "--no-shake" }), Is.False);
        }

        [Test]
        public void TheShakeFlagIsReadWhereverItSits()
        {
            string[] arguments = { "Blastlands.exe", "--lobby", "http://127.0.0.1:8080", "--no-shake" };

            Assert.That(ClientOptions.ScreenShake(arguments), Is.False);
            Assert.That(ClientOptions.Lobby(arguments), Is.EqualTo("http://127.0.0.1:8080"));
        }

        [Test]
        public void OnlyTheExactFlagTurnsShakeOff()
        {
            Assert.That(ClientOptions.ScreenShake(new[] { "Blastlands.exe", "--no-shaker" }), Is.True);
            Assert.That(ClientOptions.ScreenShake(new[] { "Blastlands.exe", "--No-Shake" }), Is.True);
        }
    }
}
