using Blastlands.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    public sealed class LobbyBackdrop : MonoBehaviour
    {
        private const int Width = 31;
        private const int Height = 25;
        private const float TiltDegrees = 55f;
        private const float ViewSize = 4.6f;
        private const float EyeDistance = 40f;
        private const float DriftSpeed = 0.045f;
        private const int VignetteSize = 256;

        private Camera view;
        private Vector3 centre;
        private float clock;

        public void Build(LobbyArt art, Camera camera)
        {
            ArenaTheme theme = art.ThemeFor((uint)Random.Range(1, int.MaxValue));
            var host = new GameObject("Arena", typeof(MatchView));
            host.transform.SetParent(transform, false);

            MatchView arena = host.GetComponent<MatchView>();
            arena.Dress(art.Models);
            arena.UseTheme(theme);

            uint seed = (uint)Random.Range(1, int.MaxValue);
            MatchState state = MatchFactory.Create(
                new ArenaSettings(Width, Height, 60), MatchSettings.For(GameMode.Arena), 2, seed, CharacterKits.ForSeat);
            arena.Bind(state);
            for (int i = 0; i < state.Players.Count; i++)
            {
                GameObject player = arena.PlayerViewAt(i);
                if (player != null)
                {
                    player.SetActive(false);
                }
            }

            centre = MatchView.ToWorld(new GridPos(Width / 2, Height / 2), 0f);

            var lamp = new GameObject("Backdrop light", typeof(Light));
            lamp.transform.SetParent(transform, false);
            lamp.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Light light = lamp.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.8f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;

            view = camera;
            if (view != null)
            {
                view.orthographic = true;
                view.orthographicSize = ViewSize;
                view.transform.rotation = Quaternion.Euler(TiltDegrees, 0f, 0f);
                Aim();
            }
        }

        public static void Shade(Transform canvas)
        {
            var host = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            host.transform.SetParent(canvas, false);
            LobbyChrome.Stretch(host, 0f);

            RawImage image = host.GetComponent<RawImage>();
            image.texture = Vignette();
            image.raycastTarget = false;
        }

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            Aim();
        }

        private void Aim()
        {
            if (view == null)
            {
                return;
            }

            float t = clock * DriftSpeed * Mathf.PI * 2f;
            Vector3 focus = centre + new Vector3(Mathf.Sin(t) * 6f, 0f, Mathf.Cos(t * 0.73f) * 4f);
            view.transform.position = focus - (view.transform.forward * EyeDistance);
        }

        private static Texture2D Vignette()
        {
            var texture = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
            {
                name = "Lobby vignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[VignetteSize * VignetteSize];
            for (int y = 0; y < VignetteSize; y++)
            {
                for (int x = 0; x < VignetteSize; x++)
                {
                    float dx = ((x + 0.5f) / VignetteSize * 2f) - 1f;
                    float dy = ((y + 0.5f) / VignetteSize * 2f) - 1f;
                    float edge = Mathf.Clamp01(Mathf.Sqrt((dx * dx) + (dy * dy)) / 1.35f);
                    float alpha = Mathf.Lerp(0.32f, 0.86f, edge * edge);
                    pixels[(y * VignetteSize) + x] = new Color32(8, 7, 5, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
