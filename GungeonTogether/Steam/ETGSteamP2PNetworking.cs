using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// ETG Steam P2P Networking using reflection to access ETG's built-in Steamworks.NET
    /// This implementation uses reflection to safely interact with ETG's Steam integration
    /// without requiring external dependencies or causing TypeLoadException issues.
    /// 
    /// This class now uses modular helper classes for better organization:
    /// - SteamReflectionHelper: Handles all reflection-based Steam API access
    /// - SteamCallbackManager: Manages Steam callbacks and events
    /// - SteamHostManager: Handles host discovery and session management
    /// - SteamFallbackDetection: Provides fallback join detection when callbacks fail
    /// </summary>
    public class ETGSteamP2PNetworking : ISteamNetworking
    {
        public static ETGSteamP2PNetworking Instance { get; private set; }

        private bool isInitialized = false;
        
        // Debounce mechanism for PrintFriendsList to prevent console spam
        private static float lastPrintFriendsListTime = 0f;
        private static readonly float printFriendsListCooldown = 2.0f; // 2 seconds minimum between calls

        public ETGSteamP2PNetworking()
        {
            try
            {
                Instance = this;
                isInitialized = false; // Will be set to true when/if initialization succeeds
                // Wire up Steam callback events
                SteamCallbackManager.OnOverlayJoinRequested += HandleOverlayJoinRequest;
                // Debug.Log("[ETGSteamP2P] Created Steam P2P networking instance (lazy initialization)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error creating instance: {e.Message}");
                isInitialized = false;
            }
        }

        /// <summary>
        /// Ensure Steam types are initialized (lazy initialization)
        /// </summary>
        private void EnsureInitialized()
        {
            if (isInitialized) return;
            try
            {
                // Initialize Steam reflection helper
                if (!SteamReflectionHelper.IsInitialized)
                {
                    SteamReflectionHelper.InitializeSteamTypes();
                }

                isInitialized = SteamReflectionHelper.IsInitialized;
                if (isInitialized)
                {
                    SteamCallbackManager.InitializeSteamCallbacks();
                    // if (SteamCallbackManager.AreCallbacksRegistered)
                    // {
                    //     Debug.Log("[ETGSteamP2P] Steam join callbacks registered - invites should work");
                    // }
                    // else
                    // {
                    //     Debug.LogWarning("[ETGSteamP2P] Steam join callbacks NOT registered - invites may not work");
                    // }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Failed to initialize Steam types: {e.Message}");
            }
        }

        /// <summary>
        /// Get current Steam user ID (cached to prevent log spam)
        /// </summary>
        public ulong GetSteamID()
        {
            try
            {
                EnsureInitialized();
                return SteamReflectionHelper.GetSteamID();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Join a Steam lobby by ID
        /// </summary>
        public bool JoinLobby(ulong lobbyId)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] [JoinLobby] Called with lobbyId: {lobbyId} (type: {lobbyId.GetType().FullName})");
                if (lobbyId.Equals(0))
                {
                    Debug.LogError("[ETGSteamP2P] Invalid lobby ID: 0");
                    return false;
                }
                // Use reflection helper to call Steam's JoinLobby
                var joinLobbyMethod = SteamReflectionHelper.JoinLobbyMethod;
                if (ReferenceEquals(joinLobbyMethod, null))
                {
                    Debug.LogError("[ETGSteamP2P] JoinLobbyMethod is null - cannot join lobby");
                    return false;
                }
                var csteamId = SteamReflectionHelper.ConvertToCSteamID(lobbyId);
                Debug.Log($"[ETGSteamP2P] [JoinLobby] Converted lobbyId to csteamId: {csteamId} (type: {(csteamId == null ? "null" : csteamId.GetType().FullName)})");
                if (ReferenceEquals(csteamId, null))
                {
                    Debug.LogError("[ETGSteamP2P] Failed to convert lobbyId to CSteamID");
                    return false;
                }
                joinLobbyMethod.Invoke(null, new object[] { csteamId });
                Debug.Log($"[ETGSteamP2P] JoinLobby invoked for lobby: {lobbyId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ETGSteamP2P] Exception while joining lobby: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Leave current lobby
        /// </summary>
        public bool LeaveLobby()
        {
            try
            {
                EnsureInitialized();
                return SteamHostManager.LeaveLobby();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error leaving lobby: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set lobby metadata that friends can see
        /// </summary>
        public bool SetLobbyData(string key, string value)
        {
            try
            {
                EnsureInitialized();
                return SteamHostManager.SetLobbyData(key, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error setting lobby data: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setup Rich Presence for hosting a multiplayer session
        /// This enables "Join Game" in Steam overlay and friends list
        /// </summary>
        public void StartHostingSession()
        {
            try
            {
                EnsureInitialized();
                SteamHostManager.StartHostingSession();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error starting hosting session: {e.Message}");
            }
        }

        /// <summary>
        /// Setup Rich Presence for joining a multiplayer session
        /// </summary>
        public void StartJoiningSession(ulong hostSteamId)
        {
            try
            {
                EnsureInitialized();
                SteamHostManager.StartJoiningSession(hostSteamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error starting joining session: {e.Message}");
            }
        }

        /// <summary>
        /// Stop multiplayer session and clear Rich Presence
        /// </summary>
        public void StopSession()
        {
            try
            {
                EnsureInitialized();
                SteamHostManager.StopSession();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error stopping session: {e.Message}");
            }
        }

        /// <summary>
        /// Check if Steam networking is available
        /// </summary>
        public bool IsAvailable()
        {
            try
            {
                EnsureInitialized();
                return isInitialized && SteamReflectionHelper.IsInitialized;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error checking availability: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleanup when shutting down
        /// </summary>
        public void Shutdown()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Shutting down Steam P2P networking");

                // Stop any active session
                StopSession();

                // Clean up events
                SteamCallbackManager.OnOverlayJoinRequested -= HandleOverlayJoinRequest;

                isInitialized = false;
                Instance = null;

                Debug.Log("[ETGSteamP2P] Steam P2P networking shutdown complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error during shutdown: {e.Message}");
            }
        }

        /// <summary>
        /// Check for pending join requests (manual polling)
        /// This simulates Steam callback handling
        /// </summary>
        public void CheckForJoinRequests()
        {
            try
            {
                // Process Steam callbacks
                SteamCallbackManager.ProcessSteamCallbacks();

                // Check for any fallback-detected join requests
                // This is handled automatically by the fallback detection system
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error checking for join requests: {e.Message}");
            }
        }

        /// <summary>
        /// Handle Steam overlay "Join Game" requests
        /// </summary>
        private static void HandleOverlayJoinRequest(string connectString)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Processing overlay join request: {connectString}");

                // Try to parse as lobby ID first
                if (ulong.TryParse(connectString, out ulong lobbyId))
                {
                    Debug.Log($"[ETGSteamP2P] Attempting to join lobby with ID: {lobbyId}");
                    // Join the Steam lobby, not direct P2P to user
                    Instance?.JoinLobby(lobbyId);
                    // Optionally, fire an event or notification here if needed
                }
                else
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not parse lobby ID from connect string: {connectString}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling overlay join request: {e.Message}");
            }
        }

        /// <summary>
        /// Set Steam Rich Presence to show in friends list and enable "Join Game"
        /// </summary>
        public bool SetRichPresence(string key, string value)
        {
            try
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error setting Rich Presence: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear all Rich Presence data
        /// </summary>
        public bool ClearRichPresence()
        {
            try
            {
                EnsureInitialized();

                var clearRichPresenceMethod = SteamReflectionHelper.ClearRichPresenceMethod;
                if (!ReferenceEquals(clearRichPresenceMethod, null))
                {
                    object result = clearRichPresenceMethod.Invoke(null, null);
                    if (!ReferenceEquals(result, null) && result is bool success)
                    {
                        if (success)
                        {
                            Debug.Log("[ETGSteamP2P] Cleared Rich Presence");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error clearing Rich Presence: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Discover the correct method signatures for ETG's Steamworks P2P methods
        /// This will help us understand what parameters IsP2PPacketAvailable expects
        /// </summary>
        public void DiscoverMethodSignatures()
        {
            try
            {
                EnsureInitialized();

                if (SteamReflectionHelper.IsInitialized)
                {
                    Debug.Log("[ETGSteamP2P] Steam types are initialized and method signatures have been discovered");
                    Debug.Log("[ETGSteamP2P] Check the initialization logs for detailed method signature information");
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] Steam types not initialized - cannot discover method signatures");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error discovering method signatures: {e.Message}");
            }
        }

        // Wrapper methods for host management
        public static void RegisterAsHost() => SteamHostManager.RegisterAsHost();
        public static ulong[] GetAvailableHosts() => SteamHostManager.GetAvailableHosts();
        public static Dictionary<ulong, SteamHostManager.HostInfo> GetAvailableHostsDict() => SteamHostManager.GetAvailableHostsDict();
        public static void SetInviteInfo(ulong hostSteamId, string lobbyId = "") => SteamHostManager.SetInviteInfo(hostSteamId, lobbyId);
        public static ulong GetLastInviterSteamId() => SteamHostManager.GetLastInviterSteamId();
        public static ulong GetBestAvailableHost() => SteamHostManager.GetBestAvailableHost();
        public static void ClearInviteInfo() => SteamHostManager.ClearInviteInfo();
        public static void UnregisterAsHost() => SteamHostManager.UnregisterAsHost();
        public static void BroadcastHostAvailability() => SteamHostManager.BroadcastHostAvailability();

        // Callback management wrappers
        public static void TriggerOverlayJoinEvent(string hostSteamId) => SteamCallbackManager.TriggerOverlayJoinEvent(hostSteamId);
        public static string GetCallbackStatus() => SteamCallbackManager.GetCallbackStatus();
        public static void ProcessSteamCallbacks() => SteamCallbackManager.ProcessSteamCallbacks();

        // Properties for compatibility
        public static bool IsCurrentlyHosting => SteamHostManager.IsCurrentlyHosting;
        public static bool AreCallbacksRegistered => SteamCallbackManager.AreCallbacksRegistered;

        // Static events for backward compatibility
        public static event Action<string> OnOverlayJoinRequested
        {
            add => SteamCallbackManager.OnOverlayJoinRequested += value;
            remove => SteamCallbackManager.OnOverlayJoinRequested -= value;
        }

        public static event Action<bool> OnOverlayActivated
        {
            add => SteamCallbackManager.OnOverlayActivated += value;
            remove => SteamCallbackManager.OnOverlayActivated -= value;
        }

        // Backward compatibility methods for Steam Friends functionality
        public SteamFriendsHelper.FriendInfo[] GetETGFriends() => SteamFriendsHelper.GetETGFriends();
        public SteamFriendsHelper.FriendInfo[] GetSteamFriends() => SteamFriendsHelper.GetSteamFriends();
        public SteamFriendsHelper.FriendInfo[] GetSteamFriends(int dummy) => SteamFriendsHelper.GetSteamFriends(); // Overload for compatibility
        public void PrintFriendsList() => SteamFriendsHelper.PrintFriendsList();
        public void DebugSteamFriends() => SteamFriendsHelper.DebugSteamFriends();

        // Define FriendInfo struct for compatibility (alias to SteamFriendsHelper.FriendInfo)
        public struct FriendInfo
        {
            public ulong steamId;
            public string name;
            public string personaName; // Add personaName field for compatibility
            public bool isOnline;
            public bool isInGame;
            public string gameInfo;

            // Additional properties for compatibility with existing UI code
            public bool isPlayingETG;
            public string currentGameName;
        }

        /// <summary>
        /// Update method called regularly to process Steam callbacks and check for incoming packets
        /// This should be called from a MonoBehaviour's Update() method or similar game loop
        /// </summary>
        public void Update()
        {
            try
            {
                // Process Steam callbacks every frame
                CheckForJoinRequests();

                // Update host management
                if (SteamHostManager.IsCurrentlyHosting)
                {
                    // Broadcast host availability periodically (every 5 seconds)
                    if (ReferenceEquals(Time.frameCount % 300, 0)) // Every 5 seconds at 60fps
                    {
                        SteamHostManager.BroadcastHostAvailability();
                    }
                }
            }
            catch (Exception e)
            {
                // Don't spam errors in the update loop
                if (ReferenceEquals(Time.frameCount % 600, 0)) // Log every 10 seconds at 60fps
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error in Update(): {e.Message}");
                }
            }
        }

        /// <summary>
        /// Event triggered when a player joins the Steam lobby (host only)
        /// </summary>
        public event Action<ulong, string> OnPlayerJoined
        {
            add { SteamHostManager.OnPlayerJoined += value; }
            remove { SteamHostManager.OnPlayerJoined -= value; }
        }
    }
}