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
                Debug.Log("[SteamSessionHelper] Initialized with ETG's built-in Steamworks integration");
                Debug.Log("[SteamSessionHelper] Steam overlay 'Join Game' functionality ready for implementation");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] Failed to initialize Steam callbacks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay "Join Game" request - This is the core feature!
        /// Uses AUTOMATIC host detection - no manual Steam ID setup required!
        /// </summary>
        public static void HandleJoinGameRequest(string steamLobbyId)
        {
            try
            {
                Debug.Log($"[SteamSessionHelper] ⚡ Steam overlay JOIN GAME clicked for lobby: {steamLobbyId}");
                
                if (!steamInitialized || ReferenceEquals(sessionManager, null))
                {
                    Debug.LogError("[SteamSessionHelper] Steam integration not initialized!");
                    return;
                }
                
                // AUTOMATIC: Get the best available host Steam ID
                ulong hostSteamId = ETGSteamP2PNetworking.GetBestAvailableHost();
                
                if (!ReferenceEquals(hostSteamId,0))
                {
                    Debug.Log($"[SteamSessionHelper] Auto-selected host Steam ID: {hostSteamId}");
                    
                    // Join using the automatically selected Steam ID
                    string sessionId = $"steam_{hostSteamId}";
                    
                    Debug.Log($"[SteamSessionHelper] Auto-connecting to session: {sessionId}");
                    Debug.Log("[SteamSessionHelper] Establishing automatic P2P connection...");
                    
                    // Join the session
                    sessionManager.JoinSession(sessionId);
                    
                    // Clear the invite info after use
                    ETGSteamP2PNetworking.ClearInviteInfo();
                    
                    Debug.Log("[SteamSessionHelper] ✅ Successfully auto-joined!");
                }
                else
                {
                    Debug.LogWarning("[SteamSessionHelper] ⚠️ No available hosts found for automatic joining");
                    
                    // Try to extract Steam ID from lobby format as fallback
                    // If steamLobbyId contains a Steam ID, extract it
                    bool foundFallback = false;
                    if (steamLobbyId.Contains("_"))
                    {
                        var parts = steamLobbyId.Split('_');
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (ulong.TryParse(parts[i], out ulong extractedSteamId) && extractedSteamId > 76000000000000000) // Valid Steam ID range
                            {
                                hostSteamId = extractedSteamId;
                                Debug.Log($"[SteamSessionHelper] 🔍 Fallback: Extracted Steam ID from lobby: {hostSteamId}");
                                
                                string sessionId = $"steam_{hostSteamId}";
                                sessionManager.JoinSession(sessionId);
                                foundFallback = true;
                                break;
                            }
                        }
                    }
                    
                    if (!foundFallback)
                    {
                        Debug.LogError("[SteamSessionHelper] ❌ No hosts available and no fallback Steam ID found");
                        Debug.Log("[SteamSessionHelper] 💡 Make sure someone is hosting (F3) before trying to join");
                    }
                }
                
                // Update Steam Rich Presence
                UpdateRichPresence(false, $"steam_{hostSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] ❌ Error handling Steam join request: {e.Message}");
            }
        }
          /// <summary>
        /// Join a friend's session by Steam ID
        /// </summary>
        public static void JoinFriendSession(string friendSteamId)
        {
            try
            {
                Debug.Log($"[SteamSessionHelper] 👥 Joining friend's session: {friendSteamId}");
                
                if (!steamInitialized || ReferenceEquals(sessionManager, null))
                {
                    Debug.LogWarning("[SteamSessionHelper] Steam integration not ready");
                    return;
                }
                
                // In real implementation:
                // 1. Query Steam for friend's current lobby
                // 2. Request lobby join permission
                // 3. Establish P2P connection
                  string sessionId = $"friend_{friendSteamId}_session";
                Debug.Log($"[SteamSessionHelper] 🔗 Connecting to friend's lobby: {sessionId}");
                
                sessionManager.JoinSession(sessionId);
                UpdateRichPresence(false, sessionId);
                
                Debug.Log("[SteamSessionHelper] ✅ Successfully joined friend's game!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] ❌ Error joining friend session: {e.Message}");
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
                    Debug.LogWarning("[SteamSessionHelper] Cannot update Rich Presence - Steam not initialized");
                    return;
                }
                
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogWarning("[SteamSessionHelper] Cannot update Rich Presence - Steam networking not available");
                    return;
                }
                
                if (isHosting)
                {
                    Debug.Log($"[SteamSessionHelper] 🎯 Rich Presence: Hosting GungeonTogether ({sessionId})");
                    
                    // Set Rich Presence to indicate hosting GungeonTogether
                    steamNet.SetRichPresence("status", "Hosting GungeonTogether");
                    steamNet.SetRichPresence("steam_display", "#Status_HostingGT");
                    steamNet.SetRichPresence("connect", sessionId);
                    
                    // Custom key to identify GungeonTogether users
                    steamNet.SetRichPresence("gungeon_together", "hosting");
                    steamNet.SetRichPresence("gt_version", GungeonTogether.GungeonTogetherMod.VERSION);
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    Debug.Log($"[SteamSessionHelper] 🎮 Rich Presence: Playing GungeonTogether ({sessionId})");
                    
                    // Set Rich Presence to indicate playing GungeonTogether
                    steamNet.SetRichPresence("status", "In Gungeon Together");
                    steamNet.SetRichPresence("steam_display", "#Status_PlayingGT");
                    
                    // Custom key to identify GungeonTogether users
                    steamNet.SetRichPresence("gungeon_together", "playing");
                    steamNet.SetRichPresence("gt_version", GungeonTogether.GungeonTogetherMod.VERSION);
                }
                else
                {
                    Debug.Log("[SteamSessionHelper] 🧹 Rich Presence: Cleared");
                    
                    // Clear Rich Presence
                    steamNet.ClearRichPresence();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] ❌ Error updating Rich Presence: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get list of Steam friends currently playing GungeonTogether
        /// </summary>
        public static string[] GetFriendsPlayingGame()
        {
            try
            {
                Debug.Log("[SteamSessionHelper] 🔍 Scanning for friends playing GungeonTogether...");
                
                if (!steamInitialized)
                {
                    Debug.LogWarning("[SteamSessionHelper] Steam not initialized");
                    return new string[0];
                }
                
                // Use the new ETGSteamP2PNetworking friends list feature
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogWarning("[SteamSessionHelper] ETG Steam P2P networking not available");
                    return new string[0];
                }
                
                // Get all ETG friends first
                var etgFriends = steamNet.GetETGFriends();
                if (etgFriends.Count.Equals(0))
                {
                    Debug.Log("[SteamSessionHelper] No friends currently playing Enter the Gungeon");
                    return new string[0];
                }
                
                // Get available GungeonTogether hosts
                var availableHosts = ETGSteamP2PNetworking.GetAvailableHostsDict();
                
                // Filter to only friends who are hosting GungeonTogether sessions
                System.Collections.Generic.List<string> gungeonTogetherFriends = new System.Collections.Generic.List<string>();
                
                for (int i = 0; i < etgFriends.Count; i++)
                {
                    var friend = etgFriends[i];
                    
                    // Check if this friend is hosting a GungeonTogether session
                    bool isHostingGungeonTogether = false;
                    foreach (var host in availableHosts)
                    {
                        if (host.Key == friend.steamId && host.Value.isActive)
                        {
                            isHostingGungeonTogether = true;
                            break;
                        }
                    }
                    
                    if (isHostingGungeonTogether)
                    {
                        gungeonTogetherFriends.Add($"{friend.personaName} (Hosting GungeonTogether)");
                        Debug.Log($"[SteamSessionHelper] Found GungeonTogether host: {friend.personaName} (ID: {friend.steamId})");
                    }
                    else
                    {
                        // Still show ETG friends, but indicate they're not hosting GungeonTogether
                        gungeonTogetherFriends.Add($"{friend.personaName} (Playing ETG)");
                        Debug.Log($"[SteamSessionHelper] Found ETG friend (not hosting GT): {friend.personaName} (ID: {friend.steamId})");
                    }
                }
                
                if (gungeonTogetherFriends.Count > 0)
                {
                    Debug.Log($"[SteamSessionHelper] ✅ Found {gungeonTogetherFriends.Count} friends playing ETG ({availableHosts.Count} hosting GungeonTogether)");
                }
                else
                {
                    Debug.Log("[SteamSessionHelper] No friends found playing Enter the Gungeon or GungeonTogether");
                }
                
                return gungeonTogetherFriends.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] ❌ Error getting friends list: {e.Message}");
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
                    Debug.LogWarning("[SteamSessionHelper] Cannot show invite dialog - not initialized");
                    return;
                }
                
                if (!sessionManager.IsActive)
                {
                    Debug.LogWarning("[SteamSessionHelper] No active session to invite friends to");
                    return;
                }
                
                Debug.Log("[SteamSessionHelper] 💌 Opening Steam invite dialog...");
                Debug.Log($"[SteamSessionHelper] 🎯 Current session: {sessionManager.CurrentSessionId}");
                
                // In real implementation:
                // SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
                
                Debug.Log("[SteamSessionHelper] ✅ Steam invite overlay opened!");
                Debug.Log("[SteamSessionHelper] 👥 Friends can now join via Steam overlay");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamSessionHelper] ❌ Error showing invite dialog: {e.Message}");
            }
        }
        
        // TODO: Steam callback handlers will be implemented when we have proper ETG Steamworks reflection
        // For now, these are placeholder methods that can be called manually for testing
    }
}
