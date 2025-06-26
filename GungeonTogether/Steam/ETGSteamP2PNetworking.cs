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
        
        // Events using custom delegates
        public event PlayerJoinedHandler OnPlayerJoined;
        public event PlayerLeftHandler OnPlayerLeft;
        public event DataReceivedHandler OnDataReceived;
        
        // Join request handling
        public event System.Action<ulong> OnJoinRequested;
        
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
                SteamCallbackManager.OnJoinRequested += HandleJoinRequested;
                
                Debug.Log("[ETGSteamP2P] Created Steam P2P networking instance (lazy initialization)");
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
                    Debug.Log("[ETGSteamP2P] Successfully initialized using ETG's built-in Steamworks.NET");
                    
                    // Initialize Steam callbacks for overlay join functionality
                    SteamCallbackManager.InitializeSteamCallbacks();
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] Steam networking not available - ETG may not have P2P support");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Failed to initialize Steam types: {e.Message}");
                isInitialized = false;
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
        /// Send P2P packet to target Steam user
        /// </summary>
        public bool SendP2PPacket(ulong targetSteamId, byte[] data)
        {
            try
            {
                EnsureInitialized();
                
                if (!isInitialized || ReferenceEquals(SteamReflectionHelper.SendP2PPacketMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] P2P networking not available");
                    return false;
                }
                
                // Convert Steam ID to proper format
                object steamIdParam = SteamReflectionHelper.ConvertToCSteamID(targetSteamId);
                if (ReferenceEquals(steamIdParam, null))
                {
                    steamIdParam = targetSteamId; // Fallback to raw ulong
                }
                
                // Try different parameter signatures for SendP2PPacket
                bool success = SteamReflectionHelper.TryDifferentSendSignatures(steamIdParam, data);
                
                if (success)
                {
                    // Only log successful sends in debug mode or for important packets
                    // This prevents spam when sending frequent updates
                    return true;
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error sending P2P packet: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check for incoming P2P packets and process them
        /// </summary>
        public void CheckForIncomingPackets()
        {
            try
            {
                EnsureInitialized();
                
                if (!isInitialized) return;
                
                var isP2PPacketAvailableMethod = SteamReflectionHelper.IsP2PPacketAvailableMethod;
                
                if (!ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    // Simple approach: try the most common signature patterns
                    try
                    {
                        uint packetSize = 0;
                        int channel = 0;
                        
                        // Try pattern 1: (out uint, int) - most common
                        object[] args = new object[] { packetSize, channel };
                        object result = isP2PPacketAvailableMethod.Invoke(null, args);
                        
                        if (result is bool hasPacket && hasPacket)
                        {
                            // Get packet size from out parameter
                            packetSize = Convert.ToUInt32(args[0]);
                            if (packetSize > 0)
                            {
                                ReadIncomingPacket(packetSize);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // First attempt failed, try alternative approach
                        try
                        {
                            uint packetSize = 1024; // Default size
                            int channel = 0;
                            
                            object[] args = new object[] { packetSize, channel };
                            object result = isP2PPacketAvailableMethod.Invoke(null, args);
                            
                            if (result is bool hasPacket && hasPacket)
                            {
                                ReadIncomingPacket(1024); // Use default size
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore double failures to avoid spam
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Don't spam errors for packet checking - only log once every 5 seconds
                if (ReferenceEquals(Time.frameCount % 300, 0)) // Log once every 5 seconds at 60fps
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error checking for incoming packets: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Read an incoming P2P packet
        /// </summary>
        private void ReadIncomingPacket(uint knownSize = 0)
        {
            try
            {
                var readP2PPacketMethod = SteamReflectionHelper.ReadP2PPacketMethod;
                if (ReferenceEquals(readP2PPacketMethod, null)) return;
                
                // Try reading with known size first
                uint packetSize = knownSize > 0 ? knownSize : 1024; // Default buffer size
                byte[] buffer = new byte[packetSize];
                uint actualSize = 0;
                uint channel = 0;
                ulong remoteSteamId = 0;
                
                object[] parameters = new object[] { buffer, packetSize, actualSize, remoteSteamId, channel };
                object result = readP2PPacketMethod.Invoke(null, parameters);
                
                if (result is bool success && success)
                {
                    // Extract the actual values from the parameters
                    actualSize = Convert.ToUInt32(parameters[2]);
                    remoteSteamId = Convert.ToUInt64(parameters[3]);
                    
                    // Resize buffer to actual size
                    byte[] actualData = new byte[actualSize];
                    Array.Copy(buffer, actualData, actualSize);
                    
                    // Process the received packet
                    ProcessReceivedPacket(remoteSteamId, actualData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error reading incoming packet: {e.Message}");
            }
        }
        
        /// <summary>
        /// Process a received P2P packet and fire the appropriate events
        /// </summary>
        private void ProcessReceivedPacket(ulong remoteSteamId, byte[] packetData)
        {
            try
            {
                if (ReferenceEquals(packetData, null) || ReferenceEquals(packetData.Length, 0))
                    return;
                
                string packetText = System.Text.Encoding.UTF8.GetString(packetData);
                
                // Handle different packet types
                if (packetText.StartsWith("HANDSHAKE:"))
                {
                    HandleHandshakePacket(remoteSteamId, packetText);
                }
                else if (packetText.StartsWith("WELCOME:"))
                {
                    Debug.Log($"[ETGSteamP2P] Received welcome from {remoteSteamId}: {packetText}");
                    OnPlayerJoined?.Invoke(remoteSteamId);
                }
                else if (packetText.StartsWith("DISCONNECT:"))
                {
                    Debug.Log($"[ETGSteamP2P] Player {remoteSteamId} disconnected: {packetText}");
                    OnPlayerLeft?.Invoke(remoteSteamId);
                }
                else
                {
                    // Regular data packet
                    OnDataReceived?.Invoke(remoteSteamId, packetData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error processing received packet: {e.Message}");
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
                
                if (ulong.TryParse(connectString, out ulong hostSteamId))
                {
                    // Set invite info so the system knows someone wants to join us
                    SteamHostManager.SetInviteInfo(hostSteamId);
                    
                    // Fire join requested event
                    Instance?.OnJoinRequested?.Invoke(hostSteamId);
                    
                    Debug.Log($"[ETGSteamP2P] Processed overlay join request for host: {hostSteamId}");
                }
                else
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not parse Steam ID from connect string: {connectString}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling overlay join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle handshake packets for P2P connection establishment
        /// </summary>
        private void HandleHandshakePacket(ulong remoteSteamId, string packetText)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Received handshake from {remoteSteamId}: {packetText}");
                
                // Extract handshake info
                string[] parts = packetText.Split(':');
                if (parts.Length >= 2)
                {
                    string handshakeType = parts[1];
                    
                    if (string.Equals(handshakeType, "REQUEST"))
                    {
                        // Someone wants to connect to us
                        // Send handshake response
                        string response = "HANDSHAKE:RESPONSE";
                        byte[] responseData = System.Text.Encoding.UTF8.GetBytes(response);
                        
                        if (SendP2PPacket(remoteSteamId, responseData))
                        {
                            Debug.Log($"[ETGSteamP2P] Sent handshake response to {remoteSteamId}");
                            
                            // Accept the connection
                            if (AcceptP2PSession(remoteSteamId))
                            {
                                OnPlayerJoined?.Invoke(remoteSteamId);
                            }
                        }
                    }
                    else if (string.Equals(handshakeType, "RESPONSE"))
                    {
                        // Host responded to our handshake
                        Debug.Log($"[ETGSteamP2P] Handshake successful with host {remoteSteamId}");
                        OnPlayerJoined?.Invoke(remoteSteamId);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling handshake packet: {e.Message}");
            }
        }
        
        /// <summary>
        /// Accept P2P session with user
        /// </summary>
        public bool AcceptP2PSession(ulong steamId)
        {
            try
            {
                EnsureInitialized();
                
                var acceptP2PSessionMethod = SteamReflectionHelper.AcceptP2PSessionMethod;
                if (!ReferenceEquals(acceptP2PSessionMethod, null))
                {
                    object steamIdParam = SteamReflectionHelper.ConvertToCSteamID(steamId);
                    if (!ReferenceEquals(steamIdParam, null))
                    {
                        object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamIdParam });
                        if (!ReferenceEquals(result, null) && result is bool success)
                        {
                            if (success)
                            {
                                Debug.Log($"[ETGSteamP2P] Accepted P2P session with {steamId}");
                                SteamFallbackDetection.AcceptSession(steamId);
                                return true;
                            }
                        }
                    }
                    else
                    {
                        // Fallback: try with raw ulong
                        object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamId });
                        if (!ReferenceEquals(result, null) && result is bool success)
                        {
                            if (success)
                            {
                                Debug.Log($"[ETGSteamP2P] Accepted P2P session with {steamId} (raw ulong)");
                                SteamFallbackDetection.AcceptSession(steamId);
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error accepting P2P session: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Close P2P session with user
        /// </summary>
        public bool CloseP2PSession(ulong steamId)
        {
            try
            {
                EnsureInitialized();
                
                var closeP2PSessionMethod = SteamReflectionHelper.CloseP2PSessionMethod;
                if (!ReferenceEquals(closeP2PSessionMethod, null))
                {
                    object steamIdParam = SteamReflectionHelper.ConvertToCSteamID(steamId);
                    object result = closeP2PSessionMethod.Invoke(null, new object[] { steamIdParam ?? steamId });
                    
                    if (!ReferenceEquals(result,null) && result is bool success)
                    {
                        if (success)
                        {
                            Debug.Log($"[ETGSteamP2P] Closed P2P session with {steamId}");
                            SteamFallbackDetection.RemoveAcceptedSession(steamId);
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error closing P2P session: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Set Steam Rich Presence to show in friends list and enable "Join Game"
        /// </summary>
        public bool SetRichPresence(string key, string value)
        {
            try
            {
                EnsureInitialized();
                
                var setRichPresenceMethod = SteamReflectionHelper.SetRichPresenceMethod;
                if (!ReferenceEquals(setRichPresenceMethod, null))
                {
                    object result = setRichPresenceMethod.Invoke(null, new object[] { key, value });
                    if (!ReferenceEquals(result,null) && result is bool success)
                    {
                        if (success)
                        {
                            Debug.Log($"[ETGSteamP2P] Set Rich Presence - {key}: {value}");
                            return true;
                        }
                    }
                }
                
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
                    if (!ReferenceEquals(result,null) && result is bool success)
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
        /// Create a Steam lobby for multiplayer session
        /// </summary>
        public bool CreateLobby(int maxPlayers = 4)
        {
            try
            {
                EnsureInitialized();
                return SteamHostManager.CreateLobby(maxPlayers);
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
        public bool JoinLobby(ulong lobbyId)
        {
            try
            {
                EnsureInitialized();
                return SteamHostManager.JoinLobby(lobbyId);
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
        /// Handle a player attempting to join via Steam overlay "Join Game"
        /// </summary>
        public void HandleJoinRequest(ulong joinerSteamId)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Handling join request from {joinerSteamId}");
                
                if (AcceptP2PSession(joinerSteamId))
                {
                    // Send welcome message
                    string welcomeMessage = "WELCOME:Connected to session";
                    byte[] welcomeData = System.Text.Encoding.UTF8.GetBytes(welcomeMessage);
                    
                    if (SendP2PPacket(joinerSteamId, welcomeData))
                    {
                        Debug.Log($"[ETGSteamP2P] Sent welcome message to {joinerSteamId}");
                        OnPlayerJoined?.Invoke(joinerSteamId);
                    }
                    else
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Failed to send welcome message to {joinerSteamId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ETGSteamP2P] Failed to accept P2P session with {joinerSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Simulate a join request for testing (F4 key functionality)
        /// </summary>
        public void SimulateJoinRequest(ulong hostSteamId)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Simulating join request to host: {hostSteamId}");
                
                // Set invite info
                SteamHostManager.SetInviteInfo(hostSteamId);
                
                // Fire the join requested event
                OnJoinRequested?.Invoke(hostSteamId);
                    
                Debug.Log($"[ETGSteamP2P] Simulated join request completed for host: {hostSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error simulating join request: {e.Message}");
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
        
        /// <summary>
        /// Initiate a P2P connection to another Steam user
        /// </summary>
        public void InitiateP2PConnection(ulong targetSteamId)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Initiating P2P connection to {targetSteamId}");
                
                // Accept the P2P session first
                if (AcceptP2PSession(targetSteamId))
                {
                    // Send handshake request
                    string handshake = "HANDSHAKE:REQUEST";
                    byte[] handshakeData = System.Text.Encoding.UTF8.GetBytes(handshake);
                    
                    if (SendP2PPacket(targetSteamId, handshakeData))
                    {
                        Debug.Log($"[ETGSteamP2P] Sent handshake request to {targetSteamId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Failed to send handshake to {targetSteamId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ETGSteamP2P] Failed to accept P2P session with {targetSteamId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error initiating P2P connection: {e.Message}");
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
                
                // Check for incoming P2P packets (throttle to every few frames to avoid spam)
                if (ReferenceEquals(Time.frameCount % 3,  0)) // Check every 3rd frame (~20 times per second at 60fps)
                {
                    CheckForIncomingPackets();
                }
                
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
        /// Handle join requests from Steam callbacks
        /// </summary>
        private static void HandleJoinRequested(ulong steamId)
        {
            try
            {
                Instance?.OnJoinRequested?.Invoke(steamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling join request: {e.Message}");
            }
        }
    }
}
