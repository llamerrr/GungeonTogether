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
        public string currentHostId { get; private set; }
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
            Debug.Log("[SimpleSessionManager] Steam-compatible session manager initialized");
        }
        
        public void StartSession()
        {
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
            Status = "Starting Steam session...";
            connectedPlayers.Clear();
            Debug.Log("[SimpleSessionManager] Location validated - starting multiplayer session");
            EnsureSteamNetworkingInitialized();
            if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
            {
                var steamId = steamNet.GetSteamID();
                if (!ReferenceEquals(steamId, null) && (!ReferenceEquals(steamId, 0)))
                {
                    currentHostId = $"steam_{steamId}";
                    IsHost = true;
                    Status = $"Hosting: {steamId} (Waiting for connections)";
                    Debug.Log($"[SimpleSessionManager] Hosting Steam session with ID: {currentHostId}");
                    UpdateSteamNetworking();
                    ETGSteamP2PNetworking.RegisterAsHost();
                    SteamSessionHelper.UpdateRichPresence(true, currentHostId);
                    if (!ReferenceEquals(steamNet, null))
                    {
                        steamNet.StartHostingSession();
                    }
                }
                else if (!ReferenceEquals(steamId, 0))
                {
                    currentHostId = $"steam_{steamId}";
                    Status = $"Hosting: {steamId} (Waiting for connections)";
                    ETGSteamP2PNetworking.RegisterAsHost();
                    SteamSessionHelper.UpdateRichPresence(true, currentHostId);
                }
                else
                {
                    Status = "Failed to start Steam session";
                    Debug.LogError("[SimpleSessionManager] Could not get Steam ID for hosting");
                }
            }
            else
            {
                currentHostId = GenerateSessionId();
                Status = $"Hosting offline: {currentHostId}";
                Debug.LogWarning("[SimpleSessionManager] Steam not available - hosting offline session");
            }
            InitializePlayerSync();
        }
        
        public void JoinSession(string sessionId)
        {
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
            Status = $"Connecting to Steam session: {sessionId}";
            currentHostId = sessionId;
            connectedPlayers.Clear();
            Debug.Log("[SimpleSessionManager] Location validated - joining multiplayer session");
            EnsureSteamNetworkingInitialized();
            if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
            {
                ulong hostSteamId = ExtractSteamIdFromSession(sessionId);
                if (!ReferenceEquals(hostSteamId, 0))
                {
                    Debug.Log($"[SimpleSessionManager] Initiating connection to host: {hostSteamId}");
                    steamNet.StartJoiningSession(hostSteamId);
                    Status = $"Connected to host: {hostSteamId}";
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
                Status = $"Joined offline: {sessionId}";
                Debug.LogWarning("[SimpleSessionManager] Steam not available - joining offline session");
            }
            InitializePlayerSync();
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
                Debug.Log("[SimpleSessionManager] Unregistered from GungeonTogether host registry");
            }
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
                playerSync?.Update();
                CheckConnections();
                UpdateSteamNetworking();
                // Log lobby joins if hosting
                if (IsHost)
                {
                    SteamHostManager.PollAndLogLobbyJoins();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error in Update: {e.Message}");
            }
        }
        
        private void UpdateSteamNetworking()
        {
            try
            {
                // Removed call to steamNet.Update() (legacy)
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error updating Steam networking: {e.Message}");
            }
        }
        
        private void InitializePlayerSync()
        {
            try
            {
                playerSync = new PlayerSynchronizer(this);
                Debug.Log("[SimpleSessionManager] Player synchronization initialized (ready for networking integration)");
            } catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Failed to initialize player sync: {e.Message}");
            }
        }
        
        private ulong ExtractSteamIdFromSession(string sessionId)
        {
            try
            {
                if (sessionId.StartsWith("steam_"))
                {
                    string steamIdStr = sessionId.Substring(6);
                    if (ulong.TryParse(steamIdStr, out ulong steamId))
                    {
                        return steamId;
                    }
                }
                else if (sessionId.StartsWith("friend_"))
                {
                    string[] parts = sessionId.Split('_');
                    if (parts.Length >= 2 && ulong.TryParse(parts[1], out ulong steamId))
                    {
                        return steamId;
                    }
                }
                else if (sessionId.StartsWith("steam_lobby_"))
                {
                    string lobbyPart = sessionId.Substring("steam_lobby_".Length);
                    if (ulong.TryParse(lobbyPart, out ulong lobbyId))
                    {
                        Debug.Log($"[SimpleSessionManager] Extracted lobby ID: {lobbyId}, getting host Steam ID...");
                        ulong hostSteamId = SteamReflectionHelper.GetLobbyOwner(lobbyId);
                        if (hostSteamId != 0)
                        {
                            Debug.Log($"[SimpleSessionManager] ✅ Lobby {lobbyId} is owned by Steam ID: {hostSteamId}");
                            return hostSteamId;
                        }
                        else
                        {
                            Debug.LogWarning($"[SimpleSessionManager] ❌ Could not get lobby owner for lobby {lobbyId} - lobby may not exist or Steam not ready");
                            return 0;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Failed to parse lobby ID from: {lobbyPart}");
                        return 0;
                    }
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
        
        private string GenerateSessionId()
        {
            return $"gungeon_session_{DateTime.Now.Ticks % 1000000}";
        }
        
        private void EnsureSteamNetworkingInitialized()
        {
            if (!ReferenceEquals(steamNet, null)) return;
            try
            {
                Debug.Log("[SimpleSessionManager] Attempting to initialize ETG Steam P2P networking...");
                steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    Debug.Log("[SimpleSessionManager] ETG Steam P2P networking initialized successfully");
                }
                else
                {
                    Debug.LogWarning("[SimpleSessionManager] Steam not available - will run in offline mode");
                    steamNet = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Failed to initialize Steam P2P networking: {e.Message}");
                Debug.LogError($"[SimpleSessionManager] Stack trace: {e.StackTrace}");
                steamNet = null;
            }
        }
        
        private void CheckConnections()
        {
            try
            {
                if (Time.time - lastConnectionCheck < CONNECTION_CHECK_INTERVAL) return;
                lastConnectionCheck = Time.time;
                if (IsHost && Time.time - lastStatusLog >= STATUS_LOG_INTERVAL)
                {
                    lastStatusLog = Time.time;
                    LogHostConnectionStatus("Periodic status report");
                }
                var playersToRemove = new List<ulong>();
                foreach (var kvp in connectedPlayers)
                {
                    var steamId = kvp.Key;
                    var connection = kvp.Value;
                    float timeSinceLastActivity = Time.time - connection.lastActivity;
                    float timeSinceConnection = Time.time - connection.connectionTime;
                    if (!connection.isConnected)
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Player {steamId} is not connected (removing)");
                        playersToRemove.Add(steamId);
                    }
                    else if (timeSinceLastActivity > 60.0f)
                    {
                        Debug.LogWarning($"[SimpleSessionManager] Player {steamId} timeout - no activity for {timeSinceLastActivity:F1}s (removing)");
                        playersToRemove.Add(steamId);
                    }
                }
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
        
        private void OnPlayerDisconnected(ulong steamId)
        {
            try
            {
                Debug.Log($"[SimpleSessionManager] Player {steamId} disconnected");
                if (connectedPlayers.ContainsKey(steamId))
                {
                    connectedPlayers.Remove(steamId);
                    if (IsHost)
                    {
                        LogHostConnectionStatus($"Player {steamId} disconnected");
                    }
                    playerSync?.OnPlayerDisconnected(steamId);
                }
                UpdateConnectionStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleSessionManager] Error handling player disconnection: {e.Message}");
            }
        }
        
        private void UpdateConnectionStatus()
        {
            Status = IsHost ? $"Hosting session ({connectedPlayers.Count} players)" : $"Connected to host";
        }
        
        private void LogHostConnectionStatus(string context)
        {
            Debug.Log($"[SimpleSessionManager] Host status: {context} | Players: {connectedPlayers.Count}");
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
    }
}
