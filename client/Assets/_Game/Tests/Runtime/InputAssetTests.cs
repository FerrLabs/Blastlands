using Blastlands.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime.Tests
{
    // Drives real devices through the real asset. InputReadingTests covers the reading
    // logic on its own, which a binding resolving to nothing passes untouched: the
    // asset is written by hand, and a composite written flat leaves every key reading
    // zero with nothing logged and every other test still green.
    public class InputAssetTests : InputTestFixture
    {
        private PlayerDevices devices;
        private Keyboard keyboard;
        private Gamepad first;
        private Gamepad second;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            first = InputSystem.AddDevice<Gamepad>();
            second = InputSystem.AddDevice<Gamepad>();
            devices = new PlayerDevices(2);
        }

        public override void TearDown()
        {
            devices.Dispose();
            base.TearDown();
        }

        private PlayerInput Sampled(int player)
        {
            devices.PollPresses();
            return devices.Sample(player);
        }

        [Test]
        public void KeysMoveTheFirstPlayer()
        {
            Press(keyboard.dKey);
            Assert.That(Sampled(0).MoveX, Is.EqualTo(StickReader.Range));

            Release(keyboard.dKey);
            Press(keyboard.qKey);
            Assert.That(Sampled(0).MoveX, Is.EqualTo(-StickReader.Range));
        }

        // Up on a keyboard is down the grid, and the sign is flipped in one place only.
        [Test]
        public void KeysAgreeWithTheGridOnWhichWayIsUp()
        {
            Press(keyboard.wKey);
            Assert.That(Sampled(0).MoveY, Is.EqualTo(-StickReader.Range));

            Release(keyboard.wKey);
            Press(keyboard.sKey);
            Assert.That(Sampled(0).MoveY, Is.EqualTo(StickReader.Range));
        }

        [Test]
        public void TheOtherLayoutsReachTheSameAction()
        {
            Press(keyboard.rightArrowKey);
            Assert.That(Sampled(0).MoveX, Is.EqualTo(StickReader.Range));

            Release(keyboard.rightArrowKey);
            Press(keyboard.aKey);
            Assert.That(Sampled(0).MoveX, Is.EqualTo(-StickReader.Range));
        }

        [Test]
        public void TheKeyboardDrivesNobodyButTheFirstPlayer()
        {
            Press(keyboard.dKey);
            Assert.That(Sampled(1).MoveX, Is.EqualTo(0));
        }

        [Test]
        public void EachPadDrivesItsOwnPlayer()
        {
            Set(second.dpad.right, 1f);
            Assert.That(Sampled(1).MoveX, Is.EqualTo(StickReader.Range));
            Assert.That(Sampled(0).MoveX, Is.EqualTo(0));

            Set(second.dpad.right, 0f);
            Set(first.dpad.left, 1f);
            Assert.That(Sampled(0).MoveX, Is.EqualTo(-StickReader.Range));
        }

        // The stick is what a player holds between ticks, so it is read when the tick
        // asks rather than latched.
        [Test]
        public void TheStickReachesTheSeatItBelongsTo()
        {
            Set(second.leftStick, new UnityEngine.Vector2(1f, 0f));
            Assert.That(Sampled(1).MoveX, Is.GreaterThan(0));
        }

        [Test]
        public void APressBetweenTicksIsNotLost()
        {
            PressAndRelease(keyboard.eKey);
            Assert.That(Sampled(0).DropBomb, Is.True);

            // A frame apart, because WasPressedThisFrame stays true for the rest of the
            // update it happened in: polling twice inside one update would latch the
            // same press again and hide a latch that never clears.
            InputSystem.Update();
            Assert.That(Sampled(0).DropBomb, Is.False);

            PressAndRelease(keyboard.spaceKey);
            Assert.That(Sampled(0).Dash, Is.True);
        }
    }
}
