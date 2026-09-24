using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class TickBlend
    {
        private const float TeleportDistance = 2.5f;

        private Vector3 from;
        private Vector3 to;
        private Vector3 shown;
        private bool placed;

        public Vector3 Shown
        {
            get { return shown; }
        }

        public Vector3 Heading
        {
            get { return to - from; }
        }

        public Vector3 Show(Vector3 target, bool ticked, float fraction)
        {
            if (!placed || (target - shown).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                from = target;
                to = target;
                shown = target;
                placed = true;
                return shown;
            }

            if (ticked)
            {
                from = shown;
                to = target;
            }

            shown = Vector3.Lerp(from, to, Mathf.Clamp01(fraction));
            return shown;
        }

        public void Snap(Vector3 target)
        {
            from = target;
            to = target;
            shown = target;
            placed = true;
        }
    }
}
