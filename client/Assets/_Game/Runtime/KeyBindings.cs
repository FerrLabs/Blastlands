using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Blastlands.Runtime
{
    public static class KeyBindings
    {
        public const string ControlsResource = "BlastlandsControls";

        public static readonly HudAction[] Rebindable = { HudAction.Bomb, HudAction.Dash, HudAction.Shove, HudAction.Ability };

        private static InputActionAsset current;

        public static InputActionAsset Load()
        {
            InputActionAsset controls = Resources.Load<InputActionAsset>(ControlsResource);
            if (controls == null)
            {
                throw new InvalidOperationException("Input actions asset Resources/" + ControlsResource + " is missing");
            }

            InputActionAsset copy = Object.Instantiate(controls);
            Apply(copy, SettingsChoice.Keys);
            return copy;
        }

        public static void Apply(InputActionAsset asset, string overrides)
        {
            if (string.IsNullOrEmpty(overrides))
            {
                return;
            }

            try
            {
                asset.LoadBindingOverridesFromJson(overrides);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Blastlands: ignoring saved key bindings that no longer load: " + e.Message);
            }
        }

        public static void Save(InputActionAsset asset)
        {
            SettingsChoice.ChooseKeys(asset.SaveBindingOverridesAsJson());
            if (current != null)
            {
                Object.Destroy(current);
            }

            current = null;
        }

        public static void Reset(InputActionAsset asset)
        {
            asset.RemoveAllBindingOverrides();
            Save(asset);
        }

        public static bool Taken(InputActionAsset asset, InputAction rebound, int index)
        {
            string path = rebound.bindings[index].effectivePath;
            foreach (InputAction action in asset)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite || (action == rebound && i == index))
                    {
                        continue;
                    }

                    if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static InputAction ActionFor(InputActionAsset asset, HudAction action)
        {
            return asset.FindAction(ActionPath(action), true);
        }

        public static int KeyboardBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite
                    && (binding.path.StartsWith("<Keyboard>", StringComparison.Ordinal)
                        || binding.path.StartsWith("<Mouse>", StringComparison.Ordinal)))
                {
                    return i;
                }
            }

            return -1;
        }

        public static string Shown(InputActionAsset asset, HudAction action)
        {
            InputAction input = ActionFor(asset, action);
            int index = KeyboardBinding(input);
            return index < 0 ? string.Empty : Label(input, index);
        }

        public static string Label(InputAction input, int index)
        {
            string local = input.GetBindingDisplayString(index);
            string path = input.bindings[index].effectivePath;
            if (local.Length <= 1 || !path.StartsWith("<Keyboard>", StringComparison.Ordinal))
            {
                return local.ToUpperInvariant();
            }

            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice)
                .ToUpperInvariant();
        }

        public static string Rebound(InputActionAsset asset, HudAction action)
        {
            InputAction input = ActionFor(asset, action);
            int index = KeyboardBinding(input);
            if (index < 0 || string.IsNullOrEmpty(input.bindings[index].overridePath))
            {
                return null;
            }

            return Label(input, index);
        }

        public static string Rebound(HudAction action)
        {
            if (string.IsNullOrEmpty(SettingsChoice.Keys))
            {
                return null;
            }

            current = current != null ? current : Load();
            return Rebound(current, action);
        }

        public static string Named(HudAction action)
        {
            switch (action)
            {
                case HudAction.Dash:
                    return "Dash";
                case HudAction.Shove:
                    return "Shove";
                case HudAction.Ability:
                    return "Ability";
                default:
                    return "Drop a bomb";
            }
        }

        private static string ActionPath(HudAction action)
        {
            switch (action)
            {
                case HudAction.Dash:
                    return "Player/Dash";
                case HudAction.Shove:
                    return "Player/Shove";
                case HudAction.Ability:
                    return "Player/Ability";
                default:
                    return "Player/Drop";
            }
        }
    }
}
