using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blastlands.Runtime.Tests
{
    public class KeyBindingsTests
    {
        private static InputActionAsset Fresh()
        {
            return Object.Instantiate(Resources.Load<InputActionAsset>(KeyBindings.ControlsResource));
        }

        [Test]
        public void TheBindingARebindChangesIsTheKeyboardOrMouseOneNeverThePad()
        {
            InputActionAsset asset = Fresh();

            foreach (HudAction action in KeyBindings.Rebindable)
            {
                InputAction input = KeyBindings.ActionFor(asset, action);
                int index = KeyBindings.KeyboardBinding(input);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), action.ToString());
                Assert.That(input.bindings[index].path, Does.Not.StartWith("<Gamepad>"), action.ToString());
            }

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void AKeyAlreadyUsedByMovementOrAnotherActionCountsAsTaken()
        {
            InputActionAsset asset = Fresh();
            InputAction dash = KeyBindings.ActionFor(asset, HudAction.Dash);
            int index = KeyBindings.KeyboardBinding(dash);

            dash.ApplyBindingOverride(index, "<Keyboard>/w");
            Assert.That(KeyBindings.Taken(asset, dash, index), Is.True, "W is a part of the movement composite");

            dash.ApplyBindingOverride(index, "<Keyboard>/e");
            Assert.That(KeyBindings.Taken(asset, dash, index), Is.True, "E drops a bomb");

            dash.ApplyBindingOverride(index, "<Keyboard>/f");
            Assert.That(KeyBindings.Taken(asset, dash, index), Is.False);

            dash.RemoveBindingOverride(index);
            Assert.That(KeyBindings.Taken(asset, dash, index), Is.False, "its own default is not a clash");
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void NamedKeysReadInEnglishAndMouseButtonsStayShort()
        {
            InputActionAsset asset = Fresh();
            InputAction dash = KeyBindings.ActionFor(asset, HudAction.Dash);
            int index = KeyBindings.KeyboardBinding(dash);

            dash.ApplyBindingOverride(index, "<Keyboard>/leftShift");
            Assert.That(KeyBindings.Label(dash, index), Is.EqualTo("LEFT SHIFT"));

            dash.ApplyBindingOverride(index, "<Keyboard>/space");
            Assert.That(KeyBindings.Label(dash, index), Is.EqualTo("SPACE"));

            dash.ApplyBindingOverride(index, "<Mouse>/leftButton");
            Assert.That(KeyBindings.Label(dash, index), Is.EqualTo("LMB"));
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void OnlyARebindReplacesTheDrawnPrompt()
        {
            InputActionAsset asset = Fresh();
            InputAction drop = KeyBindings.ActionFor(asset, HudAction.Bomb);

            Assert.That(KeyBindings.Rebound(asset, HudAction.Bomb), Is.Null, "the default keeps its glyph");

            drop.ApplyBindingOverride(KeyBindings.KeyboardBinding(drop), "<Keyboard>/f");
            Assert.That(KeyBindings.Rebound(asset, HudAction.Bomb), Is.EqualTo("F"));

            string saved = asset.SaveBindingOverridesAsJson();
            InputActionAsset reloaded = Fresh();
            KeyBindings.Apply(reloaded, saved);
            Assert.That(KeyBindings.Rebound(reloaded, HudAction.Bomb), Is.EqualTo("F"), "the saved form is what the match loads");

            KeyBindings.Apply(reloaded, "not json");
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(reloaded);
        }
    }
}
