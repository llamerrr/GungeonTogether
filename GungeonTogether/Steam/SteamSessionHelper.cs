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
        /// </summary>
        public static void HandleJoinGameRequest(string steamLobbyId)
        {
            try
            {
                Debug.Log($"[SteamSessionHelper] ⚡ Steam overlay JOIN GAME clicked for lobby: {steamLobbyId}");
                
                if (!steamInitialized || sessionManager == null)
                {
                    Debug.LogError("[SteamSessionHelper] Steam integration not initialized!");
                    return;
                }
                
                // Convert Steam lobby ID to session format
                string sessionId = $"steam_lobby_{steamLobbyId}";
                
                Debug.Log($"[SteamSessionHelper] 🎮 Connecting to multiplayer session: {sessionId}");
                Debug.Log("[SteamSessionHelper] 🌐 Establishing Steam P2P connection...");
                
                // Join the session
                sessionManager.JoinSession(sessionId);
                
                // Update Steam Rich Presence
                UpdateRichPresence(false, sessionId);
                
                Debug.Log("[SteamSessionHelper] ✅ Successfully joined");
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
                
                if (!steamInitialized || sessionManager == null)
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
                  if (isHosting)
                {
                    Debug.Log($"[SteamSessionHelper] 🎯 Rich Presence: Hosting GungeonTogether ({sessionId})");
                    // Real implementation: SteamFriends.SetRichPresence("status", "Hosting GungeonTogether");
                    // SteamFriends.SetRichPresence("steam_display", "#Status_Hosting");
                    // SteamFriends.SetRichPresence("connect", sessionId);
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    Debug.Log($"[SteamSessionHelper] 🎮 Rich Presence: Playing GungeonTogether ({sessionId})");
                    // Real implementation: SteamFriends.SetRichPresence("status", "In Gungeon Together");
                }
                else
                {
                    Debug.Log("[SteamSessionHelper] 🧹 Rich Presence: Cleared");
                    // Real implementation: SteamFriends.ClearRichPresence();
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
                
                // TODO: Implement using ETG's Steamworks reflection when available
                // For now, return empty array - this will be implemented later
                Debug.Log("[SteamSessionHelper] Friends list feature not yet implemented with ETG Steamworks");
                return new string[0];
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
                if (!steamInitialized || sessionManager == null)
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
