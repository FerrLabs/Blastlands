using System.IO;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using UnityEngine;

namespace Blastlands.Runtime
{
    // One process, several matches. Each slot owns a port for the life of the process,
    // asks the lobby what to play on it, plays it, and asks again, so a process that
    // carries four slots stands in for four of the one-match processes this replaced
    // while loading the engine once.
    //
    // It only ever exits on purpose: when the supervisor says the pod is going away and
    // every slot has finished what it was playing. A match that fails is that match's
    // problem, released to the lobby and logged, and the other slots carry on.
    public sealed class ServerHost : MonoBehaviour
    {
        private const float DrainCheckSeconds = 1f;

        private HostOptions options;
        private LobbyRegistrar registrar;
        private MatchSlot[] slots;
        private float sinceDrainCheck;
        private bool draining;
        private bool quitting;

        public void Run(HostOptions hostOptions)
        {
            options = hostOptions;

            // The tick rate is the frame rate here, set once for every match the process
            // will run. A server with nothing to draw would otherwise run the loop as fast
            // as the machine allows, and an idle slot would bill a whole core for the
            // privilege of waiting.
            if (options.OwnsProcess)
            {
                Application.targetFrameRate = MatchSettings.Default.TicksPerSecond;
                QualitySettings.vSyncCount = 0;
            }

            Debug.Log(
                $"Blastlands server: {options.Slots} slots on ports {options.FirstPort} to {options.PortOf(options.Slots - 1)}, "
                + $"lobby {options.LobbyUrl}");

            registrar = gameObject.AddComponent<LobbyRegistrar>();
            registrar.Begin(options);

            slots = new MatchSlot[options.Slots];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject("Slot " + options.PortOf(i));
                slot.transform.SetParent(transform, false);
                slots[i] = slot.AddComponent<MatchSlot>();
                slots[i].Watch(options.PortOf(i), options, registrar);
            }
        }

        public ServerCredentials Credentials
        {
            get { return registrar.Credentials; }
        }

        public string Problem
        {
            get { return registrar.Problem; }
        }

        public bool SlotBusy
        {
            get { return slots[0].Busy; }
        }

        public float SecondsSinceSlotPolled
        {
            get { return slots[0].SecondsSincePoll; }
        }

        private void Update()
        {
            if (slots == null || quitting || !options.OwnsProcess)
            {
                return;
            }

            sinceDrainCheck += Time.unscaledDeltaTime;
            if (!draining && sinceDrainCheck >= DrainCheckSeconds)
            {
                sinceDrainCheck = 0f;
                if (options.DrainFile != null && File.Exists(options.DrainFile))
                {
                    StartDraining();
                }
            }

            if (draining && AllIdle())
            {
                quitting = true;
                Debug.Log("Blastlands server: drained, shutting down");
                Application.Quit(ServerExit.Ok);
            }
        }

        // Every slot stops asking for work at once, and the ones that are playing finish
        // first. A slot that has stopped polling also stops being offered by the lobby,
        // so nothing new lands here while the last matches play out.
        private void StartDraining()
        {
            draining = true;
            Debug.Log("Blastlands server: draining, no new match will be taken");

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Drain();
            }
        }

        private bool AllIdle()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Busy)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
