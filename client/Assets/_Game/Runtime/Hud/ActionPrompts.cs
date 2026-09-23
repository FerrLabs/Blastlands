namespace Blastlands.Runtime
{
    public static class ActionPrompts
    {
        public static PromptGlyph For(InputDeviceKind device, HudAction action)
        {
            bool keyboard = device == InputDeviceKind.Keyboard;

            switch (action)
            {
                case HudAction.Dash:
                    return keyboard ? PromptGlyph.KeySpace : PromptGlyph.PadShoulder;
                case HudAction.Shove:
                    return keyboard ? PromptGlyph.MouseLeft : PromptGlyph.PadWest;
                case HudAction.Ability:
                    return keyboard ? PromptGlyph.MouseRight : PromptGlyph.PadNorth;
                default:
                    return keyboard ? PromptGlyph.KeyE : PromptGlyph.PadSouth;
            }
        }

        public static bool OnKeyboard(PromptGlyph glyph)
        {
            return glyph == PromptGlyph.KeyE
                || glyph == PromptGlyph.KeySpace
                || glyph == PromptGlyph.MouseLeft
                || glyph == PromptGlyph.MouseRight;
        }

        public static string KeyLabel(PromptGlyph glyph)
        {
            switch (glyph)
            {
                case PromptGlyph.KeyE:
                    return "E";
                case PromptGlyph.KeySpace:
                    return "SPACE";
                default:
                    return string.Empty;
            }
        }
    }
}
