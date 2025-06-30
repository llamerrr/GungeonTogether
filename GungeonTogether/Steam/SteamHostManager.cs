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
        
        // Add caching for host scanning too
        private static float lastHostScan = 0f;
        private static readonly float hostScanInterval = 3.0f; // Scan for hosts every 3 seconds max
        
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
            // Debug.Log($"[ETGSteamP2P] Auto-received invite from Steam ID: {hostSteamId}");
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
                // Debug.Log($"[ETGSteamP2P] Added host from invite: {hostSteamId}");
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
                    // Debug.LogWarning($"[ETGSteamP2P] Could not get own Steam ID for host filtering: {ex.Message}");
                }
                // First priority: Direct invite (but not from ourselves)
                if (!lastInvitedBySteamId.Equals(0UL) && !lastInvitedBySteamId.Equals(mySteamId))
                {
                    // Debug.Log($"[ETGSteamP2P] Using direct invite: {lastInvitedBySteamId}");
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
                // if (!ReferenceEquals(bestHost,0))
                // {
                //     Debug.Log($"[ETGSteamP2P] Auto-selected best host: {bestHost}");
                // }
                // else
                // {
                //     Debug.Log("[ETGSteamP2P] No available hosts found (excluding self)");
                // }
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
            // Debug.Log("[ETGSteamP2P] Cleared invite info");
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
                    // Debug.Log($"[ETGSteamP2P] Registered as host: {mySteamId}");
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
                    // Debug.Log($"[ETGSteamP2P] Unregistered as host: {currentHostSteamId}");
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
                    // Debug.LogWarning($"[ETGSteamP2P] Could not get own Steam ID for host filtering: {ex.Message}");
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
                // CRITICAL: Actively scan Steam friends for ETG players who might be hosting
                try
                {
                    ScanFriendsForHosts();
                }
                catch (Exception ex)
                {
                    // Debug.LogWarning($"[ETGSteamP2P] Error scanning friends for hosts: {ex.Message}");
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
                // if (mySteamId > 0 && activeHostsList.Count < availableHosts.Count)
                // {
                //     Debug.Log($"[ETGSteamP2P] Filtered out own Steam ID {mySteamId} from available hosts");
                // }
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
                    Debug.LogError("[ETGSteamP2P] Cannot start hosting session - Steam ID not available");
                    return;
                }
                // Set Rich Presence to show we're hosting
                var setRichPresenceMethod = SteamReflectionHelper.SetRichPresenceMethod;
                if (!ReferenceEquals(setRichPresenceMethod, null))
                {
                    setRichPresenceMethod.Invoke(null, new object[] { "status", "In Game" });
                    setRichPresenceMethod.Invoke(null, new object[] { "steam_display", "#Status_InGame" });
                }
                // Register as host
                RegisterAsHost();
                // Create lobby if possible
                var createLobbyMethod = SteamReflectionHelper.CreateLobbyMethod;
                if (!ReferenceEquals(createLobbyMethod, null))
                {
                    try
                    {
                        var result = createLobbyMethod.Invoke(null, new object[] { 1, 50 });
                        isLobbyHost = true;
                        ulong lobbyId = 0;
                        if (!ReferenceEquals(result, null))
                        {
                            // Try to extract lobby ID robustly
                            if (result is ulong)
                            {
                                lobbyId = (ulong)result;
                            }
                            else
                            {
                                var type = result.GetType();
                                var mSteamIDProp = type.GetProperty("m_SteamID");
                                var steamIDProp = type.GetProperty("steamID");
                                var valueField = type.GetField("m_SteamID");
                                var altValueField = type.GetField("steamID");
                                if (!ReferenceEquals(mSteamIDProp, null))
                                {
                                    lobbyId = (ulong)mSteamIDProp.GetValue(result, null);
                                }
                                else if (!ReferenceEquals(steamIDProp, null))
                                {
                                    lobbyId = (ulong)steamIDProp.GetValue(result, null);
                                }
                                else if (!ReferenceEquals(valueField, null))
                                {
                                    lobbyId = (ulong)valueField.GetValue(result);
                                }
                                else if (!ReferenceEquals(altValueField, null))
                                {
                                    lobbyId = (ulong)altValueField.GetValue(result);
                                }
                                else
                                {
                                    // Try parsing ToString() as ulong
                                    ulong parsedId = 0;
                                    if (ulong.TryParse(result.ToString(), out parsedId) && !ReferenceEquals(parsedId, 0UL))
                                    {
                                        lobbyId = parsedId;
                                    }
                                    else
                                    {
                                        Debug.LogError($"[Host manager] Could not extract lobby ID from result of type {type.FullName}. ");
                                        Debug.LogError($"[Host manager] Result: {result}");
                                    }
                                }
                            }
                        }
                        if (!ReferenceEquals(lobbyId, 0))
                        {
                            currentLobbyId = lobbyId;
                            Debug.Log($"[Host manager] Hosting lobby with ID: {currentLobbyId}");
                            UpdateRichPresenceConnectToLobby();
                        }
                        else
                        {
                            Debug.LogError($"[Host manager] Failed to get valid lobby ID from CreateLobby result. Result: {result}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ETGSteamP2P] Could not create lobby: {e.Message}");
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
        public static bool CreateLobby(int maxPlayers = 50)
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
                        // Set the lobby ID from the result if possible
                        ulong lobbyId = 0;
                        if (result is ulong)
                        {
                            lobbyId = (ulong)result;
                        }
                        else if (!ReferenceEquals(result.GetType().GetProperty("m_SteamID"), null))
                        {
                            lobbyId = (ulong)result.GetType().GetProperty("m_SteamID").GetValue(result, null);
                        }
                        if (!ReferenceEquals(lobbyId, 0))
                        {
                            currentLobbyId = lobbyId;
                            Debug.Log($"[ETGSteamP2P] Hosting lobby with ID: {currentLobbyId}");
                            UpdateRichPresenceConnectToLobby();
                        }
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
        
        /// <summary>
        /// Scan Steam friends to find those playing ETG who might be hosting GungeonTogether
        /// </summary>
        public static void ScanFriendsForHosts()
        {
            try
            {
                // Don't scan too frequently
                if (Time.time - lastHostScan < hostScanInterval)
                {
                    return;
                }
                
                lastHostScan = Time.time;
                
                // Debug.Log("[SteamHostManager] Scanning friends for GungeonTogether hosts...");
                
                // Get Steam friends who are playing ETG
                var friends = SteamFriendsHelper.GetSteamFriends();
                
                // Debug.Log($"[ETGSteamP2P] Scanning {friends.Length} Steam friends for GungeonTogether hosts...");
                
                int etgPlayersFound = 0;
                int actualHostsFound = 0;
                int potentialHostsAdded = 0;
                
                foreach (var friend in friends)
                {
                    if (friend.isPlayingETG && friend.isOnline)
                    {
                        etgPlayersFound++;
                        
                        // Debug.Log($"[ETGSteamP2P] Found friend {friend.name} ({friend.steamId}) playing ETG - checking if hosting GungeonTogether...");
                        
                        // Check if this friend is actually hosting GungeonTogether by checking Rich Presence
                        bool isHostingGungeonTogether = false;
                        try
                        {
                            // Check for GungeonTogether-specific Rich Presence keys
                            string gungeonTogetherStatus = SteamReflectionHelper.GetFriendRichPresence(friend.steamId, "gungeon_together");
                            string gtVersion = SteamReflectionHelper.GetFriendRichPresence(friend.steamId, "gt_version");
                            string connectString = SteamReflectionHelper.GetFriendRichPresence(friend.steamId, "connect");
                            
                            // Friend is hosting if they have GungeonTogether Rich Presence set to "hosting"
                            if (string.Equals(gungeonTogetherStatus, "hosting") || 
                                (!string.IsNullOrEmpty(gtVersion) && !string.IsNullOrEmpty(connectString)))
                            {
                                isHostingGungeonTogether = true;
                                actualHostsFound++;
                                Debug.Log($"[ETGSteamP2P] ✅ {friend.name} is hosting GungeonTogether (status: {gungeonTogetherStatus}, version: {gtVersion})");
                            }
                            else if (!string.IsNullOrEmpty(gungeonTogetherStatus))
                            {
                                Debug.Log($"[ETGSteamP2P] 📝 {friend.name} is playing GungeonTogether but not hosting (status: {gungeonTogetherStatus})");
                            }
                            else
                            {
                                Debug.Log($"[ETGSteamP2P] ❌ {friend.name} is playing Enter the Gungeon but not GungeonTogether (no GT Rich Presence)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ETGSteamP2P] Could not check Rich Presence for {friend.name}: {ex.Message}");
                        }
                        
                        // Only add as host if they're actually hosting GungeonTogether
                        if (isHostingGungeonTogether)
                        {
                            if (!availableHosts.ContainsKey(friend.steamId))
                            {
                                availableHosts[friend.steamId] = new HostInfo
                                {
                                    steamId = friend.steamId,
                                    sessionName = $"{friend.name}'s GungeonTogether",
                                    playerCount = 1,
                                    lastSeen = Time.time,
                                    isActive = true
                                };
                                potentialHostsAdded++;
                                
                                Debug.Log($"[ETGSteamP2P] ✅ Added {friend.name} as confirmed GungeonTogether host");
                            }
                            else
                            {
                                // Update existing entry
                                var hostInfo = availableHosts[friend.steamId];
                                hostInfo.lastSeen = Time.time;
                                hostInfo.isActive = true;
                                hostInfo.sessionName = $"{friend.name}'s GungeonTogether";
                                availableHosts[friend.steamId] = hostInfo;
                                

Debug.Log($"[ETGSteamP2P] 🔄 Updated existing host entry for {friend.name}");
                            }
                        }
                    }
                }
                
                Debug.Log($"[ETGSteamP2P] Friend scan complete: {etgPlayersFound} playing ETG, {actualHostsFound} hosting GungeonTogether, {potentialHostsAdded} new hosts added");
                
                if (etgPlayersFound == 0)
                {
                    Debug.Log("[ETGSteamP2P] No friends currently playing Enter the Gungeon");
                }
                else if (actualHostsFound == 0)
                {
                    Debug.Log("[ETGSteamP2P] No friends currently hosting GungeonTogether multiplayer sessions");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error scanning friends for hosts: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set the Rich Presence 'connect' field to the current lobby ID (if valid)
        /// </summary>
        private static void UpdateRichPresenceConnectToLobby()
        {
            var setRichPresenceMethod = SteamReflectionHelper.SetRichPresenceMethod;
            if (!ReferenceEquals(setRichPresenceMethod, null) && !ReferenceEquals(currentLobbyId, 0))
            {
                Debug.Log($"[Host Manager] Setting Rich Presence 'connect' to lobby ID: {currentLobbyId}");
                setRichPresenceMethod.Invoke(null, new object[] { "connect", currentLobbyId.ToString() });
            }
        }
        
        /// <summary>
        /// Log when a player joins via invite or overlay (for debugging/analytics)
        /// </summary>
        public static void LogPlayerJoinedViaInviteOrOverlay(ulong steamId)
        {
            Debug.Log($"[ETGSteamP2P] Player joined via invite/overlay: SteamID={steamId}");
        }
        
        /// <summary>
        /// Polls the current lobby for new members and logs when someone joins.
        /// Should be called periodically by the host.
        /// </summary>
        private static HashSet<ulong> _lastLobbyMembers = new HashSet<ulong>();
        public static void PollAndLogLobbyJoins()
        {
            if (!isCurrentlyHosting || ReferenceEquals(currentLobbyId, 0))
                return;
            var joinLobbyMethod = SteamReflectionHelper.JoinLobbyMethod;
            var getNumMembersMethod = SteamReflectionHelper.GetLobbyDataMethod?.DeclaringType?.GetMethod("GetNumLobbyMembers");
            var getMemberByIndexMethod = SteamReflectionHelper.GetLobbyDataMethod?.DeclaringType?.GetMethod("GetLobbyMemberByIndex");
            if (ReferenceEquals(getNumMembersMethod, null) || ReferenceEquals(getMemberByIndexMethod, null))
                return;
            var csteamId = SteamReflectionHelper.ConvertToCSteamID(currentLobbyId);
            if (ReferenceEquals(csteamId, null))
                return;
            int memberCount = 0;
            try
            {
                memberCount = (int)getNumMembersMethod.Invoke(null, new object[] { csteamId });
            }
            catch { return; }
            var currentMembers = new HashSet<ulong>();
            for (int i = 0; i < memberCount; i++)
            {
                object memberObj = getMemberByIndexMethod.Invoke(null, new object[] { csteamId, i });
                ulong memberId = 0;
                if (memberObj is ulong ul) memberId = ul;
                else if (memberObj != null && ulong.TryParse(memberObj.ToString(), out ulong parsed)) memberId = parsed;
                if (!ReferenceEquals(memberId, 0))
                    currentMembers.Add(memberId);
            }
            foreach (var member in currentMembers)
            {
                if (!_lastLobbyMembers.Contains(member))
                {
                    Debug.Log($"[ETGSteamP2P] Host detected new player joined lobby: SteamID={member}");
                }
            }
            _lastLobbyMembers = currentMembers;
        }
    }
}
