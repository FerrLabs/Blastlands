using System;
using Blastlands.Core;
using Blastlands.Core.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using PlayerInput = Blastlands.Core.PlayerInput;

namespace Blastlands.Runtime
{
    // The only thing in the project that talks to a socket.
    //
    // Netcode for GameObjects is used for its transport and its named messages and for
    // nothing else: no NetworkBehaviour, no NetworkVariable, no NetworkTransform. Those
    // replicate objects, and the thing being replicated here is a deterministic
    // simulation whose whole state already fits in a message. Letting NGO own positions
    // would put a second, floating-point idea of where everybody is next to the fixed
    // point one that decides who dies.
    public sealed class MatchTransport : MonoBehaviour
    {
        public const string InputMessage = "blastlands.input";
        public const string SnapshotMessage = "blastlands.snapshot";

        // Sent once, on connect, reliably. A join-time constant does not belong in the
        // snapshot, which goes out thirty times a second and drops on purpose. Without
        // it a client drives the seat the server gave it and watches player zero.
        public const string SeatMessage = "blastlands.seat";

        // Sent once at the end, reliably. See AnnounceResult.
        public const string ResultMessage = "blastlands.result";

        // A seat whose inputs have all been refused for this long is a client the server
        // cannot hear, which is worth one line in a log. At thirty ticks it is two
        // seconds: long enough that a burst of loss says nothing, short enough to still
        // be looking at the match it describes.
        private const int RefusalsWorthSaying = 60;

        // Comfortably inside a datagram. Unreliable messages are not fragmented, so one
        // larger than this would simply not arrive; the sender falls back rather than
        // dropping the tick. Measured against real matches the snapshot peaks under a
        // kilobyte, so the fallback should be rare and its absence should be suspicious.
        public const int DatagramSafeSize = 1100;

        private readonly byte[] outgoing = new byte[SnapshotCodec.MaxSize];
        // Not readonly: FastBufferReader.ReadBytesSafe takes its target by ref, and a
        // readonly field cannot be passed that way. Neither is ever reassigned.
        private byte[] incoming = new byte[SnapshotCodec.MaxSize];
        private byte[] inputBytes = new byte[InputCodec.Size];

        private NetworkManager network;
        private GameObject networkHost;
        private SeatTable seats;
        private InputBuffer[] buffers;
        private PlayerInput[] applied;
        private MatchState clientState;
        private int lastSnapshotTick = -1;
        private int fallbacks;
        private bool complained;
        private bool finished;
        private int[] refusals;
        private bool[] toldAbout;

        // A snapshot shorter than its own tick field cannot even be looked at.
        private const int TickFieldSize = 4;

        public event Action<int> SeatTaken;
        public event Action<int> SeatLost;
        public event Action<RoundOutcome, int> MatchEnded;

        public bool Running
        {
            get { return network != null && (network.IsServer || network.IsClient); }
        }

        public int Seat { get; private set; } = SeatTable.NoSeat;

        // How many seats are actually occupied, for the server's wait before kickoff.
        // Read off the table rather than off NGO's connection list, because a connection
        // turned away for want of a seat is still connected for a moment.
        public int Occupied
        {
            get { return seats == null ? 0 : seats.Occupied; }
        }

        public int LastSnapshotTick
        {
            get { return lastSnapshotTick; }
        }

        // Built in code rather than dropped in a scene, for the reason ServerBootstrap
        // gives: a dedicated server has no scene to wire and no inspector to fill in.
        //
        // Left at the root of the scene rather than parented to this component, because
        // NGO refuses to run a nested NetworkManager: it logs
        // "NetworkManager cannot be nested" from OnEnable and never initialises, so
        // CustomMessagingManager stays null and every broadcast throws. Nothing about
        // that is visible without running the thing, which is how it survived a green
        // suite and a clean compile.
        private NetworkManager Build(string address, ushort port)
        {
            networkHost = new GameObject("Blastlands Network");

            NetworkManager manager = networkHost.AddComponent<NetworkManager>();
            UnityTransport transport = networkHost.AddComponent<UnityTransport>();

            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = false,
                EnableSceneManagement = false,
                TickRate = 30
            };

