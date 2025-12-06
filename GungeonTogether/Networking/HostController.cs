using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Systems.Logging;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking
{
    public class HostController : IHost
    {
        private List<ulong> _connectedClients = new List<ulong>();
        private SteamP2PManager _p2p;

        public void Initialize()
        {
            _p2p = SteamP2PManager.Instance;
            Debug.Log("HostController Initialized.");
        }

        public void StartSession()
        {
            // Initialize game state for hosting
            Debug.Log("Session Started.");
        }

        public void Update()
        {
            // Host specific logic (e.g. enemy spawning management)
        }

        public void Shutdown()
        {
            foreach (var client in _connectedClients)
            {
                // Send disconnect packet
            }
            _connectedClients.Clear();
        }

        public void HandleJoinRequest(ulong playerId)
        {
            if (!_connectedClients.Contains(playerId))
            {
                _connectedClients.Add(playerId);
                Debug.Log($"Player {playerId} joined the session.");
                
                // Send accept packet
                // Send initial state
            }
        }

        public void SendPacket(ulong targetId, INetworkPacket packet, bool reliable = true)
        {
            byte[] data = Serialization.PacketSerializer.Serialize(packet);
            _p2p.SendPacket(targetId, data, reliable);
        }

        public void Broadcast(INetworkPacket packet, ulong excludeId = 0, bool reliable = true)
        {
            byte[] data = Serialization.PacketSerializer.Serialize(packet);
            foreach (var client in _connectedClients)
            {
                if (client != excludeId)
                {
                    _p2p.SendPacket(client, data, reliable);
                }
            }
        }
    }
}
