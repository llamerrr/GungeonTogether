using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;

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
        public string currentHostId { get; private set; }
        public bool IsHost { get; private set; }

        // Steam P2P networking using ETG's built-in Steamworks (stored as interface to avoid TypeLoadException)
        private ISteamNetworking steamNet;

        // Player synchronization
        private PlayerSynchroniser playerSync;

        // Real connection tracking
        private Dictionary<ulong, PlayerConnection> connectedPlayers;
        private float lastConnectionCheck;
        private const float CONNECTION_CHECK_INTERVAL = 1.0f;

        // Connection status logging
        private float lastStatusLog;
        private const float STATUS_LOG_INTERVAL = 60.0f; // Log status every 10 seconds

        public struct PlayerConnection
        {
            public ulong steamId;
            public string playerName;
            public bool isConnected;
            public float lastActivity;
            public float connectionTime;
        }

        public SimpleSessionManager()
        {
            IsActive = false;
            Status = "Ready";
            currentHostId = null;
            IsHost = false;
            connectedPlayers = new Dictionary<ulong, PlayerConnection>();
            steamNet = null;
            GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Steam-compatible session manager initialized");
        }

        // Subscribe to player join events after Steam networking is initialized
        private void SubscribeToSteamEvents()
        {
            if (!ReferenceEquals(steamNet, null))
            {
                steamNet.OnPlayerJoined += OnPlayerJoined;
            }
        }

        // Handler for new player joining the lobby (host only)
        private void OnPlayerJoined(ulong steamId, string lobbyId)
        {
            if (!connectedPlayers.ContainsKey(steamId))
            {
                connectedPlayers[steamId] = new PlayerConnection
                {
                    steamId = steamId,
                    playerName = $"Player_{steamId}",
                    isConnected = true,
                    lastActivity = Time.time,
                    connectionTime = Time.time
                };
                GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Player joined lobby: {steamId} (lobby {lobbyId})");
                UpdateConnectionStatus();
            }
        }

        public void StartSession()
        {
            if (!IsValidLocationForMultiplayer())
            {
                string currentLocation = GetCurrentLocationName();
                Status = $"Cannot start session from: {currentLocation}";
                GungeonTogether.Logging.Debug.LogWarning($"[SimpleSessionManager] Cannot start multiplayer session from current location: {currentLocation}");
                GungeonTogether.Logging.Debug.LogWarning("[SimpleSessionManager] Multiplayer can only be started from Main Menu or Gungeon Foyer");
                return;
            }
            else
            {
                IsActive = true;
                IsHost = true;
                Status = "Starting Steam session...";
                connectedPlayers.Clear();
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Location validated - starting multiplayer session");
                EnsureSteamNetworkingInitialized();
                SubscribeToSteamEvents();
                InitializePlayerSync();

                // Initialize NetworkManager as host
                var lobbyId = SteamCallbackManager.Instance.GetCurrentLobbyId();
                GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Attempting to initialize NetworkManager as host with lobby ID: {lobbyId}");
                if (NetworkManager.Instance.InitializeAsHost(lobbyId))
                {
                    GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] NetworkManager initialized as host successfully");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError("[SimpleSessionManager] Failed to initialize NetworkManager as host");
                }

                SteamCallbackManager.Instance.HostLobby();
            }

        }

        public void JoinSession(string sessionId)
        {
            GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager][DEBUG] JoinSession called with sessionId: {sessionId}");
            GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager][DEBUG] Before join - IsActive: {IsActive}, IsHost: {IsHost}");

            if (!IsValidLocationForMultiplayer())
            {
                string currentLocation = GetCurrentLocationName();
                Status = $"Cannot join session from: {currentLocation}";
                GungeonTogether.Logging.Debug.LogWarning($"[SimpleSessionManager] Cannot join multiplayer session from current location: {currentLocation}");
                GungeonTogether.Logging.Debug.LogWarning("[SimpleSessionManager] Multiplayer can only be joined from Main Menu or Gungeon Foyer");
                return;
            }
            IsActive = true;
            IsHost = false;
            Status = $"Connecting to Steam session: {sessionId}";
            currentHostId = sessionId;
            connectedPlayers.Clear();

            GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager][DEBUG] After setting flags - IsActive: {IsActive}, IsHost: {IsHost}, currentHostId: {currentHostId}");
            GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Location validated - joining multiplayer session");
            EnsureSteamNetworkingInitialized();
            if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
            {
                // Only accept lobby IDs (not host Steam IDs)
                ulong lobbyId = 0;
                if (sessionId.StartsWith("lobby_") && ulong.TryParse(sessionId.Substring(6), out lobbyId) && !ReferenceEquals(lobbyId, 0UL))
                {
                    GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Initiating connection to lobby: {lobbyId}");
                    steamNet.JoinLobby(lobbyId);
                    Status = $"Connected to lobby: {lobbyId}";
                }
                else
                {
                    Status = "Invalid session format (must be lobby ID)";
                    GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Invalid session format: {sessionId}");
                    return;
                }
            }
            else
            {
                Status = $"Joined offline: {sessionId}";
                GungeonTogether.Logging.Debug.LogWarning("[SimpleSessionManager] Steam not available - joining offline session");
            }
            InitializePlayerSync();

            // Initialize NetworkManager as client
            // Try to get the host Steam ID from the lobby
            ulong hostSteamId = 0UL;
            try
            {
                // If we joined a lobby, try to get the lobby owner's Steam ID
                if (sessionId.StartsWith("lobby_") && ulong.TryParse(sessionId.Substring(6), out ulong joinedLobbyId))
                {
                    // This would need to be implemented in Steam callback manager
                    // For now, use a placeholder
                    hostSteamId = SteamCallbackManager.Instance.GetLobbyOwnerSteamId(joinedLobbyId);
                }
                if (hostSteamId == 0UL)
                {
                    hostSteamId = 76561198000000000UL; // Fallback placeholder
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SimpleSessionManager] Could not get host Steam ID: {e.Message}");
                hostSteamId = 76561198000000000UL; // Fallback placeholder
            }

            GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Attempting to initialize NetworkManager as client connecting to host: {hostSteamId}");
            if (NetworkManager.Instance.InitializeAsClient(hostSteamId))
            {
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] NetworkManager initialized as client successfully");
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[SimpleSessionManager] Failed to initialize NetworkManager as client");
            }
        }

        public void StopSession()
        {
            var wasHosting = IsHost;
            var sessionId = currentHostId;
            IsActive = false;
            IsHost = false;
            currentHostId = null;
            Status = "Stopped";
            connectedPlayers.Clear();
            if (wasHosting)
            {
                ETGSteamP2PNetworking.UnregisterAsHost();
                SteamSessionHelper.UpdateRichPresence(false, null);
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Unregistered from GungeonTogether host registry");
            }
            playerSync?.Cleanup();
            playerSync = null;
            if (wasHosting)
            {
                GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Stopped hosting session: {sessionId} ({connectedPlayers.Count} players disconnected)");
            }
            else
            {
                GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Left session: {sessionId}");
            }
        }

        public void Update()
        {
            try
            {
                if (!IsActive) return;

                // Ensure PlayerSynchroniser is properly initialized when player is available
                if (playerSync != null && GameManager.Instance?.PrimaryPlayer != null)
                {
                    // Re-initialize if the local player wasn't available during first initialization
                    var playerSyncType = playerSync.GetType();
                    var localPlayerField = playerSyncType.GetField("localPlayer",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (localPlayerField != null)
                    {
                        var localPlayer = localPlayerField.GetValue(playerSync);
                        if (localPlayer == null)
                        {
                            GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Re-initializing PlayerSynchroniser now that player is available");
                            playerSync.Initialize();
                        }
                    }
                }

                // Update networking
                NetworkManager.Instance.Update();

                playerSync?.Update();

                // Poll for new lobby members every second if hosting
                if (IsHost && Time.time - lastConnectionCheck >= CONNECTION_CHECK_INTERVAL)
                {
                    GungeonTogether.Steam.SteamHostManager.CheckForLobbyJoins();
                    lastConnectionCheck = Time.time;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Error in Update: {e.Message}");
            }
        }


        private void InitializePlayerSync()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Initializing player synchronization...");
                playerSync = PlayerSynchroniser.Instance;
                playerSync.Initialize(); // Explicitly call Initialize
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Player synchronization initialized successfully");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Failed to initialize player sync: {e.Message}");
            }
        }

        private string GenerateSessionId()
        {
            return $"gungeon_session_{DateTime.Now.Ticks % 1000000}";
        }

        private void EnsureSteamNetworkingInitialized()
        {
            if (!ReferenceEquals(steamNet, null)) return;
            try
            {
                GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] Attempting to initialize ETG Steam P2P networking...");
                steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    GungeonTogether.Logging.Debug.Log("[SimpleSessionManager] ETG Steam P2P networking initialized successfully");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SimpleSessionManager] Steam not available - will run in offline mode");
                    steamNet = null;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Failed to initialize Steam P2P networking: {e.Message}");
                GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Stack trace: {e.StackTrace}");
                steamNet = null;
            }
        }

        private void OnPlayerDisconnected(ulong steamId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Player {steamId} disconnected");
                if (connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers.Remove(steamId);
                    if (IsHost)
                    {
                        LogHostConnectionStatus($"Player {steamId} disconnected");
                    }
                    // TODO: Add OnPlayerDisconnected method to PlayerSynchroniser
                    // playerSync?.OnPlayerDisconnected(steamId);
                }
                UpdateConnectionStatus();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SimpleSessionManager] Error handling player disconnection: {e.Message}");
            }
        }

        private void UpdateConnectionStatus()
        {
            Status = IsHost ? $"Hosting session ({connectedPlayers.Count} players)" : $"Connected to host";
        }

        private void LogHostConnectionStatus(string context)
        {
            GungeonTogether.Logging.Debug.Log($"[SimpleSessionManager] Host status: {context} | Players: {connectedPlayers.Count}");
        }

        private bool IsValidLocationForMultiplayer()
        {
            // Placeholder: always allow for now
            return true;
        }

        private string GetCurrentLocationName()
        {
            // Placeholder: return dummy location
            return "Main Menu";
        }

        /// <summary>
        /// Returns a list of connected player Steam IDs (host only).
        /// </summary>
        public List<ulong> ConnectedPlayerSteamIds
        {
            get { return new List<ulong>(connectedPlayers.Keys); }
        }
    }
}
