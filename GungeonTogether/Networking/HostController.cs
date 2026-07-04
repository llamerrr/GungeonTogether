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
        private float _nextPositionSendTime;
        private const float PositionSendInterval = 0.25f;

        public void Initialise()
        {
            _p2p = SteamP2PManager.Instance;
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
            if (_connectedClients.Count == 0) return;
            if (Time.realtimeSinceStartup < _nextPositionSendTime) return;

            _nextPositionSendTime = Time.realtimeSinceStartup + PositionSendInterval;
            var packet = PlayerManager.Instance.CreateLocalPositionPacket(_p2p.LocalSteamID);
            if (packet != null)
            {
                Broadcast(packet, reliable: false);
            }
        }

        public void Shutdown()
        {
            if (_p2p != null)
            {
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
            Debug.Log($"[Host] HandleJoinRequest from {playerId}, version={protocolVersion}, expected={NetworkManager.ProtocolVersion}");

            if (protocolVersion != NetworkManager.ProtocolVersion)
            {
                var reject = new ConnectionRejectedPacket { ProtocolVersion = NetworkManager.ProtocolVersion };
                SendPacket(playerId, reject, reliable: true);
                Debug.Log($"[Host] Rejected {playerId} – protocol mismatch");
                return;
            }

            if (_connectedClients.Contains(playerId))
            {
                Debug.Log($"[Host] Player {playerId} already connected");
                return;
            }

            _connectedClients.Add(playerId);
            Debug.Log($"[Host] Player {playerId} added to connected clients (total {_connectedClients.Count})");

            // Accept the P2P session (already accepted via callback, but ensure it)
            AcceptP2PSession(playerId);

            // Send ConnectionAcceptedPacket
            var accept = new ConnectionAcceptedPacket
            {
                HostId = SteamReflectionHelper.GetLocalSteamId(),
                ProtocolVersion = NetworkManager.ProtocolVersion
            };
            SendPacket(playerId, accept, reliable: true);

            // Now broadcast current world state to the new client
            SendCurrentWorldState(playerId);
        }

        private void SendCurrentWorldState(ulong targetId)
        {
            var gm = ETGReflectionHelper.GetGameManager();
            if (gm == null) return;

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
            SendPacket(targetId, packet, reliable: true);
            Debug.Log($"[Host] Sent initial world state to {targetId}: isFoyer={isFoyer}, floor={floorIndex}");
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
            PlayerManager.Instance.UpdateRemotePlayer(senderId, packet.Position, packet.Rotation, packet.SpriteId, packet.FlipX);

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
            int count = 0;
            foreach (var client in _connectedClients)
            {
                if (client != excludeId)
                {
                    _p2p.SendPacket(client, data, reliable);
                    count++;
                }
            }
            Debug.Log($"[Host] Broadcasted {packet.Type} to {count} clients (excluded {excludeId})");
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
