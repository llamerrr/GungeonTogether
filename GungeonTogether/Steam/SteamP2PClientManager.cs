using System;
using UnityEngine;

namespace GungeonTogether.Steam
{
    public class SteamP2PClientManager
    {
        private int _virtualPort = 1;
        private ulong _hostSteamId;
        private ulong _clientSteamId;
        private bool _isConnected;

        // Heartbeat timing
        private float _lastHeartbeatTime = 0f;
        private float _lastHeartbeatLogTime = 0f;
        private const float HEARTBEAT_INTERVAL = 2.0f; // Send heartbeat every 2 seconds

        public SteamP2PClientManager(ulong hostSteamId, ulong clientSteamId)
        {
            _hostSteamId = hostSteamId;
            _clientSteamId = clientSteamId;
            GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager] Created client manager for host {hostSteamId}, client {clientSteamId}");
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
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][JOINER] Attempting to send {data?.Length ?? 0} bytes to host {_hostSteamId}");
                bool sent = SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, data);
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][JOINER] SendToHost(byte[]): sent={sent}, bytes={data?.Length ?? 0}");
                if (!sent)
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamP2PClientManager][JOINER] Failed to send packet to host");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager][JOINER] Send packet error: {e.Message}");
            }
        }

        public void SendToHost(NetworkPacket packet)
        {
            if (!_isConnected) return;

            try
            {
                var serializedPacket = PacketSerializer.SerializePacket(packet);
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][JOINER] SendToHost(NetworkPacket): type={packet.Type}, bytes={(serializedPacket != null ? serializedPacket.Length : 0)}");
                if (serializedPacket != null)
                {
                    SendToHost(serializedPacket);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager][JOINER] Send packet error: {e.Message}");
            }
        }

        public void Update()
        {
            if (!_isConnected) return;

            try
            {
                // Send periodic heartbeat to host
                if (UnityEngine.Time.time - _lastHeartbeatTime >= HEARTBEAT_INTERVAL)
                {
                    SendHeartbeat();
                    _lastHeartbeatTime = UnityEngine.Time.time;
                }

                // Use the new efficient packet polling method
                var packets = SteamNetworkingSocketsHelper.PollIncomingPackets();

                foreach (var packet in packets)
                {
                    if (packet.data.Length > 0 && packet.senderSteamId.Equals(_hostSteamId))
                    {
                        ProcessReceivedPacket(packet.data, packet.senderSteamId);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Update error: {e.Message}");
            }
        }

        private void ProcessReceivedPacket(byte[] data, ulong senderSteamId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][JOINER] Received packet from {senderSteamId}, bytes={data?.Length ?? 0}");
                // Check if it's a special message
                string message = System.Text.Encoding.UTF8.GetString(data);

                if (message.StartsWith("HEARTBEAT:"))
                {
                    // Respond to heartbeat to keep connection alive
                    byte[] responseData = System.Text.Encoding.UTF8.GetBytes($"HEARTBEAT_ACK:{_clientSteamId}");
                    SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, responseData);
                    GungeonTogether.Logging.Debug.Log("[SteamP2PClientManager] Responded to host heartbeat");
                }
                else if (message.StartsWith("WELCOME_TO_HOST:") || message.StartsWith("WELCOME_RESPONSE:"))
                {
                    // Host welcomed us, acknowledge and notify
                    byte[] ackData = System.Text.Encoding.UTF8.GetBytes($"WELCOME_ACK:{_clientSteamId}");
                    SteamNetworkingSocketsHelper.SendP2PPacket(_hostSteamId, ackData);
                    GungeonTogether.Logging.Debug.Log("[SteamP2PClientManager] Acknowledged host welcome");

                    // Notify NetworkManager that we've connected to host
                    if (!NetworkManager.Instance.IsHost())
                    {
                        NetworkManager.Instance.HandlePlayerJoin(_hostSteamId);
                    }
                }
                else
                {
                    // Try to deserialize as network packet
                    var packet = PacketSerializer.DeserializePacket(data);
                    if (packet.HasValue)
                    {
                        NetworkManager.Instance.QueueIncomingPacket(packet.Value);
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager] Processed network packet: {packet.Value.Type} from host");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[SteamP2PClientManager] Failed to deserialize packet from host");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Error processing packet from host: {e.Message}");
            }
        }

        private void SendPlayerJoinPacket()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][DEBUG] SendPlayerJoinPacket called - Client SteamId: {_clientSteamId}, Host SteamId: {_hostSteamId}");

                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var localPlayer = GameManager.Instance?.PrimaryPlayer;
                var currentPosition = localPlayer != null ? (Vector2)localPlayer.transform.position : Vector2.zero;

                var joinData = new PlayerJoinData
                {
                    PlayerId = _clientSteamId,
                    PlayerName = $"Player_{_clientSteamId}",
                    Position = currentPosition,
                    MapName = currentScene
                };

                var packet = new NetworkPacket(PacketType.PlayerJoin, _clientSteamId, PacketSerializer.SerializeObject(joinData));
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][DEBUG] Created PlayerJoin packet with proper data, about to send to host");
                SendToHost(packet);
                GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager][DEBUG] PlayerJoin packet sent successfully");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] Join packet error: {e.Message}");
            }
        }

        private void SendHeartbeat()
        {
            try
            {
                // Send heartbeat packet to host
                var heartbeatPacket = new NetworkPacket
                {
                    Type = PacketType.HeartBeat,
                    SenderId = _clientSteamId,
                    Data = new byte[0],
                    Timestamp = UnityEngine.Time.time
                };

                SendToHost(heartbeatPacket);

                // More reliable logging - every 5 seconds
                if (UnityEngine.Time.time - _lastHeartbeatLogTime > 5.0f)
                {
                    GungeonTogether.Logging.Debug.Log($"[SteamP2PClientManager] Sent heartbeat to host {_hostSteamId} at {UnityEngine.Time.time:F1}s");
                    _lastHeartbeatLogTime = UnityEngine.Time.time;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PClientManager] SendHeartbeat error: {e.Message}");
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
