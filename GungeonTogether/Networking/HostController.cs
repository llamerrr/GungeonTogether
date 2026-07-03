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
            _p2p.OnP2PSessionFailed += HandleP2PSessionFailed;
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
                _p2p.OnP2PSessionFailed -= HandleP2PSessionFailed;
            }

            foreach (var client in _connectedClients)
            {
                // Send disconnect packet
            }
            _connectedClients.Clear();
        }

        private void HandleP2PSessionFailed(ulong clientId)
        {
            HandleClientDisconnect(clientId);
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
            HandleJoinRequest(playerId, NetworkManager.ProtocolVersion);
        }

        public void HandleJoinRequest(ulong playerId, int protocolVersion)
        {
            if (protocolVersion != NetworkManager.ProtocolVersion)
            {
                // Send rejection
                var reject = new ConnectionRejectedPacket { ProtocolVersion = NetworkManager.ProtocolVersion };
                SendPacket(playerId, reject, reliable: true);
                Debug.Log($"Rejected connection from {playerId} – protocol mismatch (got {protocolVersion}, expected {NetworkManager.ProtocolVersion})");
                return;
            }

            if (!_connectedClients.Contains(playerId))
            {
                _connectedClients.Add(playerId);
                Debug.Log($"Player {playerId} joined the session.");
                // send accept packet
                var accept = new ConnectionAcceptedPacket
                {
                    HostId = SteamReflectionHelper.GetLocalSteamId(),
                    ProtocolVersion = NetworkManager.ProtocolVersion
                };
                SendPacket(playerId, accept, reliable: true);
                // send initial state..

                // Spawn for host's own view
                Vector2 spawnPos = new Vector2(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-2f, 2f)); // or get from game
                PlayerManager.Instance.SpawnRemotePlayer(playerId, spawnPos, 0f);

                // Broadcast to all clients (including the new one)
                var joinPacket = new PlayerJoinPacket
                {
                    PlayerId = playerId,
                    Position = spawnPos,
                    Rotation = 0f
                };
                Broadcast(joinPacket, excludeId: 0, reliable: true);
            }
            // Send current world state to the new client
            var gm = ETGReflectionHelper.GetGameManager();
            if (gm != null)
            {
                bool isFoyer = ETGReflectionHelper.IsInFoyer();
                int floorIndex = ETGReflectionHelper.GetCurrentFloorIndex();
                string roomId = ETGReflectionHelper.GetCurrentRoomIdentifier();
                Vector3 pos = ETGReflectionHelper.GetPlayerPosition();
                float rot = ETGReflectionHelper.GetPlayerRotation();

                var packet = new WorldStatePacket
                {
                    IsFoyer = isFoyer,
                    FloorIndex = floorIndex,
                    RoomIdentifier = roomId,
                    Position = new Vector2(pos.x, pos.y),
                    Rotation = rot
                };
                SendPacket(playerId, packet, reliable: true);
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
        public void HandleClientDisconnect(ulong clientId)
        {
            if (_connectedClients.Remove(clientId))
            {
                Debug.Log($"Client {clientId} disconnected.");
            }
        }
    }

}
