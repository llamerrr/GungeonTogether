using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Handles reflection-based access to ETG's built-in Steamworks.NET API
    /// </summary>
    public static class SteamReflectionHelper
    {
        // Reflection types and methods for ETG's Steamworks
        private static Type steamUserType;
        private static Type steamFriendsType;
        private static Type steamNetworkingType;
        private static Type steamMatchmakingType;
        private static Type steamUtilsType;
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
        private static MethodInfo readP2PSessionRequestMethod;
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
        private static MethodInfo getLobbyOwnerMethod;
        
        // Steam Friends methods
        private static MethodInfo getFriendCountMethod;
        private static MethodInfo getFriendByIndexMethod;
        private static MethodInfo getFriendPersonaNameMethod;
        private static MethodInfo getFriendPersonaStateMethod;
        private static MethodInfo getFriendGamePlayedMethod;
        private static MethodInfo getFriendRichPresenceMethod;
        
        private static bool initialized = false;
        
        // Cache for Steam ID to prevent repeated expensive reflection calls
        private static ulong cachedSteamId = 0;
        private static bool steamIdCached = false;
        
        // Cache the steamworks assembly to avoid repeated lookups
        private static Assembly cachedSteamworksAssembly = null;
        
        // Working signature tracking for SendP2PPacket
        private static int workingSendSignatureIndex = -1;
        
        /// <summary>
        /// Initialize Steam types via reflection to use ETG's Steamworks
        /// </summary>
        public static void InitializeSteamTypes()
        {
            try
            {
                // Get ETG's Assembly-CSharp-firstpass which contains Steamworks types (discovered via diagnostics)
                Assembly steamworksAssembly = null;
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                
                // Find Assembly-CSharp-firstpass which contains ETG's Steamworks.NET
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (string.Equals(assemblies[i].GetName().Name, "Assembly-CSharp-firstpass"))
                    {
                        steamworksAssembly = assemblies[i];
                        break;
                    }
                }
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Assembly-CSharp-firstpass not found - Steamworks.NET not available");
                    return;
                }
                
                // Cache the assembly for future use
                cachedSteamworksAssembly = steamworksAssembly;
                
                Debug.Log("[ETGSteamP2P] Found Assembly-CSharp-firstpass with Steamworks.NET");
                
                // Find Steam types in Steamworks namespace (discovered via diagnostics)
                steamUserType = steamworksAssembly.GetType("Steamworks.SteamUser", false);
                steamFriendsType = steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                steamMatchmakingType = steamworksAssembly.GetType("Steamworks.SteamMatchmaking", false);
                steamUtilsType = steamworksAssembly.GetType("Steamworks.SteamUtils", false);
                steamAppsType = steamworksAssembly.GetType("Steamworks.SteamApps", false);
                
                // Additional callback types
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
                CacheSteamUserMethods();
                CacheSteamNetworkingMethods();
                CacheSteamFriendsMethods();
                CacheSteamMatchmakingMethods();
                
                initialized = (!ReferenceEquals(steamNetworkingType, null) && !ReferenceEquals(sendP2PPacketMethod, null));
                
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
        
        private static void CacheSteamUserMethods()
        {
            if (!ReferenceEquals(steamUserType, null))
            {
                // Try common Steamworks.NET method names for getting Steam ID
                getSteamIdMethod = steamUserType.GetMethod("GetSteamID", BindingFlags.Public | BindingFlags.Static);
                if (ReferenceEquals(getSteamIdMethod, null))
                {
                    getSteamIdMethod = steamUserType.GetMethod("get_SteamID", BindingFlags.Public | BindingFlags.Static);
                }
            }
        }
        
        private static void CacheSteamNetworkingMethods()
        {
            if (!ReferenceEquals(steamNetworkingType, null))
            {
                // Discover all SendP2PPacket method overloads to find the correct signature
                DiscoverSendP2PPacketSignatures(steamNetworkingType);

                readP2PPacketMethod = steamNetworkingType.GetMethod("ReadP2PPacket", BindingFlags.Public | BindingFlags.Static);
                readP2PSessionRequestMethod = steamNetworkingType.GetMethod("ReadP2PSessionRequest", BindingFlags.Public | BindingFlags.Static);
                
                // Try to discover IsP2PPacketAvailable with different signatures
                DiscoverIsP2PPacketAvailableSignature(steamNetworkingType);
                
                acceptP2PSessionMethod = steamNetworkingType.GetMethod("AcceptP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                closeP2PSessionMethod = steamNetworkingType.GetMethod("CloseP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                
                // Debug output for packet methods
                Debug.Log($"[ETGSteamP2P] Packet methods found:");
                Debug.Log($"[ETGSteamP2P]   ReadP2PPacket: {(!ReferenceEquals(readP2PPacketMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"[ETGSteamP2P]   ReadP2PSessionRequest: {(!ReferenceEquals(readP2PSessionRequestMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"[ETGSteamP2P]   IsP2PPacketAvailable: {(!ReferenceEquals(isP2PPacketAvailableMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"[ETGSteamP2P]   AcceptP2PSessionWithUser: {(!ReferenceEquals(acceptP2PSessionMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"[ETGSteamP2P]   CloseP2PSessionWithUser: {(!ReferenceEquals(closeP2PSessionMethod, null) ? "Found" : "Not found")}");
                
                // Log all available networking methods for debugging
                if (ReferenceEquals(readP2PPacketMethod, null) || ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] P2P packet reception methods not found!");
                    // List all methods containing "P2P" for debugging
                    var allMethods = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    Debug.Log("[ETGSteamP2P] Available SteamNetworking methods containing 'P2P':");
                    foreach (var method in allMethods)
                    {
                        if (method.Name.Contains("P2P"))
                        {
                            var paramStr = "";
                            var parameters = method.GetParameters();
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                if (i > 0) paramStr += ", ";
                                paramStr += parameters[i].ParameterType.Name + " " + parameters[i].Name;
                            }
                            Debug.Log($"[ETGSteamP2P]   {method.Name}({paramStr})");
                        }
                    }
                }
            }
        }
        
        private static void CacheSteamFriendsMethods()
        {
            if (!ReferenceEquals(steamFriendsType, null))
            {
                setRichPresenceMethod = steamFriendsType.GetMethod("SetRichPresence", BindingFlags.Public | BindingFlags.Static);
                clearRichPresenceMethod = steamFriendsType.GetMethod("ClearRichPresence", BindingFlags.Public | BindingFlags.Static);
                
                // Cache friends list methods
                getFriendCountMethod = steamFriendsType.GetMethod("GetFriendCount", BindingFlags.Public | BindingFlags.Static);
                getFriendByIndexMethod = steamFriendsType.GetMethod("GetFriendByIndex", BindingFlags.Public | BindingFlags.Static);
                getFriendPersonaNameMethod = steamFriendsType.GetMethod("GetFriendPersonaName", BindingFlags.Public | BindingFlags.Static);
                getFriendPersonaStateMethod = steamFriendsType.GetMethod("GetFriendPersonaState", BindingFlags.Public | BindingFlags.Static);
                getFriendGamePlayedMethod = steamFriendsType.GetMethod("GetFriendGamePlayed", BindingFlags.Public | BindingFlags.Static);
                getFriendRichPresenceMethod = steamFriendsType.GetMethod("GetFriendRichPresence", BindingFlags.Public | BindingFlags.Static);
                
                Debug.Log($"[ETGSteamP2P] Friends methods found:");
                Debug.Log($"  GetFriendCount: {(!ReferenceEquals(getFriendCountMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"  GetFriendByIndex: {(!ReferenceEquals(getFriendByIndexMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"  GetFriendPersonaName: {(!ReferenceEquals(getFriendPersonaNameMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"  GetFriendPersonaState: {(!ReferenceEquals(getFriendPersonaStateMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"  GetFriendGamePlayed: {(!ReferenceEquals(getFriendGamePlayedMethod, null) ? "Found" : "Not found")}");
                Debug.Log($"  GetFriendRichPresence: {(!ReferenceEquals(getFriendRichPresenceMethod, null) ? "Found" : "Not found")}");
                
                // Log GetFriendGamePlayed method signature for debugging
                if (!ReferenceEquals(getFriendGamePlayedMethod, null))
                {
                    var parameters = getFriendGamePlayedMethod.GetParameters();
                    var paramStr = "";
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) paramStr += ", ";
                        string prefix = parameters[i].IsOut ? "out " : (parameters[i].ParameterType.IsByRef ? "ref " : "");
                        paramStr += $"{prefix}{parameters[i].ParameterType.Name} {parameters[i].Name}";
                    }
                    Debug.Log($"[ETGSteamP2P]   GetFriendGamePlayed signature: {getFriendGamePlayedMethod.ReturnType.Name} GetFriendGamePlayed({paramStr})");
                }
            }
        }
        
        private static void CacheSteamMatchmakingMethods()
        {
            if (!ReferenceEquals(steamMatchmakingType, null))
            {
                createLobbyMethod = steamMatchmakingType.GetMethod("CreateLobby", BindingFlags.Public | BindingFlags.Static);
                joinLobbyMethod = steamMatchmakingType.GetMethod("JoinLobby", BindingFlags.Public | BindingFlags.Static);
                leaveLobbyMethod = steamMatchmakingType.GetMethod("LeaveLobby", BindingFlags.Public | BindingFlags.Static);
                setLobbyDataMethod = steamMatchmakingType.GetMethod("SetLobbyData", BindingFlags.Public | BindingFlags.Static);
                getLobbyDataMethod = steamMatchmakingType.GetMethod("GetLobbyData", BindingFlags.Public | BindingFlags.Static);
                setLobbyJoinableMethod = steamMatchmakingType.GetMethod("SetLobbyJoinable", BindingFlags.Public | BindingFlags.Static);
                inviteUserToLobbyMethod = steamMatchmakingType.GetMethod("InviteUserToLobby", BindingFlags.Public | BindingFlags.Static);
                getLobbyOwnerMethod = steamMatchmakingType.GetMethod("GetLobbyOwner", BindingFlags.Public | BindingFlags.Static);
            }
        }
        
        /// <summary>
        /// Discover all SendP2PPacket method signatures and cache the working one
        /// </summary>
        private static void DiscoverSendP2PPacketSignatures(Type steamNetworkingType)
        {
            try
            {
                var allMethods = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var sendMethods = new List<MethodInfo>();
                
                // Find all SendP2PPacket methods
                foreach (var method in allMethods)
                {
                    if (string.Equals(method.Name, "SendP2PPacket"))
                    {
                        sendMethods.Add(method);
                    }
                }
                
                Debug.Log($"[ETGSteamP2P] Found {sendMethods.Count} SendP2PPacket method signatures:");
                
                for (int i = 0; i < sendMethods.Count; i++)
                {
                    var method = sendMethods[i];
                    var parameters = method.GetParameters();
                    var paramStr = "";
                    
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        if (j > 0) paramStr += ", ";
                        paramStr += parameters[j].ParameterType.Name + " " + parameters[j].Name;
                    }
                    
                    Debug.Log($"[ETGSteamP2P]   Signature {i}: {method.Name}({paramStr})");
                }
                
                // Use the first one as default, but we'll try different signatures in TryDifferentSendSignatures
                if (sendMethods.Count > 0)
                {
                    sendP2PPacketMethod = sendMethods[0];
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error discovering SendP2PPacket signatures: {e.Message}");
            }
        }
        
        /// <summary>
        /// Discover the correct IsP2PPacketAvailable method signature
        /// </summary>
        private static void DiscoverIsP2PPacketAvailableSignature(Type steamNetworkingType)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Discovering IsP2PPacketAvailable method signature...");
                
                var allMethodsTemp = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var allMethodsList = new List<MethodInfo>();
                
                // Filter methods with name "IsP2PPacketAvailable" without LINQ
                foreach (var method in allMethodsTemp)
                {
                    if (string.Equals(method.Name, "IsP2PPacketAvailable"))
                    {
                        allMethodsList.Add(method);
                    }
                }
                
                var allMethods = allMethodsList.ToArray();
                
                Debug.Log($"[ETGSteamP2P] Found {allMethods.Length} IsP2PPacketAvailable method(s)");
                
                foreach (var method in allMethods)
                {
                    var parameters = method.GetParameters();
                    var paramParts = new List<string>();
                    
                    // Build parameter string without LINQ
                    foreach (var p in parameters)
                    {
                        string prefix = p.IsOut ? "out " : (p.ParameterType.IsByRef ? "ref " : "");
                        paramParts.Add($"{prefix}{p.ParameterType.Name} {p.Name}");
                    }
                    
                    var paramStr = string.Join(", ", paramParts.ToArray());
                    
                    Debug.Log($"[ETGSteamP2P]   Signature: {method.ReturnType.Name} IsP2PPacketAvailable({paramStr})");
                    
                    // Look for the most common signature: bool IsP2PPacketAvailable(out uint, int)
                    if (parameters.Length >= 1 && parameters.Length <= 2)
                    {
                        var firstParam = parameters[0];
                        bool isOutUint = firstParam.IsOut && 
                                        (firstParam.ParameterType.GetElementType().Equals(typeof(uint)) || 
                                         firstParam.ParameterType.GetElementType().Equals(typeof(System.UInt32)));
                        
                        if (isOutUint)
                        {
                            isP2PPacketAvailableMethod = method;
                            
                            // Log the exact signature we selected for debugging
                            var selectedParamStr = string.Join(", ", paramParts.ToArray());
                            Debug.Log($"[ETGSteamP2P] ✅ Selected IsP2PPacketAvailable with out uint parameter");
                            Debug.Log($"[ETGSteamP2P] ✅ Selected signature: {method.ReturnType.Name} IsP2PPacketAvailable({selectedParamStr})");
                            Debug.Log($"[ETGSteamP2P] ✅ Parameter count: {parameters.Length}");
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var p = parameters[i];
                                Debug.Log($"[ETGSteamP2P] ✅ Param {i}: {(p.IsOut ? "out " : "")}{p.ParameterType.Name} {p.Name}");
                            }
                            return;
                        }
                    }
                }
                
                // Fallback: just take the first one if we can't find the ideal signature
                if (allMethods.Length > 0)
                {
                    isP2PPacketAvailableMethod = allMethods[0];
                    var fallbackParams = isP2PPacketAvailableMethod.GetParameters();
                    var fallbackParamParts = new List<string>();
                    foreach (var p in fallbackParams)
                    {
                        string prefix = p.IsOut ? "out " : (p.ParameterType.IsByRef ? "ref " : "");
                        fallbackParamParts.Add($"{prefix}{p.ParameterType.Name} {p.Name}");
                    }
                    var fallbackParamStr = string.Join(", ", fallbackParamParts.ToArray());
                    Debug.Log($"[ETGSteamP2P] ⚠️ Using fallback IsP2PPacketAvailable method");
                    Debug.Log($"[ETGSteamP2P] ⚠️ Fallback signature: {isP2PPacketAvailableMethod.ReturnType.Name} IsP2PPacketAvailable({fallbackParamStr})");
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] ❌ No IsP2PPacketAvailable method found!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error discovering IsP2PPacketAvailable signature: {e.Message}");
                // Fallback to original method
                isP2PPacketAvailableMethod = steamNetworkingType.GetMethod("IsP2PPacketAvailable", BindingFlags.Public | BindingFlags.Static);
            }
        }
        
        /// <summary>
        /// Get current Steam user ID (cached to prevent log spam)
        /// </summary>
        public static ulong GetSteamID()
        {
            try
            {
                // Return cached value if available
                if (steamIdCached && (cachedSteamId != 0))
                {
                    return cachedSteamId;
                }
                
                if (!initialized)
                {
                    InitializeSteamTypes();
                }
                
                if (!ReferenceEquals(getSteamIdMethod, null))
                {
                    object result = getSteamIdMethod.Invoke(null, null);
                    if (!ReferenceEquals(result, null))
                    {
                        // Try different casting approaches for different Steamworks types
                        try
                        {
                            // First try direct cast for primitive types
                            if (result is ulong directULong)
                            {
                                cachedSteamId = directULong;
                                steamIdCached = true;
                                return directULong;
                            }
                            
                            // Try direct convert for numeric types
                            ulong steamId = Convert.ToUInt64(result);
                            cachedSteamId = steamId;
                            steamIdCached = true;
                            return steamId;
                        }
                        catch (Exception)
                        {
                            // Try accessing struct fields if it's a struct
                            Type resultType = result.GetType();
                            
                            // Check for common Steamworks struct field names
                            var idField = resultType.GetField("m_SteamID") ?? 
                                         resultType.GetField("SteamID") ?? 
                                         resultType.GetField("steamID") ??
                                         resultType.GetField("value") ??
                                         resultType.GetField("Value");
                            
                            if (!ReferenceEquals(idField, null))
                            {
                                object fieldValue = idField.GetValue(result);
                                if (!ReferenceEquals(fieldValue, null))
                                {
                                    ulong fieldSteamId = Convert.ToUInt64(fieldValue);
                                    cachedSteamId = fieldSteamId;
                                    steamIdCached = true;
                                    return fieldSteamId;
                                }
                            }
                            
                            // Try ToString() as last resort
                            string stringValue = result.ToString();
                            if (ulong.TryParse(stringValue, out ulong parsedId))
                            {
                                cachedSteamId = parsedId;
                                steamIdCached = true;
                                return parsedId;
                            }
                            
                            // Only log warning once, not on every call
                            if (!steamIdCached)
                            {
                                Debug.LogWarning($"[ETGSteamP2P] Could not extract Steam ID from type {resultType.FullName}");
                            }
                        }
                    }
                    else
                    {
                        // Only log warning once
                        if (!steamIdCached)
                        {
                            Debug.LogWarning("[ETGSteamP2P] GetSteamID method returned null");
                        }
                    }
                }
                else
                {
                    // Only log warning once
                    if (!steamIdCached)
                    {
                        Debug.LogWarning("[ETGSteamP2P] GetSteamID method not found");
                    }
                }
                
                return 0;
            }
            catch (Exception e)
            {
                // Only log error once
                if (!steamIdCached)
                {
                    Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: {e.Message}");
                }
                return 0;
            }
        }
        
        /// <summary>
        /// Try different SendP2PPacket method signatures to find one that works
        /// </summary>
        public static bool TryDifferentSendSignatures(object steamIdParam, byte[] data)
        {
            try
            {
                if (ReferenceEquals(steamNetworkingType, null))
                    return false;
                
                var allMethods = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var sendMethods = new List<MethodInfo>();
                
                // Find all SendP2PPacket methods
                foreach (var method in allMethods)
                {
                    if (string.Equals(method.Name, "SendP2PPacket"))
                    {
                        sendMethods.Add(method);
                    }
                }
                
                // If we have a working signature, try it first
                if (workingSendSignatureIndex >= 0 && workingSendSignatureIndex < sendMethods.Count)
                {
                    if (TrySendWithSignature(sendMethods[workingSendSignatureIndex], steamIdParam, data))
                    {
                        return true;
                    }
                }
                
                // Try all signatures
                for (int i = 0; i < sendMethods.Count; i++)
                {
                    if (i == workingSendSignatureIndex) continue; // Already tried this one
                    
                    if (TrySendWithSignature(sendMethods[i], steamIdParam, data))
                    {
                        workingSendSignatureIndex = i;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error trying different send signatures: {e.Message}");
                return false;
            }
        }
        
        private static bool TrySendWithSignature(MethodInfo method, object steamIdParam, byte[] data)
        {
            try
            {
                var parameters = method.GetParameters();
                
                // Try different parameter combinations based on common Steamworks patterns
                if (parameters.Length == 5) // Common: steamid, data, length, channel, sendtype
                {
                    object result = method.Invoke(null, new object[] { steamIdParam, data, (uint)data.Length, 0, 2 });
                    return result is bool success && success;
                }
                else if (parameters.Length == 4) // steamid, data, length, channel
                {
                    object result = method.Invoke(null, new object[] { steamIdParam, data, (uint)data.Length, 0 });
                    return result is bool success && success;
                }
                else if (parameters.Length == 3) // steamid, data, length
                {
                    object result = method.Invoke(null, new object[] { steamIdParam, data, (uint)data.Length });
                    return result is bool success && success;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false; // This signature didn't work
            }
        }
        
        /// <summary>
        /// Convert ulong Steam ID to CSteamID object for Steamworks.NET methods
        /// </summary>
        public static object ConvertToCSteamID(ulong steamId)
        {
            try
            {
                if (!initialized)
                {
                    InitializeSteamTypes();
                }
                
                // Try to find CSteamID type using cached assembly
                Assembly steamworksAssembly = cachedSteamworksAssembly;
                
                // If not cached, try to find it
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    
                    for (int i = 0; i < assemblies.Length; i++)
                    {
                        if (string.Equals(assemblies[i].GetName().Name, "Assembly-CSharp-firstpass"))
                        {
                            steamworksAssembly = assemblies[i];
                            cachedSteamworksAssembly = steamworksAssembly; // Cache it
                            break;
                        }
                    }
                }
                
                if (ReferenceEquals(steamworksAssembly, null))
                    return steamId; // Fallback to raw ulong
                
                var cSteamIDType = steamworksAssembly.GetType("Steamworks.CSteamID", false);
                if (ReferenceEquals(cSteamIDType, null))
                    return steamId; // Fallback to raw ulong
                
                // Try to create CSteamID from ulong
                var constructor = cSteamIDType.GetConstructor(new Type[] { typeof(ulong) });
                if (!ReferenceEquals(constructor, null))
                {
                    return constructor.Invoke(new object[] { steamId });
                }
                
                return steamId; // Fallback to raw ulong
            }
            catch (Exception)
            {
                return steamId; // Fallback to raw ulong
            }
        }
        
        /// <summary>
        /// Get the cached Steamworks assembly reference
        /// </summary>
        public static Assembly GetSteamworksAssembly()
        {
            if (ReferenceEquals(cachedSteamworksAssembly, null) && !initialized)
            {
                InitializeSteamTypes();
            }
            return cachedSteamworksAssembly;
        }
        
        /// <summary>
        /// Get Rich Presence data for a specific Steam friend
        /// </summary>
        public static string GetFriendRichPresence(ulong friendSteamId, string key)
        {
            try
            {
                if (ReferenceEquals(getFriendRichPresenceMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] GetFriendRichPresence method not available");
                    return "";
                }
                
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[ETGSteamP2P] Rich Presence key is null or empty");
                    return "";
                }
                
                var steamIdParam = ConvertToCSteamID(friendSteamId);
                if (ReferenceEquals(steamIdParam, null))
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not convert Steam ID {friendSteamId} to CSteamID");
                    return "";
                }
                
                var result = getFriendRichPresenceMethod.Invoke(null, new object[] { steamIdParam, key });
                
                return result?.ToString() ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error getting friend Rich Presence for key '{key}': {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// Create a CSteamID object from a ulong Steam ID
        /// </summary>
        public static object CreateCSteamID(ulong steamId)
        {
            return ConvertToCSteamID(steamId);
        }
        
        /// <summary>
        /// Extract ulong Steam ID from a CSteamID object or other Steam ID representation
        /// </summary>
        public static ulong ExtractSteamId(object steamIdObj)
        {
            if (ReferenceEquals(steamIdObj, null))
                return 0;
                
            try
            {
                // If it's already a ulong, return it directly
                if (steamIdObj is ulong directULong)
                {
                    return directULong;
                }
                
                // Try direct conversion first
                try
                {
                    return Convert.ToUInt64(steamIdObj);
                }
                catch (Exception)
                {
                    // Conversion failed, try reflection approach
                }
                
                // If it's a CSteamID object, try to extract the value using reflection
                var steamIdType = steamIdObj.GetType();
                
                // Look for common CSteamID value properties/fields
                var valueProperty = steamIdType.GetProperty("m_SteamID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (!ReferenceEquals(valueProperty, null))
                {
                    var value = valueProperty.GetValue(steamIdObj, null);
                    return Convert.ToUInt64(value);
                }
                
                var valueField = steamIdType.GetField("m_SteamID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (!ReferenceEquals(valueField, null))
                {
                    var value = valueField.GetValue(steamIdObj);
                    return Convert.ToUInt64(value);
                }
                
                // Try ToString and parse
                var stringValue = steamIdObj.ToString();
                if (ulong.TryParse(stringValue, out ulong parsedId))
                {
                    return parsedId;
                }
                
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error extracting Steam ID from object: {e.Message}");
                return 0;
            }
        }

        // Property accessors for the cached methods
        public static bool IsInitialized => initialized;
        public static MethodInfo SendP2PPacketMethod => sendP2PPacketMethod;
        public static MethodInfo ReadP2PPacketMethod => readP2PPacketMethod;
        public static MethodInfo ReadP2PSessionRequestMethod => readP2PSessionRequestMethod;
        public static MethodInfo IsP2PPacketAvailableMethod => isP2PPacketAvailableMethod;
        public static MethodInfo AcceptP2PSessionMethod => acceptP2PSessionMethod;
        public static MethodInfo CloseP2PSessionMethod => closeP2PSessionMethod;
        public static MethodInfo SetRichPresenceMethod => setRichPresenceMethod;
        public static MethodInfo ClearRichPresenceMethod => clearRichPresenceMethod;
        public static MethodInfo CreateLobbyMethod => createLobbyMethod;
        public static MethodInfo JoinLobbyMethod => joinLobbyMethod;
        public static MethodInfo LeaveLobbyMethod => leaveLobbyMethod;
        public static MethodInfo SetLobbyDataMethod => setLobbyDataMethod;
        public static MethodInfo GetLobbyDataMethod => getLobbyDataMethod;
        public static MethodInfo SetLobbyJoinableMethod => setLobbyJoinableMethod;
        public static MethodInfo InviteUserToLobbyMethod => inviteUserToLobbyMethod;
        public static MethodInfo GetLobbyOwnerMethod => getLobbyOwnerMethod;
        
        // Friends methods accessors
        public static MethodInfo GetFriendCountMethod => getFriendCountMethod;
        public static MethodInfo GetFriendByIndexMethod => getFriendByIndexMethod;
        public static MethodInfo GetFriendPersonaNameMethod => getFriendPersonaNameMethod;
        public static MethodInfo GetFriendPersonaStateMethod => getFriendPersonaStateMethod;
        public static MethodInfo GetFriendGamePlayedMethod => getFriendGamePlayedMethod;
        public static MethodInfo GetFriendRichPresenceMethod => getFriendRichPresenceMethod;
        
        /// <summary>
        /// Get the Steam ID of the lobby owner/host
        /// </summary>
        public static ulong GetLobbyOwner(ulong lobbyId)
        {
            try
            {
                if (ReferenceEquals(getLobbyOwnerMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] GetLobbyOwner method not available");
                    return 0;
                }
                
                var lobbyIdParam = ConvertToCSteamID(lobbyId);
                if (ReferenceEquals(lobbyIdParam, null))
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not convert lobby ID {lobbyId} to CSteamID");
                    return 0;
                }
                
                var result = getLobbyOwnerMethod.Invoke(null, new object[] { lobbyIdParam });
                
                if (!ReferenceEquals(result, null))
                {
                    var ownerSteamId = ExtractSteamId(result);
                    Debug.Log($"[ETGSteamP2P] GetLobbyOwner: Lobby {lobbyId} is owned by Steam ID {ownerSteamId}");
                    return ownerSteamId;
                }
                
                Debug.LogWarning($"[ETGSteamP2P] GetLobbyOwner returned null for lobby {lobbyId}");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting lobby owner for lobby {lobbyId}: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Call IsP2PPacketAvailable using the correct parameter signature discovered at initialization
        /// </summary>
        public static bool CallIsP2PPacketAvailable(out uint packetSize, int channel = 0)
        {
            packetSize = 0;
            try
            {
                // Check if Steamworks is initialized before calling any API
                if (!IsSteamworksInitialized())
                {
                    Debug.LogWarning("[ETGSteamP2P] Steamworks is not initialized. Skipping IsP2PPacketAvailable call.");
                    return false;
                }
                if (ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    return false;
                }
                var parameters = isP2PPacketAvailableMethod.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                }
                if (parameters.Length == 2)
                {
                    // Pattern: (out uint packetSize, int channel) or (ref uint, int)
                    var outType = parameters[0].ParameterType.GetElementType();
                    object outVal = Activator.CreateInstance(outType);
                    object[] args = new object[] { outVal, channel };
                    object result = isP2PPacketAvailableMethod.Invoke(null, args);
                    if (result is bool hasPacket)
                    {
                        try
                        {
                            packetSize = Convert.ToUInt32(args[0]);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ETGSteamP2P] Could not extract packetSize from out/ref param: {ex.Message}");
                        }
                        return hasPacket;
                    }
                }
                else if (parameters.Length == 1)
                {
                    var firstParam = parameters[0];
                    if (firstParam.IsOut && firstParam.ParameterType.GetElementType().Equals(typeof(uint)))
                    {
                        // Pattern: (out uint packetSize)
                        var outType = firstParam.ParameterType.GetElementType();
                        object outVal = Activator.CreateInstance(outType);
                        object[] args = new object[] { outVal };
                        object result = isP2PPacketAvailableMethod.Invoke(null, args);
                        if (result is bool hasPacket)
                        {
                            try
                            {
                                packetSize = Convert.ToUInt32(args[0]);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ETGSteamP2P] Could not extract packetSize from out/ref param: {ex.Message}");
                            }
                            Debug.Log($"[ETGSteamP2P] 🔍 IsP2PPacketAvailable (out uint): {hasPacket}, Size: {packetSize}");
                            return hasPacket;
                        }
                    }
                    else
                    {
                        // Pattern: (int channel)
                        object[] args = new object[] { channel };
                        object result = isP2PPacketAvailableMethod.Invoke(null, args);
                        if (result is bool hasPacket)
                        {
                            Debug.Log($"[ETGSteamP2P] 🔍 IsP2PPacketAvailable (int channel): {hasPacket}");
                            return hasPacket;
                        }
                    }
                }
                else if (parameters.Length == 0)
                {
                    // Pattern: no parameters
                    object result = isP2PPacketAvailableMethod.Invoke(null, new object[0]);
                    if (result is bool hasPacket)
                    {
                        Debug.Log($"[ETGSteamP2P] 🔍 IsP2PPacketAvailable (no params): {hasPacket}");
                        return hasPacket;
                    }
                }
                Debug.LogWarning($"[ETGSteamP2P] ⚠️ Unsupported IsP2PPacketAvailable parameter count: {parameters.Length}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] ❌ Error calling IsP2PPacketAvailable: {e}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"[ETGSteamP2P] ❌ InnerException: {e.InnerException}");
                }
                return false;
            }
        }

        private static bool IsSteamworksInitialized()
        {
            // Try to use SteamManager.Initialized if available
            Type steamManagerType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.Equals("SteamManager"))
                    {
                        steamManagerType = type;
                        break;
                    }
                }
                if (!ReferenceEquals(steamManagerType, null))
                    break;
            }
            if (!ReferenceEquals(steamManagerType, null))
            {
                var prop = steamManagerType.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Static);
                if (!ReferenceEquals(prop, null))
                {
                    var value = prop.GetValue(null, null);
                    if (value is bool b)
                        return b;
                }
            }
            // Fallback: try SteamAPI.IsSteamRunning() if available
            Type steamApiType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.FullName != null && type.FullName.Equals("Steamworks.SteamAPI"))
                    {
                        steamApiType = type;
                        break;
                    }
                }
                if (!ReferenceEquals(steamApiType, null))
                    break;
            }
            if (!ReferenceEquals(steamApiType, null))
            {
                var isSteamRunningMethod = steamApiType.GetMethod("IsSteamRunning", BindingFlags.Public | BindingFlags.Static);
                if (!ReferenceEquals(isSteamRunningMethod, null))
                {
                    var value = isSteamRunningMethod.Invoke(null, null);
                    if (value is bool b)
                        return b;
                }
            }
            // If all else fails, assume not initialized
            return false;
        }
    }
}
