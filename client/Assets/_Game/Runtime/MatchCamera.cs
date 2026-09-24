using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // Directs however many cameras the chosen mode needs.
    //
    // The grid version never moved: the whole arena was always on screen, because a
    // player had to see every tile that was about to be on fire. A bigger arena and free
    // movement give that up on purpose — you no longer read the whole board, which trades
    // the tactical overview for a brawler's closeness. That is a change of nature rather
    // than a setting, which is why the global mode stays in the code instead of going away,
    // although no driver picks it today.
    [RequireComponent(typeof(Camera))]
    public sealed class MatchCamera : MonoBehaviour
    {
        private CameraMode mode = CameraMode.Global;
        [SerializeField] private float tiltDegrees = 55f;

        // Wide enough to show a band of the surrounding scenery. Framing the arena
        // exactly slices the decoration at the edges, which looks like a bug.
        [SerializeField] private float margin = 2.8f;

        // Small enough that the view is narrower than the arena, or the clamp below has
        // no slack to work with and the camera sits pinned to the centre — following
        // nothing. On a 15x13 arena at 16:9, anything above about 5 is already wider
        // than the whole board.
        [SerializeField] private float followSize = 4f;

        private const float TunedAspect = 16f / 9f;
        [SerializeField] private float followSmoothing = 0.18f;

        // How far ahead of the player the view sits, and how much further while dashing.
        // A camera that lags a dash makes the dash feel worse than not having one.
        [SerializeField] private float lookahead = 1.6f;
        [SerializeField] private float dashLookahead = 3.2f;

        // Floor for the global mode's zoom. Without a limit on how far it pulls back,
        // four players in four corners are four specks — the problem a moving camera was
        // supposed to solve.
        [SerializeField] private float globalMinSize = 4f;

        // How far a blast can throw the view, in world units. Small on purpose: the
        // camera is how a player reads the board, and one that moves far enough to be
        // noticed as movement has stopped doing its job.
        [SerializeField] private float shakeDistance = 0.32f;

        // Off switch. Camera shake is a common migraine and motion sickness trigger, so
        // it has to be possible to play without it rather than only to endure it. There
        // is no settings screen to drive this yet, so for now it is the inspector or
        // --no-shake on the command line.
        [SerializeField] private bool screenShake = true;

        private readonly List<Camera> views = new List<Camera>();
        private readonly List<Vector3> velocities = new List<Vector3>();
        private readonly List<CameraShake> shakes = new List<CameraShake>();

        private MatchState state;
        private Camera own;
        private float lastAspect;

        // The island's extent, worked out once when the match is bound. Framing the
        // bounds instead would spend a fifth of the screen on empty sky, because the
        // rectangle is now the box the island was cut from rather than the board.
        private Vector2 groundMin;
        private Vector2 groundMax;

        private int localSeats = 1;

        // Which seat the first viewport belongs to. Zero for a local match, where the
        // seats a machine owns start at the beginning. On a networked client it is the
        // seat the server gave this connection: without it a client seated at 2 drives
        // player 2 and watches player 0, which is not a camera bug on screen so much as a
        // game that does not respond.
        private int firstSeat;

        public CameraMode Mode
        {
            get { return mode; }
        }

        public int ViewCount
        {
            get { return views.Count; }
        }

        public static float FollowSizeFor(float followSize, float aspect)
        {
            return aspect > TunedAspect ? followSize * TunedAspect / aspect : followSize;
        }

        public static CameraMode ModeFor(int localSeats)
        {
            return localSeats > 1 ? CameraMode.Split : CameraMode.Follow;
        }

        public Camera ViewAt(int index)
        {
            return index >= 0 && index < views.Count ? views[index] : null;
        }

        // Whose eyes a viewport renders through. Split hands each viewport one seat.
        // Global has no single viewpoint at all: it is the whole couch watching one
        // screen, so it renders the union of everyone sitting there. Nobody can hide
        // from the person next to them anyway.
        public void ViewersOf(int index, List<int> into)
        {
            into.Clear();

            if (mode == CameraMode.Split)
            {
                into.Add(firstSeat + index);
                return;
            }

            if (mode == CameraMode.Follow)
            {
                into.Add(firstSeat);
                return;
            }

            // The seats this machine owns, which start at firstSeat and not at zero. This
            // is the branch MatchFog reads, and in Classic Blinded that list is the whole
            // of what a client is allowed to see: left at zero, a client seated anywhere
            // else is shown player zero's vision, with an opponent beside them hidden and
            // no way to tell by looking that the wrong eyes are being used.
            for (int i = 0; i < localSeats; i++)
            {
                into.Add(firstSeat + i);
            }
        }

        private void Awake()
        {
            screenShake = screenShake && ClientOptions.ScreenShake(System.Environment.GetCommandLineArgs());
        }

        public void Bind(MatchState matchState, int seats)
        {
            Bind(matchState, seats, 0);
        }

        public void Bind(MatchState matchState, int seats, int seatOfFirstViewport)
        {
            state = matchState;
            localSeats = seats < 1 ? 1 : seats;
            firstSeat = seatOfFirstViewport < 0 ? 0 : seatOfFirstViewport;
            lastAspect = 0f;
            MeasureGround();
            Rebuild();
        }

        // What a blast does to each viewport, worked out per viewport because in
        // split-screen the same bomb is next to one player and across the board from
        // another.
        public void Felt(Vector3 at, int flameTiles)
        {
            if (!screenShake)
            {
                return;
            }

            for (int i = 0; i < views.Count && i < shakes.Count; i++)
            {
                Camera view = views[i];
                if (view == null)
                {
                    continue;
                }

                Vector3 watching = Watching(view);
                float distance = Vector2.Distance(
                    new Vector2(at.x, at.z), new Vector2(watching.x, watching.z));

                shakes[i].Felt(CameraShake.StrengthOf(distance, flameTiles, CameraShake.ReachTiles));
            }
        }

        public bool SingleViewportIsListening(out Vector3 at, out float halfWidth)
        {
            at = Vector3.zero;
            halfWidth = 0f;

            if (views.Count != 1 || views[0] == null)
            {
                return false;
            }

            at = Watching(views[0]);
            halfWidth = views[0].orthographicSize * views[0].aspect;
            return true;
        }

        private static Vector3 Watching(Camera view)
        {
            return view.transform.position + (view.transform.forward * 40f);
        }

        public void Use(CameraMode next)
        {
            mode = next;
            if (state != null)
            {
                Rebuild();
            }
        }

        private void LateUpdate()
        {
            if (state == null)
            {
                return;
            }

            if (!Mathf.Approximately(Aspect(), lastAspect))
            {
                Rebuild();
            }

            for (int i = 0; i < views.Count; i++)
            {
                Aim(i);
            }
        }

        private void Rebuild()
        {
            lastAspect = Aspect();

            int wanted = mode == CameraMode.Split ? Mathf.Clamp(localSeats, 1, Mathf.Max(1, state.Players.Count)) : 1;

            for (int i = views.Count - 1; i >= wanted; i--)
            {
                if (views[i] != null && views[i] != Own())
                {
                    Destroy(views[i].gameObject);
                }

                views.RemoveAt(i);
                velocities.RemoveAt(i);
                shakes.RemoveAt(i);
            }

            while (views.Count < wanted)
            {
                views.Add(views.Count == 0 ? Own() : Clone(views.Count));
                velocities.Add(Vector3.zero);

                // A phase per viewport, so one bomb reaching two of them does not shake
                // both the same way at the same moment, which reads as the whole window
                // moving rather than as two people feeling the same blast.
                shakes.Add(new CameraShake(shakes.Count * 0.41f));
            }

            for (int i = 0; i < views.Count; i++)
            {
                views[i].orthographic = true;
                views[i].rect = ViewportFor(i, wanted);
                views[i].transform.rotation = Quaternion.Euler(tiltDegrees, 0f, 0f);
            }
        }

        private Camera Clone(int index)
        {
            var host = new GameObject("Match Camera " + index, typeof(Camera));
            host.transform.SetParent(transform.parent, false);

            Camera clone = host.GetComponent<Camera>();
            clone.CopyFrom(Own());
            clone.depth = Own().depth + index;
            return clone;
        }

        // Two players split across rather than down, which keeps each view wider than it
        // is tall. Three or four take quadrants; a third player leaves an empty corner
        // rather than handing someone a differently shaped view to read.
        private static Rect ViewportFor(int index, int count)
        {
            if (count <= 1)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            if (count == 2)
            {
                return index == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f);
            }

            float x = (index % 2) * 0.5f;
            float y = index < 2 ? 0.5f : 0f;
            return new Rect(x, y, 0.5f, 0.5f);
        }

        private void Aim(int index)
        {
            Camera view = views[index];
            if (view == null)
            {
                return;
            }

            float size;
            Vector3 focus = mode == CameraMode.Global
                ? GlobalFocus(view, out size)
                : FollowFocus(view, index, out size);

            view.orthographicSize = size;

            Vector3 target = Clamp(focus, view, size);
            Vector3 looking = view.transform.position + (view.transform.forward * 40f);

            Vector3 velocity = velocities[index];
            looking = Vector3.SmoothDamp(looking, target, ref velocity, followSmoothing);
            velocities[index] = velocity;

            Vector3 seat = looking - (view.transform.forward * 40f);

            // After the smoothing, not before. Fed through SmoothDamp the shake would be
            // averaged away into a slow drift, which is the opposite of the point.
            if (index < shakes.Count)
            {
                Vector2 jolt = shakes[index].Advance(Time.deltaTime, shakeDistance);
                seat += new Vector3(jolt.x, 0f, jolt.y);
            }

            view.transform.position = seat;
        }

        private Vector3 FollowFocus(Camera view, int index, out float size)
        {
            size = FollowSizeFor(followSize, Aspect(view));

            PlayerState player = PlayerFor(index);
            if (player == null)
            {
                return ArenaCentre();
            }

            Vector3 at = MatchView.ToWorld(player.Position, 0f);
            GridPos ahead = Directions.Delta(player.Facing);
            float reach = player.Dashing ? dashLookahead : lookahead;

            return at + new Vector3(ahead.X * reach, 0f, -ahead.Y * reach);
        }

        // Frames whoever is still alive. Dead players drop out of the framing, or the
        // survivors spend the rest of the round zoomed out around a corpse.
        private Vector3 GlobalFocus(Camera view, out float size)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            int alive = 0;

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                if (!player.Alive)
                {
                    continue;
                }

                Vector3 at = MatchView.ToWorld(player.Position, 0f);
                min = Vector2.Min(min, new Vector2(at.x, at.z));
                max = Vector2.Max(max, new Vector2(at.x, at.z));
                alive++;
            }

            if (alive == 0)
            {
                size = WholeArenaSize(view);
                return ArenaCentre();
            }

            float tilt = tiltDegrees * Mathf.Deg2Rad;
            float halfWidth = ((max.x - min.x) * 0.5f) + margin;
            float halfDepth = (((max.y - min.y) * 0.5f) + margin) * Mathf.Sin(tilt);

            size = Mathf.Clamp(
                Mathf.Max(halfDepth, halfWidth / Aspect(view)),
                globalMinSize,
                WholeArenaSize(view));

            return new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
        }

        // Never shows much more than a margin outside the arena: past that the view is
        // mostly scenery and the player loses their sense of where the edges are.
        private Vector3 Clamp(Vector3 focus, Camera view, float size)
        {
            float halfDepth = size / Mathf.Sin(tiltDegrees * Mathf.Deg2Rad);
            float halfWidth = size * Aspect(view);

            float slackX = Mathf.Max(0f, ((groundMax.x - groundMin.x) * 0.5f) + margin - halfWidth);
            float slackZ = Mathf.Max(0f, ((groundMax.y - groundMin.y) * 0.5f) + margin - halfDepth);

            Vector3 centre = ArenaCentre();
            return new Vector3(
                Mathf.Clamp(focus.x, centre.x - slackX, centre.x + slackX),
                0f,
                Mathf.Clamp(focus.z, centre.z - slackZ, centre.z + slackZ));
        }

        private float WholeArenaSize(Camera view)
        {
            float tilt = tiltDegrees * Mathf.Deg2Rad;
            float halfWidth = ((groundMax.x - groundMin.x) * 0.5f) + margin;
            float halfDepth = (((groundMax.y - groundMin.y) * 0.5f) + margin) * Mathf.Sin(tilt);
            return Mathf.Max(halfDepth, halfWidth / Aspect(view));
        }

        private PlayerState PlayerFor(int index)
        {
            int which = firstSeat + (mode == CameraMode.Split ? index : 0);
            return which < state.Players.Count ? state.Players[which] : null;
        }

        private Vector3 ArenaCentre()
        {
            return new Vector3((groundMin.x + groundMax.x) * 0.5f, 0f, (groundMin.y + groundMax.y) * 0.5f);
        }

        private void MeasureGround()
        {
            groundMin = new Vector2(float.MaxValue, float.MaxValue);
            groundMax = new Vector2(float.MinValue, float.MinValue);

            for (int y = 0; y < state.Arena.Height; y++)
            {
                for (int x = 0; x < state.Arena.Width; x++)
                {
                    if (state.Arena[new GridPos(x, y)] == TileKind.Void)
                    {
                        continue;
                    }

                    Vector3 at = MatchView.ToWorld(new GridPos(x, y), 0f);
                    groundMin = Vector2.Min(groundMin, new Vector2(at.x, at.z));
                    groundMax = Vector2.Max(groundMax, new Vector2(at.x, at.z));
                }
            }

            if (groundMin.x > groundMax.x)
            {
                groundMin = Vector2.zero;
                groundMax = new Vector2(state.Arena.Width - 1, -(state.Arena.Height - 1));
            }
        }

        private Camera Own()
        {
            if (own == null)
            {
                own = GetComponent<Camera>();
            }

            return own;
        }

        private float Aspect()
        {
            return Aspect(Own());
        }

        private static float Aspect(Camera view)
        {
            float aspect = view.aspect;
            return aspect > 0.01f ? aspect : 16f / 9f;
        }
    }
}
