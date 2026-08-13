using Blastlands.Core;
using UnityEngine.InputSystem;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    // New Input System, read directly from the device. The rebindable .inputactions
    // asset is a separate piece of work; this exists so the game is playable now.
    public static class KeyboardInput
    {
        public static PlayerInput Sample(bool dropLatch)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return PlayerInput.None;
            }

            Direction move = Direction.None;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                move = Direction.Right;
            }
            else if (keyboard.qKey.isPressed || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                move = Direction.Left;
            }
            else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                move = Direction.Down;
            }
            else if (keyboard.zKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                move = Direction.Up;
            }

            return new PlayerInput(move, dropLatch);
        }

        public static bool DropPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }
    }
}
