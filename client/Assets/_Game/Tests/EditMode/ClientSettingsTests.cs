using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class ClientSettingsTests
    {
        [Test]
        public void VolumeStopsAtSilenceAndAtFull()
        {
            Assert.That(ClientSettings.StepVolume(0, -1), Is.Zero);
            Assert.That(ClientSettings.StepVolume(ClientSettings.VolumeSteps, 1), Is.EqualTo(ClientSettings.VolumeSteps));
            Assert.That(ClientSettings.VolumeLevel(0), Is.Zero, "muted has to be silent, not quiet");
            Assert.That(ClientSettings.VolumeLevel(ClientSettings.VolumeSteps), Is.EqualTo(1f));
            Assert.That(ClientSettings.VolumeLevel(99), Is.EqualTo(1f), "a value saved by another build never goes past full");
        }

        [Test]
        public void TheHudSizeStepsWithoutWrappingAndNormalChangesNothing()
        {
            Assert.That(ClientSettings.StepHud(HudSize.Large, 1), Is.EqualTo(HudSize.Large));
            Assert.That(ClientSettings.StepHud(HudSize.Small, -1), Is.EqualTo(HudSize.Small));
            Assert.That(ClientSettings.HudFactor(HudSize.Normal), Is.EqualTo(1f));
            Assert.That(ClientSettings.HudFactor(HudSize.Small), Is.LessThan(1f));
            Assert.That(ClientSettings.HudFactor(HudSize.Large), Is.GreaterThan(1f));
        }
    }
}
