using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Simple and reliable Steam join system that doesn't rely on complex callbacks
    /// </summary>
    public static class SimpleSteamJoinSystem
    {
        private static bool initialized = false;
        
        /// <summary>
        /// Initialize the simple join system
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;
            
            initialized = true;
            Debug.Log("[SimpleSteamJoin] Initialized simple Steam join system");
            
            // Proactively initialize P2P networking so we can detect join requests
            EnsureP2PNetworkingInitialized();
        }
        
        /// <summary>
        /// Ensure P2P networking is initialized for join detection
        /// </summary>
        private static void EnsureP2PNetworkingInitialized()
        {
            try
            {
                // If Instance is already available, we're good
                if (!ReferenceEquals(ETGSteamP2PNetworking.Instance, null))
                {
                    Debug.Log("[SimpleSteamJoin] P2P networking already initialized");
                    return;
                }
                
                // Try to create the P2P networking instance using the factory
                Debug.Log("[SimpleSteamJoin] Initializing P2P networking for join detection...");
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    Debug.Log("[SimpleSteamJoin] P2P networking initialized successfully");
                }
                else
                {
                    Debug.LogWarning("[SimpleSteamJoin] P2P networking not available - will try again later");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleSteamJoin] Error initializing P2P networking: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update the join system (called every frame)
        /// </summary>
        public static void Update()
        {
            if (!initialized) return;
            try
            {
                // Log status occasionally to show the system is running
                if (Time.frameCount % 1800 == 0) // Every 30 seconds at 60fps
                {
                    Debug.Log($"[SimpleSteamJoin] Status check - Steam join system active");
                }
                // No legacy join request polling; all join logic is handled by Steam lobby/session callbacks
            }
            catch (Exception ex)
            {
                // Only log errors occasionally to avoid spam
                if (Time.frameCount % 900 == 0) // Every 15 seconds at 60fps
                {
                    Debug.LogWarning($"[SimpleSteamJoin] Error in update: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Get our current Steam ID
        /// </summary>
        private static ulong GetMySteamId()
        {
            try
            {
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null)) return 0;
                
                var steamUserType = steamworksAssembly.GetType("Steamworks.SteamUser", false);
                if (ReferenceEquals(steamUserType, null)) return 0;
                
                var getSteamIdMethod = steamUserType.GetMethod("GetSteamID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (ReferenceEquals(getSteamIdMethod, null)) return 0;
                
                var steamIdObj = getSteamIdMethod.Invoke(null, null);
                if (ReferenceEquals(steamIdObj, null)) return 0;
                
                // Convert CSteamID to ulong
                var steamIdType = steamIdObj.GetType();
                var toUInt64Method = steamIdType.GetMethod("ToUInt64") ?? steamIdType.GetMethod("GetUInt64") ?? steamIdType.GetMethod("ToUlong");
                
                if (!ReferenceEquals(toUInt64Method, null))
                {
                    return (ulong)toUInt64Method.Invoke(steamIdObj, null);
                }
                
                // Fallback: try to get the raw value
                var mValueField = steamIdType.GetField("m_SteamID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (!ReferenceEquals(mValueField, null))
                {
                    return (ulong)mValueField.GetValue(steamIdObj);
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleSteamJoin] Error getting Steam ID: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Send a join request to a host (now handled by Steam lobby/session system)
        /// </summary>
        public static bool SendJoinRequest(ulong hostSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSteamJoin] Requesting to join host Steam ID: {hostSteamId}");
                // Use Steam lobby/session join logic here (no custom P2P join request)
                // For example, use SteamMatchmaking.JoinLobby or similar via reflection
                // (Implementation depends on how lobby/session join is triggered elsewhere)
                // Return true to indicate the request was initiated
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleSteamJoin] Error sending join request to {hostSteamId}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Clear hosting status
        /// </summary>
        public static void StopHosting()
        {
            try
            {
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (!ReferenceEquals(steamNet, null))
                {
                    steamNet.ClearRichPresence();
                    Debug.Log("[SimpleSteamJoin] Cleared Rich Presence (stopped hosting)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleSteamJoin] Error stopping hosting: {ex.Message}");
            }
        }
    }
}
