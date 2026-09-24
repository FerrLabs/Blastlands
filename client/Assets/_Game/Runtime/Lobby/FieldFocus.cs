using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class FieldFocus : MonoBehaviour
    {
        private const float FadePerSecond = 7f;
        private const float PulsePerSecond = 4f;
        private const float GrownScale = 1.03f;

        private static readonly Color Glow = new Color(0.94f, 0.54f, 0.33f, 1f);

        private TMP_InputField field;
        private Image background;
        private Outline edge;
        private Color rest;
        private float focus;

        public static void Attach(TMP_InputField field, Image background)
        {
            FieldFocus focus = field.gameObject.AddComponent<FieldFocus>();
            focus.field = field;
            focus.background = background;
            focus.rest = background.color;
            focus.edge = field.gameObject.AddComponent<Outline>();
            focus.edge.effectDistance = new Vector2(4f, -4f);
            focus.edge.useGraphicAlpha = false;
            focus.Apply();

            field.customCaretColor = true;
            field.caretColor = Glow;
            field.caretWidth = 3;
            field.caretBlinkRate = 1.1f;
            field.selectionColor = new Color(Glow.r, Glow.g, Glow.b, 0.35f);
        }

        private void Update()
        {
            focus = Mathf.MoveTowards(focus, field.isFocused ? 1f : 0f, Time.unscaledDeltaTime * FadePerSecond);
            Apply();
        }

        private void Apply()
        {
            float pulse = field != null && field.isFocused ? 0.7f + (0.3f * Mathf.Sin(Time.unscaledTime * PulsePerSecond)) : 1f;
            edge.effectColor = new Color(Glow.r, Glow.g, Glow.b, focus * pulse);
            background.color = Color.Lerp(rest, Color.Lerp(rest, Glow, 0.25f), focus);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, GrownScale, focus);
        }
    }
}
