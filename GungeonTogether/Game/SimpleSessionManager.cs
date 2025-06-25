using System;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Steam-compatible session manager for GungeonTogether multiplayer
    /// Supports Steam P2P networking and session management
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
        public SimpleSessionManager()
        {
            IsActive = false;
            Status = "Ready";
            CurrentSessionId = null;
            IsHost = false;
            
            // Don't initialize Steam networking in constructor - do it lazily when needed
            Debug.Log("[SimpleSessionManager] Created session manager (Steam networking will be initialized on demand)");
            steamNet = null;
            
            Debug.Log("[SimpleSessionManager] Steam-compatible session manager initialized");
        }        public void StartSession()
        {
            IsActive = true;
            IsHost = true;
            Status = "Starting Steam P2P session...";
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Start hosting with ETG's Steam P2P networking
            if (steamNet != null && steamNet.IsAvailable())
            {
                var steamId = steamNet.GetSteamID();
                if (steamId != 0)
                {
                    CurrentSessionId = $"steam_{steamId}";
                    Status = $"Hosting P2P: {steamId}";
                    Debug.Log($"[SimpleSessionManager] Hosting Steam P2P session: {steamId}");
                    
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
            Status = $"Joining Steam P2P session: {sessionId}";
            CurrentSessionId = sessionId;
            
            // Initialize Steam networking if not already done
            EnsureSteamNetworkingInitialized();
            
            // Extract Steam ID from session format and initiate P2P connection
            if (steamNet != null && steamNet.IsAvailable())
            {
                ulong hostSteamId = ExtractSteamIdFromSession(sessionId);
                if (hostSteamId != 0)
                {
                    // Setup Rich Presence for joining
                    steamNet.StartJoiningSession(hostSteamId);
                    
                    // Accept P2P session with host
                    if (steamNet.AcceptP2PSession(hostSteamId))
                    {
                        Status = $"Connected to P2P: {hostSteamId}";
                        Debug.Log($"[SimpleSessionManager] Connected to Steam P2P session: {hostSteamId}");
                    }
                    else
                    {
                        Status = "Failed to connect";
                        Debug.LogError($"[SimpleSessionManager] Failed to connect to P2P session: {hostSteamId}");
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
                Debug.Log($"[SimpleSessionManager] Stopped hosting session: {sessionId}");
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
                Debug.Log($"[SimpleSessionManager] Join request received from Steam ID: {joinerSteamId}");
                
                if (IsHost && steamNet != null)
                {
                    // Handle the join request
                    steamNet.HandleJoinRequest(joinerSteamId);
                    Debug.Log($"[SimpleSessionManager] Processed join request from {joinerSteamId}");
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
        
    }
}
