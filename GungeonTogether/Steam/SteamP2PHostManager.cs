using System;
using System.Collections.Generic;
using System.Text;

namespace GungeonTogether.Steam
{
    public class SteamP2PHostManager
    {
        private readonly Dictionary<ulong, object> _clientConnections = new Dictionary<ulong, object>();
        private object _listenSocket;
        private int _virtualPort = 1;
        private ulong _lobbyId;
        private ulong _hostSteamId;

        private float _lastHeartbeatTime;
        private const float HEARTBEAT_INTERVAL = 2.0f; // Send heartbeat every 2 seconds

        public SteamP2PHostManager(ulong lobbyId, ulong hostSteamId)
        {
            _lobbyId = lobbyId;
            _hostSteamId = hostSteamId;
            // Subscribe to the P2P session request callback
            SteamNetworkingSocketsHelper.OnP2PSessionRequested += OnP2PSessionRequestedFromClient;
            StartListening();
        }

        private void OnP2PSessionRequestedFromClient(ulong clientSteamId)
        {
            // Only handle if not already connected and not self
            if (clientSteamId == _hostSteamId || _clientConnections.ContainsKey(clientSteamId))
                return;
            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] [P2P CALLBACK] Session accepted for client {clientSteamId}, sending welcome packet.");
            // Send welcome packet after session is accepted
            byte[] welcomeData = System.Text.Encoding.UTF8.GetBytes($"WELCOME_TO_HOST:{_hostSteamId}");
            bool sentWelcome = SteamNetworkingSocketsHelper.SendP2PPacket(clientSteamId, welcomeData);
            if (sentWelcome)
            {
                _clientConnections[clientSteamId] = clientSteamId;
                GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] [P2P CALLBACK] Welcome packet sent and client added: {clientSteamId}");
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamP2PHostManager] [P2P CALLBACK] Failed to send welcome packet to client: {clientSteamId}");
            }
        }

        private void StartListening()
        {
            // For P2P, we don't need to create a listen socket - we just accept incoming sessions
            GungeonTogether.Logging.Debug.Log("[SteamP2PHostManager] Host is ready to accept P2P connections");

            // Important: Start listening for P2P session requests immediately
            _lastHeartbeatTime = UnityEngine.Time.time;
        }

        public void ConnectClientsInLobby()
        {
            // Use reflection to get SteamMatchmaking and CSteamID
            var steamworksAssembly = typeof(SteamReflectionHelper).Assembly;
            var steamMatchmakingType = steamworksAssembly.GetType("Steamworks.SteamMatchmaking");
            var csteamIdType = steamworksAssembly.GetType("Steamworks.CSteamID");
            var getNumLobbyMembers = steamMatchmakingType.GetMethod("GetNumLobbyMembers");
            var getLobbyMemberByIndex = steamMatchmakingType.GetMethod("GetLobbyMemberByIndex");
            object csteamLobbyId = Activator.CreateInstance(csteamIdType, _lobbyId);
            int memberCount = (int)getNumLobbyMembers.Invoke(null, new object[] { csteamLobbyId });

            for (int i = 0; i < memberCount; i++)
            {
                object memberIdObj = getLobbyMemberByIndex.Invoke(null, new object[] { csteamLobbyId, i });
                ulong memberSteamId = (ulong)memberIdObj.GetType().GetField("m_SteamID").GetValue(memberIdObj);

                // Never add the host's own SteamID to _clientConnections
                if (memberSteamId.Equals(_hostSteamId) || _clientConnections.ContainsKey(memberSteamId))
                {
                    continue;
                }
                // Do not send welcome or accept session here; let the callback handle it
            }

            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] P2P connection check complete. Connected clients: {_clientConnections.Count}");
        }

        public void SendToAllClients(byte[] data)
        {
            foreach (var kvp in _clientConnections)
            {
                ulong clientSteamId = kvp.Key;
                try
                {
                    bool sent = SteamNetworkingSocketsHelper.SendP2PPacket(clientSteamId, data);
                    if (!sent)
                    {
                        GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Failed to send data to client {clientSteamId}");
                    }
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Send error to client {kvp.Key}: {e.Message}");
                }
            }
        }

        public void SendToClient(ulong clientSteamId, byte[] data)
        {
            if (!_clientConnections.ContainsKey(clientSteamId)) return;

            try
            {
                bool sent = SteamNetworkingSocketsHelper.SendP2PPacket(clientSteamId, data);
                if (!sent)
                {
                    GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Failed to send data to client {clientSteamId}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Send error to client {clientSteamId}: {e.Message}");
            }
        }

        public void ReceiveMessages()
        {
            try
            {
                // Use the new efficient packet polling method
                var packets = SteamNetworkingSocketsHelper.PollIncomingPackets();

                foreach (var packet in packets)
                {
                    if (packet.data.Length > 0)
                    {
                        ProcessReceivedPacket(packet.data, packet.senderSteamId);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Receive error: {e.Message}");
            }
        }

        private void ProcessReceivedPacket(byte[] data, ulong senderSteamId)
        {
            try
            {
                // Convert to string to check for special messages
                string dataStr = System.Text.Encoding.UTF8.GetString(data);

                if (dataStr.StartsWith("CLIENT_HANDSHAKE:"))
                {
                    // Client is initiating connection
                    string steamIdStr = dataStr.Substring("CLIENT_HANDSHAKE:".Length);
                    if (ulong.TryParse(steamIdStr, out ulong clientSteamId))
                    {
                        // Never add the host's own SteamID as a client
                        if (!clientSteamId.Equals(_hostSteamId) && !_clientConnections.ContainsKey(clientSteamId))
                        {
                            _clientConnections[clientSteamId] = clientSteamId;
                            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Added client to connections: {clientSteamId}");

                            // Send welcome response and notify NetworkManager
                            byte[] welcomeResponse = System.Text.Encoding.UTF8.GetBytes($"WELCOME_RESPONSE:{_hostSteamId}");
                            SteamNetworkingSocketsHelper.SendP2PPacket(clientSteamId, welcomeResponse);
                            NetworkManager.Instance.HandlePlayerJoin(clientSteamId);
                        }
                    }
                }
                else
                {
                    // Try to process as network packet
                    var packet = PacketSerializer.DeserializePacket(data);
                    if (packet.HasValue)
                    {
                        NetworkManager.Instance.QueueIncomingPacket(packet.Value);
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[SteamP2PHostManager] Failed to deserialize network packet from {senderSteamId}");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Error processing packet from {senderSteamId}: {e.Message}");
            }
        }

        public void DisconnectClient(ulong clientSteamId)
        {
            if (_clientConnections.ContainsKey(clientSteamId))
            {
                _clientConnections.Remove(clientSteamId);
                GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Disconnected client: {clientSteamId}");
            }
        }

        public Dictionary<ulong, object> GetConnectedClients()
        {
            return new Dictionary<ulong, object>(_clientConnections);
        }

        public void Update()
        {
            // Process incoming messages
            ReceiveMessages();

            // Send periodic heartbeat to keep connections alive
            if (UnityEngine.Time.time - _lastHeartbeatTime >= HEARTBEAT_INTERVAL)
            {
                SendHeartbeat();
                _lastHeartbeatTime = UnityEngine.Time.time;
            }
        }

        private void SendHeartbeat()
        {
            if (_clientConnections.Count > 0)
            {
                string heartbeatMessage = $"HEARTBEAT:{UnityEngine.Time.time}";
                byte[] heartbeatData = Encoding.UTF8.GetBytes(heartbeatMessage);
                SendToAllClients(heartbeatData);
            }
            else
            {
                // Log connection status periodically when no clients are connected
                if (UnityEngine.Time.time % 10f < 0.1f) // Every ~10 seconds
                {
                    LogConnectionStatus();
                }
            }
        }

        public void LogConnectionStatus()
        {
            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Connection Status:");
            GungeonTogether.Logging.Debug.Log($"  Lobby ID: {_lobbyId}");
            GungeonTogether.Logging.Debug.Log($"  Host Steam ID: {_hostSteamId}");
            GungeonTogether.Logging.Debug.Log($"  Connected Clients: {_clientConnections.Count}");

            foreach (var kvp in _clientConnections)
            {
                GungeonTogether.Logging.Debug.Log($"    Client: {kvp.Key}");
            }
        }
    }
}
