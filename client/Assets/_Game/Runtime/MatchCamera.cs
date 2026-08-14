using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // No camera control by design: the whole arena is always on screen, because a
    // player has to be able to see every tile that is about to be on fire.
    [RequireComponent(typeof(Camera))]
    public sealed class MatchCamera : MonoBehaviour
    {
        [SerializeField] private float tiltDegrees = 55f;
        // Wide enough to show a band of the surrounding scenery. Framing the arena
        // exactly slices the decoration at the edges, which looks like a bug.
        [SerializeField] private float margin = 2.8f;

        private Camera view;
        private Arena framed;
        private float lastAspect;

        public void Bind(Arena arena)
        {
            framed = arena;
            lastAspect = 0f;
            Frame();
        }

        private void Update()
        {
            // The aspect ratio is unknown until the game view exists, and changes on resize.
            if (framed != null && !Mathf.Approximately(Aspect(), lastAspect))
            {
                Frame();
            }
        }

        private void Frame()
        {
            if (framed == null)
            {
                return;
            }

            Camera camera = View();
            camera.orthographic = true;
            lastAspect = Aspect();

            float tilt = tiltDegrees * Mathf.Deg2Rad;

            // Tilting compresses the arena's depth on screen by sin(tilt). The width is
            // unaffected, so the vertical half-extent is the larger of the two needs.
            float halfWidth = (framed.Width * 0.5f) + margin;
            float halfDepth = ((framed.Height * 0.5f) + margin) * Mathf.Sin(tilt);

            camera.orthographicSize = Mathf.Max(halfDepth, halfWidth / lastAspect);

            var centre = new Vector3((framed.Width - 1) * 0.5f, 0f, -(framed.Height - 1) * 0.5f);
            transform.rotation = Quaternion.Euler(tiltDegrees, 0f, 0f);
            transform.position = centre - (transform.forward * 40f);
        }

        private Camera View()
        {
            if (view == null)
            {
                view = GetComponent<Camera>();
            }

            return view;
        }

        private float Aspect()
        {
            float aspect = View().aspect;
            return aspect > 0.01f ? aspect : 16f / 9f;
        }
    }
}
