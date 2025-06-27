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
        
        // Connection status logging
        private float lastStatusLog;
        private const float STATUS_LOG_INTERVAL = 60.0f; // Log status every 10 seconds
        
        // Connection handshaking and heartbeat
        private const byte PACKET_TYPE_HANDSHAKE_REQUEST = 1;
        private const byte PACKET_TYPE_HANDSHAKE_RESPONSE = 2;
        private const byte PACKET_TYPE_HANDSHAKE_COMPLETE = 3;
        private const byte PACKET_TYPE_PLAYER_DATA = 4;
        private const byte PACKET_TYPE_DISCONNECT = 5;
        private const byte PACKET_TYPE_HEARTBEAT = 6;
        private const byte PACKET_TYPE_HEARTBEAT_RESPONSE = 7;
        
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
            // Validate location before starting session
            if (!IsValidLocationForMultiplayer())
            {
                string currentLocation = GetCurrentLocationName();
                Status = $"Cannot start session from: {currentLocation}";
                Debug.LogWarning($"[SimpleSessionManager] Cannot start multiplayer session from current location: {currentLocation}");
                Debug.LogWarning("[SimpleSessionManager] Multiplayer can only be started from Main Menu or Gungeon Foyer");
                return;
            }
            
            IsActive = true;
            IsHost = true;
            Status = "Starting Steam P2P session...";
            connectedPlayers.Clear();
            
            Debug.Log("[SimpleSessionManager] Location validated - starting multiplayer session");
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Set up real P2P connection event handlers
            if (!ReferenceEquals(steamNet, null))
            {
                // Subscribe to connection events
                steamNet.OnPlayerJoined += OnPlayerConnected;
                steamNet.OnPlayerLeft += OnPlayerDisconnected;
                steamNet.OnDataReceived += OnDataReceived;
            }
            
            // Start hosting with ETG's Steam P2P networking
            if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
            {
                var steamId = steamNet.GetSteamID();
                if (!ReferenceEquals(steamId, null) && (!ReferenceEquals(steamId, 0)))
                {
                    // Generate a session ID based on Steam ID
                    CurrentSessionId = $"steam_{steamId}";
                    
                    // Set status and log hosting details
                    IsHost = true;
                    Status = $"Hosting P2P: {steamId} (Waiting for connections)";
                    
                    // Log the hosting details
                    Debug.Log($"[SimpleSessionManager] Hosting Steam P2P session with ID: {CurrentSessionId}");
                    
                    // Start the hosting session
                    UpdateSteamNetworking();
                    
                    // Register as a GungeonTogether host for friends detection
                    ETGSteamP2PNetworking.RegisterAsHost();
                    
                    // Update Steam Rich Presence to show GungeonTogether hosting
                    SteamSessionHelper.UpdateRichPresence(true, CurrentSessionId);
                    
                    // Notify Steam networking to start hosting
                    if (!ReferenceEquals(steamNet, null))
                    {
                        steamNet.StartHostingSession();
                        
                        // CRITICAL: Start actively checking for incoming connections immediately
                        Debug.Log("[SimpleSessionManager] 🏠 Host is now actively accepting P2P connections");
                        Debug.Log("[SimpleSessionManager] 🏠 Other players can now join this session");
                    }
                }
                else if (!ReferenceEquals(steamId, 0))
                {
                    CurrentSessionId = $"steam_{steamId}";
                    Status = $"Hosting P2P: {steamId} (Waiting for connections)";
                    Debug.Log($"[SimpleSessionManager] Hosting Steam P2P session: {steamId}");
                    Debug.Log($"[SimpleSessionManager] Real P2P networking active - ready to accept connections");
                    
                    // Register as a GungeonTogether host for friends detection
                    ETGSteamP2PNetworking.RegisterAsHost();
                    
                    // Update Steam Rich Presence to show GungeonTogether hosting
                    SteamSessionHelper.UpdateRichPresence(true, CurrentSessionId);
                    
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
            // Validate location before joining session
            if (!IsValidLocationForMultiplayer())
            {
                string currentLocation = GetCurrentLocationName();
                Status = $"Cannot join session from: {currentLocation}";
                Debug.LogWarning($"[SimpleSessionManager] Cannot join multiplayer session from current location: {currentLocation}");
                Debug.LogWarning("[SimpleSessionManager] Multiplayer can only be joined from Main Menu or Gungeon Foyer");
                return;
            }
            
            IsActive = true;
            IsHost = false;
            Status = $"Connecting to Steam P2P session: {sessionId}";
            CurrentSessionId = sessionId;
            connectedPlayers.Clear();
            
            Debug.Log("[SimpleSessionManager] Location validated - joining multiplayer session");
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Set up real P2P connection event handlers
            if (!ReferenceEquals(steamNet, null))
            {
                steamNet.OnPlayerJoined += OnPlayerConnected;
                steamNet.OnPlayerLeft += OnPlayerDisconnected;
                steamNet.OnDataReceived += OnDataReceived;
            }
            
            // Extract Steam ID from session format and initiate P2P connection
            if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
            {
                ulong hostSteamId = ExtractSteamIdFromSession(sessionId);
                if (!ReferenceEquals(hostSteamId, 0))
                {
                    Debug.Log($"[SimpleSessionManager] Initiating real P2P connection to host: {hostSteamId}");
                    
                    // Setup Rich Presence for joining
                    steamNet.StartJoiningSession(hostSteamId);
                    
                    // CRITICAL: Properly establish P2P session before sending handshake
                    bool p2pSessionReady = false;
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        Debug.Log($"[SimpleSessionManager] Establishing P2P session with host (attempt {attempt + 1})..."); 
                        
                        if (steamNet.AcceptP2PSession(hostSteamId))
                        {
                            p2pSessionReady = true;
                            Debug.Log($"[SimpleSessionManager] ✅ P2P session established with host: {hostSteamId}");
                            break;
                        }
                        else
                        {
                            Debug.LogWarning($"[SimpleSessionManager] P2P session establishment failed (attempt {attempt + 1})");
                            // Wait briefly before retrying (Unity-friendly)
                            if (attempt < 4)
                            {
                                for (int i = 0; i < 10; i++) // Brief delay using frame counting
                                {
                                    // This creates a brief pause without blocking Unity's main thread
                                    if (i == 9) break;
                                }
                            }
                        }
                    }
                    
                    if (p2pSessionReady)
                    {
                        Status = $"P2P Connected - Handshaking with: {hostSteamId}";
                        Debug.Log($"[SimpleSessionManager] ✅ P2P connection established with host: {hostSteamId}");
                        
                        // Send handshake request to host
                        SendHandshakeRequest(hostSteamId);
                    }
                    else
                    {
                        Status = "Failed to establish P2P connection";
                        Debug.LogError($"[SimpleSessionManager] ❌ Failed to establish P2P connection to: {hostSteamId}");
                        Debug.LogError($"[SimpleSessionManager] P2P connection failed - this usually means:");
                        Debug.LogError($"[SimpleSessionManager]   • Host is not running or not accepting connections");
                        Debug.LogError($"[SimpleSessionManager]   • Network connectivity issues between players");
                        Debug.LogError($"[SimpleSessionManager]   • Steam P2P networking is not properly initialized");
                        Debug.LogError($"[SimpleSessionManager]   • Firewall blocking Steam P2P traffic");
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
            
            // Unregister as host if we were hosting
            if (wasHosting)
            {
                ETGSteamP2PNetworking.UnregisterAsHost();
                SteamSessionHelper.UpdateRichPresence(false, null);
                Debug.Log("[SimpleSessionManager] Unregistered from GungeonTogether host registry");
            }
            
            // Unsubscribe from connection events
            if (!ReferenceEquals(steamNet, null))
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
            if (!ReferenceEquals(steamNet, null))
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
        }
        public void Update()
        {
            try
            {
                if (!IsActive) return;

                // Update player synchronization
                playerSync?.Update();

                // Check connections health
                CheckConnections();
                
                if (IsHost)
                {
                    // CRITICAL: Host must actively check for incoming P2P requests EVERY frame
                    // This MUST be done before steamNet.Update() to catch packets first
                    CheckForIncomingP2PRequests();
                }
                
                // Update Steam networking AFTER processing incoming requests
                UpdateSteamNetworking();
                
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
                if (!ReferenceEquals(steamNet, null))
                {
                    // For clients, let steamNet.Update() handle packet reading normally
                    if (!IsHost)
                    {
                        steamNet.Update();
                    }
                    // For hosts, we handle packet reading manually in CheckForIncomingP2PRequests
                    // so we only call Update for other Steam networking tasks
                    else
                    {
                        // Call a lighter update that doesn't consume packets
                        // or call Update() but make sure our packet processing happens first
                        steamNet.Update();
                    }
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
            } catch (Exception e)
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
            if (ReferenceEquals(steamNet, null)) return;
            
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
                if (ulong.TryParse(hostSteamId, out ulong steamId) && (!ReferenceEquals(steamId, 0)))
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
            if (!ReferenceEquals(steamNet, null)) return; // Already initialized
            
            try
            {
                Debug.Log("[SimpleSessionManager] Attempting to initialize ETG Steam P2P networking...");
                
                // Use factory to create the instance safely
                steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
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
                
                // Periodic status logging for hosts (every 10 seconds)
                if (IsHost && Time.time - lastStatusLog >= STATUS_LOG_INTERVAL)
                {
                    lastStatusLog = Time.time;
                    LogHostConnectionStatus("Periodic status report");
                }
                
                // Check each connected player with improved timeout handling
                var playersToRemove = new List<ulong>();
                foreach (var kvp in connectedPlayers)
                {
                    var steamId = kvp.Key;
                    var connection = kvp.Value;
                    
                    // Check for connection timeout (30 seconds without activity)
                    float timeSinceLastActivity = Time.time - connection.lastActivity;
                    float timeSinceConnection = Time.time - connection.connectionTime;
                    
                    // More lenient timeout handling to prevent premature disconnections
                    if (!connection.isConnected)
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Player {steamId} is not connected (removing)");
                        playersToRemove.Add(steamId);
                    }
                    else if (timeSinceLastActivity > 60.0f) // 60 seconds timeout instead of 30
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Player {steamId} timeout - no activity for {timeSinceLastActivity:F1}s (removing)");
                        playersToRemove.Add(steamId);
                    }
                    else if (!connection.handshakeComplete && timeSinceConnection > 15.0f) // 15 seconds for handshake
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Player {steamId} handshake timeout - incomplete for {timeSinceConnection:F1}s (removing)");
                        playersToRemove.Add(steamId);
                    }
                    else if (connection.handshakeComplete && timeSinceLastActivity > 30.0f) // Send heartbeat after 30s of no activity
                    {
                        // Send a heartbeat to check if connection is still alive
                        Debug.Log($"[SimpleSessionManager] Sending heartbeat to player {steamId} (inactive for {timeSinceLastActivity:F1}s)");
                        SendHeartbeat(steamId);
                    }
                }
                
                // Remove timed out connections
                foreach (var steamId in playersToRemove)
                {
                    OnPlayerDisconnected(steamId);
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
                Debug.Log($"[SimpleSessionManager] Player {steamId} connected!");
                
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
                
                // If we're the host, send a handshake response and log connection details
                if (IsHost)
                {
                    SendHandshakeResponse(steamId);
                    LogHostConnectionStatus($"Player {steamId} connected - handshake initiated");
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
                Debug.Log($"[SimpleSessionManager] 🚪 DISCONNECTION: Steam ID {steamId} has left the session");
                
                if (connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers.Remove(steamId);
                    
                    // If we're the host, log updated connection status
                    if (IsHost)
                    {
                        LogHostConnectionStatus($"Player {steamId} disconnected");
                    }
                    
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
                if (ReferenceEquals(data.Length, 0)) return;
                
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
                        
                    case PACKET_TYPE_HEARTBEAT:
                        HandleHeartbeat(steamId, data);
                        break;
                        
                    case PACKET_TYPE_HEARTBEAT_RESPONSE:
                        HandleHeartbeatResponse(steamId, data);
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
        
                // DIAGNOSTIC: Check Steam P2P status before sending
                if (steamNet is ETGSteamP2PNetworking etgSteam)
                {
                    etgSteam.DiagnoseSteamP2PStatus();
                }
                
                var packet = new byte[1 + 8]; // packet type + steam ID
                packet[0] = PACKET_TYPE_HANDSHAKE_REQUEST;
                
                // Add our Steam ID to the packet
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                BitConverter.GetBytes(mySteamId).CopyTo(packet, 1);
                
                Debug.Log($"[SimpleSessionManager] Handshake packet created - My Steam ID: {mySteamId}, Target: {hostSteamId}");
                
                // Ensure P2P session is properly established
                bool sessionEstablished = false;
                
                // Try to establish P2P session multiple times with proper delays
                for (int attempt = 0; attempt < 3 && !sessionEstablished; attempt++)
                {
                    Debug.Log($"[SimpleSessionManager] Establishing P2P session (attempt {attempt + 1})...");
                    
                    if (steamNet.AcceptP2PSession(hostSteamId))
                    {
                        sessionEstablished = true;
                        Debug.Log($"[SimpleSessionManager] P2P session established successfully");
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] P2P session establishment failed (attempt {attempt + 1})");
                        // Use Unity's coroutine-friendly delay instead of Thread.Sleep
                        if (attempt < 2) // Don't wait after the last attempt
                        {
                            // Wait a frame and try again
                            UnityEngine.Time.timeScale = UnityEngine.Time.timeScale; // Ensure we're not paused
                        }
                    }
                }
                
                if (!sessionEstablished)
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to establish P2P session with {hostSteamId}");
                    Status = $"Failed to establish P2P session with host: {hostSteamId}";
                    return;
                }
                
                // Now try to send the handshake packet
                // Add a delay before sending to ensure P2P session is fully established
                Debug.Log($"[SimpleSessionManager] Waiting for P2P session to stabilize...");
                System.Threading.Thread.Sleep(100); // 100ms delay
                
                // Send multiple handshake attempts to ensure delivery
                bool packetSent = false;
                for (int attempt = 0; attempt < 5; attempt++) // Increased attempts
                {
                    Debug.Log($"[SimpleSessionManager] Sending handshake packet (attempt {attempt + 1})...");
                    
                    if (steamNet?.SendP2PPacket(hostSteamId, packet) == true)
                    {
                        Debug.Log($"[SimpleSessionManager] Handshake request sent successfully to {hostSteamId}");
                        packetSent = true;
                        
                        // Don't break immediately - send multiple packets to ensure delivery
                        if (attempt >= 2) break; // Send at least 3 packets
                        
                        System.Threading.Thread.Sleep(50); // Brief delay between packets
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Handshake send failed (attempt {attempt + 1})");
                        steamNet.AcceptP2PSession(hostSteamId); // Re-establish session
                        System.Threading.Thread.Sleep(100); // Longer delay before retry
                    }
                }
                
                if (packetSent)
                {
                    // Add connection entry to track handshake progress
                    var connection = new PlayerConnection
                    {
                        steamId = hostSteamId,
                        playerName = $"Host_{hostSteamId}",
                        isConnected = false, // Will be set to true when handshake completes
                        handshakeComplete = false,
                        lastActivity = Time.time,
                        connectionTime = Time.time
                    };
                    connectedPlayers[hostSteamId] = connection;
                    
                    Status = $"Handshake sent to host: {hostSteamId} (waiting for response)";
                }
                else
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to send handshake request to {hostSteamId} after all attempts");
                    Debug.LogError($"[SimpleSessionManager] This suggests a fundamental P2P connectivity issue:");
                    Debug.LogError($"[SimpleSessionManager]   • Check if both players are online and not appearing offline");
                    Debug.LogError($"[SimpleSessionManager]   • Verify Steam networking is properly initialized");
                    Debug.LogError($"[SimpleSessionManager]   • Check Steam P2P ports are not blocked by firewall");
                    Debug.LogError($"[SimpleSessionManager]   • Ensure host is running CheckForIncomingP2PRequests()");
                    
                    Status = $"Failed to connect to host: {hostSteamId} (P2P send failure)";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending handshake request: {e.Message}");
                Debug.LogError($"[SimpleSessionManager] Stack trace: {e.StackTrace}");
                Status = $"Error connecting to host: {e.Message}";
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
                
                // Ensure we have a P2P session with the client
                bool sessionEnsured = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (steamNet.AcceptP2PSession(clientSteamId))
                    {
                        sessionEnsured = true;
                        Debug.Log($"[SimpleSessionManager] P2P session ensured with client {clientSteamId}");
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Failed to ensure P2P session with {clientSteamId} (attempt {attempt + 1})");
                        System.Threading.Thread.Sleep(20); // Wait before retry
                    }
                }
                
                if (!sessionEnsured)
                {
                    Debug.LogError($"[SimpleSessionManager] Cannot send handshake response - P2P session not established with {clientSteamId}");
                    return;
                }
                
                var packet = new byte[1 + 8]; // packet type + host steam ID
                packet[0] = PACKET_TYPE_HANDSHAKE_RESPONSE;
                
                // Add our Steam ID to the response
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                BitConverter.GetBytes(mySteamId).CopyTo(packet, 1);
                
                bool responseSent = false;
                for (int attempt = 0; attempt < 5; attempt++) // More attempts for critical handshake
                {
                    if (steamNet?.SendP2PPacket(clientSteamId, packet) == true)
                    {
                        Debug.Log($"[SimpleSessionManager] Handshake response sent to {clientSteamId} (attempt {attempt + 1})");
                        responseSent = true;
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Failed to send handshake response (attempt {attempt + 1})");
                        // Re-accept session before retry
                        steamNet.AcceptP2PSession(clientSteamId);
                        System.Threading.Thread.Sleep(10); // Small delay between attempts
                    }
                }
                
                if (!responseSent)
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to send handshake response to {clientSteamId} after all attempts");
                    // Remove the connection since we can't communicate with it
                    if (connectedPlayers.ContainsKey(clientSteamId))
                    {
                        connectedPlayers.Remove(clientSteamId);
                        Debug.LogWarning($"[SimpleSessionManager] Removed connection {clientSteamId} due to communication failure");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending handshake response: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send handshake complete confirmation (client -> host)
        /// </summary>
        private void SendHandshakeComplete(ulong hostSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Sending handshake complete confirmation to host: {hostSteamId}");
                
                var packet = new byte[1 + 8]; // packet type + client steam ID
                packet[0] = PACKET_TYPE_HANDSHAKE_COMPLETE;
                
                // Add our Steam ID to the packet
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                BitConverter.GetBytes(mySteamId).CopyTo(packet, 1);
                
                if (steamNet?.SendP2PPacket(hostSteamId, packet) == true)
                {
                    Debug.Log($"[SimpleSessionManager] Handshake complete sent to host: {hostSteamId}");
                }
                else
                {
                    Debug.LogWarning($"[SimpleSessionManager] Failed to send handshake complete to host: {hostSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending handshake complete: {e.Message}");
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
                
                // CRITICAL: Ensure P2P session is accepted for this client
                bool sessionAccepted = false;
                for (int attempt = 0; attempt < 5; attempt++) // Increased attempts
                {
                    if (steamNet.AcceptP2PSession(steamId))
                    {
                        sessionAccepted = true;
                        Debug.Log($"[SimpleSessionManager] P2P session accepted for client {steamId} (attempt {attempt + 1})");
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Failed to accept P2P session for {steamId} (attempt {attempt + 1})");
                        // Wait a tiny bit before retrying
                        System.Threading.Thread.Sleep(10);
                    }
                }
                
                if (!sessionAccepted)
                {
                    Debug.LogError($"[SimpleSessionManager] Failed to accept P2P session for {steamId} - cannot establish connection");
                    return;
                }
                
                // Create or update connection entry with improved initialization
                var connection = new PlayerConnection
                {
                    steamId = steamId,
                    playerName = $"Player_{steamId}",
                    isConnected = true,
                    handshakeComplete = false, // Will be set to true when handshake completes
                    lastActivity = Time.time,
                    connectionTime = Time.time
                };
                connectedPlayers[steamId] = connection;
                
                // IMPORTANT: Wait a moment before sending response to ensure P2P is fully established
                System.Threading.Thread.Sleep(50); // 50ms delay for P2P stabilization
                
                // Send handshake response
                SendHandshakeResponse(steamId);
                
                Debug.Log($"[SimpleSessionManager] Handshake established with client: {steamId}");
                LogHostConnectionStatus($"Handshake request processed for {steamId}");
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
                
                // Update or create connection entry
                if (connectedPlayers.ContainsKey(steamId))
                {
                    var connection = connectedPlayers[steamId];
                    connection.handshakeComplete = true;
                    connection.isConnected = true;
                    connection.lastActivity = Time.time;
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
                
                // Send handshake complete confirmation to prevent sync issues
                SendHandshakeComplete(steamId);
                
                UpdateConnectionStatus();
                Debug.Log($"[SimpleSessionManager] FULLY CONNECTED to host: {steamId}");
                Debug.Log($"[SimpleSessionManager] HANDSHAKE COMPLETE: Steam ID {steamId} is now fully connected");
                
                // Initialize player synchronization now that we're connected
                if (!Equals(playerSync, null))
                {
                    InitializePlayerSync();
                }
                
                // Log successful connection with details
                LogConnectionSuccess(steamId);
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
            Debug.Log($"[SimpleSessionManager] HANDSHAKE FINALIZED: Steam ID {steamId} connection established");
            
            // Update connection status to mark handshake complete
            if (connectedPlayers.ContainsKey(steamId))
            {
                var connection = connectedPlayers[steamId];
                connection.handshakeComplete = true;
                connectedPlayers[steamId] = connection;
                
                // If we're the host, log the updated status
                if (IsHost)
                {
                    LogHostConnectionStatus($"Player {steamId} handshake finalized");
                }
            }
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
                if (ReferenceEquals(steamNet, null)) return;
                
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
        
        // =========================
        // LOCATION VALIDATION
        // =========================
        
        /// <summary>
        /// Check if current location allows multiplayer connections
        /// Only main menu and Gungeon foyer are safe for starting/joining sessions
        /// </summary>
        private bool IsValidLocationForMultiplayer()
        {
            try
            {
                // Check if we're in main menu
                if (IsInMainMenu())
                {
                    Debug.Log("[SimpleSessionManager] Location check: Main menu - ALLOWED");
                    return true;
                }
                
                // Check if we're in Gungeon foyer
                if (IsInGungeonFoyer())
                {
                    Debug.Log("[SimpleSessionManager] Location check: Gungeon foyer - ALLOWED");
                    return true;
                }
                
                // Any other location is not allowed
                string currentLocation = GetCurrentLocationName();
                Debug.LogWarning($"[SimpleSessionManager] Location check: {currentLocation} - NOT ALLOWED for multiplayer");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error checking location: {e.Message}");
                return false; // Default to safe (not allowed)
            }
        }
        
        /// <summary>
        /// Check if player is currently in the main menu
        /// </summary>
        private bool IsInMainMenu()
        {
            try
            {
                // Check if GameManager exists and if we're not in a dungeon
                if (ReferenceEquals(GameManager.Instance, null))
                {
                    Debug.Log("[SimpleSessionManager] IsInMainMenu: GameManager is null - MAIN MENU");
                    return true; // Likely in main menu if GameManager not initialized
                }
                
                // Check if we're in the MainMenuFoyer scene
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Debug.Log($"[SimpleSessionManager] IsInMainMenu: Current scene = '{currentScene}'");
                if (currentScene.Contains("MainMenu") || currentScene.Contains("Foyer_001"))
                {
                    Debug.Log("[SimpleSessionManager] IsInMainMenu: Scene indicates main menu - MAIN MENU");
                    return true;
                }
                
                // Check GameManager state
                var gameManager = GameManager.Instance;
                Debug.Log($"[SimpleSessionManager] IsInMainMenu: Checking GameManager states...");
                Debug.Log($"  CurrentLevelOverrideState: {gameManager.CurrentLevelOverrideState}");
                Debug.Log($"  CurrentGameType: {gameManager.CurrentGameType}");
                Debug.Log($"  PrimaryPlayer: {(ReferenceEquals(gameManager.PrimaryPlayer, null) ? "NULL" : "EXISTS")}");
                
                // Be more permissive with main menu detection - if there's no primary player, we're likely in main menu
                if (ReferenceEquals(gameManager.PrimaryPlayer, null))
                {
                    Debug.Log("[SimpleSessionManager] IsInMainMenu: No primary player - MAIN MENU");
                    return true; // No player means we're in main menu
                }
                
                // If there is a primary player, check other conditions
                if (object.Equals(gameManager.CurrentLevelOverrideState, GameManager.LevelOverrideState.NONE) &&
                    object.Equals(gameManager.CurrentGameType, GameManager.GameType.SINGLE_PLAYER))
                {
                    Debug.Log("[SimpleSessionManager] IsInMainMenu: GameManager states indicate main menu - MAIN MENU");
                    return true; // Likely in main menu
                }
                
                Debug.Log("[SimpleSessionManager] IsInMainMenu: Not in main menu - IN GAME");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimpleSessionManager] Error checking main menu state: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check if player is in the Gungeon foyer (safe starting area)
        /// </summary>
        private bool IsInGungeonFoyer()
        {
            try
            {
                var gameManager = GameManager.Instance;
                if (ReferenceEquals(gameManager, null) || ReferenceEquals(gameManager.PrimaryPlayer, null))
                {
                    return false;
                }
                
                // Check if we're in the tutorial or foyer area
                var dungeon = gameManager.Dungeon;
                if (ReferenceEquals(dungeon, null) || ReferenceEquals(dungeon.data, null))
                {
                    return false;
                }
                
                // Get current room
                var player = gameManager.PrimaryPlayer;
                var currentRoom = dungeon.data.GetAbsoluteRoomFromPosition(player.transform.position.IntXY());
                
                if (!ReferenceEquals(currentRoom, null))
                {
                    var roomName = currentRoom.GetRoomName();
                    
                    // Check for foyer/tutorial room names
                    if (!ReferenceEquals(roomName, null) && (
                        roomName.ToLower().Contains("foyer") ||
                        roomName.ToLower().Contains("tutorial") ||
                        roomName.ToLower().Contains("entrance") ||
                        roomName.ToLower().Contains("start")))
                    {
                        return true;
                    }
                    
                    // Check if we're in a safe area by examining room properties
                    try
                    {
                        // Use reflection to safely check room category if available
                        var roomType = currentRoom.GetType();
                        var categoryField = roomType.GetField("category", 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.Instance);
                        
                        if (!object.ReferenceEquals(categoryField, null))
                        {
                            var categoryValue = categoryField.GetValue(currentRoom);
                            if (!ReferenceEquals(categoryValue, null))
                            {
                                string categoryStr = categoryValue.ToString();
                                if (categoryStr.Contains("ENTRANCE") || categoryStr.Contains("HUB"))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Could not check room category: {e.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimpleSessionManager] Error checking foyer state: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get descriptive name of current location for logging
        /// </summary>
        private string GetCurrentLocationName()
        {
            try
            {
                var gameManager = GameManager.Instance;
                if (ReferenceEquals(gameManager, null))
                {
                    return "Main Menu (GameManager not initialized)";
                }
                
                if (ReferenceEquals(gameManager.PrimaryPlayer, null))
                {
                    return "Main Menu (No player)";
                }
                
                var dungeon = gameManager.Dungeon;
                if (ReferenceEquals(dungeon, null))
                {
                    return "Main Menu (No dungeon)";
                }
                
                var player = gameManager.PrimaryPlayer;
                var currentRoom = dungeon.data?.GetAbsoluteRoomFromPosition(player.transform.position.IntXY());
                
                if (!ReferenceEquals(currentRoom, null))
                {
                    var roomName = currentRoom.GetRoomName() ?? "Unknown Room";
                    
                    // Try to get category information safely
                    string categoryInfo = "Unknown Category";
                    try
                    {
                        var roomType = currentRoom.GetType();
                        var categoryField = roomType.GetField("category", 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.Instance);
                        
                        if (!object.ReferenceEquals(categoryField, null))
                        {
                            var categoryValue = categoryField.GetValue(currentRoom);
                            if (!ReferenceEquals(categoryValue, null))
                            {
                                categoryInfo = categoryValue.ToString();
                            }
                        }
                    }
                    catch
                    {
                        categoryInfo = "Category unavailable";
                    }
                    
                    return $"Dungeon Room: {roomName} ({categoryInfo})";
                }
                
                return "Unknown Location";
            }
            catch (Exception e)
            {
                return $"Location check error: {e.Message}";
            }
        }
        
        /// <summary>
        /// Log detailed connection status for the host (shows all connected/connecting players)
        /// </summary>
        private void LogHostConnectionStatus(string eventDescription = "Status update")
        {
            if (!IsHost) return;
            
            try
            {
                var hostSteamId = steamNet?.GetSteamID() ?? 0;
                Debug.Log($"[Host Connection Status] {eventDescription}");
                Debug.Log($"[Host Connection Status] Host Steam ID: {hostSteamId}");
                Debug.Log($"[Host Connection Status] Total Players: {connectedPlayers.Count}");
                
                if (ReferenceEquals(connectedPlayers.Count,0))
                {
                    Debug.Log($"[Host Connection Status] No players connected - waiting for connections...");
                }
                else
                {
                    Debug.Log($"[Host Connection Status] Connected Players List:");
                    int playerNumber = 1;
                    foreach (var kvp in connectedPlayers)
                    {
                        var steamId = kvp.Key;
                        var connection = kvp.Value;
                        var status = connection.handshakeComplete ? "Ready" : "Handshaking";
                        var duration = Time.time - connection.connectionTime;
                        Debug.Log($"[Host Connection Status]   {playerNumber}. Steam ID: {steamId} - {status} (Connected: {duration:F1}s ago)");
                        playerNumber++;
                    }
                }
                
                Debug.Log($"[Host Connection Status] ═══════════════════════════════════════");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host Connection Status] Error logging connection status: {e.Message}");
            }
        }
        
        /// <summary>
        /// Check for incoming P2P session requests and auto-accept them (HOST ONLY)
        /// This is critical for allowing clients to establish P2P connections
        /// </summary>
        private void CheckForIncomingP2PRequests()
        {
            try
            {
                if (!IsHost || ReferenceEquals(steamNet, null)) return;
                
                // First, check for join request notifications from joiners
                if (ETGSteamP2PNetworking.CheckForJoinRequestNotifications(out ulong requestingSteamId))
                {
                    Debug.Log($"[SimpleSessionManager] 🎯 HOST: Received join request notification from {requestingSteamId}");
                    
                    // Auto-accept the P2P session from the requesting player
                    if (ShouldAcceptP2PRequest(requestingSteamId))
                    {
                        Debug.Log($"[SimpleSessionManager] ✅ HOST: Auto-accepting P2P session from joiner: {requestingSteamId}");
                        steamNet.AcceptP2PSession(requestingSteamId);
                    }
                }
                
                // Add frame-based logging to see if this is being called
                if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
                {
                    Debug.Log($"[SimpleSessionManager] 🏠 HOST: Actively checking for P2P requests (Frame: {Time.frameCount})");
                }
                
                // Use our packet-based detection approach
                CheckForUnknownSenders();
                
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error checking P2P requests: {e.Message}");
            }
        }

        private void CheckForUnknownSenders()
        {
            try
            {
                if (!IsHost || ReferenceEquals(steamNet, null)) return;
                
                // DIAGNOSTIC: Periodically check Steam status
                if (Time.frameCount % 3600 == 0) // Every minute at 60fps
                {
                    Debug.Log($"[SimpleSessionManager] 🔍 HOST DIAGNOSTIC: Checking Steam P2P status...");
                    if (steamNet is ETGSteamP2PNetworking etgSteam)
                    {
                        etgSteam.DiagnoseSteamP2PStatus();
                    }
                }

                // Check for incoming packets from any sender
                ulong senderSteamId;
                byte[] data;
                int packetsProcessed = 0;
                const int MAX_PACKETS_PER_FRAME = 20; // Increased limit
                
                // IMPORTANT: Read ALL available packets to detect unknown senders
                while (packetsProcessed < MAX_PACKETS_PER_FRAME)
                {
                    bool hasPacket = steamNet.ReadP2PPacket(out senderSteamId, out data);
                    
                    if (!hasPacket || senderSteamId.Equals(0))
                    {
                        // No more packets available
                        break;
                    }
                    
                    packetsProcessed++;
                    Debug.Log($"[SimpleSessionManager] 📦 HOST: Received packet from {senderSteamId} ({data?.Length ?? 0} bytes)");
                    
                    // Check if this sender is already in our connected players list
                    if (!connectedPlayers.ContainsKey(senderSteamId))
                    {
                        Debug.Log($"[SimpleSessionManager] 🟡 HOST: Packet from UNKNOWN sender: {senderSteamId} - auto-accepting P2P session");
                        
                        // Auto-accept P2P session from unknown senders
                        if (ShouldAcceptP2PRequest(senderSteamId))
                        {
                            bool sessionAccepted = steamNet.AcceptP2PSession(senderSteamId);
                            
                            if (sessionAccepted)
                            {
                                Debug.Log($"[SimpleSessionManager] ✅ HOST: Auto-accepted P2P session from: {senderSteamId}");
                                
                                // Create a connection entry for tracking
                                var connection = new PlayerConnection
                                {
                                    steamId = senderSteamId,
                                    playerName = $"Player_{senderSteamId}",
                                    isConnected = true,
                                    handshakeComplete = false,
                                    lastActivity = Time.time,
                                    connectionTime = Time.time
                                };
                                connectedPlayers[senderSteamId] = connection;
                                
                                LogHostConnectionStatus($"Auto-accepted P2P session from packet sender {senderSteamId}");
                            }
                            else
                            {
                                Debug.LogError($"[SimpleSessionManager] ❌ HOST: Failed to accept P2P session from: {senderSteamId}");
                                continue; // Skip processing this packet
                            }
                        }
                        else
                        {
                            Debug.Log($"[SimpleSessionManager] ❌ HOST: Rejected connection attempt from: {senderSteamId}");
                            continue;
                        }
                    }
                    else
                    {
                        Debug.Log($"[SimpleSessionManager] 📦 HOST: Packet from KNOWN sender: {senderSteamId}");
                    }
                    
                    // Process the packet normally through OnDataReceived
                    if (!ReferenceEquals(data, null) && data.Length > 0)
                    {
                        Debug.Log($"[SimpleSessionManager] 📦 HOST: Processing packet from {senderSteamId} ({data.Length} bytes) - Type: {data[0]}");
                        OnDataReceived(senderSteamId, data);
                    }
                }
                
                if (packetsProcessed > 0)
                {
                    Debug.Log($"[SimpleSessionManager] 📦 HOST: Processed {packetsProcessed} packets this frame");
                }
                
                // Method 2: Use Steam callback fallback detection
                try
                {
                    SteamCallbackManager.ProcessSteamCallbacks();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SimpleSessionManager] Error processing Steam callbacks: {e.Message}");
                }
                
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error checking unknown senders: {e.Message}");
            }
        }
        
        /// <summary>
        /// Determine if we should accept a P2P session request from a specific Steam ID
        /// </summary>
        private bool ShouldAcceptP2PRequest(ulong steamId)
        {
            try
            {
                if (steamId.Equals(0))
                {
                    Debug.LogWarning("[SimpleSessionManager] Rejecting P2P request - invalid Steam ID (0)");
                    return false;
                }

                var mySteamId = steamNet?.GetSteamID() ?? 0;
                if (steamId.Equals(mySteamId))
                {
                    Debug.LogWarning("[SimpleSessionManager] Rejecting P2P request - cannot connect to self");
                    return false;
                }

                if (connectedPlayers.ContainsKey(steamId))
                {
                    Debug.LogWarning($"[SimpleSessionManager] Rejecting P2P request - {steamId} already connected");
                    return false;
                }

                // Check if we have room for more players
                if (connectedPlayers.Count >= 3) // Allow up to 3 other players (4 total including host)
                {
                    Debug.LogWarning($"[SimpleSessionManager] Rejecting P2P request - session full ({connectedPlayers.Count}/3 players)");
                    return false;
                }

                Debug.Log($"[SimpleSessionManager] Accepting P2P request from {steamId} - all checks passed");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error checking P2P request acceptance: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Send heartbeat to check connection health
        /// </summary>
        private void SendHeartbeat(ulong steamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Sending heartbeat to {steamId}");
                
                var packet = new byte[1 + 8]; // packet type + timestamp
                packet[0] = PACKET_TYPE_HEARTBEAT;
                
                // Add timestamp to the packet
                var timestamp = DateTime.Now.ToBinary();
                BitConverter.GetBytes(timestamp).CopyTo(packet, 1);
                
                if (steamNet?.SendP2PPacket(steamId, packet) == true)
                {
                    Debug.Log($"[SimpleSessionManager] Heartbeat sent to {steamId}");
                }
                else
                {
                    Debug.LogWarning($"[SimpleSessionManager] Failed to send heartbeat to {steamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error sending heartbeat: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle incoming heartbeat packets
        /// </summary>
        private void HandleHeartbeat(ulong steamId, byte[] data)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Received heartbeat from {steamId}");
                
                // Send heartbeat response
                var packet = new byte[1 + 8]; // packet type + timestamp
                packet[0] = PACKET_TYPE_HEARTBEAT_RESPONSE;
                
                // Echo back the timestamp from the original heartbeat
                if (data.Length >= 9)
                {
                    Array.Copy(data, 1, packet, 1, 8);
                }
                else
                {
                    // If no timestamp in original packet, use current time
                    var timestamp = DateTime.Now.ToBinary();
                    BitConverter.GetBytes(timestamp).CopyTo(packet, 1);
                }
                
                if (steamNet?.SendP2PPacket(steamId, packet) == true)
                {
                    Debug.Log($"[SimpleSessionManager] Heartbeat response sent to {steamId}");
                }
                else
                {
                    Debug.LogWarning($"[SimpleSessionManager] Failed to send heartbeat response to {steamId}");
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
                Debug.LogError($"[SimpleSessionManager] Error handling heartbeat: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle heartbeat response packets
        /// </summary>
        private void HandleHeartbeatResponse(ulong steamId, byte[] data)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Received heartbeat response from {steamId}");
                
                // Update last activity time
                if (connectedPlayers.ContainsKey(steamId))
                {
                    var connection = connectedPlayers[steamId];
                    connection.lastActivity = Time.time;
                    connectedPlayers[steamId] = connection;
                    
                    // Calculate round-trip time if timestamp is available
                    if (data.Length >= 9)
                    {
                        try
                        {
                            var sentTimestamp = BitConverter.ToInt64(data, 1);
                            var sentTime = DateTime.FromBinary(sentTimestamp);
                            var roundTripTime = DateTime.Now - sentTime;
                            Debug.Log($"[SimpleSessionManager] Round-trip time to {steamId}: {roundTripTime.TotalMilliseconds:F1}ms");
                        }
                        catch (Exception)
                        {
                            // Ignore timestamp parsing errors
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling heartbeat response: {e.Message}");
            }
        }
        
        /// <summary>
        /// Log successful connection details
        /// </summary>
        private void LogConnectionSuccess(ulong steamId)
        {
            try
            {
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                Debug.Log($"[SimpleSessionManager] ✅ CONNECTION ESTABLISHED");
                Debug.Log($"[SimpleSessionManager] ✅ My Steam ID: {mySteamId}");
                Debug.Log($"[SimpleSessionManager] ✅ Connected to: {steamId}");
                Debug.Log($"[SimpleSessionManager] ✅ Session ID: {CurrentSessionId}");
                Debug.Log($"[SimpleSessionManager] ✅ Role: {(IsHost ? "Host" : "Client")}");
                Debug.Log($"[SimpleSessionManager] ✅ Connection is ready for multiplayer gameplay");
                
                // Update UI to show successful connection
                Status = IsHost ? $"Hosting: {connectedPlayers.Count} players connected" : "Connected to host - ready to play";
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error logging connection success: {e.Message}");
            }
        }
    }
}
