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
        
        // Add missing reflection method fields
        private static System.Reflection.MethodInfo sendP2PPacketMethod;
        private static System.Reflection.MethodInfo readP2PPacketMethod;
        private static System.Reflection.MethodInfo isP2PPacketAvailableMethod;

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

                // Initialize our method references
                sendP2PPacketMethod = SteamReflectionHelper.SendP2PPacketMethod;
                readP2PPacketMethod = SteamReflectionHelper.ReadP2PPacketMethod;
                isP2PPacketAvailableMethod = SteamReflectionHelper.IsP2PPacketAvailableMethod;

                isInitialized = SteamReflectionHelper.IsInitialized;

                if (isInitialized)
                {
                    // Initialize Steam callbacks for overlay join functionality
                    SteamCallbackManager.InitializeSteamCallbacks();

                    // Check callback registration status
                    if (SteamCallbackManager.AreCallbacksRegistered)
                    {
                        Debug.Log("[ETGSteamP2P] Steam join callbacks registered - invites should work");
                    }
                    else
                    {
                        Debug.LogWarning("[ETGSteamP2P] Steam join callbacks NOT registered - invites may not work");
                    }

                    // Process any pending join requests that arrived during early initialization
                    SteamCallbackManager.ProcessPendingJoinRequests();
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

                if (targetSteamId.Equals(0) || ReferenceEquals(data, null) || data.Length.Equals(0))
                {
                    Debug.LogError($"[ETGSteamP2P] Invalid send parameters - SteamID: {targetSteamId}, Data: {data?.Length ?? 0} bytes");
                    return false;
                }

                // CRITICAL: Verify P2P session exists before sending

                // First, ensure P2P session is accepted (critical for Steam P2P)
                bool sessionEnsured = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (AcceptP2PSession(targetSteamId))
                    {
                        sessionEnsured = true;
                        break;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(50); // Wait before retry
                    }
                }

                if (!sessionEnsured)
                {
                    Debug.LogError($"[ETGSteamP2P] Cannot establish P2P session with {targetSteamId}");
                    return false;
                }

                // Wait for session to stabilize
                System.Threading.Thread.Sleep(100);

                if (!ReferenceEquals(sendP2PPacketMethod, null))
                {
                    object steamIdParam = SteamReflectionHelper.ConvertToCSteamID(targetSteamId);
                    if (!ReferenceEquals(steamIdParam, null))
                    {
                        // Try multiple send attempts with different EP2PSend modes
                        var sendModes = new object[]
                        {
                            2, // k_EP2PSendReliable
                            0, // k_EP2PSendUnreliable  
                            3  // k_EP2PSendReliableWithBuffering
                        };

                        foreach (var sendMode in sendModes)
                        {
                            Debug.Log($"[ETGSteamP2P] 📤 Attempting send with mode {sendMode}...");

                            // Call: SendP2PPacket(CSteamID steamIDRemote, byte[] data, uint cubData, EP2PSend eP2PSendType, int nChannel)
                            object result = sendP2PPacketMethod.Invoke(null, new object[]
                            {
                                steamIdParam,
                                data,
                                (uint)data.Length,
                                sendMode,  // EP2PSend mode
                                0          // Channel 0
                            });

                            if (!ReferenceEquals(result, null) && result is bool success && success)
                            {
                                return true;
                            }
                        }

                        Debug.LogError($"[ETGSteamP2P] All send attempts failed for {targetSteamId}");
                        return false;
                    }
                    else
                    {
                        Debug.LogError($"[ETGSteamP2P] Failed to convert Steam ID {targetSteamId} to CSteamID");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("[ETGSteamP2P] SendP2PPacket method not found via reflection");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Exception in SendP2PPacket: {e.Message}");
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

                // Use the exact Steam API signature: Boolean IsP2PPacketAvailable(out UInt32& pcubMsgSize, Int32 nChannel)
                if (!ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    uint packetSize = 0;
                    int channel = 0;
                    
                    // For out parameters in reflection, we need to pass by reference
                    object[] args = new object[] { packetSize, channel };
                    object result = isP2PPacketAvailableMethod.Invoke(null, args);
                    
                    if (result is bool hasPacket && hasPacket)
                    {
                        // Get the actual packet size from the out parameter (args[0] gets modified by the method)
                        packetSize = Convert.ToUInt32(args[0]);
                        
                        if (packetSize > 0)
                        {
                            // Now read the packet using exact signature: Boolean ReadP2PPacket(Byte[] pubDest, UInt32 cubDest, out UInt32& pcubMsgSize, out CSteamID& psteamIDRemote, Int32 nChannel)
                            if (!ReferenceEquals(readP2PPacketMethod, null))
                            {
                                byte[] buffer = new byte[packetSize];
                                uint actualSize = 0;
                                object steamIdObj = SteamReflectionHelper.CreateCSteamID(0);
                                
                                object[] readArgs = new object[] { buffer, packetSize, actualSize, steamIdObj, channel };
                                object readResult = readP2PPacketMethod.Invoke(null, readArgs);
                                
                                if (readResult is bool success && success)
                                {
                                    actualSize = Convert.ToUInt32(readArgs[2]);
                                    ulong senderSteamId = SteamReflectionHelper.ExtractSteamId(readArgs[3]);
                                    
                                    if (actualSize > 0 && senderSteamId > 0)
                                    {
                                        // Copy the actual data
                                        byte[] packetData = new byte[actualSize];
                                        Array.Copy(buffer, packetData, actualSize);
                                        
                                        ProcessReceivedPacket(senderSteamId, packetData);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Don't spam errors for packet checking - only log once every 5 seconds
                if (Time.frameCount % 300 == 0) // Log once every 5 seconds at 60fps
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
                if (ReferenceEquals(readP2PPacketMethod, null)) return;

                // Try reading with known size first
                uint packetSize = knownSize > 0 ? knownSize : 1024; // Default buffer size
                byte[] buffer = new byte[packetSize];
                uint actualSize = 0;
                uint channel = 0;
                object remoteSteamIdObj = null;

                object[] parameters = new object[] { buffer, packetSize, actualSize, remoteSteamIdObj, channel };
                
                try
                {
                    object result = readP2PPacketMethod.Invoke(null, parameters);

                    if (result is bool success && success)
                    {
                        // Extract the actual values from the parameters with better error handling
                        try
                        {
                            actualSize = Convert.ToUInt32(parameters[2]);
                            
                            // Handle Steam ID conversion more robustly
                            ulong remoteSteamId = 0;
                            if (!ReferenceEquals(parameters[3], null))
                            {
                                try
                                {
                                    remoteSteamId = Convert.ToUInt64(parameters[3]);
                                }
                                catch (Exception)
                                {
                                    // Try reflection-based conversion for Steam ID objects
                                    var steamIdObj = parameters[3];
                                    var steamIdType = steamIdObj.GetType();
                                    
                                    var idField = steamIdType.GetField("m_SteamID") ?? 
                                                 steamIdType.GetField("SteamID") ?? 
                                                 steamIdType.GetField("steamID") ??
                                                 steamIdType.GetField("value") ??
                                                 steamIdType.GetField("Value");
                                    
                                    if (!ReferenceEquals(idField, null))
                                    {
                                        var fieldValue = idField.GetValue(steamIdObj);
                                        remoteSteamId = Convert.ToUInt64(fieldValue);
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[ETGSteamP2P] Could not extract Steam ID from type: {steamIdType.FullName}");
                                        return;
                                    }
                                }
                            }

                            // Resize buffer to actual size
                            byte[] actualData = new byte[actualSize];
                            Array.Copy(buffer, actualData, actualSize);

                            Debug.Log($"[ETGSteamP2P] 📨 ReadIncomingPacket SUCCESS: {actualSize} bytes from {remoteSteamId}");

                            // Process the received packet
                            ProcessReceivedPacket(remoteSteamId, actualData);
                        }
                        catch (Exception paramError)
                        {
                            Debug.LogError($"[ETGSteamP2P] Error processing ReadP2PPacket parameters: {paramError.Message}");
                        }
                    }
                }
                catch (Exception invokeError)
                {
                    Debug.LogError($"[ETGSteamP2P] Error invoking ReadP2PPacket: {invokeError.Message}");
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

                    // Send join request notification to the host so they know we're trying to join
                    SendJoinRequestToHost(hostSteamId);

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

                if (steamId.Equals(0)) return false;

                // Try multiple times to ensure session acceptance
                bool success = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var acceptP2PSessionMethod = SteamReflectionHelper.AcceptP2PSessionMethod;
                    if (!ReferenceEquals(acceptP2PSessionMethod, null))
                    {
                        object steamIdParam = SteamReflectionHelper.ConvertToCSteamID(steamId);
                        if (!ReferenceEquals(steamIdParam, null))
                        {
                            object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamIdParam });
                            if (!ReferenceEquals(result, null) && result is bool accepted && accepted)
                            {
                                success = true;
                                Debug.Log($"[ETGSteamP2P] ✅ P2P session accepted with {steamId} (attempt {attempt + 1})");
                                SteamFallbackDetection.AcceptSession(steamId);
                                break;
                            }
                            else
                            {
                                Debug.LogWarning($"[ETGSteamP2P] P2P session acceptance failed (attempt {attempt + 1})");
                                System.Threading.Thread.Sleep(10); // Brief delay before retry
                            }
                        }
                        else
                        {
                            // Fallback: try with raw ulong
                            object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamId });
                            if (!ReferenceEquals(result, null) && result is bool accepted && accepted)
                            {
                                success = true;
                                Debug.Log($"[ETGSteamP2P] ✅ P2P session accepted with {steamId} (raw ulong, attempt {attempt + 1})");
                                SteamFallbackDetection.AcceptSession(steamId);
                                break;
                            }
                            else
                            {
                                Debug.LogWarning($"[ETGSteamP2P] P2P session acceptance failed with raw ulong (attempt {attempt + 1})");
                                System.Threading.Thread.Sleep(10); // Brief delay before retry
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("[ETGSteamP2P] AcceptP2PSession method not found");
                        break;
                    }
                }

                if (!success)
                {
                    Debug.LogError($"[ETGSteamP2P] ❌ Failed to accept P2P session with {steamId} after 3 attempts");
                }

                return success;
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

                    if (!ReferenceEquals(result, null) && result is bool success)
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
                    if (!ReferenceEquals(result, null) && result is bool success)
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

        /// <summary>
        /// Read P2P session requests from Steam
        /// </summary>
        public bool ReadP2PSessionRequest(out ulong requestingSteamId)
        {
            requestingSteamId = 0;
            try
            {
                EnsureInitialized();

                if (!isInitialized) return false;

                // Try to use Steam's session request reading if available
                var readSessionRequestMethod = SteamReflectionHelper.ReadP2PSessionRequestMethod;
                if (!ReferenceEquals(readSessionRequestMethod, null))
                {
                    object[] parameters = new object[] { (ulong)0 };
                    object result = readSessionRequestMethod.Invoke(null, parameters);

                    if (result is bool hasRequest && hasRequest)
                    {
                        requestingSteamId = Convert.ToUInt64(parameters[0]);
                        Debug.Log($"[ETGSteamP2P] Found P2P session request from: {requestingSteamId}");
                        return true;
                    }
                }

                // Fallback: Check Steam callbacks for pending requests
                return SteamCallbackManager.CheckForPendingSessionRequests(out requestingSteamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error reading P2P session request: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Read P2P packets from Steam
        /// </summary>
        public bool ReadP2PPacket(out ulong senderSteamId, out byte[] data)
        {
            senderSteamId = 0;
            data = null;

            try
            {
                EnsureInitialized();

                // Log the method signature for debugging (only once per session)
                if (!ReferenceEquals(readP2PPacketMethod, null) && Time.frameCount % 1800 == 0) // Every 30 seconds at 60fps
                {
                    var parameters = readP2PPacketMethod.GetParameters();
                    var paramStr = "";
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) paramStr += ", ";
                        string prefix = parameters[i].IsOut ? "out " : (parameters[i].ParameterType.IsByRef ? "ref " : "");
                        paramStr += $"{prefix}{parameters[i].ParameterType.Name} {parameters[i].Name}";
                    }
                    Debug.Log($"[ETGSteamP2P] 🔍 ReadP2PPacket signature: {readP2PPacketMethod.ReturnType.Name} ReadP2PPacket({paramStr})");
                }
                
                // Also log IsP2PPacketAvailable signature
                if (!ReferenceEquals(isP2PPacketAvailableMethod, null) && Time.frameCount % 1800 == 0)
                {
                    var parameters = isP2PPacketAvailableMethod.GetParameters();
                    var paramStr = "";
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) paramStr += ", ";
                        string prefix = parameters[i].IsOut ? "out " : (parameters[i].ParameterType.IsByRef ? "ref " : "");
                        paramStr += $"{prefix}{parameters[i].ParameterType.Name} {parameters[i].Name}";
                    }
                    Debug.Log($"[ETGSteamP2P] 🔍 IsP2PPacketAvailable signature: {isP2PPacketAvailableMethod.ReturnType.Name} IsP2PPacketAvailable({paramStr})");
                }

                // Check if packets are available first using the smart helper
                uint packetSize = 0;
                bool hasPacket = SteamReflectionHelper.CallIsP2PPacketAvailable(out packetSize, 0);
                
                if (!hasPacket)
                {
                    // Add periodic debug logging
                    if (Time.frameCount % 600 == 0) // Every 10 seconds at 60fps
                    {
                        Debug.Log($"[ETGSteamP2P] 📭 No packets available (Frame: {Time.frameCount})");
                    }
                    return false;
                }

                Debug.Log($"[ETGSteamP2P] 📬 PACKET DETECTED! Size: {packetSize}");

                // Try to read the packet
                if (!ReferenceEquals(readP2PPacketMethod, null))
                {
                    // Get buffer size from detected packet or use default
                    uint bufferSize = packetSize > 0 ? packetSize : 1024;
                    byte[] buffer = new byte[bufferSize];
                    
                    // Try different ReadP2PPacket parameter combinations
                    bool packetReadSuccess = false;
                    uint actualSize = 0;
                    ulong senderSteamIdOut = 0;
                    
                    // Method 1: Standard signature with CSteamID
                    if (!packetReadSuccess)
                    {
                        try
                        {
                            object steamIdObj = SteamReflectionHelper.CreateCSteamID(0);
                            uint outSize = 0;
                            int channel = 0;
                            
                            object[] params1 = new object[] { buffer, bufferSize, outSize, steamIdObj, channel };
                            object result1 = readP2PPacketMethod.Invoke(null, params1);
                            
                            if (result1 is bool success1 && success1)
                            {
                                actualSize = Convert.ToUInt32(params1[2]);
                                senderSteamIdOut = SteamReflectionHelper.ExtractSteamId(params1[3]);
                                
                                if (actualSize > 0 && senderSteamIdOut > 0)
                                {
                                    packetReadSuccess = true;
                                    Debug.Log($"[ETGSteamP2P] 📨 PACKET READ SUCCESS (CSteamID method): {actualSize} bytes from {senderSteamIdOut}");
                                }
                            }
                        }
                        catch (Exception ex1)
                        {
                            Debug.Log($"[ETGSteamP2P] ReadP2PPacket CSteamID method failed: {ex1.Message}");
                        }
                    }
                    
                    // Method 2: Try with ulong Steam ID
                    if (!packetReadSuccess)
                    {
                        try
                        {
                            uint outSize = 0;
                            ulong outSteamId = 0;
                            int channel = 0;
                            
                            object[] params2 = new object[] { buffer, bufferSize, outSize, outSteamId, channel };
                            object result2 = readP2PPacketMethod.Invoke(null, params2);
                            
                            if (result2 is bool success2 && success2)
                            {
                                actualSize = Convert.ToUInt32(params2[2]);
                                senderSteamIdOut = Convert.ToUInt64(params2[3]);
                                
                                if (actualSize > 0 && senderSteamIdOut > 0)
                                {
                                    packetReadSuccess = true;
                                    Debug.Log($"[ETGSteamP2P] 📨 PACKET READ SUCCESS (ulong method): {actualSize} bytes from {senderSteamIdOut}");
                                }
                            }
                        }
                        catch (Exception ex2)
                        {
                            Debug.Log($"[ETGSteamP2P] ReadP2PPacket ulong method failed: {ex2.Message}");
                        }
                    }
                    
                    // Method 3: Try simpler signature without buffer size
                    if (!packetReadSuccess)
                    {
                        try
                        {
                            uint outSize = 0;
                            ulong outSteamId = 0;
                            int channel = 0;
                            
                            object[] params3 = new object[] { buffer, outSize, outSteamId, channel };
                            object result3 = readP2PPacketMethod.Invoke(null, params3);
                            
                            if (result3 is bool success3 && success3)
                            {
                                actualSize = Convert.ToUInt32(params3[1]);
                                senderSteamIdOut = Convert.ToUInt64(params3[2]);
                                
                                if (actualSize > 0 && senderSteamIdOut > 0)
                                {
                                    packetReadSuccess = true;
                                    Debug.Log($"[ETGSteamP2P] 📨 PACKET READ SUCCESS (simple method): {actualSize} bytes from {senderSteamIdOut}");
                                }
                            }
                        }
                        catch (Exception ex3)
                        {
                            Debug.Log($"[ETGSteamP2P] ReadP2PPacket simple method failed: {ex3.Message}");
                        }
                    }
                    
                    // If we successfully read a packet, process it
                    if (packetReadSuccess && actualSize > 0 && senderSteamIdOut > 0)
                    {
                        senderSteamId = senderSteamIdOut;
                        data = new byte[actualSize];
                        Array.Copy(buffer, data, (int)actualSize);
                        
                        Debug.Log($"[ETGSteamP2P] � Packet processed: Type={data[0]}, Size={data.Length}");
                        return true;
                    }
                    else
                    {
                        // No packet was successfully read
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("[ETGSteamP2P] ❌ ReadP2PPacket method not found");
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] ❌ Exception in ReadP2PPacket: {e.Message}");
                return false;
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
                if (ReferenceEquals(Time.frameCount % 3, 0)) // Check every 3rd frame (~20 times per second at 60fps)
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
                // Send join request notification to the host so they know we're trying to join
                SendJoinRequestToHost(steamId);
                
                Instance?.OnJoinRequested?.Invoke(steamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Check Steam API status and P2P connectivity
        /// </summary>
        public void DiagnoseSteamP2PStatus()
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] 🔍 STEAM P2P DIAGNOSTIC REPORT");
                Debug.Log($"[ETGSteamP2P] ================================");
                
                // Check if Steam is running and get basic info
                var mySteamId = GetSteamID();
                Debug.Log($"[ETGSteamP2P] My Steam ID: {mySteamId}");
                
                // Check Steam initialization
                Debug.Log($"[ETGSteamP2P] Steam reflection initialized: {SteamReflectionHelper.IsInitialized}");
                Debug.Log($"[ETGSteamP2P] ETGSteamP2P initialized: {isInitialized}");
                
                // Check P2P networking status
                Debug.Log($"[ETGSteamP2P] SendP2PPacket method available: {!ReferenceEquals(sendP2PPacketMethod, null)}");
                Debug.Log($"[ETGSteamP2P] ReadP2PPacket method available: {!ReferenceEquals(readP2PPacketMethod, null)}");
                Debug.Log($"[ETGSteamP2P] IsP2PPacketAvailable method available: {!ReferenceEquals(isP2PPacketAvailableMethod, null)}");
                Debug.Log($"[ETGSteamP2P] AcceptP2PSession method available: {!ReferenceEquals(SteamReflectionHelper.AcceptP2PSessionMethod, null)}");
                
                Debug.Log($"[ETGSteamP2P] ================================");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error in diagnostic: {e.Message}");
            }
        }

        /// <summary>
        /// Monitor for incoming P2P connections (for hosts to detect joiners)
        /// </summary>
        public static bool DetectIncomingJoinAttempts(out ulong requestingSteamId)
        {
            requestingSteamId = 0;
            
            try
            {
                var instance = Instance;
                if (ReferenceEquals(instance, null) || !instance.isInitialized)
                    return false;
                
                // Check if there are any P2P packets waiting (indicates someone is trying to connect)
                if (instance.HasP2PPacketsAvailable())
                {
                    // Read the packet to get the sender Steam ID
                    if (instance.ReadP2PPacket(out ulong senderSteamId, out byte[] data))
                    {
                        requestingSteamId = senderSteamId;
                        Debug.Log($"[ETGSteamP2P] 🎯 HOST detected incoming connection from: {senderSteamId}");
                        
                        // Put the packet back by not consuming it here
                        // The actual game logic will read it properly later
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error detecting incoming join attempts: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send a join request notification to a host (used by joiners to notify hosts)
        /// </summary>
        public static bool SendJoinRequestToHost(ulong hostSteamId)
        {
            try
            {
                var instance = Instance;
                if (ReferenceEquals(instance, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Instance not available for sending join request");
                    return false;
                }
                
                // Ensure we're initialized before sending
                instance.EnsureInitialized();
                
                // Create a special "join request" packet
                byte[] joinRequestPacket = new byte[] { 0xFF, 0xFE, 0x01 }; // Special magic bytes for join request
                
                // Try sending multiple times to ensure delivery
                bool sent = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    bool attemptResult = instance.SendP2PPacket(hostSteamId, joinRequestPacket);
                    if (attemptResult)
                    {
                        sent = true;
                        Debug.Log($"[ETGSteamP2P] Join request sent to host: {hostSteamId}");
                        break;
                    }
                    else
                    {
                        if (attempt < 2) // Don't sleep on the last attempt
                        {
                            System.Threading.Thread.Sleep(100); // Wait 100ms before retry
                        }
                    }
                }
                
                if (!sent)
                {
                    Debug.LogError($"[ETGSteamP2P] Failed to send join request to host: {hostSteamId}");
                }
                
                return sent;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error sending join request to host: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check for incoming join request notifications (for hosts)
        /// </summary>
        public static bool CheckForJoinRequestNotifications(out ulong requestingSteamId)
        {
            requestingSteamId = 0;
            
            try
            {
                var instance = Instance;
                if (ReferenceEquals(instance, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Instance not available for checking join requests");
                    return false;
                }
                
                // Log periodically to show we're checking
                if (Time.frameCount % 900 == 0) // Every 15 seconds at 60fps
                {
                    Debug.Log("[ETGSteamP2P] HOST: Actively checking for join request notifications...");
                    Debug.Log($"[ETGSteamP2P] HOST: Instance initialized: {instance.isInitialized}");
                    Debug.Log($"[ETGSteamP2P] HOST: HasP2PPacketsAvailable: {instance.HasP2PPacketsAvailable()}");
                }
                
                if (!instance.HasP2PPacketsAvailable())
                {
                    return false;
                }
                
                Debug.Log("[ETGSteamP2P] HOST: P2P packets available, checking for join requests...");
                
                // Peek at the packet to see if it's a join request notification
                if (instance.ReadP2PPacket(out ulong senderSteamId, out byte[] data))
                {
                    Debug.Log($"[ETGSteamP2P] HOST: Read packet from {senderSteamId}, data length: {data?.Length ?? 0}");
                    
                    if (!ReferenceEquals(data, null) && data.Length >= 3)
                    {
                        Debug.Log($"[ETGSteamP2P] HOST: Packet data: [{data[0]:X2}, {data[1]:X2}, {data[2]:X2}]");
                    }
                    
                    // Check if this is a join request notification packet
                    if (data != null && data.Length == 3 && 
                        data[0] == 0xFF && data[1] == 0xFE && data[2] == 0x01)
                    {
                        requestingSteamId = senderSteamId;
                        Debug.Log($"[ETGSteamP2P] HOST: Detected join request notification from: {senderSteamId}");
                        return true;
                    }
                    else
                    {
                        // This is a regular game packet, not a join request notification
                        Debug.Log($"[ETGSteamP2P] HOST: Regular packet detected from {senderSteamId}, not a join request");
                        return false;
                    }
                }
                else
                {
                    Debug.Log("[ETGSteamP2P] HOST: Failed to read P2P packet");
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error checking for join request notifications: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if there are P2P packets available
        /// </summary>
        private bool HasP2PPacketsAvailable()
        {
            try
            {
                if (!ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    uint packetSize = 0;
                    int channel = 0;
                    
                    object[] args = new object[] { packetSize, channel };
                    object result = isP2PPacketAvailableMethod.Invoke(null, args);
                    
                    bool hasPacket = result is bool && (bool)result;
                    
                    // Log periodically for debugging
                    if (Time.frameCount % 1800 == 0) // Every 30 seconds at 60fps
                    {
                        Debug.Log($"[ETGSteamP2P] HOST: Packet availability check: {hasPacket}");
                        if (hasPacket)
                        {
                            uint detectedSize = Convert.ToUInt32(args[0]);
                            Debug.Log($"[ETGSteamP2P] HOST: Detected packet size: {detectedSize}");
                        }
                    }
                    
                    return hasPacket;
                }
                return false;
            }
            catch (Exception e)
            {
                if (Time.frameCount % 3600 == 0) // Every minute at 60fps
                {
                    Debug.LogError($"[ETGSteamP2P] Error checking packet availability: {e.Message}");
                }
                return false;
            }
        }
    }
}
