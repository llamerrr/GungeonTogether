using System;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Steam
{
    public class SteamP2PClientManager
    {
        private int _virtualPort = 1;
        private ulong _hostSteamId;
        private ulong _clientSteamId;
        private bool _isConnected;

        public SteamP2PClientManager(ulong hostSteamId, ulong clientSteamId)
        {
            _hostSteamId = hostSteamId;
            _clientSteamId = clientSteamId;
            ConnectToHost();
        }

        private void ConnectToHost()
        {
            try
            {
                // CRITICAL: For P2P to work, we need to SEND a packet to the host first
                // This will initiate the P2P session from our side
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager] Initiating P2P connection to host: {_hostSteamId}");
                
                // Send an initial packet to establish the P2P session
                byte[] handshakeData = System.Text.Encoding.UTF8.GetBytes($"CLIENT_HANDSHAKE:{_clientSteamId}");
                bool handshakeSent = SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, handshakeData);
                
                if (handshakeSent)
                {
                    // Now accept the P2P session
                    bool sessionAccepted = SteamNetworkingSocketsHelper.AcceptP2PSession(_hostSteamId);
                    if (sessionAccepted)
                    {
                        _isConnected = true;
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager] Successfully connected to host: {_hostSteamId}");
                        
                        // Send join packet
                        SendPlayerJoinPacket();
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Handshake sent but failed to accept P2P session with host: {_hostSteamId}");
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Failed to send handshake to host: {_hostSteamId}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Connection error: {e.Message}");
            }
        }

        public void SendToHost(byte[] data)
        {
            if (!_isConnected) return;

            try
            {
                bool sent = SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, data);
                if (!sent)
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamP2PClientManager] Failed to send packet to host");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Send packet error: {e.Message}");
            }
        }
        
        public void SendToHost(NetworkPacket packet)
        {
            if (!_isConnected) return;

            try
            {
                var serializedPacket = PacketSerializer.SerializePacket(packet);
                if (serializedPacket != null)
                {
                    SendToHost(serializedPacket);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Send packet error: {e.Message}");
            }
        }

        public void Update()
        {
            if (!_isConnected) return;

            try
            {
                // Check for incoming packets from host
                while (SteamNetworkingSocketsHelper.IsP2PPacketAvailable())
                {
                    var packetData = SteamNetworkingSocketsHelper.ReadP2PPacket();
                    if (packetData != null)
                    {
                        // Check if it's a heartbeat message
                        string message = System.Text.Encoding.UTF8.GetString(packetData);
                        if (message.StartsWith("HEARTBEAT:"))
                        {
                            // Respond to heartbeat to keep connection alive
                            byte[] responseData = System.Text.Encoding.UTF8.GetBytes($"HEARTBEAT_ACK:{_clientSteamId}");
                            SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, responseData);
                            GungeonTogether.Logging.Debug.Log("[SteamP2PClientManager] Responded to host heartbeat");
                        }
                        else if (message.StartsWith("WELCOME_TO_HOST:"))
                        {
                            // Host welcomed us, acknowledge
                            byte[] ackData = System.Text.Encoding.UTF8.GetBytes($"WELCOME_ACK:{_clientSteamId}");
                            SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, ackData);
                            GungeonTogether.Logging.Debug.Log("[SteamP2PClientManager] Acknowledged host welcome");
                        }
                        else
                        {
                            // Try to deserialize as network packet
                            var packet = PacketSerializer.DeserializePacket(packetData);
                            if (packet.HasValue)
                            {
                                NetworkManager.Instance.QueueIncomingPacket(packet.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Update error: {e.Message}");
            }
        }

        private void SendPlayerJoinPacket()
        {
            try
            {
                var joinData = new PlayerPositionData
                {
                    PlayerId = _clientSteamId,
                    Position = Vector2.zero,
                    Velocity = Vector2.zero,
                    Rotation = 0f,
                    IsGrounded = true,
                    IsDodgeRolling = false
                };
                
                var packet = new NetworkPacket(PacketType.PlayerJoin, _clientSteamId, PacketSerializer.SerializeObject(joinData));
                SendToHost(packet);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Join packet error: {e.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_isConnected)
                {
                    SteamNetworkingSocketsHelper.CloseP2PSession(_hostSteamId);
                    _isConnected = false;
                    GungeonTogether.Logging.Debug.Log("[SteamP2PClientManager] Disconnected from host");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Disconnect error: {e.Message}");
            }
        }

        public bool IsConnected => _isConnected;
    }
}
