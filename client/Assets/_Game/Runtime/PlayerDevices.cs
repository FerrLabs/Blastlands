using System.Text;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    // Maps physical devices onto players. Gamepad i drives player i; the keyboard
    // also drives player 0, so a solo player can use either without configuring
    // anything, and a second player only has to plug a pad in.
    //
    // Devices are read directly rather than through an .inputactions asset. That
    // asset is worth having for rebinding, but it is not what makes the game
    // playable with friends today.
    public sealed class PlayerDevices
    {
        private readonly bool[] dropLatches;
        private readonly bool[] dashLatches;
        private readonly bool[] pushLatches;

        public PlayerDevices(int playerCount)
        {
            dropLatches = new bool[Mathf.Max(1, playerCount)];
            dashLatches = new bool[dropLatches.Length];
            pushLatches = new bool[dropLatches.Length];
        }

        public int PlayerCount
        {
            get { return dropLatches.Length; }
        }

        // Polled every frame, not every tick: a bomb press that lands between two
        // ticks must not be swallowed by the frame it happened on.
        public void PollPresses()
        {
            for (int player = 0; player < dropLatches.Length; player++)
            {
                if (DropPressedThisFrame(player))
                {
                    dropLatches[player] = true;
                }

                if (DashPressedThisFrame(player))
                {
                    dashLatches[player] = true;
                }

                if (PushPressedThisFrame(player))
                {
                    pushLatches[player] = true;
                }
            }
        }

        public PlayerInput Sample(int player)
        {
            if (player < 0 || player >= dropLatches.Length)
            {
                return PlayerInput.None;
            }

            bool drop = dropLatches[player];
            bool dash = dashLatches[player];
            bool push = pushLatches[player];
            dropLatches[player] = false;
            dashLatches[player] = false;
            pushLatches[player] = false;

            int moveX, moveY;
            MoveFor(player, out moveX, out moveY);
            return new PlayerInput(moveX, moveY, drop, dash, push);
        }

        public bool RerollPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                return true;
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.startButton.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        public string DescribeAssignment()
        {
            var text = new StringBuilder("player 0: keyboard");
            int pads = Gamepad.all.Count;

            for (int player = 0; player < dropLatches.Length; player++)
            {
                if (player < pads)
                {
                    text.Append(player == 0 ? " + " : ", player " + player + ": ");
                    text.Append(Gamepad.all[player].displayName);
                }
            }

            if (pads == 0)
            {
                text.Append(" (no gamepad detected)");
            }

            return text.ToString();
        }

        private static Gamepad PadFor(int player)
        {
            return player >= 0 && player < Gamepad.all.Count ? Gamepad.all[player] : null;
        }

        private bool DropPressedThisFrame(int player)
        {
            Gamepad pad = PadFor(player);
            if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }

            if (player != 0)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }

        // Left click, as asked for, and the west face button on a pad. A shove is
        // always available, so it sits on the button a hand rests on.
        private bool PushPressedThisFrame(int player)
        {
            Gamepad pad = PadFor(player);
            if (pad != null && pad.buttonWest.wasPressedThisFrame)
            {
                return true;
            }

            if (player != 0)
            {
                return false;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        // Shoulder button on a pad, shift on the keyboard: it has to be reachable
        // without letting go of a direction, because a dash with no direction is not
        // a dash.
        private bool DashPressedThisFrame(int player)
        {
            Gamepad pad = PadFor(player);
            if (pad != null && (pad.rightShoulder.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame))
            {
                return true;
            }

            if (player != 0)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame);
        }

        // The stick is passed through rather than reduced to one of four directions.
        // Movement is free now, and a body that can only be pushed along an axis catches
        // on every corner it meets.
        private static void MoveFor(int player, out int moveX, out int moveY)
        {
            Gamepad pad = PadFor(player);
            if (pad != null && PadVector(pad, out moveX, out moveY))
            {
                return;
            }

            if (player == 0)
            {
                FromDirection(KeyboardDirection(), out moveX, out moveY);
                return;
            }

            moveX = 0;
            moveY = 0;
        }

        private static bool PadVector(Gamepad pad, out int moveX, out int moveY)
        {
            // The d-pad is unambiguous, so it still wins over the stick when both are
            // pushed — it simply arrives as a full-strength vector.
            Direction fromDpad = ToDirection(pad.dpad.ReadValue());
            if (fromDpad != Direction.None)
            {
                FromDirection(fromDpad, out moveX, out moveY);
                return true;
            }

            Vector2 stick = pad.leftStick.ReadValue();
            int x = Mathf.RoundToInt(stick.x * StickReader.Range);
            int y = Mathf.RoundToInt(-stick.y * StickReader.Range);

            if (StickReader.ToDirection(x, y) == Direction.None)
            {
                moveX = 0;
                moveY = 0;
                return false;
            }

            moveX = x;
            moveY = y;
            return true;
        }

        private static void FromDirection(Direction direction, out int moveX, out int moveY)
        {
            GridPos delta = Directions.Delta(direction);
            moveX = delta.X * StickReader.Range;
            moveY = delta.Y * StickReader.Range;
        }

        private static Direction PadDirection(Gamepad pad)
        {
            if (pad == null)
            {
                return Direction.None;
            }

            // The d-pad is already four-way and unambiguous, so it wins over the stick
            // when both are pushed.
            Vector2 dpad = pad.dpad.ReadValue();
            Direction fromDpad = ToDirection(dpad);
            if (fromDpad != Direction.None)
            {
                return fromDpad;
            }

            return ToDirection(pad.leftStick.ReadValue());
        }

        // The grid's Y grows downward while the stick's grows upward, hence the flip.
        private static Direction ToDirection(Vector2 value)
        {
            return StickReader.ToDirection(
                Mathf.RoundToInt(value.x * StickReader.Range),
                Mathf.RoundToInt(-value.y * StickReader.Range));
        }

        private static Direction KeyboardDirection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Direction.None;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                return Direction.Right;
            }

            if (keyboard.qKey.isPressed || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                return Direction.Left;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                return Direction.Down;
            }

            if (keyboard.zKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                return Direction.Up;
            }

            return Direction.None;
        }
    }
}
