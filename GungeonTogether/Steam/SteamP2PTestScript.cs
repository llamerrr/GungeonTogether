using System;
using System.Text;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Steam P2P packet testing script for debugging connection and invite functionality
    /// Integrates with the existing ETGSteamP2PNetworking system
    /// </summary>
    public class SteamP2PTestScript : MonoBehaviour
    {
        private ETGSteamP2PNetworking steamNet;
        private bool isInitialized = false;
        private float lastPacketTime = 0f;
        private const float PACKET_INTERVAL = 2.0f; // Send test packet every 2 seconds
        
        // Test target Steam ID (will be set dynamically)
        private ulong targetSteamId = 76561198164987885;
        private bool autoSendEnabled = false;
        
        private void Start()
        {
            try
            {
                Debug.Log("[SteamP2PTest] Initializing Steam P2P test script...");
                
                // Get or create Steam networking instance
                steamNet = ETGSteamP2PNetworking.Instance;
                if (steamNet == null)
                {
                    var factory = SteamNetworkingFactory.TryCreateSteamNetworking();
                    steamNet = factory as ETGSteamP2PNetworking;
                }
                
                if (steamNet != null && steamNet.IsAvailable())
                {
                    isInitialized = true;
                    Debug.Log("[SteamP2PTest] Steam P2P test script initialized successfully");
                    Debug.Log("[SteamP2PTest] Controls:");
                    Debug.Log("[SteamP2PTest]   F8 - Send test packet to target");
                    Debug.Log("[SteamP2PTest]   F9 - Toggle auto-send mode");
                    Debug.Log("[SteamP2PTest]   F10 - Set target to best available host");
                    Debug.Log("[SteamP2PTest]   F11 - Show Steam callback status");
                    Debug.Log("[SteamP2PTest]   F12 - Send invite test");
                    
                    // Subscribe to events
                    steamNet.OnJoinRequested += OnJoinRequested;
                    ETGSteamP2PNetworking.OnOverlayJoinRequested += OnOverlayJoinRequested;
                }
                else
                {
                    Debug.LogWarning("[SteamP2PTest] Steam networking not available - test script disabled");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error initializing test script: {e.Message}");
            }
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            try
            {
                // Handle keyboard inputs for testing
                HandleTestInputs();
                
                // Auto-send packets if enabled
                if (autoSendEnabled && targetSteamId != 0 && Time.time - lastPacketTime >= PACKET_INTERVAL)
                {
                    SendTestPacket();
                    lastPacketTime = Time.time;
                }
                
                // Continuously check for incoming packets
                // (This is handled by the main Steam networking Update loop)
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error in Update: {e.Message}");
            }
        }
        
        private void HandleTestInputs()
        {
            // F8 - Send single test packet
            if (Input.GetKeyDown(KeyCode.F8))
            {
                SendTestPacket();
            }
            
            // F9 - Toggle auto-send mode
            if (Input.GetKeyDown(KeyCode.F9))
            {
                autoSendEnabled = !autoSendEnabled;
                Debug.Log($"[SteamP2PTest] Auto-send mode: {(autoSendEnabled ? "ENABLED" : "DISABLED")}");
                if (autoSendEnabled && targetSteamId == 0)
                {
                    SetTargetToBestHost();
                }
            }
            
            // F10 - Set target to best available host
            if (Input.GetKeyDown(KeyCode.F10))
            {
                SetTargetToBestHost();
            }
            
            // F11 - Show Steam callback status
            if (Input.GetKeyDown(KeyCode.F11))
            {
                ShowCallbackStatus();
            }
            
            // F12 - Test Steam invite functionality
            if (Input.GetKeyDown(KeyCode.F12))
            {
                TestInviteFunctionality();
            }
        }
        
        /// <summary>
        /// Send a test packet to the target Steam ID
        /// </summary>
        private void SendTestPacket()
        {
            try
            {
                if (targetSteamId == 0)
                {
                    Debug.LogWarning("[SteamP2PTest] No target Steam ID set - use F10 to set target");
                    return;
                }
                
                if (steamNet == null || !steamNet.IsAvailable())
                {
                    Debug.LogError("[SteamP2PTest] Steam networking not available");
                    return;
                }
                
                // Create test message
                string message = $"Test packet from {steamNet.GetSteamID()} at {DateTime.Now:HH:mm:ss}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                
                // Send the packet
                bool success = steamNet.SendP2PPacket(targetSteamId, data);
                
                if (success)
                {
                    Debug.Log($"[SteamP2PTest] Sent test packet to {targetSteamId}: {message}");
                }
                else
                {
                    Debug.LogError($"[SteamP2PTest] Failed to send test packet to {targetSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error sending test packet: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set target to the best available host automatically
        /// </summary>
        private void SetTargetToBestHost()
        {
            try
            {
                // First try to get the last inviter
                ulong bestHost = ETGSteamP2PNetworking.GetLastInviterSteamId();
                
                if (bestHost == 0)
                {
                    // Try to get any available host
                    bestHost = ETGSteamP2PNetworking.GetBestAvailableHost();
                }
                
                if (bestHost == 0)
                {
                    // Try to get a hardcoded test Steam ID (replace with your friend's Steam ID)
                    // This is just for testing - in real use, Steam IDs come from invites/lobbies
                    Debug.LogWarning("[SteamP2PTest] No available hosts found - you can manually set targetSteamId in code for testing");
                    Debug.LogWarning("[SteamP2PTest] Replace the targetSteamId = 0 line with your friend's Steam ID");
                    
                    // Example: targetSteamId = 76561198000000001; // Replace with actual Steam ID
                    return;
                }
                
                targetSteamId = bestHost;
                Debug.Log($"[SteamP2PTest] Target set to: {targetSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error setting target host: {e.Message}");
            }
        }
        
        /// <summary>
        /// Show Steam callback status for debugging
        /// </summary>
        private void ShowCallbackStatus()
        {
            try
            {
                Debug.Log("[SteamP2PTest] === STEAM CALLBACK STATUS ===");
                Debug.Log(ETGSteamP2PNetworking.GetCallbackStatus());
                Debug.Log($"[SteamP2PTest] Current Steam ID: {steamNet?.GetSteamID() ?? 0}");
                Debug.Log($"[SteamP2PTest] Target Steam ID: {targetSteamId}");
                Debug.Log($"[SteamP2PTest] Auto-send enabled: {autoSendEnabled}");
                
                var hosts = ETGSteamP2PNetworking.GetAvailableHosts();
                Debug.Log($"[SteamP2PTest] Available hosts: {hosts.Length}");
                for (int i = 0; i < hosts.Length; i++)
                {
                    Debug.Log($"[SteamP2PTest]   Host {i + 1}: {hosts[i]}");
                }
                Debug.Log("[SteamP2PTest] ================================");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error showing callback status: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test Steam invite functionality
        /// </summary>
        private void TestInviteFunctionality()
        {
            try
            {
                Debug.Log("[SteamP2PTest] === TESTING STEAM INVITE FUNCTIONALITY ===");
                
                // Test triggering an overlay join event manually
                var mySteamId = steamNet?.GetSteamID() ?? 0;
                if (mySteamId != 0)
                {
                    Debug.Log($"[SteamP2PTest] Triggering test overlay join event for Steam ID: {mySteamId}");
                    ETGSteamP2PNetworking.TriggerOverlayJoinEvent(mySteamId.ToString());
                }
                else
                {
                    Debug.LogError("[SteamP2PTest] Cannot test invite functionality - no Steam ID available");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error testing invite functionality: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle join requests received via Steam callbacks
        /// </summary>
        private void OnJoinRequested(ulong joinerSteamId)
        {
            try
            {
                Debug.Log($"[SteamP2PTest] JOIN REQUEST received from Steam ID: {joinerSteamId}");
                
                // Automatically set this as our target for testing
                targetSteamId = joinerSteamId;
                
                // Accept the P2P session
                if (steamNet.AcceptP2PSession(joinerSteamId))
                {
                    Debug.Log($"[SteamP2PTest] Accepted P2P session with {joinerSteamId}");
                    
                    // Send a welcome message
                    string welcomeMsg = $"Welcome! Connection established with {steamNet.GetSteamID()}";
                    byte[] welcomeData = Encoding.UTF8.GetBytes(welcomeMsg);
                    steamNet.SendP2PPacket(joinerSteamId, welcomeData);
                    
                    Debug.Log($"[SteamP2PTest] Sent welcome message to {joinerSteamId}");
                }
                else
                {
                    Debug.LogError($"[SteamP2PTest] Failed to accept P2P session with {joinerSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error handling join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay join requests
        /// </summary>
        private void OnOverlayJoinRequested(string hostSteamId)
        {
            try
            {
                Debug.Log($"[SteamP2PTest] OVERLAY JOIN REQUEST for host: {hostSteamId}");
                
                if (ulong.TryParse(hostSteamId, out ulong steamId))
                {
                    targetSteamId = steamId;
                    Debug.Log($"[SteamP2PTest] Target updated to overlay host: {steamId}");
                    
                    // Send a join request packet
                    string joinMsg = $"Join request from {steamNet?.GetSteamID() ?? 0}";
                    byte[] joinData = Encoding.UTF8.GetBytes(joinMsg);
                    
                    if (steamNet?.SendP2PPacket(steamId, joinData) == true)
                    {
                        Debug.Log($"[SteamP2PTest] Sent join request to {steamId}");
                    }
                    else
                    {
                        Debug.LogError($"[SteamP2PTest] Failed to send join request to {steamId}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error handling overlay join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Manual method to set target Steam ID for testing
        /// Call this from console or other scripts
        /// </summary>
        public void SetTargetSteamId(ulong steamId)
        {
            targetSteamId = steamId;
            Debug.Log($"[SteamP2PTest] Target manually set to: {steamId}");
        }
        
        /// <summary>
        /// Get current target Steam ID
        /// </summary>
        public ulong GetTargetSteamId()
        {
            return targetSteamId;
        }
        
        /// <summary>
        /// Enable/disable auto-send mode programmatically
        /// </summary>
        public void SetAutoSend(bool enabled)
        {
            autoSendEnabled = enabled;
            Debug.Log($"[SteamP2PTest] Auto-send mode: {(enabled ? "ENABLED" : "DISABLED")}");
        }
        
        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if (steamNet != null)
                {
                    steamNet.OnJoinRequested -= OnJoinRequested;
                }
                ETGSteamP2PNetworking.OnOverlayJoinRequested -= OnOverlayJoinRequested;
                
                Debug.Log("[SteamP2PTest] Test script cleaned up");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error during cleanup: {e.Message}");
            }
        }
    }
}
