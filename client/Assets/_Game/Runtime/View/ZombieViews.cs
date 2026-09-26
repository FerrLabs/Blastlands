using System.Collections.Generic;
using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class ZombieViews
    {
        private readonly ViewStage stage;
        private readonly float height;
        private readonly Color fallbackColor;
        private readonly Dictionary<int, ZombieView> zombieViews = new Dictionary<int, ZombieView>();
        private readonly HashSet<int> zombiesSeen = new HashSet<int>();
        private readonly List<int> zombiesGone = new List<int>();
        private readonly FallingBodies falls;

        public ZombieViews(ViewStage stage, float height, Color fallbackColor)
        {
            this.stage = stage;
            this.height = height;
            this.fallbackColor = fallbackColor;
            falls = new FallingBodies(height);
        }

        private sealed class ZombieView
        {
            public GameObject Body;
            public Animator Animator;
            public float Heading;
            public GridPos Tile;
        }

        public void Sync()
        {
            MatchState state = stage.State;
            zombiesSeen.Clear();
            float perTick = state.Settings.Survival.SpeedIn(state.Wave < 1 ? 1 : state.Wave) / (float)SubPos.UnitsPerTile;
            float tile = (MatchView.ToWorld(new GridPos(1, 0), 0f) - MatchView.ToWorld(new GridPos(0, 0), 0f)).magnitude;
            float step = perTick * tile * state.Settings.TicksPerSecond * Time.deltaTime;

            for (int i = 0; i < state.Zombies.Count; i++)
            {
                Zombie zombie = state.Zombies[i];
                zombiesSeen.Add(zombie.Id);
                Vector3 target = MatchView.ToWorld(zombie.Position, 0f);

                if (!zombieViews.TryGetValue(zombie.Id, out ZombieView shown))
                {
                    shown = Raise(zombie);
                    shown.Body.transform.position = target;
                    zombieViews[zombie.Id] = shown;
                }

                shown.Tile = zombie.Tile;
                Vector3 from = shown.Body.transform.position;
                Vector3 at = Vector3.MoveTowards(from, target, step * 1.5f);
                shown.Body.transform.position = at;
                shown.Heading = Mathf.MoveTowardsAngle(
                    shown.Heading, ViewStage.FacingAngle(zombie.Facing), PlayerViews.TurnDegreesPerSecond * Time.deltaTime);
                shown.Body.transform.rotation = Quaternion.Euler(0f, shown.Heading, 0f);

                if (shown.Animator != null)
                {
                    bool moving = (target - from).sqrMagnitude > 0.0001f;
                    shown.Animator.speed = Mathf.MoveTowards(
                        shown.Animator.speed, moving ? 1f : 0.25f, PlayerViews.CadencePerSecond * Time.deltaTime);
                }
            }

            zombiesGone.Clear();
            foreach (KeyValuePair<int, ZombieView> entry in zombieViews)
            {
                if (!zombiesSeen.Contains(entry.Key))
                {
                    zombiesGone.Add(entry.Key);
                }
            }

            for (int i = 0; i < zombiesGone.Count; i++)
            {
                ZombieView gone = zombieViews[zombiesGone[i]];
                if (stage.Sfx != null)
                {
                    stage.Sfx.Died(gone.Body.transform.position);
                }

                Vector3 push = DeathFall.PushAt(state, gone.Tile);
                falls.Drop(gone.Body, gone.Animator, DeathFall.StateFor(push, gone.Heading), true);
                zombieViews.Remove(zombiesGone[i]);
            }

            falls.Sync();
        }

        private ZombieView Raise(Zombie zombie)
        {
            GameObject prefab = stage.Art == null ? null : stage.Art.Zombie(zombie.Id);
            GameObject body = stage.Spawn(prefab, PrimitiveType.Capsule, fallbackColor, "Zombie " + zombie.Id);
            if (prefab == null)
            {
                body.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            }
            else
            {
                TileFitter.FitToHeight(body, height);
            }

            var animator = body.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController == null && stage.Art != null)
            {
                animator.runtimeAnimatorController = stage.Art.ZombieAnimator;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            return new ZombieView { Body = body, Animator = animator, Heading = ViewStage.FacingAngle(zombie.Facing) };
        }
    }
}
