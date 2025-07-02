using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

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
            StartListening();
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
            
            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Checking {memberCount} lobby members for P2P connections");
            
            for (int i = 0; i < memberCount; i++)
            {
                object memberIdObj = getLobbyMemberByIndex.Invoke(null, new object[] { csteamLobbyId, i });
                ulong memberSteamId = (ulong)memberIdObj.GetType().GetField("m_SteamID").GetValue(memberIdObj);
                
                // Never add the host's own SteamID to _clientConnections
                if (memberSteamId.Equals(_hostSteamId) || _clientConnections.ContainsKey(memberSteamId))
                {
                    continue;
                }
                
                // CRITICAL: Instead of just accepting, INITIATE P2P by sending a welcome packet
                // This will establish the P2P session properly
                byte[] welcomeData = System.Text.Encoding.UTF8.GetBytes($"WELCOME_TO_HOST:{_hostSteamId}");
                bool sentWelcome = SteamNetworkingSocketsHelper.SendP2PPacket(memberSteamId, welcomeData);
                
                if (sentWelcome)
                {
                    // Now accept the P2P session
                    bool sessionAccepted = SteamNetworkingSocketsHelper.AcceptP2PSession(memberSteamId);
                    if (sessionAccepted)
                    {
                        _clientConnections[memberSteamId] = memberSteamId; // Store steam ID as connection identifier
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Initiated and accepted P2P session with client: {memberSteamId}");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[SteamP2PHostManager] Welcome sent but failed to accept P2P session with client: {memberSteamId}");
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[SteamP2PHostManager] Failed to send welcome packet to client: {memberSteamId}");
                }
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

        public void SendTestMessageToAllClients(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            SendToAllClients(data);
            GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Sent test message to all clients: {message}");
        }

        public void ReceiveMessages()
        {
            try
            {
                // Check for available P2P packets
                while (SteamNetworkingSocketsHelper.IsP2PPacketAvailable())
                {
                    byte[] data = SteamNetworkingSocketsHelper.ReadP2PPacket();
                    if (data != null && data.Length > 0)
                    {
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Received P2P packet: {data.Length} bytes");
                        // Process the packet - in a real implementation this would be handled by NetworkManager
                        ProcessReceivedPacket(data);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Receive error: {e.Message}");
            }
        }

        private void ProcessReceivedPacket(byte[] data)
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
                        }
                    }
                }
                else if (dataStr.StartsWith("HEARTBEAT_ACK:"))
                {
                    // Client responded to heartbeat
                    string steamIdStr = dataStr.Substring("HEARTBEAT_ACK:".Length);
                    if (ulong.TryParse(steamIdStr, out ulong clientSteamId))
                    {
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Received heartbeat ack from client: {clientSteamId}");
                    }
                }
                else if (dataStr.StartsWith("WELCOME_ACK:"))
                {
                    // Client acknowledged our welcome
                    string steamIdStr = dataStr.Substring("WELCOME_ACK:".Length);
                    if (ulong.TryParse(steamIdStr, out ulong clientSteamId))
                    {
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Client acknowledged welcome: {clientSteamId}");
                    }
                }
                else
                {
                    // Try to process as network packet
                    var packet = PacketSerializer.DeserializePacket(data);
                    if (packet.HasValue)
                    {
                        NetworkManager.Instance.QueueIncomingPacket(packet.Value);
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Processed network packet: {packet.Value.Type}");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Received unknown data: {dataStr}");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamP2PHostManager] Error processing packet: {e.Message}");
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
                GungeonTogether.Logging.Debug.Log($"[SteamP2PHostManager] Sent heartbeat to {_clientConnections.Count} clients");
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
