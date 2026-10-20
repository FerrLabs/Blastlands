using System;
using System.Net;
using System.Net.Sockets;
using Blastlands.Core.Lobby;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class ProbeListener : IDisposable
    {
        private readonly int port;
        private UdpClient socket;
        private bool warned;

        public ProbeListener(int slotPort)
        {
            port = slotPort;
        }

        public string Nonce { get; private set; }

        public void Listen()
        {
            if (socket != null)
            {
                return;
            }

            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                warned = false;
            }
            catch (SocketException failed)
            {
                if (!warned)
                {
                    Debug.LogWarning($"Blastlands server: cannot listen for the lobby's probe on UDP {port}, {failed.Message}");
                    warned = true;
                }
            }
        }

        public void Drain()
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                while (socket.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] datagram = socket.Receive(ref from);
                    if (SlotProbe.TryRead(datagram, datagram.Length, out string nonce))
                    {
                        Nonce = nonce;
                    }
                }
            }
            catch (SocketException failed)
            {
                Debug.LogWarning($"Blastlands server: reading UDP {port} failed, {failed.Message}");
                Dispose();
            }
        }

        public void Dispose()
        {
            socket?.Close();
            socket = null;
        }
    }
}
