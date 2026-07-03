using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dungeonator;
using GungeonTogether.Systems.Logging;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using static ETGMod;

namespace GungeonTogether.Networking.Steam
{
    /// <summary>
    /// Handles reflection-based access to ETG's built-in Steamworks.NET API
    /// </summary>
    public static partial class SteamReflectionHelper
    {

        private static bool initialised = false;

        // Cache for Steam ID to prevent repeated expensive reflection calls
        private static ulong cachedSteamId = 0;
        private static bool steamIdCached = false;

        // Cache the steamworks assembly to avoid repeated lookups
        private static Assembly cachedSteamworksAssembly = null;

        // Working signature tracking for SendP2PPacket
        private static int workingSendSignatureIndex = -1;

        /// <summary>
        /// Initialise Steam types via reflection to use ETG's Steamworks
        /// </summary>
        public static void InitialiseSteamTypes()
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
                    // Commented out verbose logs for production cleanliness
                    // Debug.LogWarning("[ETGSteamP2P] Assembly-CSharp-firstpass not found - Steamworks.NET not available");
                    return;
                }

                // Cache the assembly for future use
                cachedSteamworksAssembly = steamworksAssembly;

                // Debug.Log("[ETGSteamP2P] Found Assembly-CSharp-firstpass with Steamworks.NET");

                // Find Steam types in Steamworks namespace (discovered via diagnostics)
                steamUserType = steamworksAssembly.GetType("Steamworks.SteamUser", false);
                steamFriendsType = steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                steamMatchmakingType = steamworksAssembly.GetType("Steamworks.SteamMatchmaking", false);
                steamUtilsType = steamworksAssembly.GetType("Steamworks.SteamUtils", false);
                steamAppsType = steamworksAssembly.GetType("Steamworks.SteamApps", false);
                gameLobbyJoinRequestedCallbackType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested_t", false);
                p2pSessionConnectFailType = steamworksAssembly.GetType("Steamworks.P2PSessionConnectFail_t", false);

                // Additional callback types
                gameJoinRequestedCallbackType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested_t", false);
                lobbyEnterCallbackType = steamworksAssembly.GetType("Steamworks.LobbyEnter_t", false);
                lobbyCreatedCallbackType = steamworksAssembly.GetType("Steamworks.LobbyCreated_t", false);
                lobbyDataUpdateCallbackType = steamworksAssembly.GetType("Steamworks.LobbyDataUpdate_t", false);

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
                Debug.Log($"  LobbyDataUpdateCallback: {lobbyDataUpdateCallbackType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameLobbyJoinRequestedCallback: {gameLobbyJoinRequestedCallbackType?.FullName ?? "NOT FOUND"}");


                // Cache frequently used methods using proper Steamworks.NET method names
                CacheSteamUserMethods();
                CacheSteamNetworkingMethods();
                CacheSteamFriendsMethods();
                CacheSteamMatchmakingMethods();

                initialised = (!ReferenceEquals(steamNetworkingType, null) && !ReferenceEquals(sendP2PPacketMethod, null));

