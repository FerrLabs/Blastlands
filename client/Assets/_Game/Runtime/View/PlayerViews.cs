using System;
using System.Collections.Generic;
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class PlayerViews
    {
        public const float TurnDegreesPerSecond = 900f;
        public const float CadencePerSecond = 4f;
        private const float GaitDampSeconds = 0.06f;

        private static readonly int Speed = Animator.StringToHash("Speed_f");

        private readonly ViewStage stage;
        private readonly float playerHeight;
        private readonly float ringRadius;
        private readonly float ringWidth;
        private readonly float groundDetailLift;
        private readonly float runClipSpeed;
        private readonly List<GameObject> playerViews = new List<GameObject>();
        private readonly List<bool> wasAlive = new List<bool>();
        private readonly List<Animator> playerAnimators = new List<Animator>();
        private readonly List<CharacterKind> castAs = new List<CharacterKind>();
        private readonly List<SubPos> lastSampled = new List<SubPos>();
        private readonly List<TickBlend> blends = new List<TickBlend>();
        private readonly List<float> headings = new List<float>();
        private readonly List<Gait> gaits = new List<Gait>();
        private readonly List<int> stillTicks = new List<int>();
        private readonly FallingBodies falls;
        private int lastSampledTick = -1;
        private PlayerTrail trail;
        private InterpolationClock clock;
        private int ownSeat = -1;
        private Func<SubPos> predictedOwn;

        public PlayerViews(ViewStage stage, float playerHeight, float ringRadius, float ringWidth, float groundDetailLift, float runClipSpeed)
        {
            this.stage = stage;
            this.playerHeight = playerHeight;
            this.ringRadius = ringRadius;
            this.ringWidth = ringWidth;
            this.groundDetailLift = groundDetailLift;
            this.runClipSpeed = runClipSpeed;
            falls = new FallingBodies(playerHeight);
        }

        public int Count
        {
            get { return playerViews.Count; }
        }

        public GameObject At(int index)
        {
            return index >= 0 && index < playerViews.Count ? playerViews[index] : null;
        }

        public void Follow(PlayerTrail playerTrail, InterpolationClock serverClock, int seat, Func<SubPos> ownPosition)
        {
            trail = playerTrail;
            clock = serverClock;
            ownSeat = seat;
            predictedOwn = ownPosition;
        }

        public bool TryShown(int seat, out Vector3 position)
        {
            if (seat < 0 || seat >= blends.Count || seat >= playerViews.Count || !playerViews[seat].activeSelf)
            {
                position = default;
                return false;
            }

            position = playerViews[seat].transform.position;
            return true;
        }

        public void Build()
        {
            MatchState state = stage.State;
            for (int i = 0; i < state.Players.Count; i++)
            {
                CharacterKind character = state.Players[i].Character;
                GameObject view = CastPlayer(i, character);
                view.SetActive(state.Players[i].Alive);
                playerViews.Add(view);
                playerAnimators.Add(Rig(view));
                castAs.Add(character);
                wasAlive.Add(state.Players[i].Alive);
                lastSampled.Add(state.Players[i].Position);
                blends.Add(new TickBlend());
                headings.Add(ViewStage.FacingAngle(state.Players[i].Facing));
                gaits.Add(new Gait(PlayerPace.Still, PlayerPace.RestingCadence));
                stillTicks.Add(0);
            }
        }

        public void Sync(bool blending, int ticksThisFrame, float tickFraction)
        {
            MatchState state = stage.State;
            PaceAnimators();

            for (int i = 0; i < playerViews.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];

                if (i < castAs.Count && castAs[i] != player.Character)
                {
                    Recast(i, player.Character);
                }

                GameObject view = playerViews[i];

                if (!player.Alive)
                {
                    if (i < wasAlive.Count && wasAlive[i])
                    {
                        wasAlive[i] = false;
                        MarkDeath(player);
                        Fall(i, player);
                    }
                    else if (!falls.Holds(view))
                    {
                        view.SetActive(false);
                    }

                    continue;
                }

                bool appearing = !view.activeSelf;
                view.SetActive(true);

                Vector3 target = MatchView.ToWorld(Placement(i, player), 0f);
                bool continuous = trail != null && clock != null && i != ownSeat;
                TickBlend blend = blends[i];
                Vector3 at;
                if (appearing || !blending)
                {
                    blend.Snap(target);
                    at = target;
                }
                else if (continuous)
                {
                    at = blend.Show(target, true, 1f);
                }
                else
                {
                    at = blend.Show(target, ticksThisFrame > 0, tickFraction);
                }

                view.transform.position = at;
                view.transform.rotation = Quaternion.Euler(0f, Turn(i, player, blend, appearing), 0f);
                Animate(i);
            }

            falls.Sync();
        }

        private void Fall(int seat, PlayerState player)
        {
            GameObject view = playerViews[seat];
            if (!view.activeSelf)
            {
                return;
            }

            Vector3 push = DeathFall.PushAt(stage.State, player.Tile);
            falls.Drop(view, playerAnimators[seat], DeathFall.StateFor(push, headings[seat]), false);
        }

        private SubPos Placement(int index, PlayerState player)
        {
            if (index == ownSeat && predictedOwn != null)
            {
                return predictedOwn();
            }

            if (trail == null || clock == null || index == ownSeat
                || !trail.TrySample(index, clock.RenderTime, out SubPos sampled))
            {
                return player.Position;
            }

            return sampled;
        }

        private GameObject CastPlayer(int seat, CharacterKind character)
        {
            GameObject prefab = stage.Art == null ? null : stage.Art.PlayerFor(seat, character);
            GameObject view = stage.Spawn(prefab, PrimitiveType.Capsule, MatchPalette.ForPlayer(seat), "Player " + seat);

            if (prefab == null)
            {
                view.transform.localScale = new Vector3(0.62f, 0.42f, 0.62f);
            }
            else
            {
                TileFitter.FitToHeight(view, playerHeight);
            }

            PlayerRing.Attach(view, MatchPalette.ForPlayer(seat), ringRadius, ringWidth, groundDetailLift * 4f);
            return view;
        }

        private void Recast(int seat, CharacterKind character)
        {
            playerViews[seat].SetActive(false);
            UnityEngine.Object.Destroy(playerViews[seat]);
            playerViews[seat] = CastPlayer(seat, character);
            playerAnimators[seat] = Rig(playerViews[seat]);
            castAs[seat] = character;
        }

        // Hands back the animator this view will be driven through, or null for the
        // primitive fallback, which has no rig to drive.
        //
        // The controller is only supplied where the prefab brought none of its own.
        // Overwriting one that is already there would throw away whatever the pack
        // author wired up.
        private Animator Rig(GameObject view)
        {
            var animator = view.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return null;
            }

            if (animator.runtimeAnimatorController == null && stage.Art != null && stage.Art.PlayerAnimator != null)
            {
                animator.runtimeAnimatorController = stage.Art.PlayerAnimator;
            }

            if (animator.runtimeAnimatorController == null)
            {
                return null;
            }

            // The simulation owns where a player is, down to the sub-tile unit, and it
            // has to stay that way: it is what makes a replay reproduce and what the
            // netcode will reconcile against. Root motion would let the clip push the
            // transform around on top of that, so the character would drift off its own
            // position by however much the animator felt like.
            //
            // Which is why the controller plays the in-place clips, the same strides
            // authored on the spot. Discarding root motion from a travelling clip leaves
            // the feet skating; playing the one drawn for this case does not.
            animator.applyRootMotion = false;

            return animator;
        }

        // The stain stays for the round: in a four-way match you often miss the moment
        // someone dies, and where it happened is worth knowing.
        private void MarkDeath(PlayerState player)
        {
            if (stage.Sfx != null)
            {
                stage.Sfx.Died(MatchView.ToWorld(player.Tile, 0f));
            }

            GameObject prefab = stage.Art == null ? null : stage.Art.DeathMarker(player.Id);
            if (prefab == null)
            {
                return;
            }

            int hash = TileHash.At(player.Tile.X, player.Tile.Y, stage.State.Seed + (uint)player.Id);

            GameObject stain = UnityEngine.Object.Instantiate(prefab, stage.Root);
            stain.name = "Death " + player.Id;
            stain.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);
            TileFitter.FitInBox(stain, 0.8f + (((hash / 7) % 40) / 100f));

            // Lifted above both the floor and the ground detail, or it z-fights with them.
            Vector3 where = MatchView.ToWorld(player.Position, groundDetailLift * 3f);
            TileFitter.PlaceAsGround(stain, where);
        }

        private float Turn(int i, PlayerState player, TickBlend blend, bool appearing)
        {
            Vector3 heading = blend.Heading;
            float wanted = heading.sqrMagnitude > 0.000001f
                ? Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg
                : ViewStage.FacingAngle(player.Facing);

            headings[i] = appearing
                ? wanted
                : Mathf.MoveTowardsAngle(headings[i], wanted, TurnDegreesPerSecond * Time.deltaTime);
            return headings[i];
        }

        private void Animate(int i)
        {
            Animator animator = i < playerAnimators.Count ? playerAnimators[i] : null;
            if (animator == null)
            {
                return;
            }

            Gait gait = gaits[i];
            animator.SetFloat(Speed, gait.Speed, GaitDampSeconds, Time.deltaTime);
            animator.speed = Mathf.MoveTowards(animator.speed, gait.Cadence, CadencePerSecond * Time.deltaTime);
        }

        // Sampled per tick rather than per frame. Render runs every frame and a position
        // only moves on a tick boundary, so a frame-to-frame delta is the true step on
        // the frames a tick landed on and zero on all the others, which at a few hundred
        // frames a second reads as a player who is standing still almost all the time.
        private void PaceAnimators()
        {
            MatchState state = stage.State;
            if (state.Tick == lastSampledTick)
            {
                return;
            }

            lastSampledTick = state.Tick;

            for (int i = 0; i < playerAnimators.Count && i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Animator animator = playerAnimators[i];

                if (animator != null)
                {
                    Gait measured = PlayerPace.For(player, lastSampled[i], state.Settings, runClipSpeed);
                    stillTicks[i] = measured.Speed < PlayerPace.Running ? stillTicks[i] + 1 : 0;
                    gaits[i] = PlayerPace.Held(measured, gaits[i], stillTicks[i]);
                }

                lastSampled[i] = player.Position;
            }
        }
    }
}
