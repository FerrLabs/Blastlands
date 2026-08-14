using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Blastlands.Runtime
{
    // One framed panel per player, pinned to a corner. Like MatchView it renders state
    // and owns none of it, so it can be switched off without changing a match.
    //
    // Built from the Synty icon meshes rather than from UI sprites, because the packs
    // ship no interface art: the icons, digits and letters are all 3D. They are drawn by
    // their own orthographic camera over the top of the arena, which is what keeps the
    // panels a fixed size on screen while the arena camera moves.
    public sealed class MatchHud : MonoBehaviour
    {
        private const float CameraSize = 5f;

        // The icon meshes face -Z, so seen from a camera looking down +Z they are drawn
        // back to front and every digit comes out mirrored: a 2 reads as an S.
        private static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

        private static readonly Vector2[] Corners =
        {
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f)
        };

        [SerializeField] private HudArt art;
        [SerializeField] private Vector2 panelSize = new Vector2(3.3f, 1.15f);
        [SerializeField] private float margin = 0.35f;
        [SerializeField] private float iconScale = 0.42f;
        [SerializeField] private float digitScale = 0.44f;

        private readonly List<Panel> panels = new List<Panel>();
        private MatchState state;
        private Camera hudCamera;
        private Material plateMaterial;
        private int hudLayer = -1;

        private sealed class Panel
        {
            public Transform Root;
            public Renderer[] Frame;
            public Transform Skull;
            public Readout[] Readouts;
        }

        private sealed class Readout
        {
            public Transform Anchor;
            public int Shown = -1;
            public readonly List<GameObject> Digits = new List<GameObject>();
        }

        public void Bind(MatchState matchState)
        {
            state = matchState;
            Rebuild();
        }

        public void Render()
        {
            if (state == null || hudCamera == null)
            {
                return;
            }

            for (int i = 0; i < panels.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Panel panel = panels[i];

                Place(panel.Root, i);
                SetDigits(panel.Readouts[0], player.BombCapacity);
                SetDigits(panel.Readouts[1], player.FireRange);
                SetDigits(panel.Readouts[2], player.SpeedSteps + 1);

                // A dead player keeps their corner. Who is left is the state of the
                // round, and a panel that vanished would reshuffle the others.
                if (panel.Skull != null)
                {
                    panel.Skull.gameObject.SetActive(!player.Alive);
                }
            }
        }

        private void Place(Transform root, int index)
        {
            Vector2 corner = Corners[index % Corners.Length];
            int row = index / Corners.Length;

            float halfHeight = CameraSize;
            float halfWidth = CameraSize * hudCamera.aspect;

            float x = corner.x * (halfWidth - margin - (panelSize.x * 0.5f));
            float y = corner.y * (halfHeight - margin - (panelSize.y * 0.5f) - (row * (panelSize.y + 0.15f)));

            root.localPosition = new Vector3(x, y, 10f);
        }

        private void Rebuild()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Root != null)
                {
                    Destroy(panels[i].Root.gameObject);
                }
            }

            panels.Clear();

            if (state == null)
            {
                return;
            }

            EnsureCamera();

            for (int i = 0; i < state.Players.Count; i++)
            {
                panels.Add(BuildPanel(i));
            }

            Render();
        }

        private void EnsureCamera()
        {
            if (hudCamera != null)
            {
                return;
            }

            hudLayer = LayerMask.NameToLayer("Hud");

            var host = new GameObject("MatchHud Camera", typeof(Camera));
            host.transform.SetParent(transform, false);

            hudCamera = host.GetComponent<Camera>();
            hudCamera.orthographic = true;
            hudCamera.orthographicSize = CameraSize;
            hudCamera.cullingMask = hudLayer >= 0 ? 1 << hudLayer : 0;
            hudCamera.nearClipPlane = 0.1f;
            hudCamera.farClipPlane = 40f;

            // URP ignores clearFlags on a base camera and wipes the colour buffer, so a
            // second camera drawn on top by depth alone erases the arena instead of
            // overlaying it. Overlays have to be stacked onto the camera they sit over.
            hudCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

            Camera main = Camera.main;
            if (main != null)
            {
                List<Camera> stack = main.GetUniversalAdditionalCameraData().cameraStack;
                if (!stack.Contains(hudCamera))
                {
                    stack.Add(hudCamera);
                }
            }

            plateMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            plateMaterial.color = new Color(0.05f, 0.04f, 0.03f, 1f);

            // The icons are Lit meshes, so without a light of their own they render as
            // silhouettes over the arena.
            var lamp = new GameObject("MatchHud Light", typeof(Light));
            lamp.transform.SetParent(host.transform, false);
            lamp.transform.localRotation = Quaternion.Euler(35f, 15f, 0f);

            Light light = lamp.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.cullingMask = hudCamera.cullingMask;
        }

        private Panel BuildPanel(int index)
        {
            var root = new GameObject("Player " + (index + 1));
            root.transform.SetParent(hudCamera.transform, false);

            Color accent = MatchPalette.ForPlayer(index);

            // The camera looks down +Z, so the backing sits at a larger z than the icons
            // it is meant to sit behind.
            Plate(root.transform, "Plate", Vector3.zero, panelSize, plateMaterial.color, 0.08f);

            var frame = new List<Renderer>
            {
                Border(root.transform, accent, new Vector2(0f, 1f)),
                Border(root.transform, accent, new Vector2(0f, -1f)),
                Border(root.transform, accent, new Vector2(-1f, 0f)),
                Border(root.transform, accent, new Vector2(1f, 0f))
            };

            var readouts = new Readout[3];
            GameObject[] icons = { IconFor(HudIcon.Bombs), IconFor(HudIcon.Fire), IconFor(HudIcon.Speed) };

            float slot = panelSize.x / 3f;
            for (int i = 0; i < 3; i++)
            {
                float left = (-panelSize.x * 0.5f) + (slot * i) + 0.28f;

                if (icons[i] != null)
                {
                    GameObject icon = Instantiate(icons[i], root.transform);
                    icon.transform.localPosition = new Vector3(left, 0f, 0f);
                    icon.transform.localRotation = FaceCamera;
                    icon.transform.localScale = Vector3.one * iconScale;
                    SetLayer(icon, hudLayer);
                }

                var anchor = new GameObject("Value " + i);
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.localPosition = new Vector3(left + 0.42f, 0f, 0f);

                readouts[i] = new Readout { Anchor = anchor.transform };
            }

            Transform skull = null;
            if (art != null && art.Dead != null)
            {
                GameObject dead = Instantiate(art.Dead, root.transform);
                dead.transform.localPosition = new Vector3((panelSize.x * 0.5f) - 0.28f, 0f, -0.08f);
                dead.transform.localRotation = FaceCamera;
                dead.transform.localScale = Vector3.one * (iconScale * 1.15f);
                SetLayer(dead, hudLayer);
                skull = dead.transform;
            }

            SetLayer(root, hudLayer);

            return new Panel
            {
                Root = root.transform,
                Frame = frame.ToArray(),
                Skull = skull,
                Readouts = readouts
            };
        }

        private enum HudIcon
        {
            Bombs,
            Fire,
            Speed
        }

        private GameObject IconFor(HudIcon icon)
        {
            if (art == null)
            {
                return null;
            }

            switch (icon)
            {
                case HudIcon.Bombs: return art.Bombs;
                case HudIcon.Fire: return art.Fire;
                default: return art.Speed;
            }
        }

        private Renderer Border(Transform parent, Color color, Vector2 edge)
        {
            const float thickness = 0.07f;

            var size = new Vector2(
                edge.x == 0f ? panelSize.x : thickness,
                edge.y == 0f ? panelSize.y : thickness);

            var at = new Vector3(
                edge.x * (panelSize.x - thickness) * 0.5f,
                edge.y * (panelSize.y - thickness) * 0.5f,
                0f);

            return Plate(parent, "Frame", at, size, color, 0.06f);
        }

        private Renderer Plate(Transform parent, string name, Vector3 at, Vector2 size, Color color, float z)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());

            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(at.x, at.y, z);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(plateMaterial) { color = color };
            return renderer;
        }

        // Digits are meshes, so a changed value means rebuilding the row. Values only
        // ever move on a pickup, which is why this is not a per-frame cost.
        private void SetDigits(Readout readout, int value)
        {
            if (readout.Shown == value || art == null)
            {
                return;
            }

            readout.Shown = value;

            for (int i = 0; i < readout.Digits.Count; i++)
            {
                Destroy(readout.Digits[i]);
            }

            readout.Digits.Clear();

            string text = Mathf.Max(0, value).ToString();
            for (int i = 0; i < text.Length; i++)
            {
                GameObject prefab = art.Digit(text[i] - '0');
                if (prefab == null)
                {
                    continue;
                }

                GameObject digit = Instantiate(prefab, readout.Anchor);
                digit.transform.localPosition = new Vector3(i * 0.3f, 0f, 0f);
                digit.transform.localRotation = FaceCamera;
                digit.transform.localScale = Vector3.one * digitScale;
                SetLayer(digit, hudLayer);
                readout.Digits.Add(digit);
            }
        }

        private static void SetLayer(GameObject target, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }
    }
}
