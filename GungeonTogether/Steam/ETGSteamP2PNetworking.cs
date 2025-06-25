using System;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Steam P2P networking using ETG's built-in Steamworks.NET via reflection
    /// This avoids TypeLoadExceptions by using the Steamworks version that's already loaded in ETG
    /// </summary>
    public class ETGSteamP2PNetworking : ISteamNetworking
    {
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
                steamAppsType = steamworksAssembly.GetType("Steamworks.SteamApps", false);
                
                // Look for callback types for join requests
                gameJoinRequestedCallbackType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested_t", false);
                lobbyEnterCallbackType = steamworksAssembly.GetType("Steamworks.LobbyEnter_t", false);
                lobbyCreatedCallbackType = steamworksAssembly.GetType("Steamworks.LobbyCreated_t", false);
                
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
                
                // Call ETG's SendP2PPacket method via reflection
                // Parameters vary by Steamworks version, try common signatures
                object[] parameters = { targetSteamId, data, data.Length, 0 }; // Last param is usually send type
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
                
                if (!isInitialized || object.ReferenceEquals(isP2PPacketAvailableMethod, null) || object.ReferenceEquals(readP2PPacketMethod, null))
                    return;
                
                // Check for available packets
                try
                {
                    // Try different IsP2PPacketAvailable signatures
                    object[] checkParams = { 0 }; // Channel 0
                    object available = isP2PPacketAvailableMethod.Invoke(null, checkParams);
                    
                    if (!object.ReferenceEquals(available, null) && available is bool hasPacket && hasPacket)
                    {
                        Debug.Log("[ETGSteamP2P] P2P packet available, attempting to read...");
                        
                        // Try to read the packet - this will vary by Steamworks version
                        TryReadP2PPacket();
                    }
                }
                catch (Exception e)
                {
                    // Try simpler signature without parameters
                    try
                    {
                        object available = isP2PPacketAvailableMethod.Invoke(null, null);
                        if (!object.ReferenceEquals(available, null) && available is bool hasPacket && hasPacket)
                        {
                            Debug.Log("[ETGSteamP2P] P2P packet available (no-param), attempting to read...");
                            TryReadP2PPacket();
                        }
                    }
                    catch (Exception e2)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not check for packets: {e.Message} / {e2.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error in Update: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to read a P2P packet using different method signatures
        /// </summary>
        private void TryReadP2PPacket()
        {
            try
            {
                // Try different ReadP2PPacket signatures
                byte[] buffer = new byte[1024]; // Standard buffer size
                
                // Try signature with out parameters (buffer, size, steamId, channel)
                object[] readParams = { buffer, buffer.Length, null, 0 };
                object result = readP2PPacketMethod.Invoke(null, readParams);
                
                if (!object.ReferenceEquals(result, null) && result is bool success && success)
                {
                    // Extract the actual data size and sender ID from out parameters
                    int dataSize = (int)readParams[1];
                    ulong senderSteamId = (ulong)readParams[2];
                    
                    // Create final data array with correct size
                    byte[] actualData = new byte[dataSize];
                    Array.Copy(buffer, actualData, dataSize);
                    
                    Debug.Log($"[ETGSteamP2P] Received P2P packet from {senderSteamId}: {dataSize} bytes");
                    
                    // Fire event for received data
                    OnDataReceived?.Invoke(senderSteamId, actualData);
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] ReadP2PPacket returned false or null");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Could not read P2P packet: {e.Message}");
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
                
                if (!object.ReferenceEquals(acceptP2PSessionMethod, null))
                {
                    object result = acceptP2PSessionMethod.Invoke(null, new object[] { steamId });
                    if (!object.ReferenceEquals(result, null) && result is bool success)
                    {
                        Debug.Log($"[ETGSteamP2P] Accepted P2P session with {steamId}: {success}");
                        return success;
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
                
                // Set Rich Presence to show game status and enable join
                SetRichPresence("status", "Hosting Gungeon Together");
                SetRichPresence("steam_display", "#Status");
                SetRichPresence("connect", steamId.ToString()); // Connect string for joining
                
                // Create a Steam lobby for proper invite functionality
                CreateLobby(4); // Support up to 4 players
                
                Debug.Log($"[ETGSteamP2P] Started hosting session with Rich Presence");
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
                LeaveLobby();
                ClearRichPresence();
                
                currentLobbyId = 0;
                isLobbyHost = false;
                
                Debug.Log($"[ETGSteamP2P] Stopped multiplayer session");
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
                // For now, we can detect join attempts through P2P connection requests
                Debug.Log("[ETGSteamP2P] Checking for join requests...");
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
    }
}
