using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Steam-compatible session manager for GungeonTogether multiplayer
    /// Supports Steam P2P networking and session management with real connections
    /// </summary>
    public class SimpleSessionManager
    {
        public bool IsActive { get; private set; }
        public string Status { get; private set; }
        public string CurrentSessionId { get; private set; }
        public bool IsHost { get; private set; }
        
        // Steam P2P networking using ETG's built-in Steamworks (stored as interface to avoid TypeLoadException)
        private ISteamNetworking steamNet;
        
        // Player synchronization
        private PlayerSynchronizer playerSync;
        
        // Real connection tracking
        private Dictionary<ulong, PlayerConnection> connectedPlayers;
        private float lastConnectionCheck;
        private const float CONNECTION_CHECK_INTERVAL = 1.0f;
        
        // Connection handshaking
        private const byte PACKET_TYPE_HANDSHAKE_REQUEST = 1;
        private const byte PACKET_TYPE_HANDSHAKE_RESPONSE = 2;
        private const byte PACKET_TYPE_HANDSHAKE_COMPLETE = 3;
        private const byte PACKET_TYPE_PLAYER_DATA = 4;
        private const byte PACKET_TYPE_DISCONNECT = 5;
        
        public struct PlayerConnection
        {
            public ulong steamId;
            public string playerName;
            public bool isConnected;
            public bool handshakeComplete;
            public float lastActivity;
            public float connectionTime;
        }
        
        public SimpleSessionManager()
        {
            IsActive = false;
            Status = "Ready";
            CurrentSessionId = null;
            IsHost = false;
            connectedPlayers = new Dictionary<ulong, PlayerConnection>();
            
            // Don't initialize Steam networking in constructor - do it lazily when needed
            Debug.Log("[SimpleSessionManager] Created session manager (Steam networking will be initialized on demand)");
            steamNet = null;
            
            Debug.Log("[SimpleSessionManager] Steam-compatible session manager initialized");
        }        public void StartSession()
        {
            IsActive = true;
            IsHost = true;
            Status = "Starting Steam P2P session...";
            connectedPlayers.Clear();
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Set up real P2P connection event handlers
            if (steamNet != null)
            {
                // Subscribe to connection events
                steamNet.OnPlayerJoined += OnPlayerConnected;
                steamNet.OnPlayerLeft += OnPlayerDisconnected;
                steamNet.OnDataReceived += OnDataReceived;
            }
            
            // Start hosting with ETG's Steam P2P networking
            if (steamNet != null && steamNet.IsAvailable())
            {
                var steamId = steamNet.GetSteamID();
                if (steamId != 0)
                {
                    CurrentSessionId = $"steam_{steamId}";
                    Status = $"Hosting P2P: {steamId} (Waiting for connections)";
                    Debug.Log($"[SimpleSessionManager] Hosting Steam P2P session: {steamId}");
                    Debug.Log($"[SimpleSessionManager] Real P2P networking active - ready to accept connections");
                    
                    // Setup Rich Presence and lobby for Steam overlay invites
                    steamNet.StartHostingSession();
                }
                else
                {
                    Status = "Failed to start Steam session";
                    Debug.LogError("[SimpleSessionManager] Could not get Steam ID for hosting");
                }
            }
            else
            {
                // Fallback for offline mode
                CurrentSessionId = GenerateSessionId();
                Status = $"Hosting offline: {CurrentSessionId}";
                Debug.LogWarning("[SimpleSessionManager] Steam not available - hosting offline session");
            }
            
            // Initialize player synchronization
            InitializePlayerSync();
        }        public void JoinSession(string sessionId)
        {
            IsActive = true;
            IsHost = false;
            Status = $"Connecting to Steam P2P session: {sessionId}";
            CurrentSessionId = sessionId;
            connectedPlayers.Clear();
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Set up real P2P connection event handlers
            if (steamNet != null)
            {
                steamNet.OnPlayerJoined += OnPlayerConnected;
                steamNet.OnPlayerLeft += OnPlayerDisconnected;
                steamNet.OnDataReceived += OnDataReceived;
            }
            
            // Extract Steam ID from session format and initiate P2P connection
            if (steamNet != null && steamNet.IsAvailable())
            {
                ulong hostSteamId = ExtractSteamIdFromSession(sessionId);
                if (hostSteamId != 0)
                {
                    Debug.Log($"[SimpleSessionManager] Initiating real P2P connection to host: {hostSteamId}");
                    
                    // Setup Rich Presence for joining
                    steamNet.StartJoiningSession(hostSteamId);
                    
                    // Accept P2P session with host and initiate handshake
                    if (steamNet.AcceptP2PSession(hostSteamId))
                    {
                        Status = $"P2P Connected - Handshaking with: {hostSteamId}";
                        Debug.Log($"[SimpleSessionManager] P2P connection established with host: {hostSteamId}");
                        
                        // Send handshake request to host
                        SendHandshakeRequest(hostSteamId);
                    }
                    else
                    {
                        Status = "Failed to establish P2P connection";
                        Debug.LogError($"[SimpleSessionManager] Failed to establish P2P connection to: {hostSteamId}");
                    }
                }
                else
                {
                    Status = "Invalid session format";
                    Debug.LogError($"[SimpleSessionManager] Invalid session format: {sessionId}");
                    return;
                }
            }
            else
            {
                // Fallback for offline mode
                Status = $"Joined offline: {sessionId}";
                Debug.LogWarning("[SimpleSessionManager] Steam not available - joining offline session");
            }
            
            // Initialize player synchronization
            InitializePlayerSync();
        }
        
        public void StopSession()
        {
            var wasHosting = IsHost;
            var sessionId = CurrentSessionId;
            
            IsActive = false;
            IsHost = false;
            CurrentSessionId = null;
            Status = "Stopped";
            
            // Send disconnect packets to all connected players
            SendDisconnectToAllPlayers();
            
            // Clean up connection tracking
            connectedPlayers.Clear();
            
            // Unsubscribe from connection events
            if (steamNet != null)
            {
                steamNet.OnPlayerJoined -= OnPlayerConnected;
                steamNet.OnPlayerLeft -= OnPlayerDisconnected;
                steamNet.OnDataReceived -= OnDataReceived;
            }
            
            // Unsubscribe from Steam overlay events
            try
            {
                ETGSteamP2PNetworking.OnOverlayJoinRequested -= OnSteamOverlayJoinRequested;
                ETGSteamP2PNetworking.OnOverlayActivated -= OnSteamOverlayActivated;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimpleSessionManager] Error unsubscribing from overlay events: {e.Message}");
            }
            
            // Close Steam P2P connections
            if (steamNet != null)
            {
                try
                {
                    // Stop Rich Presence and leave lobby
                    steamNet.StopSession();
                    steamNet.Shutdown();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SimpleSessionManager] Error shutting down Steam networking: {e.Message}");
                }
            }
            
            // Cleanup player synchronization
            playerSync?.Cleanup();
            playerSync = null;
            
            if (wasHosting)
            {
                Debug.Log($"[SimpleSessionManager] Stopped hosting session: {sessionId} ({connectedPlayers.Count} players disconnected)");
            }
            else
            {
                Debug.Log($"[SimpleSessionManager] Left session: {sessionId}");
            }
        }        public void Update()
        {
            try
            {
                if (!IsActive) return;
                
                // Update Steam networking (check for packets)
                steamNet?.Update();
                
                // Update player synchronization
                playerSync?.Update();
                
                // Check for join requests periodically
                if (IsHost)
                {
                    steamNet?.CheckForJoinRequests();
                }
                
                // Check connections health
                CheckConnections();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error in Update: {e.Message}");
            }
        }
        
        /// <summary>
        /// Update Steam networking in a separate method to isolate type references
        /// </summary>
        private void UpdateSteamNetworking()
        {
            try
            {
                if (steamNet != null)
                {
                    steamNet.Update();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error updating Steam networking: {e.Message}");
            }
        }
        
        /// <summary>
        /// Initialize player synchronization system
        /// </summary>
        private void InitializePlayerSync()
        {
            try
            {
                // Create player synchronizer
                playerSync = new PlayerSynchronizer(this);
                Debug.Log("[SimpleSessionManager] Player synchronization initialized (ready for networking integration)");
            }            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Failed to initialize player sync: {e.Message}");
            }
        }
        
        /// <summary>
        /// Extract Steam ID from session identifier
        /// </summary>
        private ulong ExtractSteamIdFromSession(string sessionId)
        {
            try
            {
                // Handle different session ID formats
                if (sessionId.StartsWith("steam_"))
                {
                    string steamIdStr = sessionId.Substring(6); // Remove "steam_" prefix
                    if (ulong.TryParse(steamIdStr, out ulong steamId))
                    {
                        return steamId;
                    }
                }
                else if (sessionId.StartsWith("friend_"))
                {
                    // Extract from friend session format: "friend_76561198000000001_session"
                    string[] parts = sessionId.Split('_');
                    if (parts.Length >= 2 && ulong.TryParse(parts[1], out ulong steamId))
                    {
                        return steamId;
                    }
                }
                else if (sessionId.StartsWith("steam_lobby_"))
                {
                    // Extract from lobby format: "steam_lobby_test_lobby_12345"
                    // For now, use a default test Steam ID
                    return 76561198000000001; // Test Steam ID
                }
                  Debug.LogWarning($"[SimpleSessionManager] Could not extract Steam ID from session: {sessionId}");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error extracting Steam ID: {e.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Generate Steam-compatible session ID
        /// </summary>
        private string GenerateSessionId()
        {
            // Generate Steam-compatible session ID
            return $"gungeon_session_{DateTime.Now.Ticks % 1000000}";
        }
        
        /// <summary>
        /// Setup Steam networking event callbacks
        /// </summary>
        private void SetupNetworkingCallbacks()
        {
            if (steamNet == null) return;
            
            try
            {
                // Subscribe directly to interface events
                steamNet.OnPlayerJoined += OnPlayerJoined;
                steamNet.OnPlayerLeft += OnPlayerLeft;
                steamNet.OnDataReceived += OnNetworkDataReceived;
                steamNet.OnJoinRequested += OnJoinRequested;
                
                // Subscribe to Steam overlay join events
                ETGSteamP2PNetworking.OnOverlayJoinRequested += OnSteamOverlayJoinRequested;
                ETGSteamP2PNetworking.OnOverlayActivated += OnSteamOverlayActivated;
                
                Debug.Log("[SimpleSessionManager] Steam networking callbacks set up successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Failed to set up networking callbacks: {e.Message}");
            }
        }
        
        // Network event handlers
        private void OnPlayerJoined(ulong playerId)
        {
            Debug.Log($"[SimpleSessionManager] Player joined: {playerId}");
            Status = $"Players connected: 2+"; // Simplified for now
        }
        
        private void OnPlayerLeft(ulong playerId)
        {
            Debug.Log($"[SimpleSessionManager] Player left: {playerId}");
            playerSync?.OnPlayerDisconnected(playerId);
            Status = $"Hosting session";
        }
        
        private void OnNetworkDataReceived(ulong senderId, byte[] data)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Received {data.Length} bytes from {senderId}");
                
                // Convert data to string to check message type
                string message = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log($"[SimpleSessionManager] Message content: {message}");
                
                // Handle different message types
                if (message.StartsWith("JOIN_REQUEST"))
                {
                    Debug.Log($"[SimpleSessionManager] Join request packet received from {senderId}");
                    if (IsHost)
                    {
                        // Auto-accept join request and send welcome
                        steamNet?.HandleJoinRequest(senderId);
                    }
                }
                else if (message.StartsWith("WELCOME"))
                {
                    Debug.Log($"[SimpleSessionManager] Welcome packet received from host {senderId}");
                    Status = $"Connected to host: {senderId}";
                }
                else
                {
                    // Handle other packet types (player updates, etc.)
                    Debug.Log($"[SimpleSessionManager] Unknown packet type from {senderId}: {message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling network data: {e.Message}");
            }
        }
        
        private void OnJoinRequested(ulong joinerSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Join request from: {joinerSteamId}");
                
                if (IsHost)
                {
                    // Accept the P2P session
                    steamNet?.AcceptP2PSession(joinerSteamId);
                    
                    // Send welcome packet
                    var welcomeData = System.Text.Encoding.UTF8.GetBytes("WELCOME");
                    steamNet?.SendP2PPacket(joinerSteamId, welcomeData);
                    
                    Status = $"Player joined: {joinerSteamId}";
                    Debug.Log($"[SimpleSessionManager] Accepted join request from: {joinerSteamId}");
                }
                else
                {
                    Debug.LogWarning($"[SimpleSessionManager] Received join request but not hosting: {joinerSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay join requests (when someone clicks "Join Game" in Steam overlay)
        /// </summary>
        private void OnSteamOverlayJoinRequested(string hostSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Steam overlay join requested for host: {hostSteamId}");
                
                // Parse Steam ID
                if (ulong.TryParse(hostSteamId, out ulong steamId) && steamId != 0)
                {
                    // Join the host session
                    var sessionId = $"steam_{steamId}";
                    Debug.Log($"[SimpleSessionManager] Joining session via Steam overlay: {sessionId}");
                    
                    // Stop current session if any
                    if (IsActive)
                    {
                        StopSession();
                    }
                    
                    // Join the requested session
                    JoinSession(sessionId);
                }
                else
                {
                    Debug.LogError($"[SimpleSessionManager] Invalid Steam ID in overlay join request: {hostSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling Steam overlay join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay activation events
        /// </summary>
        private void OnSteamOverlayActivated(bool isActive)
        {
            Debug.Log($"[SimpleSessionManager] Steam overlay activated: {isActive}");
            // Could be used for pausing/unpausing game when overlay is opened
        }
        
        /// <summary>
        /// Lazy initialization of Steam networking - only creates it when needed
        /// </summary>
        private void EnsureSteamNetworkingInitialized()
        {
            if (steamNet != null) return; // Already initialized
            
            try
            {
                Debug.Log("[SimpleSessionManager] Attempting to initialize ETG Steam P2P networking...");
                
                // Use factory to create the instance safely
                steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                
                if (steamNet != null && steamNet.IsAvailable())
                {
                    SetupNetworkingCallbacks();
                    Debug.Log("[SimpleSessionManager] ETG Steam P2P networking initialized successfully");
                }
                else
                {
                    Debug.LogWarning("[SimpleSessionManager] Steam not available - will run in offline mode");
                    steamNet = null; // Clear the instance if it's not working
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Failed to initialize Steam P2P networking: {e.Message}");
                Debug.LogError($"[SimpleSessionManager] Stack trace: {e.StackTrace}");
                steamNet = null; // Clear the instance
            }
        }
        
        /// <summary>
        /// Check the health of connections and remove inactive ones
        /// </summary>
        private void CheckConnections()
        {
            try
            {
                // Regularly check connection status
                if (Time.time - lastConnectionCheck < CONNECTION_CHECK_INTERVAL) return;
                lastConnectionCheck = Time.time;
                
                // Check each connected player
                foreach (var player in connectedPlayers.Keys)
                {
                    if (connectedPlayers.TryGetValue(player, out PlayerConnection connection))
                    {
                        // Check if the connection is active
                        if (!connection.isConnected)
                        {
                            Debug.LogWarning($"[SimpleSessionManager] Player {player} is not connected (removing)");
                            connectedPlayers.Remove(player);
                        }
                        else
                        {
                            // Optionally, send a heartbeat or ping to the player
                            // steamNet?.SendP2PPacket(player, CreatePingPacket());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error in CheckConnections: {e.Message}");
            }
        }
        
        /// <summary>
        /// Create a ping packet for testing connection
        /// </summary>
        private byte[] CreatePingPacket()
        {
            // Simple ping packet (could be expanded with more data)
            return new byte[] { 0x01, 0x02, 0x03, 0x04 };
        }
        
        // =========================
        // REAL CONNECTION HANDLING
        // =========================
        
        /// <summary>
        /// Handle when a player connects to our session
        /// </summary>
        private void OnPlayerConnected(ulong steamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] REAL P2P CONNECTION: Player {steamId} connected!");
                
                var connection = new PlayerConnection
                {
                    steamId = steamId,
                    playerName = $"Player_{steamId}",
                    isConnected = true,
                    handshakeComplete = false,
                    lastActivity = Time.time,
                    connectionTime = Time.time
                };
                
                connectedPlayers[steamId] = connection;
                
                // If we're the host, send a handshake response
                if (IsHost)
                {
                    SendHandshakeResponse(steamId);
                }
                
                UpdateConnectionStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling player connection: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle when a player disconnects from our session
        /// </summary>
        private void OnPlayerDisconnected(ulong steamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] REAL P2P DISCONNECTION: Player {steamId} disconnected");
                
                if (connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers.Remove(steamId);
                    
                    // Notify player synchronizer
                    playerSync?.OnPlayerDisconnected(steamId);
                }
                
                UpdateConnectionStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling player disconnection: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle incoming data from connected players
        /// </summary>
        private void OnDataReceived(ulong steamId, byte[] data)
        {
            try
            {
                if (data.Length == 0) return;
                
                byte packetType = data[0];
                
                switch (packetType)
                {
                    case PACKET_TYPE_HANDSHAKE_REQUEST:
                        HandleHandshakeRequest(steamId, data);
                        break;
                        
                    case PACKET_TYPE_HANDSHAKE_RESPONSE:
                        HandleHandshakeResponse(steamId, data);
                        break;
                        
                    case PACKET_TYPE_HANDSHAKE_COMPLETE:
                        HandleHandshakeComplete(steamId, data);
                        break;
                        
                    case PACKET_TYPE_PLAYER_DATA:
                        HandlePlayerData(steamId, data);
                        break;
                        
                    case PACKET_TYPE_DISCONNECT:
                        HandleDisconnect(steamId, data);
                        break;
                        
                    default:
                        Debug.LogWarning($"[SimpleSessionManager] Unknown packet type {packetType} from {steamId}");
                        break;
                }
                
                // Update last activity time
                if (connectedPlayers.ContainsKey(steamId))
                {
                    var connection = connectedPlayers[steamId];
                    connection.lastActivity = Time.time;
                    connectedPlayers[steamId] = connection;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling data from {steamId}: {e.Message}");
            }
        }
        
        // =========================
        // HANDSHAKE PROTOCOL
        // =========================
        
        /// <summary>
        /// Send handshake request to host (client -> host)
        /// </summary>
        private void SendHandshakeRequest(ulong hostSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Sending handshake request to host: {hostSteamId}");
                
                var packet = new byte[1 + 8]; // packet type + steam ID
                packet[0] = PACKET_TYPE_HANDSHAKE_REQUEST;
                
                // Add our Steam ID to the packet
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                BitConverter.GetBytes(mySteamId).CopyTo(packet, 1);
                
                if (steamNet?.SendP2PPacket(hostSteamId, packet) == true)
                {
                    Debug.Log($"[SimpleSessionManager] Handshake request sent to {hostSteamId}");
                }
                else
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to send handshake request to {hostSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending handshake request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send handshake response to client (host -> client)
        /// </summary>
        private void SendHandshakeResponse(ulong clientSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Sending handshake response to client: {clientSteamId}");
                
                var packet = new byte[1]; // Just packet type for now
                packet[0] = PACKET_TYPE_HANDSHAKE_RESPONSE;
                
                if (steamNet?.SendP2PPacket(clientSteamId, packet) == true)
                {
                    Debug.Log($"[SimpleSessionManager] Handshake response sent to {clientSteamId}");
                }
                else
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to send handshake response to {clientSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending handshake response: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle incoming handshake request (received by host)
        /// </summary>
        private void HandleHandshakeRequest(ulong steamId, byte[] data)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Received handshake request from: {steamId}");
                
                // Verify we're the host
                if (!IsHost)
                {
                    Debug.LogWarning($"[SimpleSessionManager] Received handshake request but not hosting");
                    return;
                }
                
                // Accept the connection and send response
                SendHandshakeResponse(steamId);
                
                Debug.Log($"[SimpleSessionManager] Handshake established with client: {steamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling handshake request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle incoming handshake response (received by client)
        /// </summary>
        private void HandleHandshakeResponse(ulong steamId, byte[] data)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Received handshake response from host: {steamId}");
                
                // Mark handshake as complete
                if (connectedPlayers.ContainsKey(steamId))
                {
                    var connection = connectedPlayers[steamId];
                    connection.handshakeComplete = true;
                    connectedPlayers[steamId] = connection;
                }
                else
                {
                    // Create connection entry if it doesn't exist
                    var connection = new PlayerConnection
                    {
                        steamId = steamId,
                        playerName = $"Host_{steamId}",
                        isConnected = true,
                        handshakeComplete = true,
                        lastActivity = Time.time,
                        connectionTime = Time.time
                    };
                    connectedPlayers[steamId] = connection;
                }
                
                UpdateConnectionStatus();
                Debug.Log($"[SimpleSessionManager] FULLY CONNECTED to host: {steamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling handshake response: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle handshake completion
        /// </summary>
        private void HandleHandshakeComplete(ulong steamId, byte[] data)
        {
            Debug.Log($"[SimpleSessionManager] Handshake completed with: {steamId}");
        }
        
        /// <summary>
        /// Handle player data packets (for future use)
        /// </summary>
        private void HandlePlayerData(ulong steamId, byte[] data)
        {
            // For now, just log that we received player data
            Debug.Log($"[SimpleSessionManager] Received player data from {steamId} ({data.Length} bytes)");
            
            // Pass to player synchronizer for processing
            // playerSync?.OnPlayerUpdate(steamId, playerState);
        }
        
        /// <summary>
        /// Handle disconnect packets
        /// </summary>
        private void HandleDisconnect(ulong steamId, byte[] data)
        {
            Debug.Log($"[SimpleSessionManager] Player {steamId} is disconnecting");
            OnPlayerDisconnected(steamId);
        }
        
        /// <summary>
        /// Update the session status based on connections
        /// </summary>
        private void UpdateConnectionStatus()
        {
            try
            {
                int connectedCount = connectedPlayers.Count;
                
                if (IsHost)
                {
                    var hostSteamId = steamNet?.GetSteamID() ?? 0;
                    Status = $"Hosting P2P: {hostSteamId} ({connectedCount} players connected)";
                }
                else
                {
                    var hostSteamId = ExtractSteamIdFromSession(CurrentSessionId);
                    bool fullyConnected = connectedPlayers.ContainsKey(hostSteamId) && 
                                         connectedPlayers[hostSteamId].handshakeComplete;
                    
                    if (fullyConnected)
                    {
                        Status = $"Connected to P2P: {hostSteamId} (Ready)";
                    }
                    else
                    {
                        Status = $"Connecting to P2P: {hostSteamId} (Handshaking...)";
                    }
                }
                
                Debug.Log($"[SimpleSessionManager] Connection status updated: {Status}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error updating connection status: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get count of connected players
        /// </summary>
        public int GetConnectedPlayerCount()
        {
            return connectedPlayers.Count;
        }
        
        /// <summary>
        /// Get list of connected player Steam IDs
        /// </summary>
        public ulong[] GetConnectedPlayers()
        {
            var result = new ulong[connectedPlayers.Count];
            int i = 0;
            foreach (var steamId in connectedPlayers.Keys)
            {
                result[i++] = steamId;
            }
            return result;
        }
        
        /// <summary>
        /// Send disconnect packet to all connected players before stopping
        /// </summary>
        private void SendDisconnectToAllPlayers()
        {
            try
            {
                if (steamNet == null) return;
                
                var disconnectPacket = new byte[1];
                disconnectPacket[0] = PACKET_TYPE_DISCONNECT;
                
                foreach (var steamId in connectedPlayers.Keys)
                {
                    steamNet.SendP2PPacket(steamId, disconnectPacket);
                    Debug.Log($"[SimpleSessionManager] Sent disconnect packet to {steamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending disconnect packets: {e.Message}");
            }
        }
    }
}
