using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blastlands.Runtime
{
    // Hides opponents the viewer cannot see, per camera.
    //
    // Per camera is the whole difficulty. Split-screen shows the same arena to four
    // people at once, and each of them is entitled to a different answer about who is
    // visible, so deactivating the opponent's view is not available: one GameObject
    // cannot be off for one viewport and on for the next. Renderers are switched around
    // each camera's own render instead, and switched back afterwards so the scene view,
    // and anything else pointed at the arena, is left alone.
    //
    // Only players are hidden. Bombs, flames and the arena itself stay drawn for the
    // reason Vision gives: a blast nobody could see coming is not a fair death.
    public sealed class MatchFog : MonoBehaviour
    {
        [SerializeField] private MatchView view;
        [SerializeField] private MatchCamera cameras;

        // A player who steps into a bush blinks out mid-stride without this, which reads
        // as the renderer dropping a frame rather than as somebody taking cover. Holding
        // the last sighting for a moment gives the eye time to attach the disappearance
        // to the bush, and gives away nothing the viewer did not already watch happen.
        [SerializeField] private float linger = 0.3f;

        private readonly List<int> viewers = new List<int>();
        private readonly List<Renderer[]> renderers = new List<Renderer[]>();
        private readonly List<GameObject> watched = new List<GameObject>();
        private float[] lastSeen;
        private MatchState state;

        public void Bind(MatchState matchState)
        {
            state = matchState;
            renderers.Clear();
            watched.Clear();

            int players = state == null ? 0 : state.Players.Count;
            lastSeen = new float[players * players];

            for (int i = 0; i < players; i++)
            {
                GameObject player = view == null ? null : view.PlayerViewAt(i);
                renderers.Add(player == null ? new Renderer[0] : player.GetComponentsInChildren<Renderer>(true));
                watched.Add(player);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < watched.Count; i++)
            {
                GameObject player = view == null ? null : view.PlayerViewAt(i);
                if (player != watched[i])
                {
                    watched[i] = player;
                    renderers[i] = player == null ? new Renderer[0] : player.GetComponentsInChildren<Renderer>(true);
                }
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Before;
            RenderPipelineManager.endCameraRendering += After;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Before;
            RenderPipelineManager.endCameraRendering -= After;
            Show();
        }

        // Sampled once a frame rather than inside the render callback: the same camera
        // can be rendered more than once a frame, and a linger measured there would be
        // a different length depending on how the pipeline felt like scheduling it.
        private void LateUpdate()
        {
            if (state == null)
            {
                return;
            }

            Refresh();

            int players = state.Players.Count;
            for (int viewer = 0; viewer < players; viewer++)
            {
                for (int target = 0; target < players; target++)
                {
                    if (Vision.CanSee(state, state.Players[viewer], state.Players[target]))
                    {
                        lastSeen[(viewer * players) + target] = Time.time;
                    }
                }
            }
        }

        private void Before(ScriptableRenderContext context, Camera camera)
        {
            if (state == null || cameras == null)
            {
                return;
            }

            int index = IndexOf(camera);
            if (index < 0)
            {
                return;
            }

            cameras.ViewersOf(index, viewers);

            for (int target = 0; target < renderers.Count; target++)
            {
                Enable(target, VisibleToAny(target));
            }
        }

        private void After(ScriptableRenderContext context, Camera camera)
        {
            if (IndexOf(camera) >= 0)
            {
                Show();
            }
        }

        private bool VisibleToAny(int target)
        {
            int players = state.Players.Count;

            for (int i = 0; i < viewers.Count; i++)
            {
                int viewer = viewers[i];
                if (viewer < 0 || viewer >= players)
                {
                    continue;
                }

                if (Vision.CanSee(state, state.Players[viewer], state.Players[target])
                    || Time.time - lastSeen[(viewer * players) + target] < linger)
                {
                    return true;
                }
            }

            return false;
        }

        private int IndexOf(Camera camera)
        {
            for (int i = 0; i < cameras.ViewCount; i++)
            {
                if (cameras.ViewAt(i) == camera)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Enable(int target, bool visible)
        {
            Renderer[] set = renderers[target];
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    set[i].enabled = visible;
                }
            }
        }

        private void Show()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Enable(i, true);
            }
        }
    }
}
