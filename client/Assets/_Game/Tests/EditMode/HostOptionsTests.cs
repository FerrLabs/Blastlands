using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class HostOptionsTests
    {
        private static readonly string[] Complete =
        {
            "--port", "7000",
            "--lobby", "https://lobby.example.com"
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
            Assert.That(HostOptions.TryRead(Complete, NoEnvironment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.FirstPort, Is.EqualTo(7000));
            Assert.That(options.LobbyUrl, Is.EqualTo("https://lobby.example.com"));
        }

        [Test]
        public void OneSlotAndATwoSecondPollUnlessToldOtherwise()
        {
            Assert.That(HostOptions.TryRead(Complete, NoEnvironment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.Slots, Is.EqualTo(1));
            Assert.That(options.PollSeconds, Is.EqualTo(2));
            Assert.That(options.DrainFile, Is.Null, "nothing supervises a process started by hand");
        }

        [Test]
        public void NothingGivenMeansPort7777OnTheDefaultLobby()
        {
            Assert.That(HostOptions.TryRead(new string[0], NoEnvironment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.FirstPort, Is.EqualTo(7777));
            Assert.That(options.Slots, Is.EqualTo(1));
            Assert.That(options.LobbyUrl, Is.EqualTo(ClientOptions.DefaultLobby));
        }

        [Test]
        public void ABlankLobbyFallsBackToTheDefault()
        {
            Assert.That(
                HostOptions.TryRead(null, Environment(HostOptions.LobbyVariable, "  "), out HostOptions options, out string error),
                Is.True,
                error);

            Assert.That(options.LobbyUrl, Is.EqualTo(ClientOptions.DefaultLobby));
        }

        [Test]
        public void ThePortAndSlotsComeFromTheEnvironmentToo()
        {
            Func<string, string> environment = Environment(
                HostOptions.PortVariable, "9000",
                HostOptions.SlotsVariable, "4");

            Assert.That(HostOptions.TryRead(null, environment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.PortOf(0), Is.EqualTo(9000));
            Assert.That(options.PortOf(3), Is.EqualTo(9003));
        }

        [Test]
        public void TheSlotsFlagBeatsTheEnvironment()
        {
            string[] arguments = { "--slots", "3" };

            Assert.That(
                HostOptions.TryRead(arguments, Environment(HostOptions.SlotsVariable, "8"), out HostOptions options, out string error),
                Is.True,
                error);

            Assert.That(options.Slots, Is.EqualTo(3));
        }

        [Test]
        public void SixteenSlotsIsTheMostTheLobbyAccepts()
        {
            Assert.That(
                HostOptions.TryRead(new[] { "--slots", "16" }, NoEnvironment, out HostOptions options, out string error),
                Is.True,
                error);
            Assert.That(options.Slots, Is.EqualTo(16));

            Assert.That(HostOptions.TryRead(new[] { "--slots", "17" }, NoEnvironment, out _, out error), Is.False);
            Assert.That(error, Does.Contain(HostOptions.SlotsFlag));
        }

        [Test]
        public void ASlotCountOutsideTheRangeIsRefused()
        {
            foreach (string slots in new[] { "0", "-1", "17", "four" })
            {
                Assert.That(
                    HostOptions.TryRead(null, Environment(HostOptions.SlotsVariable, slots), out _, out string error),
                    Is.False,
                    slots);
                Assert.That(error, Does.Contain(HostOptions.SlotsVariable));
            }
        }

        [Test]
        public void ARunOfPortsThatWouldPassTheLastOneIsRefused()
        {
            string[] arguments = { "--port", "65534", "--lobby", "http://x" };

            Assert.That(
                HostOptions.TryRead(arguments, Environment(HostOptions.SlotsVariable, "4"), out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("65534"));
        }

        [Test]
        public void APortSoLargeTheRunWrapsIsRefused()
        {
            // port + slots - 1 overflows int, and a wrapped negative "last port" would
            // slip under the upper bound and bind whatever the ushort cast made of it.
            string[] arguments = { "--port", "2147483647", "--lobby", "http://x" };

            Assert.That(
                HostOptions.TryRead(arguments, Environment(HostOptions.SlotsVariable, "2"), out _, out _),
                Is.False);
        }

        [Test]
        public void APortOutsideTheRangeIsRefused()
        {
            foreach (string port in new[] { "0", "65536", "-1", "seven" })
            {
                string[] arguments = { "--port", port, "--lobby", "http://x" };

                Assert.That(HostOptions.TryRead(arguments, NoEnvironment, out _, out _), Is.False, $"accepted port {port}");
            }
        }

        [Test]
        public void AFlagWithNothingAfterItIsRefusedRatherThanDefaulted()
        {
            foreach (string flag in new[] { HostOptions.PortFlag, HostOptions.LobbyFlag, HostOptions.SlotsFlag })
            {
                string[] arguments = { "--lobby", "http://x", flag };

                Assert.That(HostOptions.TryRead(arguments, NoEnvironment, out _, out string error), Is.False, flag);
                Assert.That(error, Does.Contain(flag));
            }
        }

        [Test]
        public void AnArgumentBeatsTheEnvironment()
        {
            Assert.That(
                HostOptions.TryRead(Complete, Environment(HostOptions.PortVariable, "9999"), out HostOptions options, out _),
                Is.True);

            Assert.That(options.FirstPort, Is.EqualTo(7000));
        }

        [Test]
        public void ALobbyThatIsNotAnHttpUrlIsRefused()
        {
            // It is handed to an HTTP client later. A bare host or a file path fails
            // there instead, on the first poll, with nothing pointing back here.
            foreach (string lobby in new[] { "lobby.example.com", "ftp://lobby", "/var/run/lobby.sock" })
            {
                string[] arguments = { "--port", "7000", "--lobby", lobby };

                Assert.That(HostOptions.TryRead(arguments, NoEnvironment, out _, out _), Is.False, $"accepted {lobby}");
            }
        }

        [Test]
        public void APollSlowerThanTheLobbysReadinessWindowAllowsIsRefused()
        {
            // The poll is also how a free slot stays on offer. Past MaxPollSeconds, the
            // gap between two polls can outlast the lobby's readiness window and the
            // port drops out of the pool with nothing in the logs to say why.
            Assert.That(
                HostOptions.TryRead(Complete, Environment(HostOptions.PollVariable, "30"), out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain(HostOptions.PollVariable));

            Assert.That(
                HostOptions.TryRead(Complete, Environment(HostOptions.PollVariable, "4"), out HostOptions options, out error),
                Is.True,
                error);
            Assert.That(options.PollSeconds, Is.EqualTo(4));
        }

        [Test]
        public void TheDrainFileIsTakenFromTheEnvironment()
        {
            Assert.That(
                HostOptions.TryRead(Complete, Environment(HostOptions.DrainFileVariable, " /tmp/drain "), out HostOptions options, out string error),
                Is.True,
                error);

            Assert.That(options.DrainFile, Is.EqualTo("/tmp/drain"));
        }

        [Test]
        public void SurroundingSpaceIsTrimmedRatherThanCarried()
        {
            Assert.That(
                HostOptions.TryRead(
                    new[] { "--port", " 7000 ", "--lobby", " http://x " },
                    Environment(HostOptions.SlotsVariable, " 2 "),
                    out HostOptions options,
                    out string error),
                Is.True,
                error);

            Assert.That(options.FirstPort, Is.EqualTo(7000));
            Assert.That(options.Slots, Is.EqualTo(2));
            Assert.That(options.LobbyUrl, Is.EqualTo("http://x"));
        }
    }
}
