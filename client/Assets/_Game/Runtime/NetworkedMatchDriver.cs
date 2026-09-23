using System;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using UnityEngine;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    // Plays a match somebody else is simulating.
    //
    // The mirror of LocalMatchDriver, minus the simulation. It samples the local player,
    // sends it, and draws whatever the server last said. It never calls MatchSim: the
    // state it renders is written entirely by snapshots, which is what makes editing it
    // pointless rather than difficult.
    //
    // Where it stops short is the join. There is no lobby screen yet (#7), so the
    // endpoint and the shape of the match are serialized here instead of being learned
    // from the lobby, and the arena has to be told the same size the server built. That
    // is a development harness, not the finished flow.
    public sealed class NetworkedMatchDriver : MonoBehaviour
    {
        private const int MaxCatchUpTicks = 5;

        // How far ahead of the last tick it has seen a client answers. The packet has to
        // cross the wire and land before the server plays that tick, so answering the
        // tick it just watched would arrive for one already gone. Five ticks is 166ms at
        // thirty, which covers a round trip to a nearby server and is the number to
        // raise when it does not. Prediction hides it, so a longer lead is not input lag.
        private const int LeadTicks = 5;

        private const int InterpolationDelayTicks = 3;
        private const int InterpolationSnapTicks = 15;
        private const int TrailLength = 32;

        [SerializeField] private MatchView view;
        [SerializeField] private MatchHud hud;
        [SerializeField] private MatchCamera matchCamera;
        [SerializeField] private MatchFog fog;
        [SerializeField] private ArenaTheme[] themes;

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7777;
        [SerializeField] private string ticket = "";

        [SerializeField] private GameMode mode = GameMode.Arena;

        private MatchState state;
        private MatchTransport transport;
        private PlayerDevices devices;
        private TickPacer pacer;
        private PlayerTrail trail;
        private InterpolationClock clock;
        private ClientPrediction prediction;
        private readonly CorrectionSmoother smoother = new CorrectionSmoother();
        private int nextTick = -1;
        private bool bound;
        private bool complained;
        private float waited;

        // Long enough that a slow connect is not called a fault.
        private const float SecondsBeforeComplaining = 8f;

        public MatchState State
        {
            get { return state; }
        }

        private void Awake()
        {
#if UNITY_SERVER
            // A server build has no screen, nobody sitting at it, and the endpoint this
            // would dial is its own listener. The match a server runs belongs to
            // ServerLoop, the same way the local one belongs to LocalMatchDriver.
            enabled = false;
#endif
        }

        private void Start()
        {
            // The lobby leaves an invite when it hands the match over. The serialized
            // fields are what is left for a scene opened by hand against a server
            // started by hand, which is how this is tested without a lobby running.
            if (MatchHandoff.Waiting)
            {
                MatchInvite invite = MatchHandoff.Take();
                host = invite.Host;
                port = invite.Port;
                ticket = invite.Ticket;
            }

            devices = new PlayerDevices(1);

            transport = gameObject.AddComponent<MatchTransport>();
            if (!transport.StartClient(host, (ushort)port, ticket))
            {
                transport = null;
                return;
            }

            transport.SeatTaken += OnSeated;
            transport.SnapshotApplied += OnSnapshot;
        }

        private void OnSnapshot(MatchState applied)
        {
            trail.Record(applied.Tick, applied.Players);
            clock.Heard(applied.Tick);

            if (prediction == null)
            {
                return;
            }

            SubPos before = prediction.Position;
            bool wasReady = prediction.Ready;
            if (!prediction.Reconcile(applied))
            {
                return;
            }

            if (wasReady)
            {
                smoother.Corrected(before, prediction.Position);
            }
        }

        private static MatchState Blank(MatchState like)
        {
            var blank = new MatchState(new Arena(like.Arena.Width, like.Arena.Height), like.Settings, like.Seed);
            for (int i = 0; i < like.Players.Count; i++)
            {
                blank.AddPlayer(like.Players[i].Tile);
            }

            return blank;
        }

        private SubPos OwnPosition()
        {
            return prediction.Ready
                ? smoother.Apply(prediction.Position)
                : state.Players[transport.Seat].Position;
        }

        // Bound only once the server has said which player this connection is. Binding
        // before then would point the camera at player zero, so a client seated anywhere
        // else would drive one character and watch another, which does not look like a
        // camera fault on screen: it looks like a game that ignores the controller.
        private void OnSeated(SeatAssignment assignment)
        {
            int seat = assignment.Seat;

            // Built from what the server said, not from what this client would have
            // chosen. The seed only decides the board drawn before the first snapshot
            // lands, and every snapshot carries every tile, so it does not have to match
            // the server's. The size and the player count do.
            ArenaSettings arena = (mode == GameMode.Arena ? ArenaSettings.Default : ArenaSettings.Classic)
                .Resized(assignment.Width, assignment.Height);

            // The codec refuses a player count no board could seat, but a board keeps
            // fewer spawns than it could when the island swallows one, and only
            // generating it says so. A server asking for more players than its own board
            // holds is a server this client cannot follow, so it says which numbers it
            // was given and stops rather than throwing out of a message handler.
            try
            {
                state = MatchFactory.Create(arena, MatchSettings.For(mode), assignment.Players, 1u, CharacterKits.ForSeat);
            }
            catch (ArgumentOutOfRangeException error)
            {
                Debug.LogError(
                    "Blastlands client: the server asked for a match this client cannot build, "
                    + assignment.Players + " players on " + assignment.Width + "x" + assignment.Height
                    + ". " + error.Message);
                transport.Stop();
                return;
            }

            pacer = new TickPacer(state.Settings.TicksPerSecond, MaxCatchUpTicks);
            trail = new PlayerTrail(state.Players.Count, TrailLength);
            clock = new InterpolationClock(InterpolationDelayTicks, InterpolationSnapTicks);
            transport.Adopt(state);

            // After Adopt, because the prediction runs on a copy of the board the server
            // described, and there is no board at all before the seat lands. Nothing has
            // been applied yet at this point, so there is nothing to reconcile against.
            prediction = new ClientPrediction(Blank(state), seat);
            smoother.Clear();

            if (view != null)
            {
                view.UseTheme(themes != null && themes.Length > 0 ? themes[0] : null);
                view.Bind(state);
                view.Interpolate(trail, clock, seat, OwnPosition);
            }

            if (matchCamera != null)
            {
                matchCamera.Bind(prediction.State, 1, seat);
            }

            if (hud != null)
            {
                hud.Bind(state, matchCamera, null, _ => devices.KindFor(0));
            }

            if (fog != null)
            {
                fog.Bind(state);
            }

            bound = true;
        }

        private void OnDestroy()
        {
            if (transport != null)
            {
                transport.SeatTaken -= OnSeated;
                transport.SnapshotApplied -= OnSnapshot;
            }

            if (devices != null)
            {
                devices.Dispose();
            }
        }

        private void Update()
        {
            if (transport == null)
            {
                return;
            }

            // There is no board before the seat lands, so this draws nothing and exists
            // for the complaint. A server with every seat taken disconnects the caller,
            // and the only line explaining that is on the other machine, so a black
            // window here is the whole of what the player is told.
            if (!bound)
            {
                Render();
                Complain();
                return;
            }

            // After the seat, because it reads the settings of a match that does not
            // exist before one.
            clock.Advance(Mathf.RoundToInt(Time.deltaTime * state.Settings.TicksPerSecond * InterpolationClock.UnitsPerTick));

            devices.PollPresses();

            // Stamped against the server's clock rather than a count of its own. The two
            // do not start together, and a client numbering its own ticks from zero
            // would send inputs the server discards as answers to ticks it played long
            // ago: every player frozen, with nothing anywhere reporting an error.
            //
            // Nothing is sent before the first snapshot, because until one lands there
            // is no clock to answer.
            int ticks = pacer.Advance(Time.deltaTime);
            int seen = transport.LastSnapshotTick;

            if (seen < 0)
            {
                Render();
                return;
            }

            // Pulled forward whenever the server has caught up with where this client
            // was aiming, which is also how it recovers after a stall rather than
            // spending the rest of the match behind.
            if (nextTick <= seen)
            {
                nextTick = seen + LeadTicks;
            }

            for (int i = 0; i < ticks; i++)
            {
                PlayerInput input = devices.Sample(0);
                transport.SendInput(nextTick, input);
                prediction.Step(nextTick, input);
                smoother.Tick();
                nextTick++;
            }

            Render();
        }

        // Said once, after long enough that a slow connection has had its chance. The
        // cases that reach it are a full match and a server too old to send a seat at
        // all, and both look identical from here: connected, and nothing happening.
        private void Complain()
        {
            waited += Time.deltaTime;

            if (waited < SecondsBeforeComplaining || complained)
            {
                return;
            }

            complained = true;
            Debug.LogError(
                "Blastlands: connected but never seated. The match is either full or the server is older "
                + "than the seat message. Nothing will move until a seat arrives.");
        }

        private void Render()
        {
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
