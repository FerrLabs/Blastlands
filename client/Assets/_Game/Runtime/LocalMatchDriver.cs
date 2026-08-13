using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    // Drives a match locally: player 0 is the keyboard, the rest stand still until
    // bots exist. The simulation advances on a fixed tick, never on frame time, so
    // the same inputs produce the same match at 30 or 240 FPS.
    public sealed class LocalMatchDriver : MonoBehaviour
    {
        [SerializeField] private MatchView view;
        [SerializeField] private MatchCamera matchCamera;
        [SerializeField] private int arenaWidth = 15;
        [SerializeField] private int arenaHeight = 13;
        [SerializeField] private int softBlockPercent = 70;
        [SerializeField] private int playerCount = 4;
        [SerializeField] private uint seed = 20260813u;

        private const int MaxCatchUpTicks = 5;

        private MatchState state;
        private PlayerInput[] inputs;
        private float accumulator;
        private bool dropQueued;

        public MatchState State
        {
            get { return state; }
        }

        public void Restart(uint newSeed)
        {
            seed = newSeed;
            StartMatch();
        }

        private void Start()
        {
            StartMatch();
        }

        private void StartMatch()
        {
            var arenaSettings = new ArenaSettings(arenaWidth, arenaHeight, softBlockPercent);
            state = MatchFactory.Create(arenaSettings, MatchSettings.Default, playerCount, seed);
            inputs = new PlayerInput[state.Players.Count];
            accumulator = 0f;
            dropQueued = false;

            if (view != null)
            {
                view.Bind(state);
                view.Render();
            }

            if (matchCamera != null)
            {
                matchCamera.Bind(state.Arena);
            }
        }

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            // A bomb press between two ticks must not be swallowed by the frame that
            // happens to fall between them.
            if (KeyboardInput.DropPressed())
            {
                dropQueued = true;
            }

            float step = 1f / state.Settings.TicksPerSecond;
            accumulator += Time.deltaTime;

            int ticked = 0;
            while (accumulator >= step && ticked < MaxCatchUpTicks)
            {
                accumulator -= step;
                ticked++;

                inputs[0] = KeyboardInput.Sample(dropQueued);
                dropQueued = false;

                for (int i = 1; i < inputs.Length; i++)
                {
                    inputs[i] = PlayerInput.None;
                }

                MatchSim.Tick(state, inputs);
            }

            if (ticked == MaxCatchUpTicks)
            {
                accumulator = 0f;
            }

            if (view != null)
            {
                view.Render();
            }
        }
    }
}
