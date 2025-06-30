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
        
    }
}
