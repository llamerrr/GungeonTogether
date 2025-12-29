using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Networking.Packets;
using GungeonTogether.Systems.Logging;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking
{
    public class HostController : IHost
    {
        private List<ulong> _connectedClients = new List<ulong>();
        private SteamP2PManager _p2p;

        public void Initialise()
        {
            _p2p = SteamP2PManager.Instance;
            Debug.Log("HostController Initialised.");
        }

        public void StartSession()
        {
            // Initialise game state for hosting
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

                // Ensure Steam will accept the P2P session.
                try
                {
                    if (SteamReflectionHelper.AcceptP2PSessionMethod != null)
                    {
                        object steamIdObj = SteamReflectionHelper.CreateCSteamID(playerId);
                        SteamReflectionHelper.AcceptP2PSessionMethod.Invoke(null, new object[] { steamIdObj });
                        Debug.Log($"[Host] Accepted P2P session with {playerId}.");
                    }
                }
                catch { }
                
                // Send accept packet
                // Send initial state
            }
        }

        public void HandlePlayerPosition(ulong senderId, PlayerPositionPacket packet)
        {
            if (!_connectedClients.Contains(senderId))
            {
                // If packets arrive before our ConnectionRequest route, still accept/log.
                HandleJoinRequest(senderId);
            }

            Debug.Log($"[Host] Position from {senderId}: ({packet.Position.x:0.00}, {packet.Position.y:0.00})");
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
