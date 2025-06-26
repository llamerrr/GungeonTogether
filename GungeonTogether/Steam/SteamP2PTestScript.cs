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
        private ulong targetSteamId = 0;
        private bool autoSendEnabled = false;
        
        // Ping system variables
        private bool pingSessionActive = false;
        private int pingSequenceNumber = 0;
        private System.Collections.Generic.Dictionary<int, float> pendingPings = new System.Collections.Generic.Dictionary<int, float>();
        private System.Collections.Generic.List<float> pingTimes = new System.Collections.Generic.List<float>();
        private float pingSessionStartTime = 0f;
        private int pingsToSend = 10; // Default ping count
        private int pingsSent = 0;
        private int pongsReceived = 0;
        private float pingInterval = 1f; // Send ping every 1 second
        private float lastPingTime = 0f;
        
        private void Start()
        {
            try
            {
                Debug.Log("[SteamP2PTest] Initializing Steam P2P test script...");
                
                // Get or create Steam networking instance
                steamNet = ETGSteamP2PNetworking.Instance;
                if (ReferenceEquals(steamNet,null))
                {
                    var factory = SteamNetworkingFactory.TryCreateSteamNetworking();
                    steamNet = factory as ETGSteamP2PNetworking;
                }
                
                if (!ReferenceEquals(steamNet,null) && steamNet.IsAvailable())
                {
                    isInitialized = true;
                    Debug.Log("[SteamP2PTest] Steam P2P test script initialized successfully");
                    Debug.Log("[SteamP2PTest] Controls:");
                    Debug.Log("[SteamP2PTest]   F7 - Show Steam friends list");
                    Debug.Log("[SteamP2PTest]   F8 - Start ping test session (10 pings)");
                    Debug.Log("[SteamP2PTest]   F9 - Toggle auto-send mode");
                    Debug.Log("[SteamP2PTest]   F10 - Set target to best available host");
                    Debug.Log("[SteamP2PTest]   F11 - Show Steam callback status");
                    Debug.Log("[SteamP2PTest]   F12 - Send invite test");
                    Debug.Log("[SteamP2PTest]   1-9 - Select ETG friend by number for P2P testing");
                    
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
                
                // Handle ping session logic
                HandlePingSession();
                
                // Auto-send packets if enabled
                if (autoSendEnabled && (!ReferenceEquals(targetSteamId,0)) && Time.time - lastPacketTime >= PACKET_INTERVAL)
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
            // F7 - Show friends list
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ShowFriendsList();
            }
            
            // F8 - Start ping test session
            if (Input.GetKeyDown(KeyCode.F8))
            {
                StartPingSession();
            }
            
            // F9 - Toggle auto-send mode
            if (Input.GetKeyDown(KeyCode.F9))
            {
                autoSendEnabled = !autoSendEnabled;
                Debug.Log($"[SteamP2PTest] Auto-send mode: {(autoSendEnabled ? "ENABLED" : "DISABLED")}");
                if (autoSendEnabled && targetSteamId.Equals(0))
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
            
            // Number keys 1-9 - Select ETG friend for P2P testing
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectETGFriend(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectETGFriend(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectETGFriend(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectETGFriend(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectETGFriend(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectETGFriend(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectETGFriend(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SelectETGFriend(7);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SelectETGFriend(8);
        }
        
        /// <summary>
        /// Send a test packet to the target Steam ID
        /// </summary>
        private void SendTestPacket()
        {
            try
            {
                if (targetSteamId.Equals(0))
                {
                    Debug.LogWarning("[SteamP2PTest] No target Steam ID set - use F10 to set target");
                    return;
                }
                
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogError("[SteamP2PTest] Steam networking not available");
                    return;
                }
                
                // IMPORTANT: Accept P2P session with target before sending
                Debug.Log($"[SteamP2PTest] Accepting P2P session with {targetSteamId}...");
                bool sessionAccepted = steamNet.AcceptP2PSession(targetSteamId);
                Debug.Log($"[SteamP2PTest] P2P session acceptance result: {sessionAccepted}");
                
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
                // First, try to find an ETG friend (preferred method)
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    var etgFriends = steamNet.GetETGFriends();
                    if (etgFriends.Count > 0)
                    {
                        var bestETGFriend = etgFriends[0];
                        targetSteamId = bestETGFriend.steamId;
                        Debug.Log($"[SteamP2PTest] Target set to ETG friend: {bestETGFriend.personaName} (ID: {targetSteamId})");
                        
                        // Pre-accept P2P session
                        steamNet.AcceptP2PSession(targetSteamId);
                        return;
                    }
                }
                
                // Fallback: try to get the last inviter
                ulong bestHost = ETGSteamP2PNetworking.GetLastInviterSteamId();
                
                if (bestHost.Equals(0))
                {
                    // Try to get any available host
                    bestHost = ETGSteamP2PNetworking.GetBestAvailableHost();
                }
                
                if (bestHost.Equals(0))
                {
                    // No hosts found
                    Debug.LogWarning("[SteamP2PTest] No available hosts or ETG friends found");
                    Debug.LogWarning("[SteamP2PTest] Press F7 to see friends list or wait for a Steam invite");
                    return;
                }
                
                targetSteamId = bestHost;
                Debug.Log($"[SteamP2PTest] Target set to available host: {targetSteamId}");
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
                if (!ReferenceEquals(mySteamId,0))
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
                    
                    if (Equals(steamNet?.SendP2PPacket(steamId, joinData), true))
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
        
        /// <summary>
        /// Show Steam friends list with ETG players highlighted
        /// </summary>
        private void ShowFriendsList()
        {
            try
            {
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogWarning("[SteamP2PTest] Steam networking not available for friends list");
                    return;
                }
                
                Debug.Log("[SteamP2PTest] Refreshing Steam friends list...");
                steamNet.PrintFriendsList();
                
                var etgFriends = steamNet.GetETGFriends();
                if (etgFriends.Count > 0)
                {
                    Debug.Log("[SteamP2PTest] === P2P TESTING TARGETS ===");
                    Debug.Log("[SteamP2PTest] Press number keys 1-9 to select ETG friends for P2P testing:");
                    
                    for (int i = 0; i < etgFriends.Count && i < 9; i++)
                    {
                        var friend = etgFriends[i];
                        string targetIndicator = friend.steamId.Equals(targetSteamId) ? " [CURRENT TARGET]" : "";
                        Debug.Log($"[SteamP2PTest]   {i + 1}. {friend.personaName} (ID: {friend.steamId}){targetIndicator}");
                    }
                }
                else
                {
                    Debug.Log("[SteamP2PTest] No friends are currently playing Enter the Gungeon");
                    Debug.Log("[SteamP2PTest] Ask a friend to launch ETG, then press F7 again to refresh");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTest] Failed to show friends list: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Select an ETG friend by index for P2P testing
        /// </summary>
        private void SelectETGFriend(int index)
        {
            try
            {
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogWarning("[SteamP2PTest] Steam networking not available");
                    return;
                }
                
                var etgFriends = steamNet.GetETGFriends();
                if (etgFriends.Count.Equals(0))
                {
                    Debug.LogWarning("[SteamP2PTest] No friends playing ETG - press F7 to see friends list");
                    return;
                }
                
                if (index < 0 || index >= etgFriends.Count)
                {
                    Debug.LogWarning($"[SteamP2PTest] Invalid friend index {index + 1} - only {etgFriends.Count} ETG friends available");
                    return;
                }
                
                var selectedFriend = etgFriends[index];
                targetSteamId = selectedFriend.steamId;
                
                Debug.Log($"[SteamP2PTest] ✓ Selected P2P target: {selectedFriend.personaName} (ID: {selectedFriend.steamId})");
                Debug.Log($"[SteamP2PTest] Ready for testing! Press F8 to start ping test or F9 to enable auto-send");
                
                // Automatically accept P2P session
                Debug.Log($"[SteamP2PTest] Pre-accepting P2P session with {selectedFriend.personaName}...");
                bool sessionAccepted = steamNet.AcceptP2PSession(selectedFriend.steamId);
                Debug.Log($"[SteamP2PTest] P2P session acceptance result: {sessionAccepted}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTest] Failed to select ETG friend: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set target to first available ETG friend automatically
        /// </summary>
        private void SetTargetToFirstETGFriend()
        {
            try
            {
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    return;
                }
                
                var etgFriends = steamNet.GetETGFriends();
                if (etgFriends.Count > 0)
                {
                    var firstFriend = etgFriends[0];
                    targetSteamId = firstFriend.steamId;
                    Debug.Log($"[SteamP2PTest] Auto-selected ETG friend: {firstFriend.personaName} (ID: {firstFriend.steamId})");
                    
                    // Pre-accept P2P session
                    steamNet.AcceptP2PSession(firstFriend.steamId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTest] Failed to auto-select ETG friend: {ex.Message}");
            }
        }
        
        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if(!ReferenceEquals(steamNet,null))
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
        
        // ============================================
        // PING SYSTEM IMPLEMENTATION
        // ============================================
        
        /// <summary>
        /// Start a ping test session with the target Steam ID
        /// </summary>
        public void StartPingSession()
        {
            try
            {
                if (targetSteamId.Equals(0))
                {
                    Debug.LogWarning("[SteamP2PTest] No target Steam ID set for ping test - use F10 to set target or accept Steam invite");
                    return;
                }
                
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    Debug.LogError("[SteamP2PTest] Steam networking not available for ping test");
                    return;
                }
                
                if (pingSessionActive)
                {
                    Debug.LogWarning("[SteamP2PTest] Ping session already active. Stopping current session...");
                    StopPingSession();
                }
                
                // Initialize ping session
                pingSessionActive = true;
                pingSequenceNumber = 0;
                pingsSent = 0;
                pongsReceived = 0;
                pingTimes.Clear();
                pendingPings.Clear();
                pingSessionStartTime = Time.time;
                lastPingTime = 0f;
                
                Debug.Log($"[SteamP2PTest] 🏓 Starting ping session with {targetSteamId} ({pingsToSend} pings)");
                
                // Accept P2P session first
                bool sessionAccepted = steamNet.AcceptP2PSession(targetSteamId);
                Debug.Log($"[SteamP2PTest] P2P session acceptance: {sessionAccepted}");
                
                // Subscribe to data received events if not already subscribed
                steamNet.OnDataReceived -= OnPingDataReceived;
                steamNet.OnDataReceived += OnPingDataReceived;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error starting ping session: {e.Message}");
                pingSessionActive = false;
            }
        }
        
        /// <summary>
        /// Handle ping session timing and packet sending
        /// </summary>
        private void HandlePingSession()
        {
            if (!pingSessionActive) return;
            
            try
            {
                float currentTime = Time.time;
                
                // Send next ping if it's time and we haven't sent all pings yet
                if (pingsSent < pingsToSend && currentTime - lastPingTime >= pingInterval)
                {
                    SendPing();
                    lastPingTime = currentTime;
                }
                
                // Check for ping timeouts (remove pings older than 5 seconds)
                var timeoutThreshold = currentTime - 5f;
                var keysToRemove = new System.Collections.Generic.List<int>();
                
                foreach (var kvp in pendingPings)
                {
                    if (kvp.Value < timeoutThreshold)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (int key in keysToRemove)
                {
                    pendingPings.Remove(key);
                    Debug.LogWarning($"[SteamP2PTest] Ping #{key} timed out");
                }
                
                // End session if all pings sent and no more pending, or if timeout reached
                if ((pingsSent >= pingsToSend && pendingPings.Count.Equals(0)) || 
                    (currentTime - pingSessionStartTime > 60f)) // 60 second total timeout
                {
                    CompletePingSession();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error in ping session handling: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send a single ping packet
        /// </summary>
        private void SendPing()
        {
            try
            {
                pingSequenceNumber++;
                pingsSent++;
                
                string pingMessage = $"PING|{pingSequenceNumber}|{Time.time}|{steamNet.GetSteamID()}";
                byte[] pingData = System.Text.Encoding.UTF8.GetBytes(pingMessage);
                
                bool success = steamNet.SendP2PPacket(targetSteamId, pingData);
                
                if (success)
                {
                    pendingPings[pingSequenceNumber] = Time.time;
                    Debug.Log($"[SteamP2PTest] 📤 Sent PING #{pingSequenceNumber} to {targetSteamId} ({pingsSent}/{pingsToSend})");
                }
                else
                {
                    Debug.LogError($"[SteamP2PTest] Failed to send PING #{pingSequenceNumber}");
                    pingsSent--; // Don't count failed sends
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error sending ping: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle received data for ping system
        /// </summary>
        private void OnPingDataReceived(ulong senderSteamId, byte[] data)
        {
            try
            {
                string message = System.Text.Encoding.UTF8.GetString(data);
                
                // Handle PING messages (respond with PONG)
                if (message.StartsWith("PING|"))
                {
                    HandleReceivedPing(senderSteamId, message);
                }
                // Handle PONG messages (calculate latency)
                else if (message.StartsWith("PONG|"))
                {
                    HandleReceivedPong(senderSteamId, message);
                }
                // Handle other messages (legacy test packets)
                else
                {
                    Debug.Log($"[SteamP2PTest] 📨 Received message from {senderSteamId}: {message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error processing received data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle received PING and respond with PONG
        /// </summary>
        private void HandleReceivedPing(ulong senderSteamId, string pingMessage)
        {
            try
            {
                // Parse: PING|sequence|timestamp|senderSteamId
                string[] parts = pingMessage.Split('|');
                if (parts.Length >= 4)
                {
                    int sequence = int.Parse(parts[1]);
                    float timestamp = float.Parse(parts[2]);
                    ulong originalSender = ulong.Parse(parts[3]);
                    
                    Debug.Log($"[SteamP2PTest] 📥 Received PING #{sequence} from {senderSteamId}");
                    
                    // Send PONG response
                    string pongMessage = $"PONG|{sequence}|{timestamp}|{Time.time}|{steamNet.GetSteamID()}";
                    byte[] pongData = System.Text.Encoding.UTF8.GetBytes(pongMessage);
                    
                    bool success = steamNet.SendP2PPacket(senderSteamId, pongData);
                    if (success)
                    {
                        Debug.Log($"[SteamP2PTest] 📤 Sent PONG #{sequence} to {senderSteamId}");
                    }
                    else
                    {
                        Debug.LogError($"[SteamP2PTest] Failed to send PONG #{sequence}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error handling received ping: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle received PONG and calculate latency
        /// </summary>
        private void HandleReceivedPong(ulong senderSteamId, string pongMessage)
        {
            try
            {
                // Parse: PONG|sequence|originalTimestamp|pongTimestamp|responderSteamId
                string[] parts = pongMessage.Split('|');
                if (parts.Length >= 5)
                {
                    int sequence = int.Parse(parts[1]);
                    float originalTimestamp = float.Parse(parts[2]);
                    float pongTimestamp = float.Parse(parts[3]);
                    ulong responder = ulong.Parse(parts[4]);
                    
                    if (pendingPings.ContainsKey(sequence))
                    {
                        float roundTripTime = Time.time - pendingPings[sequence];
                        float latencyMs = roundTripTime * 1000f;
                        
                        pingTimes.Add(latencyMs);
                        pendingPings.Remove(sequence);
                        pongsReceived++;
                        
                        Debug.Log($"[SteamP2PTest] 📥 Received PONG #{sequence} from {senderSteamId} - Latency: {latencyMs:F1}ms");
                    }
                    else
                    {
                        Debug.LogWarning($"[SteamP2PTest] Received unexpected PONG #{sequence} from {senderSteamId}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error handling received pong: {e.Message}");
            }
        }
        
        /// <summary>
        /// Complete the ping session and show results
        /// </summary>
        private void CompletePingSession()
        {
            try
            {
                pingSessionActive = false;
                float sessionDuration = Time.time - pingSessionStartTime;
                
                Debug.Log($"[SteamP2PTest] 🏓 Ping session completed!");
                Debug.Log($"[SteamP2PTest] ═══════════════════════════════════");
                Debug.Log($"[SteamP2PTest] Target: {targetSteamId}");
                Debug.Log($"[SteamP2PTest] Duration: {sessionDuration:F1}s");
                Debug.Log($"[SteamP2PTest] Pings sent: {pingsSent}");
                Debug.Log($"[SteamP2PTest] Pongs received: {pongsReceived}");
                Debug.Log($"[SteamP2PTest] Packet loss: {(pingsSent - pongsReceived)}/{pingsSent} ({((float)(pingsSent - pongsReceived) / pingsSent * 100):F1}%)");
                
                if (pingTimes.Count > 0)
                {
                    float minLatency = float.MaxValue;
                    float maxLatency = float.MinValue;
                    float totalLatency = 0f;
                    
                    for (int i = 0; i < pingTimes.Count; i++)
                    {
                        if (pingTimes[i] < minLatency) minLatency = pingTimes[i];
                        if (pingTimes[i] > maxLatency) maxLatency = pingTimes[i];
                        totalLatency += pingTimes[i];
                    }
                    
                    float avgLatency = totalLatency / pingTimes.Count;
                    
                    Debug.Log($"[SteamP2PTest] Latency - Min: {minLatency:F1}ms, Max: {maxLatency:F1}ms, Avg: {avgLatency:F1}ms");
                }
                else
                {
                    Debug.Log($"[SteamP2PTest] No latency data (no successful pings)");
                }
                
                Debug.Log($"[SteamP2PTest] ═══════════════════════════════════");
                
                // Connection quality assessment
                float packetLossPercent = (float)(pingsSent - pongsReceived) / pingsSent * 100;
                string connectionQuality = "EXCELLENT";
                if (packetLossPercent > 5f) connectionQuality = "GOOD";
                if (packetLossPercent > 15f) connectionQuality = "FAIR";
                if (packetLossPercent > 30f) connectionQuality = "POOR";
                if (packetLossPercent > 50f) connectionQuality = "VERY POOR";
                
                Debug.Log($"[SteamP2PTest] 🔗 P2P Connection Quality: {connectionQuality}");
                
                // Unsubscribe from events
                if (!ReferenceEquals(steamNet, null))
                {
                    steamNet.OnDataReceived -= OnPingDataReceived;
                }
                
                // Clear ping data
                pendingPings.Clear();
                pingTimes.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error completing ping session: {e.Message}");
                pingSessionActive = false;
            }
        }
        
        /// <summary>
        /// Stop the current ping session
        /// </summary>
        private void StopPingSession()
        {
            try
            {
                if (pingSessionActive)
                {
                    Debug.Log("[SteamP2PTest] Stopping ping session...");
                    CompletePingSession();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2PTest] Error stopping ping session: {e.Message}");
                pingSessionActive = false;
            }
        }
    }
}
