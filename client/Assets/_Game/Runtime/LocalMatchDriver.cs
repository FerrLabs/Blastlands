using System.Text;
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

        // Which game the match is. Arena is the open island with cover you stand in,
        // found bombs, dash and shove; Classic is the pillar lattice, bombs you own and
        // nothing else; Classic Blinded is that same board played without sight of
        // anyone you have no line to. Serialized here rather than chosen in a lobby
        // because there is no lobby screen yet, which is #7.
        [SerializeField] private GameMode mode = GameMode.Arena;

        // Best of five. Long enough that one unlucky round does not decide it, short
        // enough to finish in a sitting.
        [SerializeField] private int roundsToWin = 3;

        // Long enough to see who died and read the score in the log, short enough that
        // nobody reaches for the reroll key.
        [SerializeField] private float intermissionSeconds = 3f;

        private MatchState state;
        private MatchSeries series;
        private float intermissionRemaining;
        private bool roundRecorded;
        private PlayerInput[] inputs;
        private PlayerDevices devices;
        private BotBrain[] bots;
        private TickPacer pacer;
        private uint activeSeed;

        public MatchState State
        {
            get { return state; }
        }

        public uint ActiveSeed
        {
            get { return activeSeed; }
        }

        // The reroll key, which is a tool for looking at arenas rather than a way to
        // play. It starts a new series as well as a new round: carrying the score across
        // a deliberate reset would be scoring rounds nobody played to the end.
        public void Restart(uint newSeed)
        {
            seed = newSeed;
            series = null;
            StartMatch();
        }

        private void Awake()
        {
#if UNITY_SERVER
            // A server build has no screen, no pad and nobody sitting at it. The match
            // it runs belongs to ServerBootstrap, and this driver going first would
            // build a whole view for nobody.
            enabled = false;
#endif
        }

        private void Start()
        {
            StartMatch();
        }

        private void OnDestroy()
        {
            if (devices != null)
            {
                devices.Dispose();
            }
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

            ArenaSettings arenaSettings = mode == GameMode.Arena
                ? new ArenaSettings(arenaWidth, arenaHeight, softBlockPercent)
                : ArenaSettings.Classic;
            MatchSettings matchSettings = MatchSettings.For(mode);

            // Turned by one each round. Seats alone would give the same pad the same kit
            // for a whole series, and the kits are not equal enough for that to be fair.
            int round = series == null ? 0 : series.RoundsPlayed;
            state = MatchFactory.Create(
                arenaSettings, matchSettings, playerCount, activeSeed, CharacterKits.Seating(CharacterChoice.Current, round));

            // Survives the rebuild the next round does. A series is only built when
            // there is none, or when the seat count changed under it and the old score
            // no longer describes who is playing.
            if (series == null || series.PlayerCount != state.Players.Count)
            {
                series = new MatchSeries(state.Players.Count, Mathf.Max(1, roundsToWin));
            }

            roundRecorded = false;
            Debug.Log("Blastlands " + mode + " seed " + activeSeed);
            inputs = new PlayerInput[state.Players.Count];
            if (devices != null)
            {
                devices.Dispose();
                devices = null;
            }

            devices = new PlayerDevices(state.Players.Count);
            pacer = new TickPacer(state.Settings.TicksPerSecond, MaxCatchUpTicks);

            // Every seat a human is not holding gets a bot, so a match is full whether
            // one person is playing or four.
            bots = new BotBrain[state.Players.Count];
            int humans = Mathf.Clamp(Gamepad.all.Count, 1, bots.Length);
            for (int i = humans; i < bots.Length; i++)
            {
                bots[i] = new BotBrain(i, BotSkills.SettingsFor(botSkill));
                state.Players[i].IsBot = true;
            }

            Debug.Log("Blastlands controls: " + devices.DescribeAssignment()
                + " | humans: " + humans + ", bots: " + (bots.Length - humans) + " (" + botSkill + ")");

            if (view != null)
            {
                view.UseTheme(ThemeFor(activeSeed));
                view.Bind(state);
                view.Render();
            }

            if (matchCamera != null)
            {
                matchCamera.Bind(state, humans);
            }

            // After the cameras: the HUD lays a panel set inside each viewport, so it has
            // to be able to see how many there are and where they sit.
            if (hud != null)
            {
                hud.Bind(state, matchCamera, series, devices.KindFor);
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
                Restart(0u);
                return;
            }

            devices.PollPresses();

            int ticks = pacer.Advance(Time.deltaTime);

            for (int tick = 0; tick < ticks; tick++)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    inputs[i] = bots[i] != null ? bots[i].Think(state) : devices.Sample(i);
                }

                MatchSim.Tick(state, inputs);
            }

            if (view != null)
            {
                view.Render();
            }

            if (hud != null)
            {
                hud.Render();
            }

            if (state.Outcome != RoundOutcome.Running)
            {
                HoldOrAdvance();
            }
        }

        // A finished round is scored once, then left on screen for a beat before the
        // next one replaces it. Cutting straight to a new arena on the frame somebody
        // died hides the blast that decided it.
        private void HoldOrAdvance()
        {
            if (series == null)
            {
                return;
            }

            if (!roundRecorded)
            {
                roundRecorded = true;
                intermissionRemaining = intermissionSeconds;
                series.Record(state.Outcome, state.WinnerId);
                Debug.Log("Blastlands round " + series.RoundsPlayed + ": " + Result()
                    + " | " + Score());
            }

            if (series.Decided)
            {
                return;
            }

            intermissionRemaining -= Time.deltaTime;
            if (intermissionRemaining <= 0f)
            {
                // Derived rather than rolled, so a pinned seed describes the whole
                // series instead of only its first round. The reroll key still rolls,
                // because Restart clears the seed on its way through.
                seed = MatchSeries.NextSeed(activeSeed);
                StartMatch();
            }
        }

        private string Result()
        {
            if (state.Outcome != RoundOutcome.Winner)
            {
                return "draw";
            }

            return series.Decided
                ? "player " + state.WinnerId + " takes the series"
                : "player " + state.WinnerId;
        }

        private string Score()
        {
            var text = new StringBuilder();
            for (int i = 0; i < series.PlayerCount; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(series.Wins(i));
            }

            return text.ToString();
        }
    }
}
