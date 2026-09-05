#if UNITY_SERVER
using Blastlands.Core;
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
        private string ending;
        private int endingCode;
        private int ticksLeft;

        public void Run(MatchState matchState, ServerOptions options)
        {
            state = matchState;
            inputs = new PlayerInput[state.Players.Count];
            pacer = new TickPacer(state.Settings.TicksPerSecond, MaxCatchUpTicks);

            // A ceiling on the whole match, not a per-player timeout, which is #17's
            // job. Without one an instance whose sudden death is switched off ticks an
            // idle match until somebody notices the host is busy.
            ticksLeft = state.Settings.TicksPerSecond * MaxMatchSeconds;

            // The tick rate is the frame rate here. A server with nothing to draw will
            // otherwise run the loop as fast as the machine allows and bill a whole core
            // for the privilege of waiting.
            Application.targetFrameRate = state.Settings.TicksPerSecond;
            QualitySettings.vSyncCount = 0;

            // Listening before the first tick, so a client that connects the moment the
            // lobby hands out the endpoint is not refused while the process finishes
            // waking up. A match whose transport never came up still runs and still
            // reports: every seat sends nothing, the match times out, and the reason
            // reaches the lobby instead of the port being stranded by a hard exit.
            transport = gameObject.AddComponent<MatchTransport>();
            if (!transport.StartServer((ushort)options.ListenPort, state.Players.Count))
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

            int ticks = pacer.Advance(Time.deltaTime);

            for (int i = 0; i < ticks; i++)
            {
                // Asked for before the tick rather than after it: MatchSim advances the
                // counter on its way out, so state.Tick here is the tick about to be
                // played and the one the client stamped its input with.
                //
                // A seat nobody is connected to, or one whose packet has not arrived,
                // comes back as a player standing still or as whatever they last sent.
                // Either is a match that keeps running, which is the point.
                MatchSim.Tick(state, transport != null ? transport.InputsFor(state.Tick) : inputs);
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
