using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public static class DeathFall
    {
        public static readonly int FallBackward = Animator.StringToHash("FallBackward");
        public static readonly int FallForward = Animator.StringToHash("FallForward");
        public static readonly int FallRight = Animator.StringToHash("FallRight");
        public static readonly int FallLeft = Animator.StringToHash("FallLeft");

        private const int FireReach = 3;
        private const float BiteReach = 1.5f;

        public static Vector3 PushAt(MatchState state, GridPos tile)
        {
            Vector3 at = MatchView.ToWorld(tile, 0f);
            if (state.HasFlameAt(tile))
            {
                return AwayFromTheFireInLine(state, tile, at);
            }

            Zombie biter = NearestBiter(state, at);
            return biter == null ? Vector3.zero : at - MatchView.ToWorld(biter.Position, 0f);
        }

        public static int StateFor(Vector3 push, float headingDegrees)
        {
            var flat = new Vector3(push.x, 0f, push.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return FallBackward;
            }

            Vector3 facing = Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            float angle = Vector3.SignedAngle(facing, flat, Vector3.up);

            if (Mathf.Abs(angle) <= 45f)
            {
                return FallForward;
            }

            if (Mathf.Abs(angle) >= 135f)
            {
                return FallBackward;
            }

            return angle > 0f ? FallRight : FallLeft;
        }

        private static Vector3 AwayFromTheFireInLine(MatchState state, GridPos tile, Vector3 at)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < state.Flames.Count; i++)
            {
                GridPos fire = state.Flames[i].Tile;
                int apart = Mathf.Abs(fire.X - tile.X) + Mathf.Abs(fire.Y - tile.Y);
                bool inLine = fire.X == tile.X || fire.Y == tile.Y;
                if (inLine && apart > 0 && apart <= FireReach)
                {
                    push += (at - MatchView.ToWorld(fire, 0f)).normalized;
                }
            }

            return push;
        }

        private static Zombie NearestBiter(MatchState state, Vector3 at)
        {
            Zombie nearest = null;
            float best = BiteReach * BiteReach;
            for (int i = 0; i < state.Zombies.Count; i++)
            {
                float apart = (MatchView.ToWorld(state.Zombies[i].Position, 0f) - at).sqrMagnitude;
                if (apart <= best)
                {
                    best = apart;
                    nearest = state.Zombies[i];
                }
            }

            return nearest;
        }
    }
}
