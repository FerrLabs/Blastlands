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
        [SerializeField, Range(0.5f, 1.5f)] private float size = 0.8f;

        private readonly List<HudRoster> rosters = new List<HudRoster>();
        private readonly List<HudVitals> vitals = new List<HudVitals>();
        private readonly List<HudActions> actions = new List<HudActions>();
        private readonly List<int> viewers = new List<int>();
        private readonly List<HudFade> fades = new List<HudFade>();
        private readonly List<int> fadeViews = new List<int>();
        private readonly List<Vector2> onScreen = new List<Vector2>();
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
                round?.Render(series, state);
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

            FadeWhatCoversAPlayer();
        }

        private void FadeWhatCoversAPlayer()
        {
            int shownView = -1;
            for (int i = 0; i < fades.Count; i++)
            {
                if (fadeViews[i] != shownView)
                {
                    shownView = fadeViews[i];
                    PlayersOnScreen(shownView);
                }

                fades[i].Update(onScreen, Time.unscaledDeltaTime);
            }
        }

        private void PlayersOnScreen(int view)
        {
            onScreen.Clear();
            Camera camera = cameras == null ? Camera.main : cameras.ViewAt(view);
            if (camera == null)
            {
                return;
            }

            foreach (PlayerState player in state.Players)
            {
                if (!player.Alive)
                {
                    continue;
                }

                Vector3 point = camera.WorldToScreenPoint(MatchView.ToWorld(player.Position, 0.5f));
                if (point.z > 0f)
                {
                    onScreen.Add(point);
                }
            }
        }

        private RectTransform Block(Transform parent, string name)
        {
            return HudPlacement.Area(parent, name, WholeScreen);
        }

        private void Fade(RectTransform block, int view)
        {
            fades.Add(new HudFade(block));
            fadeViews.Add(view);
        }

        private void Rebuild()
        {
            rosters.Clear();
            vitals.Clear();
            actions.Clear();
            fades.Clear();
            fadeViews.Clear();
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

            RectTransform rosterBlock = Block(area, "Roster");
            rosters.Add(HudRoster.Build(art, rosterBlock, everyone, series, true, inset, 1f));
            Fade(rosterBlock, 0);

            RectTransform clockBlock = Block(area, "Clock");
            clock = HudClock.Build(art, clockBlock, TopCentre, inset, 1f);
            Fade(clockBlock, 0);

            if (series != null || state.Settings.Survival.Enabled)
            {
                RectTransform roundBlock = Block(area, "Round");
                round = HudRound.Build(art, roundBlock, inset, 1f);
                Fade(roundBlock, 0);
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
                    RectTransform tag = Block(area, "Tag");
                    rosters.Add(HudRoster.Build(art, tag, new[] { seat }, series, false, inset * splitScale, splitScale));
                    Fade(tag, view);
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
            RectTransform vitalsBlock = Block(area, "Vitals block");
            vitals.Add(HudVitals.Build(art, vitalsBlock, seat, inset * scale, scale));
            Fade(vitalsBlock, view);

            RectTransform actionsBlock = Block(area, "Actions block");
            actions.Add(HudActions.Build(art, actionsBlock, seat, device, inset * scale, scale));
            Fade(actionsBlock, view);
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
            scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.1f, size);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
