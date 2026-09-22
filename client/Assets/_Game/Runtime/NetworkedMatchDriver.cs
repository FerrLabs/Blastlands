using Blastlands.Core;
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
    // Where it stops short is the join. There is no lobby screen yet (#19), so the
    // endpoint and the shape of the match are serialized here instead of being learned
    // from the lobby, and the arena has to be told the same size the server built. That
    // is a development harness, not the finished flow.
    public sealed class NetworkedMatchDriver : MonoBehaviour
    {
        private const int MaxCatchUpTicks = 5;

        // How far ahead of the last tick it has seen a client answers. The packet has to
        // cross the wire and land before the server plays that tick, so answering the
        // tick it just watched would arrive for one already gone. Two ticks is 66ms at
        // thirty, which covers a round trip on a local network and is the number to
        // raise when it does not.
        private const int LeadTicks = 2;

        [SerializeField] private MatchView view;
        [SerializeField] private MatchHud hud;
        [SerializeField] private MatchCamera matchCamera;
        [SerializeField] private MatchFog fog;
        [SerializeField] private ArenaTheme[] themes;

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7777;
        [SerializeField] private string ticket = "";

        // Has to match what the server built, because the snapshot refuses a board of a
        // different size rather than writing tiles into the wrong rows. Until #19 that
        // agreement is by hand.
        [SerializeField] private int playerCount = 4;
        [SerializeField] private int arenaWidth = 15;
        [SerializeField] private int arenaHeight = 13;
        [SerializeField] private GameMode mode = GameMode.Arena;

        private MatchState state;
        private MatchTransport transport;
        private PlayerDevices devices;
        private TickPacer pacer;
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
            ArenaSettings arenaSettings = mode == GameMode.Arena
                ? new ArenaSettings(arenaWidth, arenaHeight, 60)
                : ArenaSettings.Classic;

            // The seed only decides the board this client draws before the first
            // snapshot lands, and every snapshot carries every tile, so it does not have
            // to match the server's. The size does.
            state = MatchFactory.Create(arenaSettings, MatchSettings.For(mode), playerCount, 1u);

            devices = new PlayerDevices(1);
            pacer = new TickPacer(state.Settings.TicksPerSecond, MaxCatchUpTicks);

            transport = gameObject.AddComponent<MatchTransport>();
            if (!transport.StartClient(host, (ushort)port, state, ticket))
            {
                transport = null;
                return;
            }

            transport.SeatTaken += OnSeated;
        }

        // Bound only once the server has said which player this connection is. Binding
        // before then would point the camera at player zero, so a client seated anywhere
        // else would drive one character and watch another, which does not look like a
        // camera fault on screen: it looks like a game that ignores the controller.
        private void OnSeated(int seat)
        {
            if (view != null)
            {
                view.UseTheme(themes != null && themes.Length > 0 ? themes[0] : null);
                view.Bind(state);
            }

            if (matchCamera != null)
            {
                matchCamera.Bind(state, 1, seat);
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
            }
        }

        private void Update()
        {
            if (state == null || transport == null)
            {
                return;
            }

            // Drawn even before the seat lands, so a client waiting on one shows the
            // board rather than an empty screen. A server with every seat taken
            // disconnects the caller, and the only line explaining that is on the other
            // machine, so a black window here is the whole of what the player is told.
            if (!bound)
            {
                Render();
                Complain();
                return;
            }

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
                transport.SendInput(nextTick, devices.Sample(0));
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
