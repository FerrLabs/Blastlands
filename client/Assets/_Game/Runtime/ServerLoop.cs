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

            // Started before the first tick, not after the last. The lobby is already
            // counting from the moment it allocated this match, so an instance that
            // waited until it had something to report would be reaped on its way up.
            lobby = gameObject.AddComponent<LobbyReporter>();
            lobby.Configure(options);
        }

        private const int MaxMatchSeconds = 600;

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            int ticks = pacer.Advance(Time.deltaTime);

            for (int i = 0; i < ticks; i++)
            {
                // Every seat sends nothing until #14 wires clients up. An untouched
                // PlayerInput is a player standing still, which is what an unconnected
                // seat should look like.
                MatchSim.Tick(state, inputs);
                ticksLeft--;

                if (state.Outcome != RoundOutcome.Running)
                {
                    Finish($"match {state.Outcome} after {state.Tick} ticks", ServerBootstrap.Ok);
                    return;
                }

                if (ticksLeft <= 0)
                {
                    // Non-zero: a match that ran out the clock without resolving is an
                    // instance that has to be looked at, not one that finished.
                    Finish($"match ran {MaxMatchSeconds}s without resolving", ServerBootstrap.FailedToStart);
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
