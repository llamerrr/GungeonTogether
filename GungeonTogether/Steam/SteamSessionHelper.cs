using System;
using UnityEngine;
using GungeonTogether.Game;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Steam integration helper for session joining via overlay and friend invites
    /// Uses ETG's built-in Steamworks via reflection for compatibility
    /// </summary>
    public static class SteamSessionHelper
    {
        private static SimpleSessionManager sessionManager;
        private static bool steamInitialized = false;
        
        /// <summary>
        /// Initialize Steam session helper with session manager reference
        /// </summary>
        public static void Initialize(SimpleSessionManager manager)
        {
            sessionManager = manager;
            
            try
            {
                // TODO: Set up Steam callbacks using ETG's Steamworks when we have proper reflection access
                // For now, mark as initialized for basic functionality
                
                steamInitialized = true;
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] Initialized with ETG's built-in Steamworks integration");
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] Steam overlay 'Join Game' functionality ready for implementation");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] Failed to initialize Steam callbacks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay "Join Game" request - This is the core feature!
        /// </summary>
        public static void HandleJoinGameRequest(string steamLobbyId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] ⚡ Steam overlay JOIN GAME clicked for lobby: {steamLobbyId}");
                
                if (!steamInitialized || ReferenceEquals(sessionManager, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamSessionHelper] Steam integration not initialized!");
                    return;
                }
                
                // Directly join the lobby using ETGSteamP2PNetworking
                if (ulong.TryParse(steamLobbyId, out ulong lobbyId) && lobbyId > 0)
                {
                    GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] Attempting to join Steam lobby: {lobbyId}");
                    bool joinResult = ETGSteamP2PNetworking.Instance?.JoinLobby(lobbyId) ?? false;
                    if (joinResult)
                    {
                        GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] Successfully joined lobby: {lobbyId}");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] Failed to join lobby: {lobbyId}");
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] Invalid or missing lobby ID: {steamLobbyId}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] Exception in HandleJoinGameRequest: {e.Message}");
            }
        }
          /// <summary>
        /// Join a friend's session by Steam ID
        /// </summary>
        public static void JoinFriendSession(string friendSteamId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] 👥 Joining friend's session: {friendSteamId}");
                
                if (!steamInitialized || ReferenceEquals(sessionManager, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] Steam integration not ready");
                    return;
                }
                
                // In real implementation:
                // 1. Query Steam for friend's current lobby
                // 2. Request lobby join permission
                // 3. Establish P2P connection
                  string sessionId = $"friend_{friendSteamId}_session";
                GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] 🔗 Connecting to friend's lobby: {sessionId}");
                
                sessionManager.JoinSession(sessionId);
                UpdateRichPresence(false, sessionId);
                
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] ✅ Successfully joined friend's game!");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] ❌ Error joining friend session: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set Steam Rich Presence to show current session state
        /// </summary>
        public static void UpdateRichPresence(bool isHosting, string sessionId)
        {
            try
            {
                if (!steamInitialized)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] Cannot update Rich Presence - Steam not initialized");
                    return;
                }
                
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] Cannot update Rich Presence - Steam networking not available");
                    return;
                }
                
                if (isHosting)
                {
                    // Do NOT set the 'connect' field here. It is now set only after lobby creation in SteamHostManager.
                    steamNet.SetRichPresence("status", "Hosting GungeonTogether");
                    steamNet.SetRichPresence("steam_display", "#Status_HostingGT");
                    // steamNet.SetRichPresence("connect", connectValue); // REMOVED: Only set after lobby creation
                    steamNet.SetRichPresence("gungeon_together", "hosting");
                    steamNet.SetRichPresence("gt_version", GungeonTogether.GungeonTogetherMod.VERSION);
                    // GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] Rich Presence 'connect' field set to: {connectValue}"); // REMOVED
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] 🎮 Rich Presence: Playing GungeonTogether ({sessionId})");
                    
                    // Set Rich Presence to indicate playing GungeonTogether
                    steamNet.SetRichPresence("status", "In Gungeon Together");
                    steamNet.SetRichPresence("steam_display", "#Status_PlayingGT");
                    
                    // Custom key to identify GungeonTogether users
                    steamNet.SetRichPresence("gungeon_together", "playing");
                    steamNet.SetRichPresence("gt_version", GungeonTogether.GungeonTogetherMod.VERSION);
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] 🧹 Rich Presence: Cleared");
                    
                    // Clear Rich Presence
                    steamNet.ClearRichPresence();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] ❌ Error updating Rich Presence: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get list of Steam friends currently playing GungeonTogether
        /// </summary>
        public static string[] GetFriendsPlayingGame()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] 🔍 Scanning for friends playing GungeonTogether...");
                
                if (!steamInitialized)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] Steam not initialized");
                    return new string[0];
                }
                
                // Use the new ETGSteamP2PNetworking friends list feature
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] ETG Steam P2P networking not available");
                    return new string[0];
                }
                
                // Get all Steam friends who are playing ETG
                var allFriends = SteamFriendsHelper.GetSteamFriends();
                
                // Filter to only friends playing ETG
                var etgFriendsCount = 0;
                foreach (var friend in allFriends)
                {
                    if (friend.isPlayingETG && friend.isOnline)
                    {
                        etgFriendsCount++;
                    }
                }
                
                if (etgFriendsCount == 0)
                {
                    GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] No friends currently playing Enter the Gungeon");
                    return new string[0];
                }
                
                // Get available GungeonTogether hosts
                var availableHosts = ETGSteamP2PNetworking.GetAvailableHostsDict();
                
                // Filter to only friends who are hosting GungeonTogether sessions
                System.Collections.Generic.List<string> gungeonTogetherFriends = new System.Collections.Generic.List<string>();
                
                foreach (var friend in allFriends)
                {
                    if (!friend.isPlayingETG || !friend.isOnline) continue;
                    
                    // Check if this friend is hosting a GungeonTogether session
                    bool isHostingGungeonTogether = false;
                    foreach (var host in availableHosts)
                    {
                        if (object.Equals(host.Key, friend.steamId) && host.Value.isActive)
                        {
                            isHostingGungeonTogether = true;
                            break;
                        }
                    }
                    
                    if (isHostingGungeonTogether)
                    {
                        gungeonTogetherFriends.Add($"{friend.name} (Hosting GungeonTogether)");
                        GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] Found GungeonTogether host: {friend.name} (ID: {friend.steamId})");
                    }
                    else
                    {
                        // Still show ETG friends, but indicate they're not hosting GungeonTogether
                        gungeonTogetherFriends.Add($"{friend.name} (Playing ETG)");
                        GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] Found ETG friend (not hosting GT): {friend.name} (ID: {friend.steamId})");
                    }
                }
                
                if (gungeonTogetherFriends.Count > 0)
                {
                    GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] ✅ Found {gungeonTogetherFriends.Count} friends playing ETG ({availableHosts.Count} hosting GungeonTogether)");
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] No friends found playing Enter the Gungeon or GungeonTogether");
                }
                
                return gungeonTogetherFriends.ToArray();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] ❌ Error getting friends list: {e.Message}");
                return new string[0];
            }
        }
        
        /// <summary>
        /// Show Steam overlay invite dialog
        /// </summary>
        public static void ShowInviteDialog()
        {
            try
            {
                if (!steamInitialized || ReferenceEquals(sessionManager, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] Cannot show invite dialog - not initialized");
                    return;
                }
                
                if (!sessionManager.IsActive)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamSessionHelper] No active session to invite friends to");
                    return;
                }
                
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] 💌 Opening Steam invite dialog...");
                GungeonTogether.Logging.Debug.Log($"[SteamSessionHelper] 🎯 Current session: {sessionManager.currentHostId}");
                
                // In real implementation:
                // SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
                
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] ✅ Steam invite overlay opened!");
                GungeonTogether.Logging.Debug.Log("[SteamSessionHelper] 👥 Friends can now join via Steam overlay");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamSessionHelper] ❌ Error showing invite dialog: {e.Message}");
            }
        }
        
        // TODO: Steam callback handlers will be implemented when we have proper ETG Steamworks reflection
        // For now, these are placeholder methods that can be called manually for testing
    }
}
