#if UNITY_SERVER
using Blastlands.Core;
using Blastlands.Core.Net;
using UnityEngine;

namespace Blastlands.Runtime
{
    // Ticks the match and nothing else. No view, no HUD, no camera, no audio: none of
    // them is ever bound here, and LocalMatchDriver switches itself off in a server
    // build, so the scene that ships with the client sits inert behind this.
    public sealed class ServerLoop : MonoBehaviour
    {
        // Enough that a stall does not spiral, few enough that catching up cannot itself
        // eat a frame. Same figure the local driver uses.
        private const int MaxCatchUpTicks = 5;

        private MatchState state;
        private PlayerInput[] inputs;
        private TickPacer pacer;
        private LobbyReporter lobby;
        private MatchTransport transport;
        private StandIns standIns;
        private string ending;
        private int endingCode;
        private int ticksLeft;
        private bool started;
        private float waited;

        public void Run(MatchState matchState, ServerOptions options, GameTicketVerifier tickets)
        {
            state = matchState;
            inputs = new PlayerInput[state.Players.Count];
            standIns = new StandIns(state.Players.Count, BotSettings.Normal);
            pacer = new TickPacer(state.Settings.TicksPerSecond, MaxCatchUpTicks);

            // A ceiling on the match, and deliberately not on the wait before it: the
            // counter only moves once the gate below lets the first tick through. So an
            // instance holds its port for at most PatienceSeconds + MaxMatchSeconds
            // rather than MaxMatchSeconds, which is 45s more in the worst case against
            // the ten minutes this reclaims from a match nobody joined.
            ticksLeft = state.Settings.TicksPerSecond * MaxMatchSeconds;

            // The tick rate is the frame rate here. A server with nothing to draw will
            // otherwise run the loop as fast as the machine allows and bill a whole core
            // for the privilege of waiting.
            Application.targetFrameRate = state.Settings.TicksPerSecond;
            QualitySettings.vSyncCount = 0;

            // Listening before the first tick, so a client that connects the moment the
            // lobby hands out the endpoint is not refused while the process finishes
            // waking up. A match whose transport never came up never starts: nobody can
            // reach it, so the gate below gives up once its patience runs out and the
            // reason reaches the lobby instead of the port being stranded by a hard exit.
            transport = gameObject.AddComponent<MatchTransport>();
            if (!transport.StartServer((ushort)options.ListenPort, state.Players.Count, tickets))
            {
                transport = null;
            }

            // Started before the first tick, not after the last. The lobby is already
            // counting from the moment it allocated this match, so an instance that
            // waited until it had something to report would be reaped on its way up.
            lobby = gameObject.AddComponent<LobbyReporter>();
            lobby.Configure(options);
        }

        private const int MaxMatchSeconds = 600;

        // How long an instance waits for its seats to fill before it decides nobody
        // else is coming. Long enough for a slow load and a download of the arena,
        // short enough that a match nobody joined does not hold its port for minutes.
        private const float PatienceSeconds = 45f;

        private void Update()
        {
            // The frame after the one that decided it. Everything queued on the way out,
            // the result message included, gets a network update to leave on before the
            // socket closes and the process quits.
            if (ending != null)
            {
                string reason = ending;
                ending = null;
                Finish(reason, endingCode);
                return;
            }

            if (state == null)
            {
                return;
            }

            if (!started && !MayStart())
            {
                return;
            }

            int ticks = pacer.Advance(Time.deltaTime);

            for (int i = 0; i < ticks; i++)
            {
                // Asked for before the tick rather than after it: MatchSim advances the
                // counter on its way out, so state.Tick here is the tick about to be
                // played and the one the client stamped its input with.
                //
                // A seat whose packet has not arrived comes back as whatever it last
                // sent, and a seat nobody is connected to is played by a bot until its
                // player reconnects. Either is a match that keeps running, which is the
                // point.
                PlayerInput[] tickInputs = inputs;
                if (transport != null)
                {
                    tickInputs = transport.InputsFor(state.Tick);
                    standIns.Fill(state, tickInputs, transport.IsSeated);
                }

                MatchSim.Tick(state, tickInputs);
                ticksLeft--;

                // After the tick, so what goes out is the state the inputs produced
                // rather than the one they were about to change.
                if (transport != null)
                {
                    transport.Broadcast(state);
                }

                if (state.Outcome != RoundOutcome.Running)
                {
                    // Sent reliably and on its own, rather than trusted to the outcome
                    // byte of an unreliable snapshot. That snapshot is the only one that
                    // ever carries the result, and unreliable delivery is exactly what
                    // you do not want for a message that is sent once.
                    //
                    // Torn down a frame later rather than here: NGO flushes its send
                    // queue on a later network update, so shutting the socket inside the
                    // tick that produced the result can take that result with it.
                    if (transport != null)
                    {
                        transport.AnnounceResult(state);
                    }

                    ending = $"match {state.Outcome} after {state.Tick} ticks";
                    endingCode = ServerBootstrap.Ok;
                    return;
                }

                if (ticksLeft <= 0)
                {
                    // Non-zero: a match that ran out the clock without resolving is an
                    // instance that has to be looked at, not one that finished.
                    ending = $"match ran {MaxMatchSeconds}s without resolving";
                    endingCode = ServerBootstrap.FailedToStart;
                    return;
                }
            }
        }

        // Whether the match may begin. Ticking an arena whose seats are empty is worse
        // than waiting: the players who did connect watch a match they cannot win, and
        // the ones who have not arrived yet find it already under way.
        private bool MayStart()
        {
            waited += Time.deltaTime;
            int connected = transport != null ? transport.Occupied : 0;

            switch (KickoffDecision.For(state.Players.Count, connected, waited, PatienceSeconds))
            {
                case Kickoff.Play:
                    started = true;
                    Debug.Log(
                        $"Blastlands server: starting with {connected} of "
                        + $"{state.Players.Count} seats after {waited:F1}s");
                    return true;

                case Kickoff.GiveUp:
                    // A transport that never came up is not the same story: nobody
                    // could have joined, and the exit code is the machine-readable half
                    // of that. Reporting Ok would file a bind failure as a success.
                    if (transport == null)
                    {
                        ending = "the transport never came up, so nobody could join";
                        endingCode = ServerBootstrap.FailedToStart;
                        return false;
                    }

                    // Ok rather than a failure code: an instance nobody joined did its
                    // job. Treating it as a crash would put the pod into a restart
                    // backoff for something that is going to happen on a quiet evening.
                    ending = $"nobody joined within {PatienceSeconds:F0}s, releasing the match";
                    endingCode = ServerBootstrap.Ok;
                    return false;

                default:
                    return false;
            }
        }

        // Quitting is the last step rather than the first. Application.Quit does not
        // wait for anything in flight, so releasing after asking to quit would race the
        // process out from under its own DELETE and strand the port.
        private void Finish(string reason, int code)
        {
            state = null;
            Debug.Log("Blastlands server: " + reason);

            if (transport != null)
            {
                transport.Stop();
                transport = null;
            }

            if (lobby == null)
            {
                Application.Quit(code);
                return;
            }

            lobby.Release(() => Application.Quit(code));
        }
    }
}
#endif
