using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class PressFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressScale = 0.96f;
        private const float EasePerSecond = 18f;

        private Selectable owner;
        private bool hovered;
        private bool selected;
        private bool pressed;

        public static void Attach(Selectable owner)
        {
            PressFeel feel = owner.gameObject.AddComponent<PressFeel>();
            feel.owner = owner;

            ColorBlock colors = owner.colors;
            colors.highlightedColor = new Color(1f, 0.88f, 0.76f);
            colors.selectedColor = new Color(1f, 0.88f, 0.76f);
            colors.pressedColor = new Color(0.86f, 0.70f, 0.58f);
            colors.fadeDuration = 0.08f;
            owner.colors = colors;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
        }

        private void Update()
        {
            float target = 1f;
            if (owner.IsInteractable())
            {
                target = pressed ? PressScale : hovered || selected ? HoverScale : 1f;
            }

            float scale = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-EasePerSecond * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
