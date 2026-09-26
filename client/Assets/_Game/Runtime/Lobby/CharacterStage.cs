using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class CharacterStage : MonoBehaviour
    {
        private const int Width = 512;
        private const int Height = 768;
        private const float ModelHeight = 1.8f;
        private const float TurnDegreesPerSecond = 28f;

        private static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

        private RenderTexture texture;
        private GameObject model;

        public Texture Texture
        {
            get { return texture; }
        }

        private void Awake()
        {
            texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.name = "Character stage";

            var camera = new GameObject("Stage camera", typeof(Camera));
            camera.transform.SetParent(transform, false);
            camera.transform.position = Origin + new Vector3(0f, 1f, -4.6f);
            camera.transform.LookAt(Origin + new Vector3(0f, 0.9f, 0f));

            Camera view = camera.GetComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0f, 0f, 0f, 0f);
            view.fieldOfView = 26f;
            view.targetTexture = texture;
            view.cullingMask = ~0;

            var lamp = new GameObject("Stage light", typeof(Light));
            lamp.transform.SetParent(transform, false);
            lamp.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            Light light = lamp.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
        }

        public void Show(MatchArt art, CharacterKind character)
        {
            ShowModel(art == null ? null : art.ForCharacter(character), art == null ? null : art.PlayerAnimator);
        }

        public void ShowModel(GameObject prefab, RuntimeAnimatorController fallbackAnimator)
        {
            if (model != null)
            {
                Destroy(model);
                model = null;
            }

            if (prefab == null)
            {
                return;
            }

            model = Instantiate(prefab, Origin, Quaternion.Euler(0f, 160f, 0f), transform);
            TileFitter.FitToHeight(model, ModelHeight);
            model.transform.position = Origin;

            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (animator.runtimeAnimatorController == null && fallbackAnimator != null)
                {
                    animator.runtimeAnimatorController = fallbackAnimator;
                }

                animator.applyRootMotion = false;
            }
        }

        private void Update()
        {
            if (model != null)
            {
                model.transform.Rotate(0f, TurnDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
            }
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
