using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Manages Steam callbacks for overlay join functionality and invite handling
    /// </summary>
    public static class SteamCallbackManager
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
        public static event Action<ulong> OnJoinRequested;
        
        private static bool joinCallbacksRegistered = false;
        private static bool earlyInitialized = false;
        
        // Store pending join requests that arrive before the game is ready
        private static Queue<ulong> pendingJoinRequests = new Queue<ulong>();
        private static Queue<string> pendingOverlayJoinRequests = new Queue<string>();
        
        /// <summary>
        /// Initialize Steam callbacks for overlay join functionality and invite handling
        /// </summary>
        public static void InitializeSteamCallbacks()
        {
            if (earlyInitialized)
            {
                return;
            }
            
            earlyInitialized = true;
            
            try
            {
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Cannot initialize Steam callbacks - Steamworks assembly not available");
                    return;
                }
                
                StartSteamCommandLineMonitoring();
                
                // Find Steam callback types - try multiple possible type names
                Type callbackBaseType = steamworksAssembly.GetType("Steamworks.Callback", false) ??
                                       steamworksAssembly.GetType("Steamworks.Callback`1", false) ??
                                       steamworksAssembly.GetType("Steamworks.CCallbackBase", false) ??
                                       steamworksAssembly.GetType("Steamworks.CallResult", false) ??
                                       steamworksAssembly.GetType("Steamworks.CallResult`1", false);
                
                Type gameOverlayActivatedType = steamworksAssembly.GetType("Steamworks.GameOverlayActivated_t", false);
                Type gameLobbyJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested_t", false);
                Type gameJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested_t", false);
                
                // Also try alternate naming patterns
                if (ReferenceEquals(gameOverlayActivatedType, null))
                {
                    gameOverlayActivatedType = steamworksAssembly.GetType("Steamworks.GameOverlayActivated", false);
                }
                if (ReferenceEquals(gameLobbyJoinRequestedType, null))
                {
                    gameLobbyJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested", false);
                }
                if (ReferenceEquals(gameJoinRequestedType, null))
                {
                    gameJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested", false);
                }
                try
                {
                    // Only find callback types for diagnostic purposes - don't log them all
                    Type[] types = steamworksAssembly.GetTypes();
                    List<string> callbackTypes = new List<string>();
                    foreach (Type type in types)
                    {
                        string typeName = type.FullName ?? type.Name;
                        if (typeName.Contains("Callback") || typeName.Contains("_t"))
                        {
                            callbackTypes.Add(typeName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ETGSteamP2P] Could not enumerate callback types: " + ex.Message);
                }
                
                // CRITICAL: Ensure Steam callbacks are being processed
                var steamApiType = steamworksAssembly.GetType("Steamworks.SteamAPI", false);
                if (!ReferenceEquals(steamApiType, null))
                {
                    runCallbacksMethod = steamApiType.GetMethod("RunCallbacks", BindingFlags.Public | BindingFlags.Static);
                    if (ReferenceEquals(runCallbacksMethod, null))
                    {
                        Debug.LogWarning("[ETGSteamP2P] SteamAPI.RunCallbacks method not found");
                    }
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] SteamAPI type not found");
                }

                if (ReferenceEquals(callbackBaseType, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Steam callback base type not found - implementing fallback polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true;
                }
                else
                {
                    Debug.Log("[ETGSteamP2P] Attempting to register Steam callbacks for P2P join detection...");
                    bool registered = false;
                    
                    bool richPresenceRegistered = TryRegisterCallback(steamworksAssembly, callbackBaseType, gameJoinRequestedType, "OnGameRichPresenceJoinRequested");
                    if (richPresenceRegistered)
                    {
                        registered = true;
                    }
                    
                    bool lobbyRegistered = TryRegisterCallback(steamworksAssembly, callbackBaseType, gameLobbyJoinRequestedType, "OnGameLobbyJoinRequested");
                    if (lobbyRegistered)
                    {
                        registered = true;
                    }
                    
                    // Register LobbyEnter_t callback
                    var lobbyEnterType = steamworksAssembly.GetType("Steamworks.LobbyEnter_t", false);
                    if (!ReferenceEquals(lobbyEnterType, null))
                    {
                        TryRegisterCallback(steamworksAssembly, callbackBaseType, lobbyEnterType, "OnLobbyEnter");
                        Debug.Log("[SteamCallbackManager] Registered LobbyEnter_t callback");
                    }
                    
                    if (registered)
                    {
                        Debug.Log("[ETGSteamP2P] Steam callbacks registered successfully");
                        joinCallbacksRegistered = true;
                    }
                    else
                    {
                        Debug.LogWarning("[ETGSteamP2P] Failed to register Steam callbacks - using fallback detection");
                        SteamFallbackDetection.InitializeFallbackJoinDetection();
                        joinCallbacksRegistered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[ETGSteamP2P] Failed to initialize Steam callbacks: " + ex.Message);
                SteamFallbackDetection.InitializeFallbackJoinDetection();
                joinCallbacksRegistered = true;
            }
        }

        /// <summary>
        /// Try to register a Steam callback for the specified type - comprehensive approach from working version
        /// </summary>
        private static bool TryRegisterCallback(Assembly steamworksAssembly, Type callbackBaseType, Type callbackDataType, string handlerMethodName)
        {
            try
            {
                if (ReferenceEquals(callbackDataType, null))
                {
                    return false;
                }
                
                Type genericCallbackType = null;
                
                if (callbackBaseType.IsGenericTypeDefinition)
                {
                    genericCallbackType = callbackBaseType.MakeGenericType(new Type[] { callbackDataType });
                }
                else
                {
                    Type callbackGenericType = steamworksAssembly.GetType("Steamworks.Callback`1", false);
                    if (ReferenceEquals(callbackGenericType, null))
                    {
                        return false;
                    }
                    genericCallbackType = callbackGenericType.MakeGenericType(new Type[] { callbackDataType });
                }
                
                if (ReferenceEquals(genericCallbackType, null))
                {
                    return false;
                }
                
                // Find the appropriate delegate type
                Type delegateType = null;
                object delegateInstance = null;
                
                // Try nested DispatchDelegate first
                Type nestedDispatchDelegate = steamworksAssembly.GetType("Steamworks.Callback`1+DispatchDelegate", false);
                if (!ReferenceEquals(nestedDispatchDelegate, null))
                {
                    delegateType = nestedDispatchDelegate.MakeGenericType(new Type[] { callbackDataType });
                }
                
                // Try other delegate types
                if (ReferenceEquals(delegateType, null))
                {
                    Type dispatchDelegate = steamworksAssembly.GetType("Steamworks.DispatchDelegate", false) ??
                                          steamworksAssembly.GetType("Steamworks.CallbackDispatchDelegate", false) ??
                                          steamworksAssembly.GetType("Steamworks.SteamAPICall_t+CallbackDispatchDelegate", false);
                    
                    if (!ReferenceEquals(dispatchDelegate, null))
                    {
                        if (dispatchDelegate.IsGenericTypeDefinition)
                        {
                            delegateType = dispatchDelegate.MakeGenericType(new Type[] { callbackDataType });
                        }
                        else
                        {
                            delegateType = dispatchDelegate;
                        }
                    }
                }
                
                // Fallback to Action<T>
                if (ReferenceEquals(delegateType, null))
                {
                    delegateType = typeof(Action<>).MakeGenericType(new Type[] { callbackDataType });
                }
                
                // Find our handler method
                MethodInfo handlerMethod = typeof(SteamCallbackManager).GetMethod(handlerMethodName, BindingFlags.NonPublic | BindingFlags.Static);
                if (ReferenceEquals(handlerMethod, null))
                {
                    return false;
                }
                
                try
                {
                    delegateInstance = Delegate.CreateDelegate(delegateType, handlerMethod);
                }
                catch (Exception ex)
                {
                    return false;
                }
                
                // Try different constructor approaches
                ConstructorInfo[] constructors = genericCallbackType.GetConstructors();
                
                object callbackInstance = null;
                bool success = false;
                
                foreach (ConstructorInfo constructor in constructors)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    
                    // Try single parameter constructor
                    if (parameters.Length == 1)
                    {
                        Type parameterType = parameters[0].ParameterType;
                        
                        // Try Action<T> parameter
                        if (parameterType.IsGenericType && ReferenceEquals(parameterType.GetGenericTypeDefinition(), typeof(Action<>)))
                        {
                            try
                            {
                                Delegate actionDelegate = Delegate.CreateDelegate(parameterType, handlerMethod);
                                callbackInstance = constructor.Invoke(new object[] { actionDelegate });
                                success = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                // Continue to next constructor
                            }
                        }
                        // Try compatible delegate parameter
                        else if (parameterType.IsAssignableFrom(delegateType) || delegateType.IsAssignableFrom(parameterType))
                        {
                            try
                            {
                                callbackInstance = constructor.Invoke(new object[] { delegateInstance });
                                success = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                // Continue to next constructor
                            }
                        }
                    }
                    // Try parameterless constructor with field assignment
                    else if (!success && parameters.Length == 0)
                    {
                        try
                        {
                            callbackInstance = constructor.Invoke(new object[0]);
                            
                            // Try to find and set delegate field
                            string[] fieldNames = { "m_Func", "Func", "m_pCallback", "m_Callback", "callback", "Callback" };
                            bool fieldSet = false;
                            
                            foreach (string fieldName in fieldNames)
                            {
                                FieldInfo field = genericCallbackType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (!ReferenceEquals(field, null))
                                {
                                    try
                                    {
                                        field.SetValue(callbackInstance, delegateInstance);
                                        success = true;
                                        fieldSet = true;
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        // Continue trying other fields
                                    }
                                }
                            }
                            
                            if (success)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Continue to next constructor
                        }
                    }
                    // Try multi-parameter constructor
                    else if (!success && parameters.Length >= 2)
                    {
                        try
                        {
                            object[] args = new object[parameters.Length];
                            args[0] = delegateInstance;
                            for (int i = 1; i < parameters.Length; i++)
                            {
                                args[i] = null;
                            }
                            callbackInstance = constructor.Invoke(args);
                            success = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            // Continue to next constructor
                        }
                    }
                }
                
                if (!success)
                {
                    return false;
                }
                
                if (!ReferenceEquals(callbackInstance, null))
                {
                    // Store callback instance to prevent GC
                    if (callbackDataType.Name.Contains("Lobby"))
                    {
                        lobbyCallbackHandle = callbackInstance;
                    }
                    else if (callbackDataType.Name.Contains("Overlay"))
                    {
                        overlayCallbackHandle = callbackInstance;
                    }
                    else if (callbackDataType.Name.Contains("RichPresence") || callbackDataType.Name.Contains("Join"))
                    {
                        steamCallbackHandle = callbackInstance;
                    }
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Steam callback handler for GameRichPresenceJoinRequested_t
        /// </summary>
        private static void OnGameRichPresenceJoinRequested(object param)
        {
            Debug.Log("[SteamCallbackManager] OnGameRichPresenceJoinRequested called!");
            try
            {
                if (ReferenceEquals(param, null))
                {
                    Debug.LogWarning("[SteamCallbackManager] OnGameRichPresenceJoinRequested: param is null");
                    return;
                }
                
                Type paramType = null;
                try
                {
                    paramType = param.GetType();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[SteamCallbackManager] Error getting parameter type: " + ex.Message);
                    return;
                }
                
                if (ReferenceEquals(paramType, null))
                {
                    return;
                }
                
                FieldInfo steamIdField = null;
                try
                {
                    string[] steamIdFieldNames = {
                        "m_steamIDFriend", "steamIDFriend", "m_steamID", "steamID",
                        "m_ulSteamIDFriend", "ulSteamIDFriend"
                    };
                    
                    foreach (string fieldName in steamIdFieldNames)
                    {
                        steamIdField = paramType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (!ReferenceEquals(steamIdField, null))
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[SteamCallbackManager] Error getting steamID field: " + ex.Message);
                    return;
                }
                
                if (!ReferenceEquals(steamIdField, null))
                {
                    try
                    {
                        object steamIdValue = steamIdField.GetValue(param);
                        
                        ulong extractedSteamId = 0;
                        
                        if (!ReferenceEquals(steamIdValue, null))
                        {
                            // Try direct cast to ulong
                            if (steamIdValue is ulong)
                            {
                                extractedSteamId = (ulong)steamIdValue;
                            }
                            // Try cast from long
                            else if (steamIdValue is long)
                            {
                                extractedSteamId = (ulong)(long)steamIdValue;
                            }
                            // Try cast from uint
                            else if (steamIdValue is uint)
                            {
                                extractedSteamId = (ulong)(uint)steamIdValue;
                            }
                            // Try cast from int
                            else if (steamIdValue is int)
                            {
                                extractedSteamId = (ulong)(long)(int)steamIdValue;
                            }
                            else
                            {
                                // Try reflection on CSteamID or similar types
                                Type steamIdType = steamIdValue.GetType();
                                string[] internalFieldNames = { "m_SteamID", "m_ulSteamID", "Value" };
                                
                                foreach (string internalFieldName in internalFieldNames)
                                {
                                    FieldInfo internalField = steamIdType.GetField(internalFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (!ReferenceEquals(internalField, null))
                                    {
                                        object internalValue = internalField.GetValue(steamIdValue);
                                        
                                        if (internalValue is ulong)
                                        {
                                            extractedSteamId = (ulong)internalValue;
                                            break;
                                        }
                                        else if (internalValue is long)
                                        {
                                            extractedSteamId = (ulong)(long)internalValue;
                                            break;
                                        }
                                    }
                                }
                                
                                // Try parsing ToString() as fallback
                                if (extractedSteamId == 0)
                                {
                                    string steamIdString = steamIdValue.ToString();
                                    if (ulong.TryParse(steamIdString, out ulong parsedSteamId))
                                    {
                                        extractedSteamId = parsedSteamId;
                                    }
                                }
                            }
                        }
                        
                        if (extractedSteamId != 0 && extractedSteamId > 76561197960265728UL)
                        {
                            if (!pendingJoinRequests.Contains(extractedSteamId))
                            {
                                pendingJoinRequests.Enqueue(extractedSteamId);
                            }
                            
                            ETGSteamP2PNetworking.TriggerOverlayJoinEvent(extractedSteamId.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[SteamCallbackManager] Error extracting Steam ID from Rich Presence: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error handling Rich Presence join request: " + ex.Message);
            }
        }

        /// <summary>
        /// Steam callback handler for GameLobbyJoinRequested_t
        /// </summary>
        private static void OnGameLobbyJoinRequested(object param)
        {
            Debug.Log("[SteamCallbackManager] OnGameLobbyJoinRequested called!");
            try
            {
                ulong steamId = TryDetectSteamIdFromSteamAPI();
                if (steamId != 0)
                {
                    if (!pendingJoinRequests.Contains(steamId))
                    {
                        pendingJoinRequests.Enqueue(steamId);
                    }
                    ETGSteamP2PNetworking.TriggerOverlayJoinEvent(steamId.ToString());
                }
                else
                {
                    steamId = TryGetInviteHostSteamId();
                    if (steamId != 0)
                    {
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(steamId.ToString());
                    }
                    else if (pendingJoinRequests.Count > 0)
                    {
                        ulong queuedSteamId = pendingJoinRequests.Dequeue();
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(queuedSteamId.ToString());
                    }
                    else
                    {
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent("0");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error handling Lobby join request: " + ex.Message);
                try
                {
                    ETGSteamP2PNetworking.TriggerOverlayJoinEvent("0");
                }
                catch (Exception ex2)
                {
                    Debug.LogError("[SteamCallbackManager] Even fallback failed: " + ex2.Message);
                }
            }
        }

        /// <summary>
        /// Steam callback handler for LobbyEnter_t. Handles lobby member connection logic for joiners only.
        /// </summary>
        private static void OnLobbyEnter(object param)
        {
            Debug.Log("[SteamCallbackManager] OnLobbyEnter called!");
            // Early exit if host (host never connects to lobby members)
            if (SteamHostManager.IsCurrentlyHosting)
            {
                Debug.Log("[SteamCallbackManager] OnLobbyEnter: Host detected, skipping member connection logic.");
                return;
            }
            try
            {
                // Extract lobby ID from callback parameter (reflection)
                if (!TryGetLobbyId(param, out ulong lobbyId))
                {
                    Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Failed to extract lobby ID");
                    return;
                }
                // Get Steam matchmaking reflection methods
                if (!TryGetMatchmakingMethods(out var getNumMembersMethod, out var getMemberByIndexMethod))
                {
                    Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Failed to get matchmaking methods");
                    return;
                }
                // Convert lobbyId to CSteamID object
                var csteamId = SteamReflectionHelper.ConvertToCSteamID(lobbyId);
                if (csteamId == null)
                {
                    Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Failed to convert lobby ID to CSteamID");
                    return;
                }
                // Attempt to connect to all lobby members except self
                ConnectToLobbyMembers(csteamId, getNumMembersMethod, getMemberByIndexMethod);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error in OnLobbyEnter: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the lobby ID from the callback parameter using reflection.
        /// </summary>
        private static bool TryGetLobbyId(object param, out ulong lobbyId)
        {
            lobbyId = 0;
            
            if (param == null)
            {
                Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: param is null");
                return false;
            }

            var type = param.GetType();
            var lobbyIdField = type.GetField("m_ulSteamIDLobby") ?? 
                            type.GetField("m_SteamIDLobby") ?? 
                            type.GetField("lobbyID");

            if (lobbyIdField == null)
            {
                Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Could not find lobbyId field via reflection");
                return false;
            }

            var value = lobbyIdField.GetValue(param);
            
            // Accept ulong or parse from string
            return value switch
            {
                ulong ul => (lobbyId = ul) > 0,
                _ when value != null && ulong.TryParse(value.ToString(), out lobbyId) => lobbyId > 0,
                _ => false
            };
        }

        /// <summary>
        /// Gets the Steam matchmaking methods for lobby member enumeration via reflection.
        /// </summary>
        private static bool TryGetMatchmakingMethods(out MethodInfo getNumMembersMethod, out MethodInfo getMemberByIndexMethod)
        {
            getNumMembersMethod = null;
            getMemberByIndexMethod = null;

            var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
            var matchmakingType = steamworksAssembly?.GetType("Steamworks.SteamMatchmaking", false);
            
            if (matchmakingType == null)
            {
                Debug.LogWarning("[SteamCallbackManager] Could not find SteamMatchmaking type");
                return false;
            }

            getNumMembersMethod = matchmakingType.GetMethod("GetNumLobbyMembers");
            getMemberByIndexMethod = matchmakingType.GetMethod("GetLobbyMemberByIndex");

            return getNumMembersMethod != null && getMemberByIndexMethod != null;
        }

        /// <summary>
        /// Connects to all lobby members except self using Steam P2P networking.
        /// </summary>
        private static void ConnectToLobbyMembers(object csteamId, MethodInfo getNumMembersMethod, MethodInfo getMemberByIndexMethod)
        {
            // Get member count
            if (!TryInvokeMethod(getNumMembersMethod, new[] { csteamId }, out int memberCount))
            {
                Debug.LogError("[SteamCallbackManager] Failed to get lobby member count");
                return;
            }

            var mySteamId = SteamReflectionHelper.GetSteamID();
            var networking = ETGSteamP2PNetworking.Instance;
            
            if (networking == null)
            {
                Debug.LogWarning("[SteamCallbackManager] ETGSteamP2PNetworking.Instance is null");
                return;
            }

            // Connect to each member except self
            for (int i = 0; i < memberCount; i++)
            {
                if (TryGetMemberSteamId(getMemberByIndexMethod, csteamId, i, out ulong memberSteamId) &&
                    memberSteamId != 0 && 
                    memberSteamId != mySteamId)
                {
                    networking.AcceptP2PSession(memberSteamId);
                }
            }
        }

        /// <summary>
        /// Gets the Steam ID of a lobby member at the given index.
        /// </summary>
        private static bool TryGetMemberSteamId(MethodInfo getMemberByIndexMethod, object csteamId, int index, out ulong memberSteamId)
        {
            memberSteamId = 0;

            if (!TryInvokeMethod(getMemberByIndexMethod, new[] { csteamId, index }, out object memberSteamIdObj))
            {
                Debug.LogError($"[SteamCallbackManager] Failed to get lobby member at index {index}");
                return false;
            }

            return memberSteamIdObj switch
            {
                ulong msi => (memberSteamId = msi) > 0,
                _ when memberSteamIdObj != null => TryExtractSteamIdFromObject(memberSteamIdObj, out memberSteamId),
                _ => false
            };
        }

        /// <summary>
        /// Extracts a Steam ID from a lobby member object using property or string parsing.
        /// </summary>
        private static bool TryExtractSteamIdFromObject(object steamIdObj, out ulong steamId)
        {
            steamId = 0;
            
            // Try to get m_SteamID property
            var steamIdProp = steamIdObj.GetType().GetProperty("m_SteamID");
            if (steamIdProp != null)
            {
                steamId = (ulong)steamIdProp.GetValue(steamIdObj, null);
                return steamId > 0;
            }

            // Fallback to string parsing
            return ulong.TryParse(steamIdObj.ToString(), out steamId) && steamId > 0;
        }

        /// <summary>
        /// Invokes a MethodInfo and returns the result as type T.
        /// </summary>
        private static bool TryInvokeMethod<T>(MethodInfo method, object[] parameters, out T result)
        {
            result = default(T);
            
            try
            {
                var returnValue = method.Invoke(null, parameters);
                if (returnValue is T typedResult)
                {
                    result = typedResult;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Exception calling {method.Name}: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Process Steam callbacks
        /// </summary>
        public static void ProcessSteamCallbacks()
        {
            try
            {
                if (!ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod.Invoke(null, null);
                }
                
                if (pendingJoinRequests.Count > 0 || pendingOverlayJoinRequests.Count > 0)
                {
                    Debug.Log("[ETGSteamP2P] Processing pending join requests...");
                    ProcessPendingJoinRequests();
                }
                
                SteamFallbackDetection.ProcessFallbackDetection();
                CheckForSteamJoinRequests();
            }
            catch (Exception ex)
            {
                if (Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("[ETGSteamP2P] Error processing Steam callbacks: " + ex.Message);
                }
            }
        }

        private static void CheckForSteamJoinRequests()
        {
            try
            {
                // Periodic checks can be added here if needed
            }
            catch (Exception ex)
            {
                if (Time.frameCount % 1800 == 0)
                {
                    Debug.LogWarning("[ETGSteamP2P] Error checking for Steam join requests: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Get callback status
        /// </summary>
        public static string GetCallbackStatus()
        {
            try
            {
                string status = "[ETGSteamP2P] Callback Status:\n";
                status += $"  Callbacks Registered: {joinCallbacksRegistered}\n";
                status += $"  Using Fallback Detection: {SteamFallbackDetection.IsUsingFallbackDetection}\n";
                status += "  RunCallbacks Method: " + (!ReferenceEquals(runCallbacksMethod, null) ? "✅" : "❌") + "\n";
                status += "  Lobby Callback Handle: " + (!ReferenceEquals(lobbyCallbackHandle, null) ? "✅" : "❌") + "\n";
                status += "  Overlay Callback Handle: " + (!ReferenceEquals(overlayCallbackHandle, null) ? "✅" : "❌") + "\n";
                status += "  Steam Callback Handle: " + (!ReferenceEquals(steamCallbackHandle, null) ? "✅" : "❌") + "\n";
                return status;
            }
            catch (Exception ex)
            {
                return "[ETGSteamP2P] Error getting callback status: " + ex.Message;
            }
        }

        /// <summary>
        /// Trigger overlay join event
        /// </summary>
        public static void TriggerOverlayJoinEvent(string hostSteamId)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Triggering overlay join event for host: " + hostSteamId);
                OnOverlayJoinRequested?.Invoke(hostSteamId);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ETGSteamP2P] Error triggering overlay join event: " + ex.Message);
            }
        }

        /// <summary>
        /// Trigger join requested event
        /// </summary>
        public static void TriggerJoinRequested(ulong steamId)
        {
            try
            {
                // Attempt to join the Steam lobby using ISteamMatchmaking::JoinLobby via reflection
                Debug.Log($"[ETGSteamP2P] Attempting to join lobby with ID: {steamId}");
                var joinLobbyMethod = SteamReflectionHelper.JoinLobbyMethod;
                if (!ReferenceEquals(joinLobbyMethod, null))
                {
                    // The argument is the lobby ID (CSteamID or ulong)
                    var steamIdParam = SteamReflectionHelper.ConvertToCSteamID(steamId);
                    joinLobbyMethod.Invoke(null, new object[] { steamIdParam });
                    Debug.Log($"[ETGSteamP2P] Called JoinLobby for lobby ID: {steamId}");
                }
                else
                {
                    Debug.LogError("[ETGSteamP2P] JoinLobbyMethod is null - cannot join lobby");
                }
                // Fire the event for any listeners (for legacy/compatibility)
                OnJoinRequested?.Invoke(steamId);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ETGSteamP2P] Error triggering join requested event: " + ex.Message);
            }
        }

        /// <summary>
        /// Check if callbacks are registered
        /// </summary>
        public static bool AreCallbacksRegistered
        {
            get { return joinCallbacksRegistered; }
        }

        /// <summary>
        /// Start Steam command line monitoring
        /// </summary>
        private static void StartSteamCommandLineMonitoring()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Starting Steam command line monitoring for early join detection...");
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                Debug.Log($"[ETGSteamP2P] Checking {commandLineArgs.Length} command line arguments for Steam join requests...");
                
                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    if (commandLineArgs[i].StartsWith("+connect") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        if (ulong.TryParse(target, out ulong steamId))
                        {
                            Debug.Log($"[ETGSteamP2P] Found Steam connect command for Steam ID: {steamId}");
                            pendingJoinRequests.Enqueue(steamId);
                        }
                    }
                    else if (commandLineArgs[i].StartsWith("+connect_lobby") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        if (ulong.TryParse(target, out ulong lobbyId))
                        {
                            Debug.Log($"[ETGSteamP2P] Found Steam lobby connect for lobby: {lobbyId}");
                            
                            // CRITICAL: Get the actual host Steam ID from the lobby, not the lobby ID itself
                            ulong hostSteamId = SteamReflectionHelper.GetLobbyOwner(lobbyId);
                            if (hostSteamId != 0)
                            {
                                pendingJoinRequests.Enqueue(hostSteamId);
                            }
                            else
                            {
                                Debug.LogWarning($"[ETGSteamP2P] Could not get lobby owner for lobby {lobbyId} - lobby may not exist or Steam not ready");
                            }
                        }
                    }
                    else if (commandLineArgs[i].Contains("steam://joinlobby/") || commandLineArgs[i].Contains("steam://rungame/"))
                    {
                        Debug.Log("[ETGSteamP2P] Found Steam URL in command line: " + commandLineArgs[i]);
                        TryExtractSteamIdFromUrl(commandLineArgs[i]);
                    }
                }
                
                if (pendingJoinRequests.Count > 0)
                {
                    Debug.Log($"[ETGSteamP2P] Found {pendingJoinRequests.Count} pending Steam join requests from command line");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ETGSteamP2P] Error in Steam command line monitoring: " + ex.Message);
            }
        }

        private static void TryExtractSteamIdFromUrl(string url)
        {
            try
            {
                MatchCollection matches = Regex.Matches(url, @"\d{17}");
                foreach (Match match in matches)
                {
                    if (ulong.TryParse(match.Value, out ulong steamId) && steamId > 76561197960265728UL)
                    {
                        Debug.Log($"[ETGSteamP2P] Extracted Steam ID from URL: {steamId}");
                        pendingJoinRequests.Enqueue(steamId);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ETGSteamP2P] Error extracting Steam ID from URL: " + ex.Message);
            }
        }

        /// <summary>
        /// Process pending join requests
        /// </summary>
        public static void ProcessPendingJoinRequests()
        {
            try
            {
                while (pendingJoinRequests.Count > 0)
                {
                    ulong steamId = pendingJoinRequests.Dequeue();
                    Debug.Log($"[ETGSteamP2P] Processing pending join request for Steam ID: {steamId}");
                    OnJoinRequested?.Invoke(steamId);
                }
                
                while (pendingOverlayJoinRequests.Count > 0)
                {
                    string hostSteamId = pendingOverlayJoinRequests.Dequeue();
                    Debug.Log("[ETGSteamP2P] Processing pending overlay join request for host: " + hostSteamId);
                    OnOverlayJoinRequested?.Invoke(hostSteamId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[ETGSteamP2P] Error processing pending join requests: " + ex.Message);
            }
        }

        /// <summary>
        /// Check for pending session requests
        /// </summary>
        public static bool CheckForPendingSessionRequests(out ulong requestingSteamId)
        {
            requestingSteamId = 0;
            try
            {
                if (pendingJoinRequests.Count > 0)
                {
                    requestingSteamId = pendingJoinRequests.Peek();
                    Debug.Log($"[SteamCallbackManager] Found pending session request from Steam ID: {requestingSteamId}");
                    return true;
                }
                else if (TryDetectIncomingP2PConnections(out requestingSteamId))
                {
                    Debug.Log($"[SteamCallbackManager] Detected incoming P2P connection from Steam ID: {requestingSteamId}");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error checking pending requests: " + ex.Message);
                return false;
            }
        }

        private static bool TryDetectIncomingP2PConnections(out ulong requestingSteamId)
        {
            requestingSteamId = 0;
            try
            {
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    return false;
                }
                
                Type steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                if (ReferenceEquals(steamNetworkingType, null))
                {
                    return false;
                }
                
                MethodInfo isP2PPacketAvailableMethod = steamNetworkingType.GetMethod("IsP2PPacketAvailable", BindingFlags.Public | BindingFlags.Static);
                if (!ReferenceEquals(isP2PPacketAvailableMethod, null))
                {
                    uint msgSize = 0;
                    int channel = 0;
                    object[] args = new object[] { msgSize, channel };
                    object result = isP2PPacketAvailableMethod.Invoke(null, args);
                    
                    bool hasPacket = result is bool && (bool)result;
                    if (hasPacket)
                    {
                        MethodInfo readP2PPacketMethod = steamNetworkingType.GetMethod("ReadP2PPacket", BindingFlags.Public | BindingFlags.Static);
                        if (!ReferenceEquals(readP2PPacketMethod, null))
                        {
                            msgSize = Convert.ToUInt32(args[0]);
                            if (msgSize > 0)
                            {
                                byte[] buffer = new byte[msgSize];
                                uint bytesRead = 0;
                                uint msgSizeOut = 0;
                                ulong remoteSteamId = 0;
                                object[] readArgs = new object[] { buffer, msgSize, bytesRead, remoteSteamId, msgSizeOut };
                                
                                object readResult = readP2PPacketMethod.Invoke(null, readArgs);
                                bool readSuccess = readResult is bool && (bool)readResult;
                                
                                if (readSuccess)
                                {
                                    requestingSteamId = Convert.ToUInt64(readArgs[3]);
                                    Debug.Log($"[SteamCallbackManager] Detected packet from potential joiner: {requestingSteamId}");
                                    
                                    if (!pendingJoinRequests.Contains(requestingSteamId))
                                    {
                                        pendingJoinRequests.Enqueue(requestingSteamId);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error detecting incoming P2P connections: " + ex.Message);
                return false;
            }
        }

        public static ulong TryGetInviteHostSteamId()
        {
            try
            {
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                
                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    if (commandLineArgs[i].Contains("steam://joinlobby/"))
                    {
                        string[] parts = commandLineArgs[i].Split('/');
                        if (parts.Length >= 5)
                        {
                            string lobbyIdStr = parts[4];
                            if (ulong.TryParse(lobbyIdStr, out ulong lobbyId) && lobbyId > 76561197960265728UL)
                            {
                                
                                // CRITICAL: Get the actual host Steam ID from the lobby, not the lobby ID itself
                                ulong hostSteamId = SteamReflectionHelper.GetLobbyOwner(lobbyId);
                                if (hostSteamId != 0)
                                {
                                    return hostSteamId;
                                }
                                else
                                {
                                    Debug.LogWarning($"[ETGSteamP2P] TryGetInviteHostSteamId: Could not get owner for lobby {lobbyId}");
                                    return lobbyId; // Fallback to lobby ID (will likely fail but better than nothing)
                                }
                            }
                        }
                    }
                    else if (commandLineArgs[i].StartsWith("+connect") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        if (ulong.TryParse(target, out ulong steamId) && steamId > 76561197960265728UL)
                        {
                            return steamId;
                        }
                    }
                    else if (commandLineArgs[i].StartsWith("+connect_lobby") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        if (ulong.TryParse(target, out ulong lobbyId) && lobbyId > 76561197960265728UL)
                        {
                            // CRITICAL: Get the actual host Steam ID from the lobby, not the lobby ID itself
                            ulong hostSteamId = SteamReflectionHelper.GetLobbyOwner(lobbyId);
                            if (hostSteamId != 0)
                            {
                                return hostSteamId;
                            }
                            else
                            {
                                Debug.LogWarning($"[ETGSteamP2P] CheckForNewCommandLineArgs: Could not get owner for lobby {lobbyId}");
                                return lobbyId; // Fallback to lobby ID (will likely fail but better than nothing)
                            }
                        }
                    }
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error detecting invite host Steam ID: " + ex.Message);
                return 0;
            }
        }

        private static ulong TryDetectSteamIdFromSteamAPI()
        {
            try
            {
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    return 0;
                }
                
                Type steamFriendsType = steamworksAssembly.GetType("Steamworks.SteamFriends", false);
                if (!ReferenceEquals(steamFriendsType, null))
                {
                    MethodInfo getFriendCountMethod = steamFriendsType.GetMethod("GetFriendCount", BindingFlags.Public | BindingFlags.Static);
                    if (!ReferenceEquals(getFriendCountMethod, null))
                    {
                        object[] args = new object[] { 4 }; // k_EFriendFlagImmediate
                        object result = getFriendCountMethod.Invoke(null, args);
                        
                        if (result is int friendCount && friendCount > 0)
                        {
                            MethodInfo getFriendByIndexMethod = steamFriendsType.GetMethod("GetFriendByIndex", BindingFlags.Public | BindingFlags.Static);
                            if (!ReferenceEquals(getFriendByIndexMethod, null))
                            {
                                int maxCheck = Math.Min(friendCount, 10);
                                for (int i = 0; i < maxCheck; i++)
                                {
                                    try
                                    {
                                        object[] friendArgs = new object[] { i, 4 };
                                        object friendResult = getFriendByIndexMethod.Invoke(null, friendArgs);
                                        
                                        if (!ReferenceEquals(friendResult, null))
                                        {
                                            ulong steamId = 0;
                                            
                                            if (friendResult is ulong)
                                            {
                                                steamId = (ulong)friendResult;
                                            }
                                            else
                                            {
                                                // Try to extract from CSteamID
                                                Type friendType = friendResult.GetType();
                                                FieldInfo steamIdField = friendType.GetField("m_SteamID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                if (!ReferenceEquals(steamIdField, null))
                                                {
                                                    object steamIdValue = steamIdField.GetValue(friendResult);
                                                    if (steamIdValue is ulong)
                                                    {
                                                        steamId = (ulong)steamIdValue;
                                                    }
                                                }
                                            }
                                            
                                            if (steamId != 0 && steamId > 76561197960265728UL)
                                            {
                                                if (i == 0)
                                                {
                                                    return steamId;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[SteamCallbackManager] Error checking friend {i}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
                
                Debug.LogWarning("[SteamCallbackManager] Could not detect Steam ID from Steam API");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error detecting Steam ID from Steam API: " + ex.Message);
                return 0;
            }
        }
    }
}
