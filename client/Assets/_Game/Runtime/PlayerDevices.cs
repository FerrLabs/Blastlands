using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Utilities;
using Object = UnityEngine.Object;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    public sealed class PlayerDevices : IDisposable
    {
        private const string ControlsResource = "BlastlandsControls";

        private readonly Seat[] seats;

        public PlayerDevices(int playerCount)
        {
            InputActionAsset controls = Resources.Load<InputActionAsset>(ControlsResource);
            if (controls == null)
            {
                throw new InvalidOperationException("Input actions asset Resources/" + ControlsResource + " is missing");
            }

            seats = new Seat[Mathf.Max(1, playerCount)];
            for (int player = 0; player < seats.Length; player++)
            {
                seats[player] = new Seat(Object.Instantiate(controls));
            }

            AssignDevices();
        }

        public int PlayerCount
        {
            get { return seats.Length; }
        }

        public void PollPresses()
        {
            AssignDevices();

            foreach (Seat seat in seats)
            {
                seat.Poll();
            }
        }

        public PlayerInput Sample(int player)
        {
            if (player < 0 || player >= seats.Length)
            {
                return PlayerInput.None;
            }

            return seats[player].Take();
        }

        public bool RerollPressed()
        {
            AssignDevices();

            foreach (Seat seat in seats)
            {
                if (seat.RerollPressed())
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

            for (int player = 0; player < seats.Length; player++)
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

        public InputDeviceKind KindFor(int player)
        {
            Gamepad pad = PadFor(player);
            if (pad == null)
            {
                return InputDeviceKind.Keyboard;
            }

            return pad is DualShockGamepad ? InputDeviceKind.PlayStation : InputDeviceKind.Xbox;
        }

        public void Dispose()
        {
            foreach (Seat seat in seats)
            {
                seat.Dispose();
            }
        }

        private static Gamepad PadFor(int player)
        {
            return player >= 0 && player < Gamepad.all.Count ? Gamepad.all[player] : null;
        }

        private void AssignDevices()
        {
            for (int player = 0; player < seats.Length; player++)
            {
                seats[player].Assign(PadFor(player), player == 0);
            }
        }

        private sealed class Seat : IDisposable
        {
            private readonly InputActionAsset actions;
            private readonly InputAction step;
            private readonly InputAction stick;
            private readonly InputAction drop;
            private readonly InputAction dash;
            private readonly InputAction shove;
            private readonly InputAction reroll;
            private readonly PressLatch dropLatch = new PressLatch();
            private readonly PressLatch dashLatch = new PressLatch();
            private readonly PressLatch shoveLatch = new PressLatch();

            private bool assigned;
            private Gamepad pad;
            private Keyboard keyboard;
            private Mouse mouse;

            public Seat(InputActionAsset actions)
            {
                this.actions = actions;
                step = actions.FindAction("Player/Step", true);
                stick = actions.FindAction("Player/Stick", true);
                drop = actions.FindAction("Player/Drop", true);
                dash = actions.FindAction("Player/Dash", true);
                shove = actions.FindAction("Player/Shove", true);
                reroll = actions.FindAction("Player/Reroll", true);
                actions.Enable();
            }

            public void Assign(Gamepad newPad, bool withKeyboard)
            {
                Keyboard newKeyboard = withKeyboard ? Keyboard.current : null;
                Mouse newMouse = withKeyboard ? Mouse.current : null;

                if (assigned && newPad == pad && newKeyboard == keyboard && newMouse == mouse)
                {
                    return;
                }

                assigned = true;
                pad = newPad;
                keyboard = newKeyboard;
                mouse = newMouse;

                var devices = new List<InputDevice>(3);
                if (pad != null)
                {
                    devices.Add(pad);
                }

                if (keyboard != null)
                {
                    devices.Add(keyboard);
                }

                if (mouse != null)
                {
                    devices.Add(mouse);
                }

                actions.devices = new ReadOnlyArray<InputDevice>(devices.ToArray());
            }

            public void Poll()
            {
                dropLatch.Note(drop.WasPressedThisFrame());
                dashLatch.Note(dash.WasPressedThisFrame());
                shoveLatch.Note(shove.WasPressedThisFrame());
            }

            public bool RerollPressed()
            {
                return reroll.WasPressedThisFrame();
            }

            public PlayerInput Take()
            {
                int moveX, moveY;
                MoveReader.Resolve(step.ReadValue<Vector2>(), stick.ReadValue<Vector2>(), out moveX, out moveY);
                return new PlayerInput(moveX, moveY, dropLatch.Take(), dashLatch.Take(), shoveLatch.Take());
            }

            public void Dispose()
            {
                actions.Disable();
                Object.Destroy(actions);
            }
        }
    }
}