            transport.SetConnectionData(address, port, address);
            return manager;
        }

        public bool StartServer(ushort port, int expectedPlayers)
        {
            seats = new SeatTable(expectedPlayers);
            buffers = new InputBuffer[expectedPlayers];
            applied = new PlayerInput[expectedPlayers];
            refusals = new int[expectedPlayers];
            toldAbout = new bool[expectedPlayers];
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i] = new InputBuffer();
            }

            network = Build("0.0.0.0", port);
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;

            if (!network.StartServer())
            {
                Debug.LogError("Blastlands server: the transport refused to listen on port " + port);
                return false;
            }

            if (!Ready())
            {
                return false;
            }

            network.CustomMessagingManager.RegisterNamedMessageHandler(InputMessage, OnInputReceived);
            return true;
        }

        public bool StartClient(string host, ushort port, MatchState state)
        {
            clientState = state;
            network = Build(host, port);

            if (!network.StartClient())
            {
                Debug.LogError("Blastlands client: could not reach " + host + ":" + port);
                return false;
            }

            if (!Ready())
            {
                return false;
            }

            network.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshotReceived);
            network.CustomMessagingManager.RegisterNamedMessageHandler(SeatMessage, OnSeatReceived);
            network.CustomMessagingManager.RegisterNamedMessageHandler(ResultMessage, OnResultReceived);
            return true;
        }

        // Asked once, here, rather than discovered thirty times a second in Broadcast.
        // A NetworkManager that declined to come up leaves this null and every send
        // throws, which reads as a fault in the sending code rather than as a transport
        // that never started.
        private bool Ready()
        {
            if (network.CustomMessagingManager != null)
            {
                return true;
            }

            Debug.LogError(
                "Blastlands: the transport reported success but Netcode never initialised, so there is "
                + "nothing to send messages through. Check the Netcode errors above this line.");
            Stop();
            return false;
        }

        public void Stop()
        {
            if (network == null)
            {
                return;
            }

            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
            network.Shutdown();
            network = null;

            // Reset with the connection. Left behind, the old session's tick would make
            // the gate below drop every snapshot of the next match until it counted past
            // it, and the board would sit on the last frame of the previous one saying
            // nothing, because an older tick is the ordinary reordering path.
            if (networkHost != null)
            {
                Destroy(networkHost);
                networkHost = null;
            }

            clientState = null;
            lastSnapshotTick = -1;
            complained = false;
            finished = false;
            Seat = SeatTable.NoSeat;
        }

        private void OnDestroy()
        {
            Stop();
        }

        // Server. The inputs for one tick, one per seat, with an empty seat standing
        // still and a seat whose packet has not arrived repeating what it last sent.
        public PlayerInput[] InputsFor(int tick)
        {
            for (int seat = 0; seat < buffers.Length; seat++)
            {
                applied[seat] = buffers[seat].Take(tick);
            }

            return applied;
        }

        // Server. Unreliable, because the next snapshot supersedes this one and a
        // retransmitted one arrives already stale. Oversized ones go reliably and in
        // fragments rather than not at all: a client that stops hearing is a client
        // watching a frozen board.
        public void Broadcast(MatchState state)
        {
            if (network == null || !network.IsServer)
            {
                return;
            }

            // Nobody to tell. NGO logs "clientIds is empty" for a broadcast with no
            // recipients, and this runs thirty times a second, so an idle instance waiting
            // for its players fills a log at thirty lines a second until somebody joins.
            // Seen doing exactly that in a container.
            if (network.ConnectedClientsIds.Count == 0)
            {
                return;
            }

            int size = SnapshotCodec.Write(state, outgoing);
            if (size <= 0)
            {
                Debug.LogError("Blastlands server: the snapshot for tick " + state.Tick + " did not fit");
                return;
            }

            NetworkDelivery delivery = NetworkDelivery.Unreliable;
            if (size > DatagramSafeSize)
            {
                delivery = NetworkDelivery.ReliableFragmentedSequenced;
                fallbacks++;

                if (fallbacks == 1)
                {
                    Debug.LogWarning(
                        "Blastlands server: snapshots have outgrown a datagram (" + size
                        + " bytes) and are now going out reliably, which costs latency under loss.");
                }
            }

            var writer = new FastBufferWriter(size, Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteBytesSafe(outgoing, size);
                network.CustomMessagingManager.SendNamedMessageToAll(SnapshotMessage, writer, delivery);
            }
        }

        // Server. The one message that is sent once and cannot be resent, so it goes
        // reliably and separately from the snapshot stream. A client that misses the
        // outcome byte of the unreliable snapshot carrying it would sit on a finished
        // board waiting for a match that has already ended.
        public void AnnounceResult(MatchState state)
        {
            if (network == null || !network.IsServer)
            {
                return;
            }

            var writer = new FastBufferWriter(sizeof(int) * 2, Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe((int)state.Outcome);
                writer.WriteValueSafe(state.WinnerId);
                network.CustomMessagingManager.SendNamedMessageToAll(
                    ResultMessage, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        // Client. Unreliable and unsequenced: the tick number in the message is what
        // orders these, and the buffer at the far end is built to hold one that arrives
        // out of order rather than to have the transport throw it away.
        public void SendInput(int tick, PlayerInput input)
        {
            if (network == null || !network.IsClient || !InputCodec.TryWrite(inputBytes, 0, tick, input))
            {
                return;
            }

            var writer = new FastBufferWriter(InputCodec.Size, Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteBytesSafe(inputBytes, InputCodec.Size);
                network.CustomMessagingManager.SendNamedMessage(
                    InputMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
        }

        private void OnClientConnected(ulong connection)
        {
            if (network == null || !network.IsServer)
            {
                return;
            }

            int seat = seats.Claim(connection);
            if (seat == SeatTable.NoSeat)
            {
                Debug.Log("Blastlands server: turning away a connection, every seat is taken");
                network.DisconnectClient(connection);
                return;
            }

            Debug.Log("Blastlands server: seat " + seat + " taken");

            // Reliably and once. Losing it would leave that client watching somebody
            // else for the rest of the match with nothing to say why.
            var writer = new FastBufferWriter(sizeof(int), Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(seat);
                network.CustomMessagingManager.SendNamedMessage(
                    SeatMessage, connection, writer, NetworkDelivery.ReliableSequenced);
            }

            SeatTaken?.Invoke(seat);
        }

        private void OnClientDisconnected(ulong connection)
        {
            if (seats == null)
            {
                return;
            }

            int seat = seats.Release(connection);
            if (seat == SeatTable.NoSeat)
            {
                return;
            }

            Debug.Log("Blastlands server: seat " + seat + " left");
            SeatLost?.Invoke(seat);
        }

        // A packet from a connection with no seat is dropped rather than logged. It is
        // ordinary: a client that has just been turned away, or one whose disconnect
        // raced its last input.
        private void OnInputReceived(ulong sender, FastBufferReader payload)
        {
            int seat = seats == null ? SeatTable.NoSeat : seats.SeatOf(sender);
            if (seat == SeatTable.NoSeat || payload.Length < InputCodec.Size)
            {
                return;
            }

            payload.ReadBytesSafe(ref inputBytes, InputCodec.Size);

            if (!InputCodec.TryRead(inputBytes, 0, out int tick, out PlayerInput input))
            {
                return;
            }

            if (buffers[seat].Offer(tick, input))
            {
                refusals[seat] = 0;
                return;
            }

            // Counted rather than swallowed. One refusal is a packet that lost its race,
            // which is ordinary. A run of them is a client too far away for the lead it
            // is sending with, and the seat then repeats whatever last got through: a
            // player walking into a wall until the match ends, with the buffer working
            // exactly as specified and nothing anywhere saying so.
            refusals[seat]++;
            if (refusals[seat] >= RefusalsWorthSaying && !toldAbout[seat])
            {
                toldAbout[seat] = true;
                Debug.LogWarning(
                    "Blastlands server: seat " + seat + " has had " + refusals[seat]
                    + " inputs in a row arrive too late to play. That seat is now repeating its last "
                    + "input rather than answering. The client needs a longer lead than it is using.");
            }
        }

        // Older snapshots are dropped rather than applied. Unreliable delivery reorders,
        // and the oversized ones go down a different pipeline entirely, so a stale one
        // arriving after a fresh one is expected rather than exceptional. Applying it
        // would rewind the board a few ticks and look exactly like lag.
        private void OnSnapshotReceived(ulong sender, FastBufferReader payload)
        {
            if (clientState == null)
            {
                return;
            }

            // Measured from the current position: Length counts the named message's own
            // header, which the reader is already past. Asking for Length bytes would ask
            // for more than remain.
            int size = payload.Length - payload.Position;
            if (size < TickFieldSize || size > incoming.Length)
            {
                return;
            }

            payload.ReadBytesSafe(ref incoming, size);

            // Nothing after the result. Snapshots are unreliable and every one but the
            // last carries Running, so a reordered late arrival would put a finished
            // match back to Running with nothing further coming: the frozen finished
            // board by a narrower route than the one the result message closed.
            //
            // A latch rather than a test on state.Outcome, which a round reset will
            // change for reasons of its own.
            if (finished)
            {
                return;
            }

            int tick = PeekTick(incoming);
            if (tick <= lastSnapshotTick)
            {
                return;
            }

            // The length that arrived, not the size of the buffer. This buffer is reused,
            // so a short message sits in front of the tail of the last one, and a codec
            // bounded on the array would parse through the seam and apply a board that is
            // half this tick and half the one before it.
            if (SnapshotCodec.TryApply(incoming, size, clientState))
            {
                lastSnapshotTick = clientState.Tick;
                return;
            }

            // Only while nothing has ever been applied. On a live connection a refused
            // snapshot is an ordinary corrupt packet and logging each one would be spam;
            // before the first one lands it is almost always a client built for a board
            // the server did not, which otherwise looks like no connection at all: the
            // locally generated arena sitting perfectly still, with nothing logged.
            if (lastSnapshotTick < 0 && !complained)
            {
                complained = true;
                Debug.LogError(
                    "Blastlands client: every snapshot so far has been refused. The usual cause is a board "
                    + "that does not match the server's; this client was built for "
                    + clientState.Arena.Width + "x" + clientState.Arena.Height + " and "
                    + clientState.Players.Count + " players.");
            }
        }

        private void OnSeatReceived(ulong sender, FastBufferReader payload)
        {
            if (payload.Length - payload.Position < sizeof(int))
            {
                return;
            }

            payload.ReadValueSafe(out int seat);
            Seat = seat;
            Debug.Log("Blastlands client: seated at " + seat);
            SeatTaken?.Invoke(seat);
        }

        private void OnResultReceived(ulong sender, FastBufferReader payload)
        {
            if (clientState == null || payload.Length - payload.Position < sizeof(int) * 2)
            {
                return;
            }

            payload.ReadValueSafe(out int outcome);
            payload.ReadValueSafe(out int winner);

            if (outcome < 0 || outcome > SnapshotCodec.HighestOutcome)
            {
                return;
            }

            finished = true;
            clientState.Outcome = (RoundOutcome)outcome;
            clientState.WinnerId = winner;
            MatchEnded?.Invoke((RoundOutcome)outcome, winner);
        }

        private static int PeekTick(byte[] buffer)
        {
            return buffer[0]
                | (buffer[1] << 8)
                | (buffer[2] << 16)
                | (buffer[3] << 24);
        }
    }
}
