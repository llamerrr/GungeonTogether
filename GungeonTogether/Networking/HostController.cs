using System;
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
            _p2p.OnP2PSessionRequest += HandleP2PSessionRequest;
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
            if (_p2p != null)
            {
                _p2p.OnP2PSessionRequest -= HandleP2PSessionRequest;
            }

            foreach (var client in _connectedClients)
            {
                // Send disconnect packet
            }
            _connectedClients.Clear();
        }

        private void HandleP2PSessionRequest(ulong playerId)
        {
            Debug.Log($"[Host] Received P2P session request from {playerId}.");
            AcceptP2PSession(playerId);
        }

        private void AcceptP2PSession(ulong playerId)
        {
            try
            {
                if (SteamReflectionHelper.AcceptP2PSessionMethod != null)
                {
                    object steamIdObj = SteamReflectionHelper.CreateCSteamID(playerId);
                    SteamReflectionHelper.AcceptP2PSessionMethod.Invoke(null, new object[] { steamIdObj });
                    Debug.Log($"[Host] Accepted P2P session with {playerId}.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Host] Failed to accept P2P session with {playerId}: {e.Message}");
            }
        }

        public void HandleJoinRequest(ulong playerId)
        {
            if (!_connectedClients.Contains(playerId))
            {
                _connectedClients.Add(playerId);
                Debug.Log($"Player {playerId} joined the session.");

                // Ensure Steam will accept the P2P session even if the initial callback was missed.
                AcceptP2PSession(playerId);
                
                // Send accept packet
                // Send initial state
            }
        }

        public void HandlePlayerPosition(ulong senderId, PlayerPositionPacket packet)
        {
            if (!_connectedClients.Contains(senderId))
            {
                // Don't auto-join on a bare position packet - that bypasses the
                // ConnectionRequest/ConnectionAccepted handshake (and its protocol
                // version check). Drop it and wait for a real handshake instead.
                Debug.LogWarning($"[Host] Ignored position packet from unrecognised sender {senderId} (no active connection).");
                return;
            }

            Debug.Log($"[Host] Position from {senderId}: ({packet.Position.x:0.00}, {packet.Position.y:0.00})");

            // Relay this player's position to every other connected client.
            Broadcast(packet, senderId, reliable: false);
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
