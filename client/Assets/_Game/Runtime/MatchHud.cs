using System;
using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class MatchHud : MonoBehaviour
    {
        private static readonly Rect WholeScreen = new Rect(0f, 0f, 1f, 1f);
        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        [SerializeField] private HudArt art;
        [SerializeField] private float inset = 24f;
        [SerializeField] private float splitScale = 0.62f;
        [SerializeField] private float clockClearance = 96f;

        private readonly List<HudRoster> rosters = new List<HudRoster>();
        private readonly List<HudVitals> vitals = new List<HudVitals>();
        private readonly List<HudActions> actions = new List<HudActions>();
        private readonly List<int> viewers = new List<int>();
        private HudClock clock;
        private HudRound round;

        private MatchState state;
        private MatchSeries series;
        private MatchCamera cameras;
        private Func<int, InputDeviceKind> deviceOf;
        private Canvas canvas;

        public void Bind(MatchState matchState, MatchCamera matchCameras, MatchSeries matchSeries, Func<int, InputDeviceKind> devices)
        {
            state = matchState;
            cameras = matchCameras;
            series = matchSeries;
            deviceOf = devices;
            Rebuild();
        }

        public void Render()
        {
            if (state == null)
            {
                return;
            }

            clock?.Render(state);

            if (series != null)
            {
                round?.Render(series);
            }

            foreach (HudRoster roster in rosters)
            {
                roster.Render(state, series);
            }

            foreach (HudVitals panel in vitals)
            {
                panel.Render(state);
            }

            foreach (HudActions buttons in actions)
            {
                buttons.Render(state);
            }
        }

        private void Rebuild()
        {
            rosters.Clear();
            vitals.Clear();
            actions.Clear();
            clock = null;
            round = null;

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }

            if (state == null || art == null)
            {
                return;
            }

            BuildCanvas();

            int views = cameras == null ? 0 : cameras.ViewCount;
            if (views <= 1)
            {
                BuildWholeScreen();
            }
            else
            {
                BuildSplit(views);
            }

            Render();
        }

        private void BuildWholeScreen()
        {
            RectTransform area = HudPlacement.Area(canvas.transform, "Screen", WholeScreen);

            var everyone = new List<int>();
            for (int i = 0; i < state.Players.Count; i++)
            {
                everyone.Add(i);
            }

            rosters.Add(HudRoster.Build(art, area, everyone, series, true, inset, 1f));
            clock = HudClock.Build(art, area, TopCentre, inset, 1f);

            if (series != null)
            {
                round = HudRound.Build(art, area, inset, 1f);
            }

            BuildLocalPlayer(area, 0, 1f);
        }

        private void BuildSplit(int views)
        {
            for (int view = 0; view < views; view++)
            {
                Camera camera = cameras.ViewAt(view);
                Rect viewport = camera == null ? WholeScreen : camera.rect;
                RectTransform area = HudPlacement.Area(canvas.transform, "View " + view, viewport);
                ClearTheClock(area, viewport);
                int seat = BuildLocalPlayer(area, view, splitScale);

                if (seat >= 0)
                {
                    rosters.Add(HudRoster.Build(art, area, new[] { seat }, series, false, inset * splitScale, splitScale));
                }
            }

            RectTransform middle = HudPlacement.Area(canvas.transform, "Middle", WholeScreen);
            clock = HudClock.Build(art, middle, Centre, 0f, splitScale);
        }

        private void ClearTheClock(RectTransform area, Rect viewport)
        {
            if (viewport.xMin > 0f)
            {
                area.offsetMin = new Vector2(clockClearance, area.offsetMin.y);
            }

            if (viewport.xMax < 1f)
            {
                area.offsetMax = new Vector2(-clockClearance, area.offsetMax.y);
            }
        }

        private int BuildLocalPlayer(Transform area, int view, float scale)
        {
            if (cameras == null)
            {
                viewers.Clear();
                viewers.Add(0);
            }
            else
            {
                cameras.ViewersOf(view, viewers);
            }

            if (viewers.Count == 0 || viewers[0] < 0 || viewers[0] >= state.Players.Count)
            {
                return -1;
            }

            int seat = viewers[0];
            InputDeviceKind device = deviceOf == null ? InputDeviceKind.Keyboard : deviceOf(seat);
            vitals.Add(HudVitals.Build(art, area, seat, inset * scale, scale));
            actions.Add(HudActions.Build(art, area, seat, device, inset * scale, scale));
            return seat;
        }

        private void BuildCanvas()
        {
            var host = new GameObject("MatchHud Canvas", typeof(Canvas), typeof(CanvasScaler));
            host.transform.SetParent(transform, false);

            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
