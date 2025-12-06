using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Systems.Logging;
using UnityEngine;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking
{
    public class ClientController : IClient
    {
        private ulong _hostId;
        private SteamP2PManager _p2p;
        public bool IsConnected { get; private set; }

        public void Initialize()
        {
            _p2p = SteamP2PManager.Instance;
            Debug.Log("ClientController Initialized.");
        }

        public void Connect(ulong hostId)
        {
            _hostId = hostId;
            // Send connection request
            Debug.Log($"Sending connection request to {hostId}...");
            IsConnected = true; // Ideally wait for handshake
        }

        public void Update()
        {
            if (!IsConnected) return;
            
            // Client specific logic (e.g. sending input/position)
        }

        public void Shutdown()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                // Send disconnect packet
                IsConnected = false;
                Debug.Log("Disconnected from host.");
            }
        }

        public void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true)
        {
            // Clients usually only send to host
            if (targetId == 0) targetId = _hostId;
            
            byte[] data = Serialization.PacketSerializer.Serialize(packet);
            _p2p.SendPacket(targetId, data, reliable);
        }
    }
}
