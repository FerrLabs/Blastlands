using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class HudActionButton
    {
        private const float CoolingIconAlpha = 0.35f;

        private readonly Image icon;
        private readonly Image fill;
        private readonly TMP_Text count;
        private readonly TMP_Text caption;
        private readonly GameObject[] parts;

        private HudActionButton(Image icon, Image fill, TMP_Text count, TMP_Text caption, GameObject[] parts)
        {
            this.icon = icon;
            this.fill = fill;
            this.count = count;
            this.caption = caption;
            this.parts = parts;
        }

        public static HudActionButton Build(
            HudArt art,
            Transform parent,
            HudAction action,
            Sprite glyph,
            InputDeviceKind device,
            Color accent,
            Vector2 offset,
            float scale,
            bool counted)
        {
            var corner = new Vector2(1f, 0f);

            GameObject dial = HudPlacement.Spawn(art.Dial, parent, action.ToString());
            HudPlacement.Pin(dial, corner, offset, scale);
            HudPlacement.Hide(dial, "Dial_Low");

            Image icon = HudPlacement.Part<Image>(dial, "Dial_Healthy/Icon");
            if (icon != null)
            {
                icon.sprite = glyph;
                icon.preserveAspect = true;
            }

            Image fill = HudPlacement.Part<Image>(dial, "Dial_Healthy/Dial Fill/Fill");
            HudPlacement.Tint(fill, accent);

            float size = DialSize(dial) * scale;
            GameObject prompt = BuildPrompt(
                art, parent, action, device, corner, offset + new Vector2(size * 0.86f, size * 0.86f), scale);

            GameObject caption = HudPlacement.Spawn(art.Caption, parent, action + " caption");
            HudPlacement.Pin(caption, corner, offset + new Vector2(size * 0.5f - 60f * scale, -34f * scale), scale * 0.4f);
            TMP_Text captionText = HudPlacement.Text(caption);
            if (captionText != null)
            {
                captionText.alignment = TextAlignmentOptions.Center;
                captionText.text = action.ToString().ToUpperInvariant();
            }

            TMP_Text count = null;
            GameObject label = null;
            if (counted)
            {
                label = HudPlacement.Spawn(art.Label, parent, action + " count");
                HudPlacement.Pin(label, corner, offset + new Vector2(size * 0.5f - 60f * scale, size * 0.06f), scale * 0.4f);
                count = HudPlacement.Text(label);
                if (count != null)
                {
                    count.alignment = TextAlignmentOptions.Center;
                }
            }

            return new HudActionButton(icon, fill, count, captionText, new[] { dial, prompt, caption, label });
        }

        public void Dress(Sprite glyph, string name)
        {
            if (icon != null)
            {
                icon.sprite = glyph;
            }

            HudPlacement.Write(caption, name.ToUpperInvariant());
        }

        public void Reveal(bool visible)
        {
            foreach (GameObject part in parts)
            {
                if (part != null && part.activeSelf != visible)
                {
                    part.SetActive(visible);
                }
            }
        }

        public void Show(float readiness, bool ready, string counter)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(readiness);
            }

            if (icon != null)
            {
                icon.color = new Color(1f, 1f, 1f, ready ? 1f : CoolingIconAlpha);
            }

            HudPlacement.Write(count, counter);
        }

        private static GameObject BuildPrompt(
            HudArt art,
            Transform parent,
            HudAction action,
            InputDeviceKind device,
            Vector2 corner,
            Vector2 offset,
            float scale)
        {
            PromptGlyph glyph = ActionPrompts.For(device, action);

            if (!ActionPrompts.OnKeyboard(glyph))
            {
                GameObject button = HudPlacement.Spawn(art.PadPrompt, parent, action + " prompt");
                HudPlacement.Pin(button, corner, offset, scale * 0.8f);
                HudPlacement.Hide(button, "Press_and_Hold");

                Image face = HudPlacement.Part<Image>(button, "ICON");
                if (face != null)
                {
                    face.sprite = art.PadGlyph(device, glyph);
                    face.preserveAspect = true;
                }

                return button;
            }

            GameObject key = HudPlacement.Spawn(art.KeyPrompt, parent, action + " prompt");
            HudPlacement.Pin(key, corner, offset, scale * 0.7f);
            HudPlacement.Hide(key, "Press and Hold");
            HudPlacement.Write(HudPlacement.Text(key), ActionPrompts.KeyLabel(glyph));

            if (glyph == PromptGlyph.MouseLeft || glyph == PromptGlyph.MouseRight)
            {
                HudPlacement.Hide(key, "Label_Input_Key");
                Image mouse = HudPlacement.Part<Image>(key, "ICON");
                if (mouse != null)
                {
                    mouse.gameObject.SetActive(true);
                    mouse.sprite = art.Mouse(glyph);
                    mouse.preserveAspect = true;
                }
            }

            return key;
        }

        private static float DialSize(GameObject dial)
        {
            return dial == null ? 0f : ((RectTransform)dial.transform).rect.width;
        }
    }
}
