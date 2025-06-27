using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Helper class for Steam Friends functionality that was removed during refactoring
    /// This provides backward compatibility for the removed methods
    /// </summary>
    public static class SteamFriendsHelper
    {
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
            
            // GungeonTogether specific properties
            public bool hasGungeonTogether;
            public string gungeonTogetherStatus; // "hosting", "playing", etc.
            public string gungeonTogetherVersion;
        }
        
        /// <summary>
        /// Get ETG's Steam friends (placeholder implementation for backward compatibility)
        /// </summary>
        public static FriendInfo[] GetETGFriends()
        {
            try
            {
                // For now, return empty array
                // This could be implemented to actually fetch friends from Steam API
                return new FriendInfo[0];
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error getting ETG friends: {e.Message}");
                return new FriendInfo[0];
            }
        }
        
        /// <summary>
        /// Get Steam friends using reflection on ETG's Steamworks.NET
        /// </summary>
        public static FriendInfo[] GetSteamFriends()
        {
            try
            {
                // Ensure Steam types are initialized
                if (!SteamReflectionHelper.IsInitialized)
                {
                    SteamReflectionHelper.InitializeSteamTypes();
                }
                
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[SteamFriendsHelper] Steamworks assembly not available");
                    return new FriendInfo[0];
                }
                
                // Get SteamFriends type
                var steamFriendsType = steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                if (ReferenceEquals(steamFriendsType, null))
                {
                    Debug.LogWarning("[SteamFriendsHelper] SteamFriends type not found");
                    return new FriendInfo[0];
                }
                
                // Get friend count
                var getFriendCountMethod = steamFriendsType.GetMethod("GetFriendCount", BindingFlags.Public | BindingFlags.Static);
                if (ReferenceEquals(getFriendCountMethod, null))
                {
                    Debug.LogWarning("[SteamFriendsHelper] GetFriendCount method not found");
                    return new FriendInfo[0];
                }
                
                // Get other required methods
                var getFriendByIndexMethod = steamFriendsType.GetMethod("GetFriendByIndex", BindingFlags.Public | BindingFlags.Static);
                var getFriendPersonaNameMethod = steamFriendsType.GetMethod("GetFriendPersonaName", BindingFlags.Public | BindingFlags.Static);
                var getFriendPersonaStateMethod = steamFriendsType.GetMethod("GetFriendPersonaState", BindingFlags.Public | BindingFlags.Static);
                var getFriendGamePlayedMethod = steamFriendsType.GetMethod("GetFriendGamePlayed", BindingFlags.Public | BindingFlags.Static);
                
                if (ReferenceEquals(getFriendByIndexMethod, null) || ReferenceEquals(getFriendPersonaNameMethod, null))
                {
                    Debug.LogWarning("[SteamFriendsHelper] Required SteamFriends methods not found");
                    return new FriendInfo[0];
                }
                
                // Get the EFriendFlags enum type for "All" flag
                var eFriendFlagsType = steamworksAssembly.GetType("Steamworks.EFriendFlags", false);
                object allFriendsFlag = 0x04; // EFriendFlags.All = 0x04 in most Steamworks versions
                
                if (!ReferenceEquals(eFriendFlagsType, null))
                {
                    try
                    {
                        var allField = eFriendFlagsType.GetField("All") ?? eFriendFlagsType.GetField("k_EFriendFlagAll");
                        if (!ReferenceEquals(allField, null))
                        {
                            allFriendsFlag = allField.GetValue(null);
                        }
                    }
                    catch
                    {
                        // Fallback to numeric value
                        allFriendsFlag = 0x04;
                    }
                }
                
                // Get friend count
                object friendCountResult = getFriendCountMethod.Invoke(null, new object[] { allFriendsFlag });
                int friendCount = Convert.ToInt32(friendCountResult);
                
                Debug.Log($"[SteamFriendsHelper] Found {friendCount} total friends");
                
                if (friendCount == 0)
                {
                    return new FriendInfo[0];
                }
                
                var friends = new List<FriendInfo>();
                
                // Iterate through friends
                for (int i = 0; i < friendCount; i++)
                {
                    try
                    {
                        // Get friend Steam ID
                        object friendIdResult = getFriendByIndexMethod.Invoke(null, new object[] { i, allFriendsFlag });
                        if (ReferenceEquals(friendIdResult, null)) continue;
                        
                        // Convert Steam ID to ulong
                        ulong friendSteamId = 0;
                        try
                        {
                            if (friendIdResult is ulong directULong)
                            {
                                friendSteamId = directULong;
                            }
                            else
                            {
                                // Try to extract from CSteamID struct
                                var friendIdType = friendIdResult.GetType();
                                var idField = friendIdType.GetField("m_SteamID") ?? 
                                             friendIdType.GetField("SteamID") ?? 
                                             friendIdType.GetField("steamID") ?? 
                                             friendIdType.GetField("value");
                                
                                if (!ReferenceEquals(idField, null))
                                {
                                    var fieldValue = idField.GetValue(friendIdResult);
                                    friendSteamId = Convert.ToUInt64(fieldValue);
                                }
                                else
                                {
                                    friendSteamId = Convert.ToUInt64(friendIdResult);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[SteamFriendsHelper] Could not convert friend ID {i}: {ex.Message}");
                            continue;
                        }
                        
                        if (friendSteamId == 0) continue;
                        
                        // Get friend name
                        string friendName = "Unknown";
                        try
                        {
                            object nameResult = getFriendPersonaNameMethod.Invoke(null, new object[] { friendIdResult });
                            if (!ReferenceEquals(nameResult, null))
                            {
                                string name = nameResult.ToString();
                                if (!string.IsNullOrEmpty(name) && !string.Equals(name.Trim(), "") && !string.Equals(name, "[unknown]", StringComparison.OrdinalIgnoreCase))
                                {
                                    friendName = name;
                                    Debug.Log($"[SteamFriendsHelper] Got friend name via GetFriendPersonaName: '{friendName}'");
                                }
                            }
                            
                            // If name is still unknown, try alternative approaches
                            if (string.Equals(friendName, "Unknown") || string.Equals(friendName, "[unknown]", StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.Log($"[SteamFriendsHelper] Trying alternative name methods for friend {friendSteamId}");
                                
                                // Try GetPlayerName method
                                var getPlayerNameMethod = steamFriendsType.GetMethod("GetPlayerName", BindingFlags.Public | BindingFlags.Static);
                                if (!ReferenceEquals(getPlayerNameMethod, null))
                                {
                                    try
                                    {
                                        object altNameResult = getPlayerNameMethod.Invoke(null, new object[] { friendIdResult });
                                        if (!ReferenceEquals(altNameResult, null))
                                        {
                                            string altName = altNameResult.ToString();
                                            if (!string.IsNullOrEmpty(altName) && !string.Equals(altName.Trim(), "") && !string.Equals(altName, "[unknown]", StringComparison.OrdinalIgnoreCase))
                                            {
                                                friendName = altName;
                                                Debug.Log($"[SteamFriendsHelper] Got friend name via GetPlayerName: '{friendName}'");
                                            }
                                        }
                                    }
                                    catch (Exception altEx)
                                    {
                                        Debug.LogWarning($"[SteamFriendsHelper] GetPlayerName failed: {altEx.Message}");
                                    }
                                }
                                
                                // Try GetFriendName if still unknown
                                if (string.Equals(friendName, "Unknown") || string.Equals(friendName, "[unknown]", StringComparison.OrdinalIgnoreCase))
                                {
                                    var getFriendNameMethod = steamFriendsType.GetMethod("GetFriendName", BindingFlags.Public | BindingFlags.Static);
                                    if (!ReferenceEquals(getFriendNameMethod, null))
                                    {
                                        try
                                        {
                                            object altNameResult = getFriendNameMethod.Invoke(null, new object[] { friendIdResult });
                                            if (!ReferenceEquals(altNameResult, null))
                                            {
                                                string altName = altNameResult.ToString();
                                                if (!string.IsNullOrEmpty(altName) && !string.Equals(altName.Trim(), "") && !string.Equals(altName, "[unknown]", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    friendName = altName;
                                                    Debug.Log($"[SteamFriendsHelper] Got friend name via GetFriendName: '{friendName}'");
                                                }
                                            }
                                        }
                                        catch (Exception altEx)
                                        {
                                            Debug.LogWarning($"[SteamFriendsHelper] GetFriendName failed: {altEx.Message}");
                                        }
                                    }
                                }
                                
                                // Final fallback: use a readable version of Steam ID
                                if (string.Equals(friendName, "Unknown") || string.Equals(friendName, "[unknown]", StringComparison.OrdinalIgnoreCase))
                                {
                                    friendName = $"Friend_{friendSteamId}";
                                    Debug.Log($"[SteamFriendsHelper] Using Steam ID fallback name: '{friendName}'");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[SteamFriendsHelper] Could not get name for friend {friendSteamId}: {ex.Message}");
                        }
                        
                        // Get friend online state
                        bool isOnline = false;
                        try
                        {
                            if (!ReferenceEquals(getFriendPersonaStateMethod, null))
                            {
                                object stateResult = getFriendPersonaStateMethod.Invoke(null, new object[] { friendIdResult });
                                int state = Convert.ToInt32(stateResult);
                                isOnline = (state != 0); // 0 = Offline, anything else = some form of online
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[SteamFriendsHelper] Could not get state for friend {friendSteamId}: {ex.Message}");
                        }
                        
                        // Get friend game info
                        bool isInGame = false;
                        string gameInfo = "";
                        bool isPlayingETG = false;
                        try
                        {
                            if (!ReferenceEquals(getFriendGamePlayedMethod, null))
                            {
                                // GetFriendGamePlayed has different signatures in different Steamworks versions
                                var parameters = getFriendGamePlayedMethod.GetParameters();
                                
                                if (parameters.Length >= 2)
                                {
                                    try
                                    {
                                        // Method 1: Try with FriendGameInfo_t struct as out parameter
                                        object[] args = new object[parameters.Length];
                                        args[0] = friendIdResult;
                                        
                                        // Initialize the out parameter with default value
                                        if (parameters[1].ParameterType.IsByRef)
                                        {
                                            var structType = parameters[1].ParameterType.GetElementType();
                                            args[1] = Activator.CreateInstance(structType);
                                        }
                                        
                                        object gameResult = getFriendGamePlayedMethod.Invoke(null, args);
                                        isInGame = Convert.ToBoolean(gameResult);
                                        
                                        if (isInGame && args.Length > 1 && !ReferenceEquals(args[1], null))
                                        {
                                            // Extract game info from the struct
                                            var gameInfoStruct = args[1];
                                            var gameInfoType = gameInfoStruct.GetType();
                                            
                                            // Look for different possible field names for the game/app ID
                                            var appIdField = gameInfoType.GetField("m_gameID") ?? 
                                                           gameInfoType.GetField("gameID") ?? 
                                                           gameInfoType.GetField("m_nGameID") ??
                                                           gameInfoType.GetField("AppID") ??
                                                           gameInfoType.GetField("m_nAppID") ??
                                                           gameInfoType.GetField("appID");
                                            
                                            if (!ReferenceEquals(appIdField, null))
                                            {
                                                try
                                                {
                                                    var appIdValue = appIdField.GetValue(gameInfoStruct);
                                                    
                                                    // Add detailed debugging to understand the structure
                                                    Debug.Log($"[SteamFriendsHelper] Debug: Friend {friendSteamId} app ID field info:");
                                                    Debug.Log($"  Field name: {appIdField.Name}");
                                                    Debug.Log($"  Field type: {appIdField.FieldType.FullName}");
                                                    Debug.Log($"  Value type: {appIdValue?.GetType().FullName ?? "null"}");
                                                    Debug.Log($"  Value: {appIdValue}");
                                                    
                                                    // Handle different field types (might be CGameID, uint, ulong, etc.)
                                                    uint appId = 0;
                                                    if (appIdValue is uint directUint)
                                                    {
                                                        appId = directUint;
                                                        Debug.Log($"[SteamFriendsHelper] Direct uint cast: {appId}");
                                                    }
                                                    else if (appIdValue is ulong ulongValue)
                                                    {
                                                        appId = (uint)(ulongValue & 0xFFFFFFFF); // Take lower 32 bits
                                                        Debug.Log($"[SteamFriendsHelper] ulong cast: {ulongValue} -> {appId}");
                                                    }
                                                    else if (appIdValue is int intValue)
                                                    {
                                                        appId = (uint)intValue;
                                                        Debug.Log($"[SteamFriendsHelper] int cast: {intValue} -> {appId}");
                                                    }
                                                    else if (!ReferenceEquals(appIdValue, null))
                                                    {
                                                        // Try to extract from CGameID or similar struct
                                                        var appIdType = appIdValue.GetType();
                                                        Debug.Log($"[SteamFriendsHelper] Trying to extract from complex type: {appIdType.FullName}");
                                                        
                                                        // Look for various possible field names
                                                        var idField = appIdType.GetField("m_nAppID") ?? 
                                                                     appIdType.GetField("AppID") ?? 
                                                                     appIdType.GetField("appID") ?? 
                                                                     appIdType.GetField("m_gameID") ??
                                                                     appIdType.GetField("m_nGameID") ??
                                                                     appIdType.GetField("ID") ??
                                                                     appIdType.GetField("value") ??
                                                                     appIdType.GetField("Value");
                                                        
                                                        if (!ReferenceEquals(idField, null))
                                                        {
                                                            var idValue = idField.GetValue(appIdValue);
                                                            Debug.Log($"[SteamFriendsHelper] Found nested field '{idField.Name}': {idValue} ({idValue?.GetType().FullName})");
                                                            
                                                            if (idValue is uint nestedUint)
                                                            {
                                                                appId = nestedUint;
                                                            }
                                                            else if (idValue is ulong nestedUlong)
                                                            {
                                                                appId = (uint)(nestedUlong & 0xFFFFFFFF);
                                                            }
                                                            else if (idValue is int nestedInt)
                                                            {
                                                                appId = (uint)nestedInt;
                                                            }
                                                            else if (!ReferenceEquals(idValue, null))
                                                            {
                                                                try
                                                                {
                                                                    appId = Convert.ToUInt32(idValue);
                                                                }
                                                                catch (Exception convertEx)
                                                                {
                                                                    Debug.LogWarning($"[SteamFriendsHelper] Could not convert nested field value to uint: {convertEx.Message}");
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // List all available fields for debugging
                                                            var fields = appIdType.GetFields();
                                                            Debug.Log($"[SteamFriendsHelper] Available fields in {appIdType.Name}:");
                                                            foreach (var field in fields)
                                                            {
                                                                Debug.Log($"[SteamFriendsHelper]   {field.Name}: {field.FieldType.Name}");
                                                            }
                                                            
                                                            // Try to extract from CGameID using different approaches
                                                            try
                                                            {
                                                                // For CGameID, the m_GameID field contains the full 64-bit game ID
                                                                // The app ID is usually in the lower 32 bits, but we need to extract it properly
                                                                var gameIdField = appIdType.GetField("m_GameID");
                                                                if (!ReferenceEquals(gameIdField, null))
                                                                {
                                                                    var gameIdValue = gameIdField.GetValue(appIdValue);
                                                                    if (gameIdValue is ulong gameId)
                                                                    {
                                                                        // For CGameID, the app ID is typically in the lower 24 bits
                                                                        // Steam uses a complex encoding, but for most games the app ID is directly extractable
                                                                        appId = (uint)(gameId & 0xFFFFFF); // Take lower 24 bits
                                                                        Debug.Log($"[SteamFriendsHelper] Extracted app ID from CGameID m_GameID field: {gameId} -> {appId}");
                                                                    }
                                                                    else if (gameIdValue is uint directAppId)
                                                                    {
                                                                        appId = directAppId;
                                                                        Debug.Log($"[SteamFriendsHelper] Got direct app ID from CGameID: {appId}");
                                                                    }
                                                                    else
                                                                    {
                                                                        // Try to convert whatever we got
                                                                        ulong convertedId = Convert.ToUInt64(gameIdValue);
                                                                        appId = (uint)(convertedId & 0xFFFFFF);
                                                                        Debug.Log($"[SteamFriendsHelper] Converted CGameID value: {convertedId} -> {appId}");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Try direct conversion as last resort
                                                                    appId = Convert.ToUInt32(appIdValue);
                                                                    Debug.Log($"[SteamFriendsHelper] Direct conversion of CGameID: {appId}");
                                                                }
                                                            }
                                                            catch (Exception directConvertEx)
                                                            {
                                                                Debug.LogWarning($"[SteamFriendsHelper] Could not extract app ID from CGameID: {directConvertEx.Message}");
                                                                // Try one more approach - sometimes the CGameID itself can be cast to ulong
                                                                try
                                                                {
                                                                    // Check if the CGameID has an implicit conversion or can be treated as a number
                                                                    var stringValue = appIdValue.ToString();
                                                                    if (ulong.TryParse(stringValue, out ulong parsedValue))
                                                                    {
                                                                        appId = (uint)(parsedValue & 0xFFFFFF);
                                                                        Debug.Log($"[SteamFriendsHelper] Parsed CGameID from string: {stringValue} -> {appId}");
                                                                    }
                                                                }
                                                                catch
                                                                {
                                                                    Debug.LogWarning($"[SteamFriendsHelper] All CGameID extraction methods failed for friend {friendSteamId}");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    
                                                    Debug.Log($"[SteamFriendsHelper] Final app ID for friend {friendSteamId}: {appId}");
                                                    
                                                    if (appId > 0)
                                                    {
                                                        // Enter the Gungeon App ID is 311690
                                                        // Advanced Gungeons & Draguns (DLC) App ID is 780000
                                                        if (appId == 311690 || appId == 780000)
                                                        {
                                                            isPlayingETG = true;
                                                            gameInfo = "Enter the Gungeon";
                                                            Debug.Log($"[SteamFriendsHelper] ✅ Friend {friendSteamId} is playing Enter the Gungeon (App ID: {appId})!");
                                                        }
                                                        else
                                                        {
                                                            gameInfo = $"App {appId}";
                                                            Debug.Log($"[SteamFriendsHelper] Friend {friendSteamId} is playing App {appId}");
                                                        }
                                                    }
                                                }
                                                catch (Exception castEx)
                                                {
                                                    Debug.LogWarning($"[SteamFriendsHelper] Could not extract app ID from game info for friend {friendSteamId}: {castEx.Message}");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception methodEx)
                                    {
                                        // Try alternative approach - some versions might not use out parameters
                                        try
                                        {
                                            // Method 2: Direct call without out parameter
                                            object gameResult = getFriendGamePlayedMethod.Invoke(null, new object[] { friendIdResult });
                                            if (!ReferenceEquals(gameResult, null))
                                            {
                                                isInGame = true;
                                                
                                                // Try to extract app ID from the result
                                                var resultType = gameResult.GetType();
                                                var appIdField = resultType.GetField("m_nAppID") ?? 
                                                               resultType.GetField("AppID") ?? 
                                                               resultType.GetField("appID") ??
                                                               resultType.GetField("m_gameID");
                                                
                                                if (!ReferenceEquals(appIdField, null))
                                                {
                                                    var appIdValue = appIdField.GetValue(gameResult);
                                                    uint appId = 0;
                                                    
                                                    try
                                                    {
                                                        if (appIdValue is uint directUint)
                                                        {
                                                            appId = directUint;
                                                        }
                                                        else if (appIdValue is ulong ulongValue)
                                                        {
                                                            appId = (uint)(ulongValue & 0xFFFFFFFF);
                                                        }
                                                        else if (appIdValue is int intValue)
                                                        {
                                                            appId = (uint)intValue;
                                                        }
                                                        else if (!ReferenceEquals(appIdValue, null))
                                                        {
                                                            appId = Convert.ToUInt32(appIdValue);
                                                        }
                                                    }
                                                    catch (Exception appIdConvertEx)
                                                    {
                                                        Debug.LogWarning($"[SteamFriendsHelper] Could not convert app ID value in alternative method: {appIdConvertEx.Message}");
                                                    }
                                                    
                                                    if (appId == 311690 || appId == 780000)
                                                    {
                                                        isPlayingETG = true;
                                                        gameInfo = "Enter the Gungeon";
                                                    }
                                                    else if (appId > 0)
                                                    {
                                                        gameInfo = $"App {appId}";
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception altEx)
                                        {
                                            Debug.LogWarning($"[SteamFriendsHelper] Could not get game info for friend {friendSteamId} (both methods failed): {methodEx.Message} | {altEx.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    // Single parameter method
                                    try
                                    {
                                        object gameResult = getFriendGamePlayedMethod.Invoke(null, new object[] { friendIdResult });
                                        isInGame = !ReferenceEquals(gameResult, null);
                                    }
                                    catch (Exception singleEx)
                                    {
                                        Debug.LogWarning($"[SteamFriendsHelper] Could not get game info for friend {friendSteamId} (single param): {singleEx.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[SteamFriendsHelper] Error processing game info for friend {friendSteamId}: {ex.Message}");
                        }
                        
                        // Check for GungeonTogether status
                        bool hasGungeonTogether = false;
                        string gungeonTogetherStatus = "";
                        string gungeonTogetherVersion = "";
                        
                        if (isPlayingETG)
                        {
                            try
                            {
                                gungeonTogetherStatus = SteamReflectionHelper.GetFriendRichPresence(friendSteamId, "gungeon_together");
                                gungeonTogetherVersion = SteamReflectionHelper.GetFriendRichPresence(friendSteamId, "gt_version");
                                
                                if (!string.IsNullOrEmpty(gungeonTogetherStatus))
                                {
                                    hasGungeonTogether = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[SteamFriendsHelper] Could not check GungeonTogether status for {friendName}: {ex.Message}");
                            }
                        }
                        
                        var friendInfo = new FriendInfo
                        {
                            steamId = friendSteamId,
                            name = friendName,
                            personaName = friendName,
                            isOnline = isOnline,
                            isInGame = isInGame,
                            gameInfo = gameInfo,
                            isPlayingETG = isPlayingETG,
                            currentGameName = gameInfo,
                            hasGungeonTogether = hasGungeonTogether,
                            gungeonTogetherStatus = gungeonTogetherStatus,
                            gungeonTogetherVersion = gungeonTogetherVersion
                        };
                        
                        friends.Add(friendInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SteamFriendsHelper] Error processing friend {i}: {ex.Message}");
                    }
                }
                
                Debug.Log($"[SteamFriendsHelper] Successfully processed {friends.Count} friends:");
                Debug.Log($"  Online friends: {friends.FindAll(f => f.isOnline).Count}");
                Debug.Log($"  Playing ETG: {friends.FindAll(f => f.isPlayingETG).Count}");
                
                return friends.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamFriendsHelper] Error getting Steam friends: {e.Message}");
                Debug.LogError($"[SteamFriendsHelper] Stack trace: {e.StackTrace}");
                return new FriendInfo[0];
            }
        }
        
        /// <summary>
        /// Print friends list for debugging (backward compatibility)
        /// </summary>
        public static void PrintFriendsList()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] === Steam Friends List ===");
                
                var friends = GetSteamFriends();
                
                Debug.Log($"[ETGSteamP2P] Total friends: {friends.Length}");
                
                int onlineFriends = 0;
                int inGameFriends = 0;
                int etgFriends = 0;
                int gungeonTogetherFriends = 0;
                
                foreach (var friend in friends)
                {
                    if (friend.isOnline) onlineFriends++;
                    if (friend.isInGame) inGameFriends++;
                    if (friend.isPlayingETG) etgFriends++;
                    
                    string status = friend.isOnline ? "Online" : "Offline";
                    if (friend.isInGame)
                    {
                        status += $" (In Game: {friend.gameInfo})";
                        if (friend.isPlayingETG)
                        {
                            status += " 🎮 ETG";
                            
                            // Check if they're actually playing GungeonTogether
                            try
                            {
                                string gungeonTogetherStatus = SteamReflectionHelper.GetFriendRichPresence(friend.steamId, "gungeon_together");
                                string gtVersion = SteamReflectionHelper.GetFriendRichPresence(friend.steamId, "gt_version");
                                
                                if (!string.IsNullOrEmpty(gungeonTogetherStatus))
                                {
                                    gungeonTogetherFriends++;
                                    if (string.Equals(gungeonTogetherStatus, "hosting"))
                                    {
                                        status += " 🌐 HOSTING GT";
                                    }
                                    else if (string.Equals(gungeonTogetherStatus, "playing"))
                                    {
                                        status += " 🤝 PLAYING GT";
                                    }
                                    else
                                    {
                                        status += $" 🔧 GT ({gungeonTogetherStatus})";
                                    }
                                    
                                    if (!string.IsNullOrEmpty(gtVersion))
                                    {
                                        status += $" v{gtVersion}";
                                    }
                                }
                                else
                                {
                                    status += " (vanilla ETG)";
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ETGSteamP2P] Could not check GungeonTogether status for {friend.name}: {ex.Message}");
                                status += " (GT status unknown)";
                            }
                        }
                    }
                    
                    Debug.Log($"[ETGSteamP2P] Friend: {friend.name} ({friend.steamId}) - {status}");
                }
                
                Debug.Log($"[ETGSteamP2P] Summary:");
                Debug.Log($"[ETGSteamP2P] • Total friends online: {onlineFriends}");
                Debug.Log($"[ETGSteamP2P] • Playing Enter the Gungeon: {etgFriends}");
                Debug.Log($"[ETGSteamP2P] • Using GungeonTogether: {gungeonTogetherFriends}");
                
                if (etgFriends == 0)
                {
                    Debug.Log("[ETGSteamP2P] 🎮 No friends currently in Enter the Gungeon");
                }
                else if (gungeonTogetherFriends == 0)
                {
                    Debug.Log("[ETGSteamP2P] 🤝 No friends currently using GungeonTogether");
                }
                
                // Show available hosts
                var hosts = SteamHostManager.GetAvailableHosts();
                var hostDict = SteamHostManager.GetAvailableHostsDict();
                Debug.Log($"[ETGSteamP2P] 🌐 Available GungeonTogether hosts: {hosts.Length}");
                
                if (hosts.Length == 0)
                {
                    Debug.Log("[ETGSteamP2P] 🌐 No confirmed GungeonTogether hosts found");
                }
                else
                {
                    for (int i = 0; i < hosts.Length; i++)
                    {
                        var hostSteamId = hosts[i];
                        string hostInfo = $"Steam ID {hostSteamId}";
                        
                        if (hostDict.ContainsKey(hostSteamId))
                        {
                            var host = hostDict[hostSteamId];
                            hostInfo = $"{host.sessionName} (Steam ID: {hostSteamId})";
                        }
                        
                        Debug.Log($"[ETGSteamP2P] Host {i + 1}: {hostInfo}");
                    }
                }
                
                Debug.Log("[ETGSteamP2P] === End Steam Friends List ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error printing friends list: {e.Message}");
            }
        }
        
        /// <summary>
        /// Debug Steam friends (backward compatibility)
        /// </summary>
        public static void DebugSteamFriends()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] DebugSteamFriends called - this method was moved during refactoring");
                Debug.Log("[ETGSteamP2P] Steam friends debugging functionality needs to be re-implemented");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error debugging Steam friends: {e.Message}");
            }
        }
    }
}
