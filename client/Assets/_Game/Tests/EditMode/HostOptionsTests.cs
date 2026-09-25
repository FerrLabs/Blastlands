using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    // The ports a process serves have to be exactly the ones the lobby's range and the
    // pod's Services say it serves. A process one port off listens where nobody is sent
    // and leaves a port the lobby offers with nothing behind it, and nothing crashes to
    // say so. Most of these pin the layout down.
    public class HostOptionsTests
    {
        private static readonly string[] Complete =
        {
            "--port", "7000",
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

        private static Func<string, string> Pod(string name, string slots = null)
        {
            return Environment(
                HostOptions.PortBaseVariable, "31000",
                HostOptions.PodNameVariable, name,
                HostOptions.SlotsVariable, slots,
                HostOptions.LobbyVariable, "https://api.blastlands.ferrlabs.com",
                HostOptions.TokenVariable, "shared-secret");
        }

        [Test]
        public void AFullCommandLineIsRead()
        {
            Assert.That(HostOptions.TryRead(Complete, NoEnvironment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.FirstPort, Is.EqualTo(7000));
            Assert.That(options.LobbyUrl, Is.EqualTo("https://lobby.example.com"));
            Assert.That(options.InstanceToken, Is.EqualTo("shared-secret"));
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
        public void OneSlotPerPodLaysThePortsOutAsBeforeAProcessCouldCarryMore()
        {
            // An image rolled out before the deployment asks for more slots has to land
            // on the ports the Services and the lobby already expect.
            Assert.That(HostOptions.TryRead(null, Pod("blastlands-server-2"), out HostOptions options, out string error), Is.True, error);

            Assert.That(options.FirstPort, Is.EqualTo(31002));
            Assert.That(options.Slots, Is.EqualTo(1));
        }

        [Test]
        public void EachPodTakesARunOfPortsAsWideAsItsSlots()
        {
            Assert.That(HostOptions.TryRead(null, Pod("blastlands-server-0", "4"), out HostOptions first, out string error), Is.True, error);
            Assert.That(HostOptions.TryRead(null, Pod("blastlands-server-2", "4"), out HostOptions third, out error), Is.True, error);

            Assert.That(first.PortOf(0), Is.EqualTo(31000));
            Assert.That(first.PortOf(3), Is.EqualTo(31003));
            Assert.That(third.PortOf(0), Is.EqualTo(31008), "pod 1 took 31004 to 31007");
            Assert.That(third.PortOf(3), Is.EqualTo(31011));
        }

        [Test]
        public void AnExplicitPortStartsTheRunWhateverThePodIsCalled()
        {
            Func<string, string> environment = Environment(
                HostOptions.PortBaseVariable, "31000",
                HostOptions.PodNameVariable, "blastlands-server-3",
                HostOptions.SlotsVariable, "4");

            Assert.That(HostOptions.TryRead(Complete, environment, out HostOptions options, out string error), Is.True, error);

            Assert.That(options.PortOf(0), Is.EqualTo(7000));
            Assert.That(options.PortOf(3), Is.EqualTo(7003));
        }

        [Test]
        public void ASlotCountOutsideTheRangeIsRefused()
        {
            foreach (string slots in new[] { "0", "-1", "33", "four" })
            {
                Assert.That(HostOptions.TryRead(null, Pod("blastlands-server-0", slots), out _, out string error), Is.False, slots);
                Assert.That(error, Does.Contain(HostOptions.SlotsVariable));
            }
        }

        [Test]
        public void APodNameWithoutAnOrdinalIsRefused()
        {
            Assert.That(HostOptions.TryRead(null, Pod("blastlands-server"), out _, out string error), Is.False);
            Assert.That(error, Does.Contain(HostOptions.PodNameVariable));
        }

        [Test]
        public void ARunOfPortsThatWouldPassTheLastOneIsRefused()
        {
            string[] arguments = { "--port", "65534", "--lobby", "http://x", "--token", "s" };

            Assert.That(
                HostOptions.TryRead(arguments, Environment(HostOptions.SlotsVariable, "4"), out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("65534"));
        }

        [Test]
        public void APortOutsideTheRangeIsRefused()
        {
            foreach (string port in new[] { "0", "65536", "-1", "seven" })
            {
                string[] arguments = { "--port", port, "--lobby", "http://x", "--token", "s" };

                Assert.That(HostOptions.TryRead(arguments, NoEnvironment, out _, out _), Is.False, $"accepted port {port}");
            }
        }

        [Test]
        public void EachMissingSettingSaysWhichOneAndHowToSupplyIt()
        {
            foreach (string flag in new[] { HostOptions.PortFlag, HostOptions.LobbyFlag, HostOptions.TokenFlag })
            {
                var without = new List<string>(Complete);
                without.RemoveRange(without.IndexOf(flag), 2);

                Assert.That(HostOptions.TryRead(without, NoEnvironment, out _, out string error), Is.False, $"started without {flag}");
                Assert.That(error, Does.Contain(flag), "the message does not name the missing setting");
            }
        }

        [Test]
        public void AFlagWithNothingAfterItIsMissingRatherThanEmpty()
        {
            // The last argument being a bare flag is what a shell leaves behind when a
            // variable it was expanding was empty.
            string[] arguments = { "--lobby", "http://x", "--token", "s", "--port" };

            Assert.That(HostOptions.TryRead(arguments, NoEnvironment, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(HostOptions.PortFlag));
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
                string[] arguments = { "--port", "7000", "--lobby", lobby, "--token", "s" };

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
                    new[] { "--port", " 7000 ", "--lobby", " http://x ", "--token", " s " },
                    Environment(HostOptions.SlotsVariable, " 2 "),
                    out HostOptions options,
                    out string error),
                Is.True,
                error);

            Assert.That(options.FirstPort, Is.EqualTo(7000));
            Assert.That(options.Slots, Is.EqualTo(2));
            Assert.That(options.LobbyUrl, Is.EqualTo("http://x"));
            Assert.That(options.InstanceToken, Is.EqualTo("s"));
        }
    }
}
