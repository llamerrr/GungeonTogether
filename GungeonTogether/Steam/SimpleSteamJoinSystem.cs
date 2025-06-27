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
        private static float lastHostBroadcast = 0f;
        private static float lastJoinCheck = 0f;
        private static HashSet<ulong> knownConnections = new HashSet<ulong>();
        
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
                    var steamNet = ETGSteamP2PNetworking.Instance;
                    Debug.Log($"[SimpleSteamJoin] Status check - P2P Instance available: {!ReferenceEquals(steamNet, null)}, IsCurrentlyHosting: {ETGSteamP2PNetworking.IsCurrentlyHosting}");
                }
                
                // If we're hosting, broadcast our availability and check for join requests
                if (ETGSteamP2PNetworking.IsCurrentlyHosting)
                {
                    UpdateHosting();
                }
                
                // Always check for incoming join requests (in case we become a host)
                CheckForJoinRequests();
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
        /// Update hosting functionality
        /// </summary>
        private static void UpdateHosting()
        {
            // Broadcast our availability every 5 seconds
            if (Time.time - lastHostBroadcast > 5f)
            {
                BroadcastHostAvailability();
                lastHostBroadcast = Time.time;
            }
        }
        
        /// <summary>
        /// Broadcast that we're available for joining
        /// </summary>
        private static void BroadcastHostAvailability()
        {
            try
            {
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null)) return;
                
                // Set Rich Presence to indicate we're hosting and available for join
                steamNet.SetRichPresence("status", "Hosting GungeonTogether");
                steamNet.SetRichPresence("steam_display", "#Status_HostingGT");
                
                // Set the connect field to our Steam ID so friends can join
                var mySteamId = GetMySteamId();
                if (mySteamId != 0)
                {
                    steamNet.SetRichPresence("connect", mySteamId.ToString());
                    Debug.Log($"[SimpleSteamJoin] Broadcasting host availability - Steam ID: {mySteamId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleSteamJoin] Error broadcasting host availability: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check for incoming join requests
        /// </summary>
        private static void CheckForJoinRequests()
        {
            // Check for join requests every 100ms
            if (Time.time - lastJoinCheck < 0.1f) return;
            lastJoinCheck = Time.time;
            
            try
            {
                // Ensure P2P networking is available before checking
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null))
                {
                    // Try to initialize networking if it's not available
                    if (Time.frameCount % 900 == 0) // Every 15 seconds at 60fps
                    {
                        Debug.Log("[SimpleSteamJoin] P2P networking not available, attempting to initialize...");
                        EnsureP2PNetworkingInitialized();
                    }
                    return;
                }
                
                // Method 1: Check for join request packets
                if (ETGSteamP2PNetworking.CheckForJoinRequestNotifications(out ulong requestingSteamId))
                {
                    Debug.Log($"[SimpleSteamJoin] Received join request from Steam ID: {requestingSteamId}");
                    HandleJoinRequest(requestingSteamId);
                    return;
                }
                
                // Method 2: Check for any new P2P connections
                CheckForNewP2PConnections();
                
            }
            catch (Exception ex)
            {
                // Only log errors occasionally
                if (Time.frameCount % 1800 == 0) // Every 30 seconds at 60fps
                {
                    Debug.LogWarning($"[SimpleSteamJoin] Error checking for join requests: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Check for new P2P connections that might be join attempts
        /// </summary>
        private static void CheckForNewP2PConnections()
        {
            try
            {
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet, null)) return;
                
                // Check if someone is trying to read a P2P packet from us
                if (steamNet.ReadP2PPacket(out ulong senderSteamId, out byte[] data))
                {
                    // If this is a new connection, it might be a join attempt
                    if (!knownConnections.Contains(senderSteamId))
                    {
                        Debug.Log($"[SimpleSteamJoin] New P2P connection detected from Steam ID: {senderSteamId}");
                        knownConnections.Add(senderSteamId);
                        
                        // If we're hosting, treat this as a potential join request
                        if (ETGSteamP2PNetworking.IsCurrentlyHosting)
                        {
                            Debug.Log($"[SimpleSteamJoin] Treating new P2P connection as join request from: {senderSteamId}");
                            HandleJoinRequest(senderSteamId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore errors - this is a best-effort check
            }
        }
        
        /// <summary>
        /// Handle a join request from a specific Steam ID
        /// </summary>
        private static void HandleJoinRequest(ulong steamId)
        {
            try
            {
                Debug.Log($"[SimpleSteamJoin] Handling join request from Steam ID: {steamId}");
                
                // Accept the P2P session
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (!ReferenceEquals(steamNet, null))
                {
                    steamNet.AcceptP2PSession(steamId);
                    Debug.Log($"[SimpleSteamJoin] Accepted P2P session with Steam ID: {steamId}");
                }
                
                // Trigger the join event through the callback manager
                SteamCallbackManager.TriggerJoinRequested(steamId);
                
                Debug.Log($"[SimpleSteamJoin] Join request from Steam ID {steamId} has been processed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleSteamJoin] Error handling join request from {steamId}: {ex.Message}");
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
        /// Send a join request to a host
        /// </summary>
        public static bool SendJoinRequest(ulong hostSteamId)
        {
            try
            {
                Debug.Log($"[SimpleSteamJoin] Sending join request to host Steam ID: {hostSteamId}");
                
                // Use the existing join request method
                bool success = ETGSteamP2PNetworking.SendJoinRequestToHost(hostSteamId);
                
                if (success)
                {
                    Debug.Log($"[SimpleSteamJoin] Successfully sent join request to {hostSteamId}");
                }
                else
                {
                    Debug.LogWarning($"[SimpleSteamJoin] Failed to send join request to {hostSteamId}");
                }
                
                return success;
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
                
                knownConnections.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleSteamJoin] Error stopping hosting: {ex.Message}");
            }
        }
    }
}
