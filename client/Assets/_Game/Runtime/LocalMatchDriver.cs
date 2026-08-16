using Blastlands.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    // Drives a match locally: player 0 is the keyboard, the rest stand still until
    // bots exist. The simulation advances on a fixed tick, never on frame time, so
    // the same inputs produce the same match at 30 or 240 FPS.
    public sealed class LocalMatchDriver : MonoBehaviour
    {
        [SerializeField] private MatchView view;
        [SerializeField] private MatchCamera matchCamera;
        [SerializeField] private MatchHud hud;
        [SerializeField] private MatchFog fog;

        // Which arena the match is dressed in. Rolled from the seed like everything
        // else about the layout, so the number that reproduces a bug reproduces what it
        // looked like too.
        [SerializeField] private ArenaTheme[] themes;
        [SerializeField] private int arenaWidth = 15;
        [SerializeField] private int arenaHeight = 13;
        [SerializeField] private int softBlockPercent = 70;
        [SerializeField] private int playerCount = 4;

        // Zero means roll a fresh arena every match. Set it to reproduce a specific
        // one: the bug report template asks for the seed precisely because it is the
        // whole arena in a single number.
        [SerializeField] private uint seed;

        private const int MaxCatchUpTicks = 5;

        [SerializeField] private BotSkill botSkill = BotSkill.Normal;

        private MatchState state;
        private PlayerInput[] inputs;
        private PlayerDevices devices;
        private BotBrain[] bots;
        private float accumulator;
        private uint activeSeed;

        public MatchState State
        {
            get { return state; }
        }

        public uint ActiveSeed
        {
            get { return activeSeed; }
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

        // Choosing the seed is not part of the simulation, so system randomness is
        // fine here. Everything downstream of it stays deterministic, which is what
        // lets a match be replayed or shared between clients from this one number.
        private static uint RollSeed()
        {
            return (uint)Random.Range(1, int.MaxValue);
        }

        private void StartMatch()
        {
            activeSeed = seed != 0u ? seed : RollSeed();

            var arenaSettings = new ArenaSettings(arenaWidth, arenaHeight, softBlockPercent);
            state = MatchFactory.Create(arenaSettings, MatchSettings.Default, playerCount, activeSeed);
            Debug.Log("Blastlands arena seed " + activeSeed);
            inputs = new PlayerInput[state.Players.Count];
            devices = new PlayerDevices(state.Players.Count);
            accumulator = 0f;

            // Every seat a human is not holding gets a bot, so a match is full whether
            // one person is playing or four.
            bots = new BotBrain[state.Players.Count];
            int humans = Mathf.Clamp(Gamepad.all.Count, 1, bots.Length);
            for (int i = humans; i < bots.Length; i++)
            {
                bots[i] = new BotBrain(i, BotSkills.SettingsFor(botSkill));
            }

            Debug.Log("Blastlands controls: " + devices.DescribeAssignment()
                + " | humans: " + humans + ", bots: " + (bots.Length - humans) + " (" + botSkill + ")");

            if (view != null)
            {
                view.UseTheme(ThemeFor(activeSeed));
                view.Bind(state);
                view.Render();
            }

            if (hud != null)
            {
                hud.Bind(state);
            }

            if (matchCamera != null)
            {
                matchCamera.Bind(state, humans);
            }

            // After the view, which is what owns the renderers it switches around.
            if (fog != null)
            {
                fog.Bind(state);
            }
        }

        private ArenaTheme ThemeFor(uint matchSeed)
        {
            if (themes == null || themes.Length == 0)
            {
                return null;
            }

            return themes[(int)(matchSeed % (uint)themes.Length)];
        }

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            // Rerolling the arena on demand is how the generator gets exercised: a
            // layout flaw only shows up across many maps, not one.
            if (devices.RerollPressed())
            {
                seed = 0u;
                StartMatch();
                return;
            }

            devices.PollPresses();

            float step = 1f / state.Settings.TicksPerSecond;
            accumulator += Time.deltaTime;

            int ticked = 0;
            while (accumulator >= step && ticked < MaxCatchUpTicks)
            {
                accumulator -= step;
                ticked++;

                for (int i = 0; i < inputs.Length; i++)
                {
                    inputs[i] = bots[i] != null ? bots[i].Think(state) : devices.Sample(i);
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

            if (hud != null)
            {
                hud.Render();
            }
        }
    }
}
