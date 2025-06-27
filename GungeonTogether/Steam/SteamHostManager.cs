using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Manages host discovery and multiplayer session management
    /// </summary>
    public static class SteamHostManager
    {
        // Steam invite handling
        private static ulong lastInvitedBySteamId = 0;
        private static string lastInviteLobbyId = "";
        private static ulong currentHostSteamId = 0; // Track who is currently hosting
        private static bool isCurrentlyHosting = false;
        
        // Automatic host discovery system
        private static Dictionary<ulong, HostInfo> availableHosts = new Dictionary<ulong, HostInfo>();
        
        // Current lobby state
        private static ulong currentLobbyId = 0;
        private static bool isLobbyHost = false;
        
        public struct HostInfo
        {
            public ulong steamId;
            public string sessionName;
            public int playerCount;
            public float lastSeen;
            public bool isActive;
        }
        
        /// <summary>
        /// Automatically set invite info when Steam overlay invite is clicked
        /// This captures the real Steam ID from Steam's callback system
        /// </summary>
        public static void SetInviteInfo(ulong hostSteamId, string lobbyId = "")
        {
            lastInvitedBySteamId = hostSteamId;
            lastInviteLobbyId = lobbyId;
            Debug.Log($"[ETGSteamP2P] Auto-received invite from Steam ID: {hostSteamId}");
            
            // Add to available hosts if not already there
            if (!availableHosts.ContainsKey(hostSteamId))
            {
                availableHosts[hostSteamId] = new HostInfo
                {
                    steamId = hostSteamId,
                    sessionName = "Friend's Session",
                    playerCount = 1,
                    lastSeen = Time.time,
                    isActive = true
                };
                Debug.Log($"[ETGSteamP2P] Added host from invite: {hostSteamId}");
            }
        }
        
        /// <summary>
        /// Get the Steam ID of the last person who invited this player
        /// Returns 0 if no invite is available
        /// </summary>
        public static ulong GetLastInviterSteamId()
        {
            return lastInvitedBySteamId;
        }
        
        /// <summary>
        /// Get the most recent available host Steam ID for automatic joining
        /// </summary>
        public static ulong GetBestAvailableHost()
        {
            try
            {
                // Get our own Steam ID to exclude it
                ulong mySteamId = 0;
                try
                {
                    mySteamId = SteamReflectionHelper.GetSteamID();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not get own Steam ID for host filtering: {ex.Message}");
                }
                
                // First priority: Direct invite (but not from ourselves)
                if (!lastInvitedBySteamId.Equals(0UL) && !lastInvitedBySteamId.Equals(mySteamId))
                {
                    Debug.Log($"[ETGSteamP2P] Using direct invite: {lastInvitedBySteamId}");
                    return lastInvitedBySteamId;
                }
                
                // Second priority: Most recent active host (excluding ourselves)
                ulong bestHost = 0;
                float mostRecent = 0;
                
                foreach (var kvp in availableHosts)
                {
                    var host = kvp.Value;
                    if (host.isActive && 
                        !ReferenceEquals(host.steamId, mySteamId) &&
                        !ReferenceEquals(host.steamId, currentHostSteamId) &&
                        host.lastSeen > mostRecent)
                    {
                        bestHost = host.steamId;
                        mostRecent = host.lastSeen;
                    }
                }
                
                if (!ReferenceEquals(bestHost,0))
                {
                    Debug.Log($"[ETGSteamP2P] Auto-selected best host: {bestHost}");
                }
                else
                {
                    Debug.Log("[ETGSteamP2P] No available hosts found (excluding self)");
                }
                
                return bestHost;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error finding best host: {e.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Clear invite information after use
        /// </summary>
        public static void ClearInviteInfo()
        {
            lastInvitedBySteamId = 0;
            lastInviteLobbyId = "";
            Debug.Log("[ETGSteamP2P] Cleared invite info");
        }
        
        /// <summary>
        /// Automatically register this player as a host when they start hosting
        /// </summary>
        public static void RegisterAsHost()
        {
            try
            {
                ulong mySteamId = SteamReflectionHelper.GetSteamID();
                if (!ReferenceEquals(mySteamId, 0))
                {
                    currentHostSteamId = mySteamId;
                    isCurrentlyHosting = true;
                    
                    availableHosts[mySteamId] = new HostInfo
                    {
                        steamId = mySteamId,
                        sessionName = "My Session",
                        playerCount = 1,
                        lastSeen = Time.time,
                        isActive = true
                    };
                    
                    Debug.Log($"[ETGSteamP2P] Registered as host: {mySteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error registering as host: {e.Message}");
            }
        }
        
        /// <summary>
        /// Stop hosting and clean up host registration
        /// </summary>
        public static void UnregisterAsHost()
        {
            try
            {
                if (isCurrentlyHosting && (!ReferenceEquals(currentHostSteamId,0)))
                {
                    availableHosts.Remove(currentHostSteamId);
                    Debug.Log($"[ETGSteamP2P] Unregistered as host: {currentHostSteamId}");
                }
                
                currentHostSteamId = 0;
                isCurrentlyHosting = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error unregistering as host: {e.Message}");
            }
        }
        
        /// <summary>
        /// Broadcast host availability to the network (called periodically when hosting)
        /// </summary>
        public static void BroadcastHostAvailability()
        {
            try
            {
                if (isCurrentlyHosting && (!ReferenceEquals(currentHostSteamId,0)))
                {
                    // Update our host info
                    if (availableHosts.ContainsKey(currentHostSteamId))
                    {
                        var info = availableHosts[currentHostSteamId];
                        info.lastSeen = Time.time;
                        info.isActive = true;
                        availableHosts[currentHostSteamId] = info;
                    }
                    
                    // In a real implementation, this would broadcast via P2P or Steam Rich Presence
                    // For now, we'll rely on Rich Presence and lobby system
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error broadcasting host availability: {e.Message}");
            }
        }
        
        /// <summary>
        /// Automatically discover available hosts on the network
        /// </summary>
        public static ulong[] GetAvailableHosts()
        {
            try
            {
                // Get our own Steam ID to exclude it from available hosts
                ulong mySteamId = 0;
                try
                {
                    mySteamId = SteamReflectionHelper.GetSteamID();
                } catch (Exception ex)
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not get own Steam ID for host filtering: {ex.Message}");
                }
                
                // Clean up old hosts
                var hostsToRemove = new List<ulong>();
                foreach (var kvp in availableHosts)
                {
                    if (Time.time - kvp.Value.lastSeen > 30f)
                    {
                        hostsToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var hostId in hostsToRemove)
                {
                    availableHosts.Remove(hostId);
                }
                
                // Return active host Steam IDs, excluding our own Steam ID
                var activeHostsList = new List<ulong>();
                foreach (var kvp in availableHosts)
                {
                    if (kvp.Value.isActive && !ReferenceEquals(kvp.Key, mySteamId))
                    {
                        activeHostsList.Add(kvp.Key);
                    }
                }
                
                if (mySteamId > 0 && activeHostsList.Count < availableHosts.Count)
                {
                    Debug.Log($"[ETGSteamP2P] Filtered out own Steam ID {mySteamId} from available hosts");
                }
                
                return activeHostsList.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting available hosts: {e.Message}");
                return new ulong[0];
            }
        }
        
        /// <summary>
        /// Get available hosts as a dictionary for compatibility with existing code
        /// </summary>
        public static Dictionary<ulong, HostInfo> GetAvailableHostsDict()
        {
            try
            {
                // Clean up old hosts
                var hostsToRemove = new List<ulong>();
                foreach (var kvp in availableHosts)
                {
                    if (Time.time - kvp.Value.lastSeen > 30f)
                    {
                        hostsToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var hostId in hostsToRemove)
                {
                    availableHosts.Remove(hostId);
                }
                
                return new Dictionary<ulong, HostInfo>(availableHosts);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting available hosts dictionary: {e.Message}");
                return new Dictionary<ulong, HostInfo>();
            }
        }
        
        /// <summary>
        /// Setup Rich Presence for hosting a multiplayer session
        /// This enables "Join Game" in Steam overlay and friends list
        /// </summary>
        public static void StartHostingSession()
        {
            try
            {
                ulong steamId = SteamReflectionHelper.GetSteamID();
                if (ReferenceEquals(steamId, 0))
                {
                    Debug.LogWarning("[ETGSteamP2P] Cannot start hosting session - Steam ID not available");
                    return;
                }
                
                // Set Rich Presence to show we're hosting
                var setRichPresenceMethod = SteamReflectionHelper.SetRichPresenceMethod;
                if (!ReferenceEquals(setRichPresenceMethod, null))
                {
                    // Set status to show we're in a multiplayer game
                    setRichPresenceMethod.Invoke(null, new object[] { "status", "In Game" });
                    setRichPresenceMethod.Invoke(null, new object[] { "steam_display", "#Status_InGame" });
                    
                    // Set connect string so friends can join
                    setRichPresenceMethod.Invoke(null, new object[] { "connect", steamId.ToString() });
                    
                    Debug.Log($"[ETGSteamP2P] Started hosting session with Steam ID: {steamId}");
                }
                
                // Register as host
                RegisterAsHost();
                
                // Create lobby if possible
                var createLobbyMethod = SteamReflectionHelper.CreateLobbyMethod;
                if (!ReferenceEquals(createLobbyMethod, null))
                {
                    try
                    {
                        // Create a lobby for 4 players
                        var result = createLobbyMethod.Invoke(null, new object[] { 1, 4 }); // ELobbyType.k_ELobbyTypePublic = 1
                        Debug.Log($"[ETGSteamP2P] Created lobby: {result}");
                        isLobbyHost = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not create lobby: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error starting hosting session: {e.Message}");
            }
        }
        
        /// <summary>
        /// Setup Rich Presence for joining a multiplayer session
        /// </summary>
        public static void StartJoiningSession(ulong hostSteamId)
        {
            try
            {
                var setRichPresenceMethod = SteamReflectionHelper.SetRichPresenceMethod;
                if (!ReferenceEquals(setRichPresenceMethod, null))
                {
                    // Set status to show we're joining a game
                    setRichPresenceMethod.Invoke(null, new object[] { "status", "Joining Game" });
                    setRichPresenceMethod.Invoke(null, new object[] { "steam_display", "#Status_JoiningGame" });
                    
                    Debug.Log($"[ETGSteamP2P] Started joining session with host: {hostSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error starting joining session: {e.Message}");
            }
        }
        
        /// <summary>
        /// Stop multiplayer session and clear Rich Presence
        /// </summary>
        public static void StopSession()
        {
            try
            {
                // Clear Rich Presence
                var clearRichPresenceMethod = SteamReflectionHelper.ClearRichPresenceMethod;
                if (!ReferenceEquals(clearRichPresenceMethod, null))
                {
                    clearRichPresenceMethod.Invoke(null, null);
                    Debug.Log("[ETGSteamP2P] Cleared Rich Presence");
                }
                
                // Leave lobby if we're in one
                if (!ReferenceEquals(currentLobbyId, 0))
                {
                    var leaveLobbyMethod = SteamReflectionHelper.LeaveLobbyMethod;
                    if (!ReferenceEquals(leaveLobbyMethod, null))
                    {
                        var steamIdParam = SteamReflectionHelper.ConvertToCSteamID(currentLobbyId);
                        leaveLobbyMethod.Invoke(null, new object[] { steamIdParam });
                        Debug.Log($"[ETGSteamP2P] Left lobby: {currentLobbyId}");
                    }
                    
                    currentLobbyId = 0;
                    isLobbyHost = false;
                }
                
                // Unregister as host
                UnregisterAsHost();
                
                Debug.Log("[ETGSteamP2P] Stopped multiplayer session");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error stopping session: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set lobby metadata that friends can see
        /// </summary>
        public static bool SetLobbyData(string key, string value)
        {
            try
            {
                if (ReferenceEquals(currentLobbyId, 0) || ReferenceEquals(SteamReflectionHelper.SetLobbyDataMethod, null))
                    return false;
                
                var steamIdParam = SteamReflectionHelper.ConvertToCSteamID(currentLobbyId);
                var result = SteamReflectionHelper.SetLobbyDataMethod.Invoke(null, new object[] { steamIdParam, key, value });
                
                if (!ReferenceEquals(result,null) && result is bool success)
                {
                    if (success)
                    {
                        Debug.Log($"[ETGSteamP2P] Set lobby data - {key}: {value}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error setting lobby data: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Create a Steam lobby for multiplayer session
        /// </summary>
        public static bool CreateLobby(int maxPlayers = 4)
        {
            try
            {
                var createLobbyMethod = SteamReflectionHelper.CreateLobbyMethod;
                if (!ReferenceEquals(createLobbyMethod, null))
                {
                    // Create lobby with specified parameters
                    // ELobbyType.k_ELobbyTypePublic = 1, maxPlayers
                    var result = createLobbyMethod.Invoke(null, new object[] { 1, maxPlayers });
                    
                    if (!ReferenceEquals(result, null))
                    {
                        Debug.Log($"[ETGSteamP2P] Creating lobby for {maxPlayers} players...");
                        isLobbyHost = true;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error creating lobby: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Join a Steam lobby by ID
        /// </summary>
        public static bool JoinLobby(ulong lobbyId)
        {
            try
            {
                var joinLobbyMethod = SteamReflectionHelper.JoinLobbyMethod;
                if (!ReferenceEquals(joinLobbyMethod, null))
                {
                    var steamIdParam = SteamReflectionHelper.ConvertToCSteamID(lobbyId);
                    var result = joinLobbyMethod.Invoke(null, new object[] { steamIdParam });
                    
                    if (!ReferenceEquals(result, null))
                    {
                        Debug.Log($"[ETGSteamP2P] Joining lobby: {lobbyId}");
                        currentLobbyId = lobbyId;
                        isLobbyHost = false;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error joining lobby: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Leave current lobby
        /// </summary>
        public static bool LeaveLobby()
        {
            try
            {
                if (!ReferenceEquals(currentLobbyId, 0) && !ReferenceEquals(SteamReflectionHelper.LeaveLobbyMethod, null))
                {
                    var steamIdParam = SteamReflectionHelper.ConvertToCSteamID(currentLobbyId);
                    SteamReflectionHelper.LeaveLobbyMethod.Invoke(null, new object[] { steamIdParam });
                    
                    Debug.Log($"[ETGSteamP2P] Left lobby: {currentLobbyId}");
                    currentLobbyId = 0;
                    isLobbyHost = false;
                    return true;
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error leaving lobby: {e.Message}");
                return false;
            }
        }
        
        // Property accessors
        public static bool IsCurrentlyHosting => isCurrentlyHosting;
        public static ulong CurrentHostSteamId => currentHostSteamId;
        public static ulong CurrentLobbyId => currentLobbyId;
        public static bool IsLobbyHost => isLobbyHost;
        public static string LastInviteLobbyId => lastInviteLobbyId;
    }
}
