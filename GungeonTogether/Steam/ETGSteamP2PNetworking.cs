using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// ETG Steam P2P Networking using reflection to access ETG's built-in Steamworks.NET
    /// This implementation uses reflection to safely interact with ETG's Steam integration
    /// without requiring external dependencies or causing TypeLoadException issues.
    /// </summary>
    public class ETGSteamP2PNetworking : ISteamNetworking
    {
        // Steam callback delegates and event system
        private static object steamCallbackHandle = null;
        private static object overlayCallbackHandle = null;
        private static object lobbyCallbackHandle = null;
        private static MethodInfo runCallbacksMethod = null;
        private static Type callbackType = null;
        private static Type overlayActivatedType = null;
        private static Type lobbyJoinRequestedType = null;
        
        // Steam callback event handlers
        public static event Action<string> OnOverlayJoinRequested;
        public static event Action<bool> OnOverlayActivated;
        
        public static ETGSteamP2PNetworking Instance { get; private set; }
        
        // Events using custom delegates
        public event PlayerJoinedHandler OnPlayerJoined;
        public event PlayerLeftHandler OnPlayerLeft;
        public event DataReceivedHandler OnDataReceived;
        
        // Reflection types and methods for ETG's Steamworks
        private static Type steamUserType;
        private static Type steamFriendsType;
        private static Type steamNetworkingType;
        private static Type steamMatchmakingType;
        private static Type steamUtilsType;
        
        // Additional reflection types for Rich Presence and lobbies
        private static Type steamAppsType;
        private static Type gameJoinRequestedCallbackType;
        private static Type lobbyEnterCallbackType;
        private static Type lobbyCreatedCallbackType;
        
        // Callback and event handling
        private static MethodInfo registerCallbackMethod;
        private static MethodInfo unregisterCallbackMethod;
        
        // Reflected methods
        private static MethodInfo getSteamIdMethod;
        private static MethodInfo sendP2PPacketMethod;
        private static MethodInfo readP2PPacketMethod;
        private static MethodInfo isP2PPacketAvailableMethod;
        private static MethodInfo acceptP2PSessionMethod;
        private static MethodInfo closeP2PSessionMethod;
        
        // Rich Presence and lobby methods
        private static MethodInfo setRichPresenceMethod;
        private static MethodInfo clearRichPresenceMethod;
        private static MethodInfo createLobbyMethod;
        private static MethodInfo joinLobbyMethod;
        private static MethodInfo leaveLobbyMethod;
        private static MethodInfo setLobbyDataMethod;
        private static MethodInfo getLobbyDataMethod;
        private static MethodInfo setLobbyJoinableMethod;
        private static MethodInfo inviteUserToLobbyMethod;
        
        // Current lobby state
        private static ulong currentLobbyId = 0;
        private static bool isLobbyHost = false;
        
        // Join request handling
        public event System.Action<ulong> OnJoinRequested;
        private static bool joinCallbacksRegistered = false;
        
        private static bool initialized = false;
        private bool isInitialized = false;
        
        // Steam invite handling
        private static ulong lastInvitedBySteamId = 0;
        private static string lastInviteLobbyId = "";
        private static ulong currentHostSteamId = 0; // Track who is currently hosting
        private static bool isCurrentlyHosting = false;
        
        // Automatic host discovery system
        private static System.Collections.Generic.Dictionary<ulong, HostInfo> availableHosts = new System.Collections.Generic.Dictionary<ulong, HostInfo>();
        
        public struct HostInfo
        {
            public ulong steamId;
            public string sessionName;
            public int playerCount;
            public float lastSeen;
            public bool isActive;
        }
        
        public ETGSteamP2PNetworking()
        {
            try
            {
                Instance = this;
                isInitialized = false; // Will be set to true when/if initialization succeeds
                
                Debug.Log("[ETGSteamP2P] Created Steam P2P networking instance (lazy initialization)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error creating instance: {e.Message}");
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// Initialize Steam types via reflection to use ETG's Steamworks
        /// </summary>
        private static void InitializeSteamTypes()
        {
            try
            {
                // Get ETG's Assembly-CSharp-firstpass which contains Steamworks types (discovered via diagnostics)
                Assembly steamworksAssembly = null;
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                
                // Find Assembly-CSharp-firstpass which contains ETG's Steamworks.NET
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (assemblies[i].GetName().Name == "Assembly-CSharp-firstpass")
                    {
                        steamworksAssembly = assemblies[i];
                        break;
                    }
                }
                
                if (object.ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Assembly-CSharp-firstpass not found - Steamworks.NET not available");
                    return;
                }
                
                Debug.Log("[ETGSteamP2P] Found Assembly-CSharp-firstpass with Steamworks.NET");
                
                // Find Steam types in Steamworks namespace (discovered via diagnostics)
                steamUserType = steamworksAssembly.GetType("Steamworks.SteamUser", false);
                steamFriendsType = steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                steamMatchmakingType = steamworksAssembly.GetType("Steamworks.SteamMatchmaking", false);
                steamUtilsType = steamworksAssembly.GetType("Steamworks.SteamUtils", false);
                
                // Find Steam callback types for overlay join functionality
                callbackType = steamworksAssembly.GetType("Steamworks.Callback", false);
                overlayActivatedType = steamworksAssembly.GetType("Steamworks.GameOverlayActivated_t", false);
                lobbyJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested_t", false);
                var steamAPIType = steamworksAssembly.GetType("Steamworks.SteamAPI", false);
                
                if (!object.ReferenceEquals(steamAPIType, null))
                {
                    runCallbacksMethod = steamAPIType.GetMethod("RunCallbacks", BindingFlags.Public | BindingFlags.Static);
                }
                
                Debug.Log($"[ETGSteamP2P] Found Steamworks types:");
                Debug.Log($"  SteamUser: {steamUserType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  SteamFriends: {steamFriendsType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  SteamNetworking: {steamNetworkingType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  SteamMatchmaking: {steamMatchmakingType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  SteamUtils: {steamUtilsType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  SteamApps: {steamAppsType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameJoinRequestedCallback: {gameJoinRequestedCallbackType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  LobbyEnterCallback: {lobbyEnterCallbackType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  LobbyCreatedCallback: {lobbyCreatedCallbackType?.FullName ?? "NOT FOUND"}");
                
                // Cache frequently used methods using proper Steamworks.NET method names
                if (!object.ReferenceEquals(steamUserType, null))
                {
                    // Try common Steamworks.NET method names for getting Steam ID
                    getSteamIdMethod = steamUserType.GetMethod("GetSteamID", BindingFlags.Public | BindingFlags.Static);
                    if (object.ReferenceEquals(getSteamIdMethod, null))
                    {
                        getSteamIdMethod = steamUserType.GetMethod("get_SteamID", BindingFlags.Public | BindingFlags.Static);
                    }
                }
                
                if (!object.ReferenceEquals(steamNetworkingType, null))
                {
                    // Try common Steamworks.NET P2P method names
                    sendP2PPacketMethod = steamNetworkingType.GetMethod("SendP2PPacket", BindingFlags.Public | BindingFlags.Static);
                    readP2PPacketMethod = steamNetworkingType.GetMethod("ReadP2PPacket", BindingFlags.Public | BindingFlags.Static);
                    isP2PPacketAvailableMethod = steamNetworkingType.GetMethod("IsP2PPacketAvailable", BindingFlags.Public | BindingFlags.Static);
                    acceptP2PSessionMethod = steamNetworkingType.GetMethod("AcceptP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                    closeP2PSessionMethod = steamNetworkingType.GetMethod("CloseP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                }
                
                // Cache Rich Presence methods
                if (!object.ReferenceEquals(steamFriendsType, null))
                {
                    setRichPresenceMethod = steamFriendsType.GetMethod("SetRichPresence", BindingFlags.Public | BindingFlags.Static);
                    clearRichPresenceMethod = steamFriendsType.GetMethod("ClearRichPresence", BindingFlags.Public | BindingFlags.Static);
                }
                
                // Cache lobby methods
                if (!object.ReferenceEquals(steamMatchmakingType, null))
                {
                    createLobbyMethod = steamMatchmakingType.GetMethod("CreateLobby", BindingFlags.Public | BindingFlags.Static);
                    joinLobbyMethod = steamMatchmakingType.GetMethod("JoinLobby", BindingFlags.Public | BindingFlags.Static);
                    leaveLobbyMethod = steamMatchmakingType.GetMethod("LeaveLobby", BindingFlags.Public | BindingFlags.Static);
                    setLobbyDataMethod = steamMatchmakingType.GetMethod("SetLobbyData", BindingFlags.Public | BindingFlags.Static);
                    getLobbyDataMethod = steamMatchmakingType.GetMethod("GetLobbyData", BindingFlags.Public | BindingFlags.Static);
                    setLobbyJoinableMethod = steamMatchmakingType.GetMethod("SetLobbyJoinable", BindingFlags.Public | BindingFlags.Static);
                    inviteUserToLobbyMethod = steamMatchmakingType.GetMethod("InviteUserToLobby", BindingFlags.Public | BindingFlags.Static);
                }
                
                if (!object.ReferenceEquals(steamFriendsType, null))
                {
                    // Try common Steamworks.NET method names for Rich Presence and lobbies
                    setRichPresenceMethod = steamFriendsType.GetMethod("SetRichPresence", BindingFlags.Public | BindingFlags.Static);
                    clearRichPresenceMethod = steamFriendsType.GetMethod("ClearRichPresence", BindingFlags.Public | BindingFlags.Static);
                    createLobbyMethod = steamFriendsType.GetMethod("CreateLobby", BindingFlags.Public | BindingFlags.Static);
                    joinLobbyMethod = steamFriendsType.GetMethod("JoinLobby", BindingFlags.Public | BindingFlags.Static);
                    leaveLobbyMethod = steamFriendsType.GetMethod("LeaveLobby", BindingFlags.Public | BindingFlags.Static);
                    setLobbyDataMethod = steamFriendsType.GetMethod("SetLobbyData", BindingFlags.Public | BindingFlags.Static);
                    getLobbyDataMethod = steamFriendsType.GetMethod("GetLobbyData", BindingFlags.Public | BindingFlags.Static);
                    setLobbyJoinableMethod = steamFriendsType.GetMethod("SetLobbyJoinable", BindingFlags.Public | BindingFlags.Static);
                    inviteUserToLobbyMethod = steamFriendsType.GetMethod("InviteUserToLobby", BindingFlags.Public | BindingFlags.Static);
                }
                
                Debug.Log($"[ETGSteamP2P] Cached methods:");
                Debug.Log($"  GetSteamID: {!object.ReferenceEquals(getSteamIdMethod, null)}");
                Debug.Log($"  SendP2PPacket: {!object.ReferenceEquals(sendP2PPacketMethod, null)}");
                Debug.Log($"  ReadP2PPacket: {!object.ReferenceEquals(readP2PPacketMethod, null)}");
                Debug.Log($"  IsP2PPacketAvailable: {!object.ReferenceEquals(isP2PPacketAvailableMethod, null)}");
                Debug.Log($"  AcceptP2PSession: {!object.ReferenceEquals(acceptP2PSessionMethod, null)}");
                Debug.Log($"  CloseP2PSession: {!object.ReferenceEquals(closeP2PSessionMethod, null)}");
                Debug.Log($"  SetRichPresence: {!object.ReferenceEquals(setRichPresenceMethod, null)}");
                Debug.Log($"  ClearRichPresence: {!object.ReferenceEquals(clearRichPresenceMethod, null)}");
                Debug.Log($"  CreateLobby: {!object.ReferenceEquals(createLobbyMethod, null)}");
                Debug.Log($"  JoinLobby: {!object.ReferenceEquals(joinLobbyMethod, null)}");
                Debug.Log($"  LeaveLobby: {!object.ReferenceEquals(leaveLobbyMethod, null)}");
                Debug.Log($"  SetLobbyData: {!object.ReferenceEquals(setLobbyDataMethod, null)}");
                Debug.Log($"  InviteUserToLobby: {!object.ReferenceEquals(inviteUserToLobbyMethod, null)}");
                Debug.Log($"  SetRichPresence: {!object.ReferenceEquals(setRichPresenceMethod, null)}");
                Debug.Log($"  ClearRichPresence: {!object.ReferenceEquals(clearRichPresenceMethod, null)}");
                Debug.Log($"  CreateLobby: {!object.ReferenceEquals(createLobbyMethod, null)}");
                Debug.Log($"  JoinLobby: {!object.ReferenceEquals(joinLobbyMethod, null)}");
                Debug.Log($"  LeaveLobby: {!object.ReferenceEquals(leaveLobbyMethod, null)}");
                Debug.Log($"  SetLobbyData: {!object.ReferenceEquals(setLobbyDataMethod, null)}");
                Debug.Log($"  GetLobbyData: {!object.ReferenceEquals(getLobbyDataMethod, null)}");
                Debug.Log($"  SetLobbyJoinable: {!object.ReferenceEquals(setLobbyJoinableMethod, null)}");
                Debug.Log($"  InviteUserToLobby: {!object.ReferenceEquals(inviteUserToLobbyMethod, null)}");
                
                initialized = (!object.ReferenceEquals(steamNetworkingType, null) && !object.ReferenceEquals(sendP2PPacketMethod, null));
                
                if (initialized)
                {
                    Debug.Log("[ETGSteamP2P] Steam types initialized successfully!");
                    
                    // Initialize Steam callbacks for overlay join functionality
                    InitializeSteamCallbacks();
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] Steam networking types not found - ETG may not have P2P networking support");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Failed to initialize Steam types: {e.Message}");
                initialized = false;
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
                if (!initialized)
                {
                    InitializeSteamTypes();
                }
                
                isInitialized = initialized;
                
                if (isInitialized)
                {
                    Debug.Log("[ETGSteamP2P] Successfully initialized using ETG's built-in Steamworks.NET");
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
        /// Get current Steam user ID
        /// </summary>
        public ulong GetSteamID()
        {
            try
            {
                EnsureInitialized();
                
                if (!object.ReferenceEquals(getSteamIdMethod, null))
                {
                    object result = getSteamIdMethod.Invoke(null, null);
                    if (!object.ReferenceEquals(result, null))
                    {
                        Debug.Log($"[ETGSteamP2P] Steam ID method returned: {result} (Type: {result.GetType().FullName})");
                        
                        // Try different casting approaches for different Steamworks types
                        try
                        {
                            // First try direct cast for primitive types
                            if (result is ulong directULong)
                            {
                                Debug.Log($"[ETGSteamP2P] Got Steam ID (direct ulong): {directULong}");
                                return directULong;
                            }
                            
                            // Try direct convert for numeric types
                            ulong steamId = Convert.ToUInt64(result);
                            Debug.Log($"[ETGSteamP2P] Got Steam ID (convert): {steamId}");
                            return steamId;
                        }
                        catch (Exception castEx)
                        {
                            Debug.Log($"[ETGSteamP2P] Direct cast failed: {castEx.Message}");
                            
                            // Try accessing struct fields if it's a struct
                            Type resultType = result.GetType();
                            Debug.Log($"[ETGSteamP2P] Steam ID result type: {resultType.FullName}");
                            
                            // Check for common Steamworks struct field names
                            var idField = resultType.GetField("m_SteamID") ?? 
                                         resultType.GetField("SteamID") ?? 
                                         resultType.GetField("steamID") ??
                                         resultType.GetField("value") ??
                                         resultType.GetField("Value");
                            
                            if (!object.ReferenceEquals(idField, null))
                            {
                                object fieldValue = idField.GetValue(result);
                                if (!object.ReferenceEquals(fieldValue, null))
                                {
                                    Debug.Log($"[ETGSteamP2P] Field {idField.Name} value: {fieldValue} (Type: {fieldValue.GetType().FullName})");
                                    ulong fieldSteamId = Convert.ToUInt64(fieldValue);
                                    Debug.Log($"[ETGSteamP2P] Got Steam ID from field: {fieldSteamId}");
                                    return fieldSteamId;
                                }
                            }
                            
                            // Try ToString() as last resort
                            string stringValue = result.ToString();
                            Debug.Log($"[ETGSteamP2P] Trying to parse string value: '{stringValue}'");
                            if (ulong.TryParse(stringValue, out ulong parsedId))
                            {
                                Debug.Log($"[ETGSteamP2P] Got Steam ID from string: {parsedId}");
                                return parsedId;
                            }
                            
                            Debug.LogWarning($"[ETGSteamP2P] Could not extract Steam ID from type {resultType.FullName}, value: {result}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[ETGSteamP2P] GetSteamID method returned null");
                    }
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] GetSteamID method not found");
                }
                
                Debug.LogWarning("[ETGSteamP2P] Could not get Steam ID - returning 0");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: {e.Message}\nStack trace: {e.StackTrace}");
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
                
                if (!isInitialized || object.ReferenceEquals(sendP2PPacketMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] P2P networking not available");
                    return false;
                }
                
                // Convert Steam ID to proper format
                object steamIdParam = ConvertToCSteamID(targetSteamId);
                if (steamIdParam == null)
                {
                    steamIdParam = targetSteamId; // Fallback to raw ulong
                }
                
                // Call ETG's SendP2PPacket method via reflection
                // Parameters vary by Steamworks version, try common signatures
                object[] parameters = { steamIdParam, data, data.Length, 0 }; // Last param is usually send type
                object result = sendP2PPacketMethod.Invoke(null, parameters);
                
                if (!object.ReferenceEquals(result, null) && result is bool success)
                {
                    if (success)
                    {
                        Debug.Log($"[ETGSteamP2P] Sent P2P packet to {targetSteamId} ({data.Length} bytes)");
                    }
                    return success;
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
        /// Check if P2P packet is available and read it
        /// </summary>
        public void Update()
        {
            try
            {
                EnsureInitialized();
                
                // Process Steam callbacks for overlay join functionality
                ProcessSteamCallbacks();
                
                // Temporarily disable packet checking to prevent signature mismatch spam
                // TODO: Need to discover the correct IsP2PPacketAvailable signature for ETG's Steamworks
                if (!isInitialized)
                    return;
                
                // AUTOMATIC: Broadcast host availability if we're hosting
                BroadcastHostAvailability();
                
                // For now, just log that we're ready for packets without actually checking
                // This prevents the method signature error spam while keeping the networking ready
                
                /* Disabled until we discover correct signatures:
                if (object.ReferenceEquals(isP2PPacketAvailableMethod, null) || object.ReferenceEquals(readP2PPacketMethod, null))
                    return;
                
                // Check for available packets with proper signature discovery
                TryCheckForPackets();
                */
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error in Update: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to check for packets with different method signatures (disabled for now)
        /// </summary>
        private void TryCheckForPackets()
        {
            // This will be re-enabled once we discover the correct method signatures
            // Current issue: ETG's IsP2PPacketAvailable has different parameters than expected
        }
        
        /// <summary>
        /// Accept P2P session with user
        /// </summary>
        public bool AcceptP2PSession(ulong steamId)
        {
            try
            {
                EnsureInitialized();
                
                if (!object.ReferenceEquals(acceptP2PSessionMethod, null))
                {
                    // Try different parameter formats for AcceptP2PSessionWithUser
                    object steamIdParam = ConvertToCSteamID(steamId);
                    if (steamIdParam != null)
                    {
                        object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamIdParam });
                        if (!object.ReferenceEquals(result, null) && result is bool success)
                        {
                            Debug.Log($"[ETGSteamP2P] Accepted P2P session with {steamId}: {success}");
                            return success;
                        }
                    }
                    else
                    {
                        // Fallback: try with raw ulong
                        object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamId });
                        if (!object.ReferenceEquals(result, null) && result is bool success)
                        {
                            Debug.Log($"[ETGSteamP2P] Accepted P2P session with {steamId}: {success}");
                            return success;
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
                
                if (!object.ReferenceEquals(closeP2PSessionMethod, null))
                {
                    object result = closeP2PSessionMethod.Invoke(null, new object[] { steamId });
                    if (!object.ReferenceEquals(result, null) && result is bool success)
                    {
                        Debug.Log($"[ETGSteamP2P] Closed P2P session with {steamId}: {success}");
                        return success;
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
                
                if (!object.ReferenceEquals(setRichPresenceMethod, null))
                {
                    object result = setRichPresenceMethod.Invoke(null, new object[] { key, value });
                    if (!object.ReferenceEquals(result, null) && result is bool success)
                    {
                        Debug.Log($"[ETGSteamP2P] Set Rich Presence {key}={value}: {success}");
                        return success;
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
                
                if (!object.ReferenceEquals(clearRichPresenceMethod, null))
                {
                    object result = clearRichPresenceMethod.Invoke(null, null);
                    if (!object.ReferenceEquals(result, null) && result is bool success)
                    {
                        Debug.Log($"[ETGSteamP2P] Cleared Rich Presence: {success}");
                        return success;
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
                
                if (!object.ReferenceEquals(createLobbyMethod, null))
                {
                    // Try common Steamworks CreateLobby signatures
                    // Usually takes ELobbyType and maxMembers
                    object[] parameters = { 1, maxPlayers }; // 1 = k_ELobbyTypePublic or similar
                    object result = createLobbyMethod.Invoke(null, parameters);
                    
                    Debug.Log($"[ETGSteamP2P] Create lobby request sent for {maxPlayers} players");
                    isLobbyHost = true;
                    
                    // Note: Actual lobby creation is async via callback
                    return true;
                }
                
                return false;
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
                
                if (!object.ReferenceEquals(joinLobbyMethod, null))
                {
                    object result = joinLobbyMethod.Invoke(null, new object[] { lobbyId });
                    Debug.Log($"[ETGSteamP2P] Join lobby request sent for lobby {lobbyId}");
                    
                    // Note: Actual join is async via callback
                    return true;
                }
                
                return false;
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
                
                if (currentLobbyId != 0 && !object.ReferenceEquals(leaveLobbyMethod, null))
                {
                    object result = leaveLobbyMethod.Invoke(null, new object[] { currentLobbyId });
                    Debug.Log($"[ETGSteamP2P] Left lobby {currentLobbyId}");
                    
                    currentLobbyId = 0;
                    isLobbyHost = false;
                    
                    return true;
                }
                
                return false;
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
                
                if (currentLobbyId != 0 && !object.ReferenceEquals(setLobbyDataMethod, null))
                {
                    object result = setLobbyDataMethod.Invoke(null, new object[] { currentLobbyId, key, value });
                    if (!object.ReferenceEquals(result, null) && result is bool success)
                    {
                        Debug.Log($"[ETGSteamP2P] Set lobby data {key}={value}: {success}");
                        return success;
                    }
                }
                
                return false;
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
                ulong steamId = GetSteamID();
                if (steamId == 0)
                {
                    Debug.LogWarning("[ETGSteamP2P] Cannot setup Rich Presence - no Steam ID");
                    return;
                }
                
                // AUTOMATIC: Register as host in the system
                RegisterAsHost();
                
                // Set Rich Presence to show game status and enable join
                SetRichPresence("status", "Hosting Gungeon Together");
                SetRichPresence("steam_display", "#Status");
                SetRichPresence("connect", steamId.ToString()); // Connect string for joining
                
                // Create a Steam lobby for proper invite functionality
                CreateLobby(4); // Support up to 4 players
                
                Debug.Log($"[ETGSteamP2P] 🎮 Automatically started hosting session with Steam ID: {steamId}");
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
                SetRichPresence("status", "Joining Gungeon Together");
                SetRichPresence("steam_display", "#Status");
                
                Debug.Log($"[ETGSteamP2P] Started joining session from host {hostSteamId}");
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
                // AUTOMATIC: Unregister as host
                UnregisterAsHost();
                
                LeaveLobby();
                ClearRichPresence();
                
                currentLobbyId = 0;
                isLobbyHost = false;
                
                Debug.Log($"[ETGSteamP2P] 🛑 Automatically stopped multiplayer session");
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
                return isInitialized;
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
                Instance = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error during shutdown: {e.Message}");
            }
        }
        
        /// <summary>
        /// Register Steam callbacks for join requests and lobby events
        /// </summary>
        private void RegisterSteamCallbacks()
        {
            try
            {
                if (joinCallbacksRegistered) return;
                
                EnsureInitialized();
                
                Debug.Log("[ETGSteamP2P] Registering Steam callbacks for join requests...");
                
                // For now, we'll simulate callback handling by checking Steam manually
                // Real implementation would register actual Steamworks callbacks
                joinCallbacksRegistered = true;
                
                Debug.Log("[ETGSteamP2P] Steam callbacks registered (manual polling mode)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error registering Steam callbacks: {e.Message}");
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
                EnsureInitialized();
                
                // In a real implementation, this would be handled by Steam callbacks
                // For now, we'll rely on direct P2P packet handling instead of polling
                // This prevents log spam while keeping the interface available
                
                // Join requests will be handled through the SendP2PPacket/ReceiveP2P system
                // when someone actually tries to join via F4 or Steam overlay
                
                // Silently return - no logging to prevent spam
            }
            catch (Exception e)
            {
                // Only log errors, not normal operation
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
                Debug.Log($"[ETGSteamP2P] Handling join request from Steam ID: {joinerSteamId}");
                
                // Accept the P2P session with the joining player
                if (AcceptP2PSession(joinerSteamId))
                {
                    Debug.Log($"[ETGSteamP2P] Accepted P2P session with joiner: {joinerSteamId}");
                    
                    // Send a welcome packet to the joining player
                    byte[] welcomeMessage = System.Text.Encoding.UTF8.GetBytes("WELCOME_TO_GUNGEON_TOGETHER");
                    if (SendP2PPacket(joinerSteamId, welcomeMessage))
                    {
                        Debug.Log($"[ETGSteamP2P] Sent welcome packet to {joinerSteamId}");
                        
                        // Fire join event
                        OnPlayerJoined?.Invoke(joinerSteamId);
                    }
                }
                else
                {
                    Debug.LogError($"[ETGSteamP2P] Failed to accept P2P session with {joinerSteamId}");
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
                
                // Send a join request packet to the host
                byte[] joinMessage = System.Text.Encoding.UTF8.GetBytes("JOIN_REQUEST_GUNGEON_TOGETHER");
                if (SendP2PPacket(hostSteamId, joinMessage))
                {
                    Debug.Log($"[ETGSteamP2P] Sent join request to host {hostSteamId}");
                    
                    // Update Rich Presence to show joining status
                    StartJoiningSession(hostSteamId);
                }
                else
                {
                    Debug.LogError($"[ETGSteamP2P] Failed to send join request to {hostSteamId}");
                }
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
                Debug.Log("[ETGSteamP2P] === Discovering Method Signatures ===");
                
                if (!object.ReferenceEquals(steamNetworkingType, null))
                {
                    Debug.Log("[ETGSteamP2P] SteamNetworking methods:");
                    
                    MethodInfo[] methods = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (method.Name.Contains("P2P"))
                        {
                            var parameters = method.GetParameters();
                            string paramString = "";
                            
                            for (int j = 0; j < parameters.Length; j++)
                            {
                                if (j > 0) paramString += ", ";
                                paramString += $"{parameters[j].ParameterType.Name} {parameters[j].Name}";
                            }
                            
                            Debug.Log($"[ETGSteamP2P]   {method.Name}({paramString}) -> {method.ReturnType.Name}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] SteamNetworking type not available for signature discovery");
                }
                
                Debug.Log("[ETGSteamP2P] === Method Signature Discovery Complete ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error discovering method signatures: {e.Message}");
            }
        }
        
        /// <summary>
        /// Automatically register this player as a host when they start hosting
        /// </summary>
        public static void RegisterAsHost()
        {
            try
            {
                var instance = Instance;
                if (instance != null && instance.IsAvailable())
                {
                    ulong mySteamId = instance.GetSteamID();
                    if (mySteamId != 0)
                    {
                        currentHostSteamId = mySteamId;
                        isCurrentlyHosting = true;
                        
                        // Add ourselves to available hosts
                        availableHosts[mySteamId] = new HostInfo
                        {
                            steamId = mySteamId,
                            sessionName = "Gungeon Together Session",
                            playerCount = 1,
                            lastSeen = Time.time,
                            isActive = true
                        };
                        
                        Debug.Log($"[ETGSteamP2P] 🏠 Automatically registered as host: {mySteamId}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error registering as host: {e.Message}");
            }
        }
        
        /// <summary>
        /// Automatically discover available hosts on the network
        /// </summary>
        public static ulong[] GetAvailableHosts()
        {
            try
            {
                // Clean up old hosts
                var hostsToRemove = new System.Collections.Generic.List<ulong>();
                foreach (var kvp in availableHosts)
                {
                    if (Time.time - kvp.Value.lastSeen > 30f) // 30 second timeout
                    {
                        hostsToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var hostId in hostsToRemove)
                {
                    availableHosts.Remove(hostId);
                    Debug.Log($"[ETGSteamP2P] 🗑️ Removed stale host: {hostId}");
                }
                
                // Return active host Steam IDs
                var activeHosts = new ulong[availableHosts.Count];
                int index = 0;
                foreach (var kvp in availableHosts)
                {
                    if (kvp.Value.isActive)
                    {
                        activeHosts[index++] = kvp.Key;
                    }
                }
                
                // Resize array to actual count
                if (index < activeHosts.Length)
                {
                    var resized = new ulong[index];
                    System.Array.Copy(activeHosts, resized, index);
                    return resized;
                }
                
                return activeHosts;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting available hosts: {e.Message}");
                return new ulong[0];
            }
        }
        
        /// <summary>
        /// Automatically set invite info when Steam overlay invite is clicked
        /// This captures the real Steam ID from Steam's callback system
        /// </summary>
        public static void SetInviteInfo(ulong hostSteamId, string lobbyId = "")
        {
            lastInvitedBySteamId = hostSteamId;
            lastInviteLobbyId = lobbyId;
            Debug.Log($"[ETGSteamP2P] 📨 Auto-received invite from Steam ID: {hostSteamId}");
            
            // Add to available hosts if not already there
            if (!availableHosts.ContainsKey(hostSteamId))
            {
                availableHosts[hostSteamId] = new HostInfo
                {
                    steamId = hostSteamId,
                    sessionName = "Friend's Session",
                    playerCount = 1,
                    lastSeen = Time.time,
                    isActive = true
                };
                Debug.Log($"[ETGSteamP2P] 📥 Added host from invite: {hostSteamId}");
            }
        }
        
        /// <summary>
        /// Get the Steam ID of the last person who invited this player
        /// Returns 0 if no invite is available
        /// </summary>
        public static ulong GetLastInviterSteamId()
        {
            return lastInvitedBySteamId;
        }
        
        /// <summary>
        /// Get the most recent available host Steam ID for automatic joining
        /// </summary>
        public static ulong GetBestAvailableHost()
        {
            try
            {
                // First priority: Direct invite
                if (lastInvitedBySteamId != 0)
                {
                    Debug.Log($"[ETGSteamP2P] 🎯 Using direct invite: {lastInvitedBySteamId}");
                    return lastInvitedBySteamId;
                }
                
                // Second priority: Most recent active host
                ulong bestHost = 0;
                float mostRecent = 0;
                
                foreach (var kvp in availableHosts)
                {
                    var host = kvp.Value;
                    if (host.isActive && host.steamId != currentHostSteamId && host.lastSeen > mostRecent)
                    {
                        bestHost = host.steamId;
                        mostRecent = host.lastSeen;
                    }
                }
                
                if (bestHost != 0)
                {
                    Debug.Log($"[ETGSteamP2P] 🔍 Auto-selected best host: {bestHost}");
                }
                else
                {
                    Debug.Log("[ETGSteamP2P] ❌ No available hosts found");
                }
                
                return bestHost;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error finding best host: {e.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Clear invite information after use
        /// </summary>
        public static void ClearInviteInfo()
        {
            lastInvitedBySteamId = 0;
            lastInviteLobbyId = "";
            Debug.Log("[ETGSteamP2P] 🧹 Cleared invite info");
        }
        
        /// <summary>
        /// Stop hosting and clean up host registration
        /// </summary>
        public static void UnregisterAsHost()
        {
            try
            {
                if (isCurrentlyHosting && currentHostSteamId != 0)
                {
                    availableHosts.Remove(currentHostSteamId);
                    Debug.Log($"[ETGSteamP2P] 🏠❌ Unregistered as host: {currentHostSteamId}");
                }
                
                currentHostSteamId = 0;
                isCurrentlyHosting = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error unregistering as host: {e.Message}");
            }
        }
        
        /// <summary>
        /// Broadcast host availability to the network (called periodically when hosting)
        /// </summary>
        public static void BroadcastHostAvailability()
        {
            try
            {
                if (isCurrentlyHosting && currentHostSteamId != 0)
                {
                    // Update our host info
                    if (availableHosts.ContainsKey(currentHostSteamId))
                    {
                        var info = availableHosts[currentHostSteamId];
                        info.lastSeen = Time.time;
                        info.isActive = true;
                        availableHosts[currentHostSteamId] = info;
                    }
                    
                    // In a real implementation, this would broadcast via P2P or Steam Rich Presence
                    // For now, we'll rely on Rich Presence and lobby system
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error broadcasting host availability: {e.Message}");
            }
        }
        
        /// <summary>
        /// Public method to trigger overlay join event (for testing and external access)
        /// </summary>
        public static void TriggerOverlayJoinEvent(string hostSteamId)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Triggering overlay join event for host: {hostSteamId}");
                OnOverlayJoinRequested?.Invoke(hostSteamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error triggering overlay join event: {e.Message}");
            }
        }
        
        /// <summary>
        /// Initialize Steam callbacks for overlay join functionality
        /// </summary>
        private static void InitializeSteamCallbacks()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Initializing Steam callbacks for overlay join functionality...");
                
                // Set up overlay activated callback
                if (!object.ReferenceEquals(overlayActivatedType, null) && !object.ReferenceEquals(callbackType, null))
                {
                    try
                    {
                        // Create callback instance for overlay activation
                        var callbackConstructor = callbackType.GetConstructor(new Type[] { typeof(Action<>).MakeGenericType(overlayActivatedType) });
                        if (!object.ReferenceEquals(callbackConstructor, null))
                        {
                            // Create delegate to handle overlay activation
                            var overlayDelegate = CreateOverlayActivatedDelegate(overlayActivatedType);
                            overlayCallbackHandle = callbackConstructor.Invoke(new object[] { overlayDelegate });
                            Debug.Log("[ETGSteamP2P] Created Steam overlay activation callback");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not create overlay callback: {e.Message}");
                    }
                }
                
                // Set up lobby join requested callback  
                if (!object.ReferenceEquals(lobbyJoinRequestedType, null) && !object.ReferenceEquals(callbackType, null))
                {
                    try
                    {
                        // Create callback instance for lobby join requests
                        var callbackConstructor = callbackType.GetConstructor(new Type[] { typeof(Action<>).MakeGenericType(lobbyJoinRequestedType) });
                        if (!object.ReferenceEquals(callbackConstructor, null))
                        {
                            // Create delegate to handle lobby join requests
                            var lobbyDelegate = CreateLobbyJoinRequestedDelegate(lobbyJoinRequestedType);
                            lobbyCallbackHandle = callbackConstructor.Invoke(new object[] { lobbyDelegate });
                            Debug.Log("[ETGSteamP2P] Created Steam lobby join requested callback");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not create lobby join callback: {e.Message}");
                    }
                }
                
                Debug.Log("[ETGSteamP2P] Steam callback initialization complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error initializing Steam callbacks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Create delegate for overlay activation events
        /// </summary>
        private static object CreateOverlayActivatedDelegate(Type overlayActivatedType)
        {
            try
            {
                var delegateType = typeof(Action<>).MakeGenericType(overlayActivatedType);
                var method = typeof(ETGSteamP2PNetworking).GetMethod(nameof(OnSteamOverlayActivated), BindingFlags.NonPublic | BindingFlags.Static);
                return Delegate.CreateDelegate(delegateType, method);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error creating overlay delegate: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Create delegate for lobby join requested events
        /// </summary>
        private static object CreateLobbyJoinRequestedDelegate(Type lobbyJoinRequestedType)
        {
            try
            {
                var delegateType = typeof(Action<>).MakeGenericType(lobbyJoinRequestedType);
                var method = typeof(ETGSteamP2PNetworking).GetMethod(nameof(OnSteamLobbyJoinRequested), BindingFlags.NonPublic | BindingFlags.Static);
                return Delegate.CreateDelegate(delegateType, method);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error creating lobby join delegate: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Handle Steam overlay activation events
        /// </summary>
        private static void OnSteamOverlayActivated(object overlayData)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Steam overlay activated!");
                
                // Extract activation flag from overlay data
                var activeField = overlayData.GetType().GetField("m_bActive");
                if (!object.ReferenceEquals(activeField, null))
                {
                    bool isActive = (bool)activeField.GetValue(overlayData);
                    Debug.Log($"[ETGSteamP2P] Overlay active: {isActive}");
                    
                    // Fire event for listeners
                    OnOverlayActivated?.Invoke(isActive);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling overlay activation: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam lobby join requested events (this is the key for overlay "Join Game")
        /// </summary>
        private static void OnSteamLobbyJoinRequested(object lobbyData)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Steam lobby join requested! (Overlay 'Join Game' clicked)");
                
                // Extract Steam ID from lobby data
                var friendIdField = lobbyData.GetType().GetField("m_steamIDFriend");
                var lobbyIdField = lobbyData.GetType().GetField("m_steamIDLobby");
                
                string hostSteamId = "unknown";
                
                if (!object.ReferenceEquals(friendIdField, null))
                {
                    var friendId = friendIdField.GetValue(lobbyData);
                    hostSteamId = friendId.ToString();
                    Debug.Log($"[ETGSteamP2P] Join request from friend: {hostSteamId}");
                }
                
                if (!object.ReferenceEquals(lobbyIdField, null))
                {
                    var lobbyId = lobbyIdField.GetValue(lobbyData);
                    Debug.Log($"[ETGSteamP2P] Join request for lobby: {lobbyId}");
                }
                
                // Fire event for session manager to handle the join
                OnOverlayJoinRequested?.Invoke(hostSteamId);
                
                Debug.Log($"[ETGSteamP2P] Overlay join request processed for host: {hostSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling lobby join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Process Steam callbacks (must be called regularly, usually in Update)
        /// </summary>
        public static void ProcessSteamCallbacks()
        {
            try
            {
                if (!object.ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod.Invoke(null, null);
                }
                
                // Also check for Steam Rich Presence changes that might indicate join requests
                CheckForSteamJoinRequests();
            }
            catch (Exception e)
            {
                // Don't spam the log with callback errors
                if (Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error processing Steam callbacks: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Check for Steam join requests through Rich Presence or other methods
        /// This is a fallback when callbacks don't work properly
        /// </summary>
        private static void CheckForSteamJoinRequests()
        {
            try
            {
                // Check command line args periodically for Steam join commands
                if (Time.frameCount % 60 == 0) // Check once per second at 60fps
                {
                    var args = System.Environment.GetCommandLineArgs();
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("+connect") && i + 1 < args.Length)
                        {
                            if (ulong.TryParse(args[i + 1], out ulong steamId) && steamId != 0)
                            {
                                // Only process if this is a new join request
                                if (steamId != lastInvitedBySteamId)
                                {
                                    Debug.Log($"[ETGSteamP2P] Detected delayed Steam join request: {steamId}");
                                    SetInviteInfo(steamId);
                                    OnOverlayJoinRequested?.Invoke(steamId.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Silently ignore errors in join request checking
            }
        }
        
        /// <summary>
        /// Convert ulong Steam ID to CSteamID object for Steamworks.NET methods
        /// </summary>
        private object ConvertToCSteamID(ulong steamId)
        {
            try
            {
                // Find CSteamID type in the Steamworks assembly
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (assemblies[i].GetName().Name == "Assembly-CSharp-firstpass")
                    {
                        Type cSteamIdType = assemblies[i].GetType("Steamworks.CSteamID", false);
                        if (!object.ReferenceEquals(cSteamIdType, null))
                        {
                            // Try constructor that takes ulong
                            var constructor = cSteamIdType.GetConstructor(new Type[] { typeof(ulong) });
                            if (!object.ReferenceEquals(constructor, null))
                            {
                                object cSteamId = constructor.Invoke(new object[] { steamId });
                                Debug.Log($"[ETGSteamP2P] Converted {steamId} to CSteamID: {cSteamId}");
                                return cSteamId;
                            }
                        }
                        break;
                    }
                }
                
                Debug.LogWarning($"[ETGSteamP2P] Could not convert {steamId} to CSteamID - using raw ulong");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error converting to CSteamID: {e.Message}");
                return null;
            }
        }
    }
}
