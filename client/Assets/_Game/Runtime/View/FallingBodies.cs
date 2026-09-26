using System.Collections.Generic;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class FallingBodies
    {
        private const float LyingSeconds = 2.4f;
        private const float SinkingSeconds = 0.8f;
        private const float BlendSeconds = 0.08f;

        private readonly float sinkDepth;
        private readonly List<Body> bodies = new List<Body>();

        public FallingBodies(float sinkDepth)
        {
            this.sinkDepth = sinkDepth;
        }

        private sealed class Body
        {
            public GameObject View;
            public Vector3 Rest;
            public float Since;
            public bool DestroyWhenGone;
        }

        public void Drop(GameObject view, Animator animator, int fallState, bool destroyWhenGone)
        {
            if (animator != null && animator.HasState(0, fallState))
            {
                animator.speed = 1f;
                animator.CrossFadeInFixedTime(fallState, BlendSeconds);
            }

            bodies.Add(new Body
            {
                View = view,
                Rest = view.transform.position,
                Since = Time.time,
                DestroyWhenGone = destroyWhenGone
            });
        }

        public bool Holds(GameObject view)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].View == view)
                {
                    return true;
                }
            }

            return false;
        }

        public void Sync()
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                Body body = bodies[i];
                if (body.View == null)
                {
                    bodies.RemoveAt(i);
                    continue;
                }

                float sunk = SunkFraction(Time.time - body.Since);
                body.View.transform.position = body.Rest + (Vector3.down * sinkDepth * sunk);

                if (sunk >= 1f)
                {
                    Remove(body);
                    bodies.RemoveAt(i);
                }
            }
        }

        private static float SunkFraction(float secondsSinceDeath)
        {
            return Mathf.Clamp01((secondsSinceDeath - LyingSeconds) / SinkingSeconds);
        }

        private static void Remove(Body body)
        {
            if (body.DestroyWhenGone)
            {
                Object.Destroy(body.View);
            }
            else
            {
                body.View.SetActive(false);
            }
        }
    }
}