                if (initialised)
                {
                    // Debug.Log("[ETGSteamP2P] Steam types initialised successfully!");
                }
                else
                {
                    // Debug.LogWarning("[ETGSteamP2P] Steam networking types not found - ETG may not have P2P networking support");
                }
            }
            catch (Exception e)
            {
                if (e is System.TypeLoadException tle)
                {
                    Debug.LogError($"[ETGSteamP2P] TypeLoadException: Type Name: {tle.TypeName}");
                    Debug.LogError($"[ETGSteamP2P] Message: {tle.Message}");
                    Debug.LogError($"[ETGSteamP2P] Failed exception: {tle}");
                }
                else
                {
                    Debug.LogError($"[ETGSteamP2P] {e.GetType().Name}: {e.Message}");
                    Debug.LogError($"[ETGSteamP2P] Stack: {e.StackTrace}");
                }
                initialised = false;
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
            if (ReferenceEquals(steamNetworkingType, null)) return;

            try
            {
                // Find SendP2PPacket – just take the first overload (we'll try different ones at send time)
                var allMethods = steamNetworkingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in allMethods)
                {
                    if (method.Name == "SendP2PPacket")
                    {
                        sendP2PPacketMethod = method;
                        break;
                    }
                }

                // Find ReadP2PPacket
                readP2PPacketMethod = steamNetworkingType.GetMethod("ReadP2PPacket", BindingFlags.Public | BindingFlags.Static);

                // Find IsP2PPacketAvailable – look for the version with out uint
                foreach (var method in allMethods)
                {
                    if (method.Name != "IsP2PPacketAvailable") continue;
                    var ps = method.GetParameters();
                    if (ps.Length >= 1 && ps[0].IsOut && ps[0].ParameterType == typeof(uint).MakeByRefType())
                    {
                        isP2PPacketAvailableMethod = method;
                        break;
                    }
                }
                // Fallback: if not found, take the first one
                if (isP2PPacketAvailableMethod == null)
                {
                    foreach (var method in allMethods)
                    {
                        if (method.Name == "IsP2PPacketAvailable")
                        {
                            isP2PPacketAvailableMethod = method;
                            break;
                        }
                    }
                }

                acceptP2PSessionMethod = steamNetworkingType.GetMethod("AcceptP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);
                closeP2PSessionMethod = steamNetworkingType.GetMethod("CloseP2PSessionWithUser", BindingFlags.Public | BindingFlags.Static);

                // Basic logging
                Debug.Log($"[ETGSteamP2P] SendP2PPacket: {(sendP2PPacketMethod != null ? "Found" : "Missing")}");
                Debug.Log($"[ETGSteamP2P] ReadP2PPacket: {(readP2PPacketMethod != null ? "Found" : "Missing")}");
                Debug.Log($"[ETGSteamP2P] IsP2PPacketAvailable: {(isP2PPacketAvailableMethod != null ? "Found" : "Missing")}");
                Debug.Log($"[ETGSteamP2P] AcceptP2PSessionWithUser: {(acceptP2PSessionMethod != null ? "Found" : "Missing")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ETGSteamP2P] Error in CacheSteamNetworkingMethods: {ex.Message}\n{ex.StackTrace}");
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

                // Debug.Log($"[ETGSteamP2P] Friends methods found:");
                // Debug.Log($"  GetFriendCount: {(!ReferenceEquals(getFriendCountMethod, null) ? "Found" : "Not found")}");
                // Debug.Log($"  GetFriendByIndex: {(!ReferenceEquals(getFriendByIndexMethod, null) ? "Found" : "Not found")}");
                // Debug.Log($"  GetFriendPersonaName: {(!ReferenceEquals(getFriendPersonaNameMethod, null) ? "Found" : "Not found")}");
                // Debug.Log($"  GetFriendPersonaState: {(!ReferenceEquals(getFriendPersonaStateMethod, null) ? "Found" : "Not found")}");
                // Debug.Log($"  GetFriendGamePlayed: {(!ReferenceEquals(getFriendGamePlayedMethod, null) ? "Found" : "Not found")}");
                // Debug.Log($"  GetFriendRichPresence: {(!ReferenceEquals(getFriendRichPresenceMethod, null) ? "Found" : "Not found")}");

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
                    // Debug.Log($"[ETGSteamP2P]   GetFriendGamePlayed signature: {getFriendGamePlayedMethod.ReturnType.Name} GetFriendGamePlayed({paramStr})");
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
                getLobbyMemberCountMethod = steamMatchmakingType.GetMethod("GetNumLobbyMembers", BindingFlags.Public | BindingFlags.Static);
                getLobbyMemberByIndexMethod = steamMatchmakingType.GetMethod("GetLobbyMemberByIndex", BindingFlags.Public | BindingFlags.Static);
            }
        }

        public static string GetPlayerName(ulong steamId)
        {
            if (getFriendPersonaNameMethod == null) return steamId.ToString();
            try
            {
                object cSteamId = ConvertToCSteamID(steamId);
                object result = getFriendPersonaNameMethod.Invoke(null, new object[] { cSteamId });
                return result?.ToString() ?? steamId.ToString();
            }
            catch { return steamId.ToString(); }
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

                if (!initialised)
                {
                    InitialiseSteamTypes();
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
                }
                else
                {
                    // Only log warning once
                    if (!steamIdCached)
                    {
                        Debug.LogWarning("[ETGSteamP2P] GetSteamID method returned null");
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
            if (!initialised)
                InitialiseSteamTypes();

            if (cachedSteamworksAssembly == null)
                throw new InvalidOperationException("Steamworks assembly not found.");

            var cSteamIDType = cachedSteamworksAssembly.GetType("Steamworks.CSteamID", false);
            if (cSteamIDType == null)
                throw new InvalidOperationException("Steamworks.CSteamID type not found.");

            var constructor = cSteamIDType.GetConstructor(new Type[] { typeof(ulong) });
            if (constructor == null)
                throw new MissingMethodException("Steamworks.CSteamID has no constructor taking a ulong.");

            return constructor.Invoke(new object[] { steamId });
        }

        /// <summary>
        /// Get the cached Steamworks assembly reference
        /// </summary>
        public static Assembly GetSteamworksAssembly()
        {
            if (ReferenceEquals(cachedSteamworksAssembly, null) && !initialised)
            {
                InitialiseSteamTypes();
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
        /// Get the local Steam ID
        /// </summary>
        public static ulong GetLocalSteamId()
        {
            return GetCurrentUserSteamId();
        }

        private static Type p2pSessionConnectFailType;
        public static Type P2PSessionConnectFailType => p2pSessionConnectFailType;

        /// <summary>
        /// Get current user Steam ID using reflection
        /// </summary>
        private static ulong GetCurrentUserSteamId()
        {
            // Use cached value if available
            if (steamIdCached && cachedSteamId != 0)
            {
                return cachedSteamId;
            }

            try
            {
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
                        }
                    }
                }

                return 0;
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                // Method invocation failed - get the actual exception
                if (tie.InnerException != null)
                {
                    // Suppress noise from expected "Steamworks not initialized" errors
                    // These happen during early startup before Steamworks is ready
                    if (tie.InnerException is System.InvalidOperationException && 
                        tie.InnerException.Message.Contains("Steamworks is not initialized"))
                    {
                        // Silently fail - will retry next frame
                        throw tie.InnerException; // Re-throw as InvalidOperationException for clean handling
                    }
                    
                    Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
                }
                else
                {
                    Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: TargetInvocationException with no inner exception");
                }
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                // Steamworks not initialized yet - expected during early startup
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting Steam ID: {e.GetType().Name}: {e.Message}");
                return 0;
            }
        }
    }
}
