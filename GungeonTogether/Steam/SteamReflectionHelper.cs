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
        
        private static bool initialized = false;
        
        // Cache for Steam ID to prevent repeated expensive reflection calls
        private static ulong cachedSteamId = 0;
        private static bool steamIdCached = false;
        
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
                
                // Try to discover IsP2PPacketAvailable with different signatures
                DiscoverIsP2PPacketAvailableSignature(steamNetworkingType);
                
                acceptP2PSessionMethod = steamNetworkingType.GetMethod("AcceptP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                closeP2PSessionMethod = steamNetworkingType.GetMethod("CloseP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                
                // Debug output for packet methods
                Debug.Log($"[ETGSteamP2P] Packet methods found:");
                Debug.Log($"[ETGSteamP2P]   ReadP2PPacket: {(!ReferenceEquals(readP2PPacketMethod, null) ? "Found" : "Not found")}");
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
                    if (ReferenceEquals(method.Name, "SendP2PPacket"))
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
                    if (ReferenceEquals(method.Name, "IsP2PPacketAvailable"))
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
                        bool isOutUint = firstParam.IsOut && (ReferenceEquals(firstParam.ParameterType.GetElementType(), typeof(uint)) || 
                                                              ReferenceEquals(firstParam.ParameterType.GetElementType(), typeof(System.UInt32)));
                        
                        if (isOutUint)
                        {
                            isP2PPacketAvailableMethod = method;
                            Debug.Log($"[ETGSteamP2P] ✅ Selected IsP2PPacketAvailable with out uint parameter");
                            return;
                        }
                    }
                }
                
                // Fallback: just take the first one if we can't find the ideal signature
                if (allMethods.Length > 0)
                {
                    isP2PPacketAvailableMethod = allMethods[0];
                    Debug.Log($"[ETGSteamP2P] ⚠️ Using fallback IsP2PPacketAvailable method");
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
                    if (ReferenceEquals(method.Name, "SendP2PPacket"))
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
                    if (ReferenceEquals(i, workingSendSignatureIndex)) continue; // Already tried this one
                    
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
                if (ReferenceEquals(parameters.Length, 5)) // Common: steamid, data, length, channel, sendtype
                {
                    object result = method.Invoke(null, new object[] { steamIdParam, data, (uint)data.Length, 0, 2 });
                    return result is bool success && success;
                }
                else if (ReferenceEquals(parameters.Length, 4)) // steamid, data, length, channel
                {
                    object result = method.Invoke(null, new object[] { steamIdParam, data, (uint)data.Length, 0 });
                    return result is bool success && success;
                }
                else if (ReferenceEquals(parameters.Length, 3)) // steamid, data, length
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
                
                // Try to find CSteamID type
                Assembly steamworksAssembly = null;
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (ReferenceEquals(assemblies[i].GetName().Name, "Assembly-CSharp-firstpass"))
                    {
                        steamworksAssembly = assemblies[i];
                        break;
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
        
        // Property accessors for the cached methods
        public static bool IsInitialized => initialized;
        public static MethodInfo SendP2PPacketMethod => sendP2PPacketMethod;
        public static MethodInfo ReadP2PPacketMethod => readP2PPacketMethod;
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
    }
}
