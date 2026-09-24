using Blastlands.Core.Net;
using Blastlands.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class UpdateBanner : MonoBehaviour
    {
        private const int Layer = 500;
        private const float Padding = 28f;
        private const float BadgeClearance = 170f;

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 CardSize = new Vector2(640f, 250f);
        private static readonly Color Dimmed = new Color(0f, 0f, 0f, 0.86f);
        private static readonly Color Panel = new Color(0.07f, 0.08f, 0.10f, 0.94f);
        private static readonly Color Blocking = new Color(1f, 0.42f, 0.32f);
        private static readonly Color Informing = new Color(0.98f, 0.86f, 0.42f);

        [SerializeField] private VersionGate gate;
        [SerializeField] private ClientUpdater updater;
        [SerializeField] private Key acceptKey = Key.F5;

        private Image scrim;
        private GameObject card;
        private TMP_Text headline;
        private TMP_Text detail;
        private UpdateVerdict shownVerdict;
        private UpdateStage shownStage;
        private bool shown;

        public void Use(VersionGate versionGate, ClientUpdater clientUpdater)
        {
            gate = versionGate;
            updater = clientUpdater;
        }

        public UpdateNotice Notice { get; private set; }

        private void Update()
        {
            if (gate == null || !gate.Answered)
            {
                return;
            }

            UpdateStage stage = updater == null ? UpdateStage.Unsupported : updater.Stage;
            if (!shown || stage != shownStage || gate.Verdict != shownVerdict)
            {
                shown = true;
                shownStage = stage;
                shownVerdict = gate.Verdict;

                Notice = UpdateNotice.For(
                    gate.Verdict, stage, updater == null ? null : updater.Failure, Application.version, gate.Release);
                Render(Notice);
            }

            if (card != null)
            {
                bool wanted = Notice.Visible && Notice.BlocksPlay;
                if (card.activeSelf != wanted)
                {
                    card.SetActive(wanted);
                }
            }

            if (Notice.OffersUpdate && Accepted())
            {
                StartCoroutine(updater.Apply(gate.Release));
            }
        }

        private bool Accepted()
        {
            Keyboard keyboard = Keyboard.current;
            return updater != null && keyboard != null && keyboard[acceptKey].wasPressedThisFrame;
        }

        private void Render(UpdateNotice notice)
        {
            if (!notice.Visible || !notice.BlocksPlay)
            {
                if (card != null)
                {
                    card.SetActive(false);
                    scrim.enabled = false;
                }

                return;
            }

            if (card == null)
            {
                Build();
            }

            card.SetActive(true);
            scrim.enabled = notice.BlocksPlay;
            headline.color = notice.BlocksPlay ? Blocking : Informing;
            headline.text = notice.Headline.ToUpperInvariant();
            detail.text = notice.OffersUpdate
                ? notice.Detail + "\nPress " + acceptKey + " to update and restart."
                : notice.Detail;
        }

        private void Build()
        {
            var host = new GameObject("Update Banner", typeof(Canvas), typeof(CanvasScaler));
            host.transform.SetParent(transform, false);

            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Layer;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;

            scrim = Fill(host.transform, "Scrim", Dimmed);

            card = Fill(host.transform, "Card", Panel).gameObject;
            var frame = (RectTransform)card.transform;
            frame.anchorMin = BottomRight;
            frame.anchorMax = BottomRight;
            frame.pivot = BottomRight;
            frame.sizeDelta = CardSize;
            frame.anchoredPosition = new Vector2(-Padding, BadgeClearance);

            headline = Write(frame, "Headline", 36f, FontStyles.Bold, -Padding, 48f);
            detail = Write(frame, "Detail", 24f, FontStyles.Normal, -(Padding + 54f), 150f);
        }

        private static Image Fill(Transform parent, string name, Color colour)
        {
            var area = new GameObject(name, typeof(RectTransform), typeof(Image));
            area.transform.SetParent(parent, false);

            var frame = (RectTransform)area.transform;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;

            Image fill = area.GetComponent<Image>();
            fill.color = colour;
            fill.raycastTarget = false;
            return fill;
        }

        private static TMP_Text Write(
            RectTransform parent, string name, float size, FontStyles style, float top, float height)
        {
            var area = new GameObject(name, typeof(RectTransform));
            area.transform.SetParent(parent, false);

            var frame = (RectTransform)area.transform;
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.sizeDelta = new Vector2(-Padding * 2f, height);
            frame.anchoredPosition = new Vector2(0f, top);

            TMP_Text text = area.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
