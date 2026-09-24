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
