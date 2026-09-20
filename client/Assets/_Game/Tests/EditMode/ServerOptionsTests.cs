using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // An instance that starts on the wrong numbers is worse than one that refuses to
    // start: a wrong port collides with a neighbour, a wrong match id releases somebody
    // else's match, and a lobby it cannot reach means a port held until a restart. So
    // most of these are about refusing, and about saying why.
    public class ServerOptionsTests
    {
        private static readonly string[] Complete =
        {
            "--port", "7777",
            "--match", "abc123",
            "--players", "4",
            "--lobby", "https://lobby.example.com",
            "--token", "shared-secret"
        };

        private static Func<string, string> NoEnvironment
        {
            get { return _ => null; }
        }

        private static Func<string, string> Environment(params string[] pairs)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                map[pairs[i]] = pairs[i + 1];
            }

            return key => map.TryGetValue(key, out string value) ? value : null;
        }

        [Test]
        public void AFullCommandLineIsRead()
        {
            Assert.That(
                ServerOptions.TryRead(Complete, NoEnvironment, out ServerOptions options, out string error),
                Is.True,
                error);

            Assert.That(options.ListenPort, Is.EqualTo(7777));
            Assert.That(options.MatchId, Is.EqualTo("abc123"));
            Assert.That(options.ExpectedPlayers, Is.EqualTo(4));
            Assert.That(options.LobbyUrl, Is.EqualTo("https://lobby.example.com"));
            Assert.That(options.InstanceToken, Is.EqualTo("shared-secret"));
        }

        [Test]
        public void TheEnvironmentAnswersWhateverTheArgumentsDoNot()
        {
            string[] arguments = { "--port", "7777" };

            Assert.That(
                ServerOptions.TryRead(
                    arguments,
                    Environment(
                        ServerOptions.MatchVariable, "abc123",
                        ServerOptions.PlayersVariable, "2",
                        ServerOptions.LobbyVariable, "http://lobby.internal",
                        ServerOptions.TokenVariable, "shared-secret"),
                    out ServerOptions options,
                    out string error),
                Is.True,
                error);

            Assert.That(options.ListenPort, Is.EqualTo(7777));
            Assert.That(options.ExpectedPlayers, Is.EqualTo(2));
        }

        [Test]
        public void AnArgumentBeatsTheEnvironment()
        {
            // A host is configured once through the environment; an argument is how one
            // instance out of several on that host is told which one it is. If the
            // general setting won, every instance on the box would take the same port.
            Assert.That(
                ServerOptions.TryRead(
                    Complete,
                    Environment(ServerOptions.PortVariable, "9999"),
                    out ServerOptions options,
                    out _),
                Is.True);

            Assert.That(options.ListenPort, Is.EqualTo(7777));
        }

        [Test]
        public void EachMissingSettingSaysWhichOneAndHowToSupplyIt()
        {
            foreach (string flag in new[]
                     {
                         ServerOptions.PortFlag,
                         ServerOptions.MatchFlag,
                         ServerOptions.PlayersFlag,
                         ServerOptions.LobbyFlag,
                         ServerOptions.TokenFlag
                     })
            {
                var without = new List<string>(Complete);
                int at = without.IndexOf(flag);
                without.RemoveRange(at, 2);

                Assert.That(
                    ServerOptions.TryRead(without, NoEnvironment, out _, out string error),
                    Is.False,
                    $"started without {flag}");

                Assert.That(error, Does.Contain(flag), "the message does not name the missing setting");
            }
        }

        [Test]
        public void AFlagWithNothingAfterItIsMissingRatherThanEmpty()
        {
            // The last argument being a bare flag is what a shell leaves behind when a
            // variable it was expanding was empty. Reading past the end of the array
            // would be the obvious way to crash on it.
            string[] arguments = { "--match", "abc", "--players", "2", "--lobby", "http://x", "--token", "s", "--port" };

            Assert.That(ServerOptions.TryRead(arguments, NoEnvironment, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.PortFlag));
        }

        [Test]
        public void ANumberThatIsNotOneIsRefusedRatherThanTreatedAsZero()
        {
            string[] arguments = { "--port", "seven", "--match", "abc", "--players", "2", "--lobby", "http://x", "--token", "s" };

            Assert.That(ServerOptions.TryRead(arguments, NoEnvironment, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("seven"));
        }

        [Test]
        public void APortOutsideTheRangeIsRefused()
        {
            foreach (string port in new[] { "0", "65536", "-1" })
            {
                string[] arguments = { "--port", port, "--match", "abc", "--players", "2", "--lobby", "http://x", "--token", "s" };

                Assert.That(
                    ServerOptions.TryRead(arguments, NoEnvironment, out _, out _),
                    Is.False,
                    $"accepted port {port}");
            }
        }

        [Test]
        public void AMatchWithNobodyInItIsRefused()
        {
            string[] arguments = { "--port", "7777", "--match", "abc", "--players", "0", "--lobby", "http://x", "--token", "s" };

            Assert.That(ServerOptions.TryRead(arguments, NoEnvironment, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(ServerOptions.PlayersFlag));
        }

        [Test]
        public void TheCeilingOnPlayersIsLeftToTheArena()
        {
            // How many an arena seats is how many spawns it generated, which MatchFactory
            // knows and this does not. Restating a number here would be a second answer
            // to drift away from the first, so a high count is accepted and fails later
            // with the count the arena actually has.
            string[] arguments = { "--port", "7777", "--match", "abc", "--players", "99", "--lobby", "http://x", "--token", "s" };

            Assert.That(ServerOptions.TryRead(arguments, NoEnvironment, out ServerOptions options, out _), Is.True);
            Assert.That(options.ExpectedPlayers, Is.EqualTo(99));

            // Spelled TestDelegate rather than passed inline, the same way
            // ArenaGeneratorTests does and for the same reason: NUnit 4 overloads
            // Assert.Throws on both TestDelegate and Action, so a bare lambda is
            // ambiguous. Unity ships NUnit 3.5, which has only the TestDelegate
            // overload, so the editor compiles the lambda happily and the dotnet build
            // in CI does not. TestDelegate is the form both versions accept.
            TestDelegate tooManyForTheBoard = () =>
                MatchFactory.Create(ArenaSettings.Default, MatchSettings.Default, options.ExpectedPlayers, 1u);

            Assert.Throws<ArgumentOutOfRangeException>(tooManyForTheBoard);
        }

        [Test]
        public void ALobbyThatIsNotAnHttpUrlIsRefused()
        {
            // It is handed to an HTTP client later. A bare host or a file path fails
            // there instead, at the moment the match is trying to report its result.
            foreach (string lobby in new[] { "lobby.example.com", "ftp://lobby", "/var/run/lobby.sock" })
            {
                string[] arguments = { "--port", "7777", "--match", "abc", "--players", "2", "--lobby", lobby, "--token", "s" };

                Assert.That(
                    ServerOptions.TryRead(arguments, NoEnvironment, out _, out _),
                    Is.False,
                    $"accepted {lobby}");
            }
        }

        [Test]
        public void SurroundingSpaceIsTrimmedRatherThanCarried()
        {
            // A compose file or a systemd unit quotes values, and the space inside the
            // quotes survives. A match id with a trailing space is a different id to the
            // lobby, and the release call would quietly 404.
            Assert.That(
                ServerOptions.TryRead(
                    new[] { "--port", " 7777 ", "--match", " abc ", "--players", " 2 ", "--lobby", " http://x ", "--token", " s " },
                    NoEnvironment,
                    out ServerOptions options,
                    out string error),
                Is.True,
                error);

            Assert.That(options.MatchId, Is.EqualTo("abc"));
            Assert.That(options.LobbyUrl, Is.EqualTo("http://x"));
            Assert.That(options.ListenPort, Is.EqualTo(7777));
        }

        [Test]
        public void TheSplitTheImageEntrypointUsesIsAccepted()
        {
            // What a pod on a fixed port actually passes: the port, the lobby and the
            // token are the host's configuration and stay in the environment, where the
            // token is also out of the process table. Only the two that change from one
            // match to the next arrive as arguments.
            Assert.That(
                ServerOptions.TryRead(
                    new[] { "--match", "abc123", "--players", "3" },
                    Environment(
                        ServerOptions.PortVariable, "7001",
                        ServerOptions.LobbyVariable, "https://api.blastlands.ferrlabs.com",
                        ServerOptions.TokenVariable, "shared-secret"),
                    out ServerOptions options,
                    out string error),
                Is.True,
                error);

            Assert.That(options.ListenPort, Is.EqualTo(7001));
            Assert.That(options.MatchId, Is.EqualTo("abc123"));
            Assert.That(options.ExpectedPlayers, Is.EqualTo(3));
            Assert.That(options.LobbyUrl, Is.EqualTo("https://api.blastlands.ferrlabs.com"));
            Assert.That(options.InstanceToken, Is.EqualTo("shared-secret"));
        }
    }
}
