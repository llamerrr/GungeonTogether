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
                UnityEngine.Debug.Log("[ETGSteamP2P] Steam callbacks already initialized early, skipping...");
                return;
            }
            earlyInitialized = true;
            try
            {
                UnityEngine.Debug.Log("[ETGSteamP2P] \uD83D\uDD04 Initializing Steam callbacks for invite and overlay join support...");
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] Cannot initialize Steam callbacks - Steamworks assembly not available");
                    return;
                }

                // Try to find a valid callback base type
                Type callbackBaseType = steamworksAssembly.GetType("Steamworks.Callback", false)
                    ?? steamworksAssembly.GetType("Steamworks.Callback`1", false)
                    ?? steamworksAssembly.GetType("Steamworks.CCallbackBase", false)
                    ?? steamworksAssembly.GetType("Steamworks.CallResult", false)
                    ?? steamworksAssembly.GetType("Steamworks.CallResult`1", false);
                Type overlayActivatedType = steamworksAssembly.GetType("Steamworks.GameOverlayActivated_t", false)
                    ?? steamworksAssembly.GetType("Steamworks.GameOverlayActivated", false);
                Type gameLobbyJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested_t", false)
                    ?? steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested", false);
                Type gameRichPresenceJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested_t", false)
                    ?? steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested", false);
                Type steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);

                Type lobbyDataUpdateType = steamworksAssembly.GetType("LobbyDataUpdate_t", false);
                if (!ReferenceEquals(lobbyDataUpdateType, null))
                {
                    try
                    {
                        TryRegisterCallback(steamworksAssembly, callbackBaseType, lobbyDataUpdateType, "OnLobbyDataUpdate");
                    }
                    catch (Exception exception)
                    { 
                        UnityEngine.Debug.LogError($"[ETGSteamP2P] Failed to register OnLobbyDataUpdate callback: {exception.Message}");
                    }
                    
                }

                // Register callbacks if possible
                if (ReferenceEquals(callbackBaseType, null))
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] ⚠️ Steam callback base type not found - implementing fallback polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true;
                    return;
                }
                bool anyRegistered = false;
                if (!ReferenceEquals(gameRichPresenceJoinRequestedType, null) && TryRegisterCallback(steamworksAssembly, callbackBaseType, gameRichPresenceJoinRequestedType, "OnGameRichPresenceJoinRequested"))
                {
                    UnityEngine.Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameRichPresenceJoinRequested_t callback");
                    anyRegistered = true;
                }
                if (!ReferenceEquals(gameLobbyJoinRequestedType, null) && TryRegisterCallback(steamworksAssembly, callbackBaseType, gameLobbyJoinRequestedType, "OnGameLobbyJoinRequested"))
                {
                    UnityEngine.Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameLobbyJoinRequested_t callback");
                    anyRegistered = true;
                }
                if (anyRegistered)
                {
                    UnityEngine.Debug.Log("[ETGSteamP2P] ✅ Steam callbacks registered successfully - join requests should work");
                    joinCallbacksRegistered = true;
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] ⚠️ Failed to register Steam callbacks - falling back to manual detection");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ETGSteamP2P] Exception during Steam callback initialization: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Try to register a Steam callback for the specified type - comprehensive approach from working version
        /// </summary>
        private static bool TryRegisterCallback(Assembly steamworksAssembly, Type callbackBaseType, Type callbackDataType, string handlerMethodName)
        {
            Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] ENTERED: handler={handlerMethodName}, callbackDataType={callbackDataType}");
            try
            {
                if (ReferenceEquals(callbackDataType, null))
                {
                    Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] callbackDataType is null for handler {handlerMethodName}");
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
                        Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] callbackGenericType is null for handler {handlerMethodName}");
                        return false;
                    }
                    genericCallbackType = callbackGenericType.MakeGenericType(new Type[] { callbackDataType });
                }

                if (ReferenceEquals(genericCallbackType, null))
                {
                    Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] genericCallbackType is null for handler {handlerMethodName}");
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
                    Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] handlerMethod {handlerMethodName} not found");
                    return false;
                }

                // Log the types and method info for diagnosis
                Debug.LogWarning($"[SteamCallbackManager] [TryRegisterCallback] About to create delegate: handlerMethod={handlerMethodName}");
                Debug.Log($"[SteamCallbackManager] [TryRegisterCallback] Registering callback: handlerMethod={handlerMethod}, delegateType={delegateType}, callbackDataType={callbackDataType}, genericCallbackType={genericCallbackType}");

                try
                {
                    delegateInstance = Delegate.CreateDelegate(delegateType, handlerMethod);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamCallbackManager] [TryRegisterCallback] Failed to create delegate: {ex.Message}\n{ex.StackTrace}");
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
                            catch (Exception)
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
                            catch (Exception)
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

                            foreach (string fieldName in fieldNames)
                            {
                                FieldInfo field = genericCallbackType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (!ReferenceEquals(field, null))
                                {
                                    try
                                    {
                                        field.SetValue(callbackInstance, delegateInstance);
                                        success = true;
                                        break;
                                    }
                                    catch (Exception)
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
                        catch (Exception)
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
                        catch (Exception)
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
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Steam callback handler for GameLobbyJoinRequested_t (Steam overlay invite).
        /// </summary>
        private static void OnGameLobbyJoinRequested(object param)
        {
            if (ReferenceEquals(param, null))
            {
                Debug.LogWarning("[SteamCallbackManager] OnGameLobbyJoinRequested: param is null");
                return;
            }
            try
            {
                Debug.Log("[SteamCallbackManager] OnGameLobbyJoinRequested called!");

                ulong lobbyId;
                if (TryGetLobbyId(param, out lobbyId) && !ReferenceEquals(lobbyId, 0UL))
                {
                    if (!ReferenceEquals(SteamNetworking, null) && !ReferenceEquals(SteamNetworking.JoinLobby, null))
                    {
                        Debug.Log($"[SteamCallbackManager] [DEBUG] Using SteamNetworking.JoinLobby(lobbyId={lobbyId})");
                        SteamNetworking.JoinLobby(lobbyId);
                        return;
                    }
                    Debug.LogWarning("[SteamCallbackManager] SteamNetworking or JoinLobby is null");
                    return;
                }
                Debug.LogWarning("[SteamCallbackManager] OnGameLobbyJoinRequested: Could not parse lobbyId from value (expected ulong)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Exception in OnGameLobbyJoinRequested: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Steam callback handler for GameRichPresenceJoinRequested_t (Steam overlay join via friend list).
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
                // Try to extract Steam ID from param
                ulong steamId = 0;
                var type = param.GetType();
                var steamIdField = type.GetField("m_steamIDFriend") ?? type.GetField("steamIDFriend") ?? type.GetField("m_steamID") ?? type.GetField("steamID") ?? type.GetField("m_ulSteamIDFriend") ?? type.GetField("ulSteamIDFriend");
                if (steamIdField != null)
                {
                    var value = steamIdField.GetValue(param);
                    if (value is ulong ul)
                    {
                        steamId = ul;
                    }
                    else if (value != null && ulong.TryParse(value.ToString(), out ulong parsed))
                    {
                        steamId = parsed;
                    }
                }
                if (!ReferenceEquals(steamId, 0))
                {
                    Debug.Log($"[SteamCallbackManager] OnGameRichPresenceJoinRequested: Triggering overlay join for Steam ID {steamId}");
                    TriggerOverlayJoinEvent(steamId.ToString());
                }
                else
                {
                    Debug.LogWarning("[SteamCallbackManager] OnGameRichPresenceJoinRequested: Could not extract Steam ID");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error handling Rich Presence join request: " + ex.Message);
            }
        }

        /// <summary>
        /// Extracts the lobby ID from the callback parameter using reflection.
        /// </summary>
        private static bool TryGetLobbyId(object param, out ulong lobbyId)
        {
            lobbyId = 0;

            if (ReferenceEquals(param, null))
            {
                Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: param is null");
                return false;
            }

            var type = param.GetType();
            var lobbyIdField = type.GetField("m_ulSteamIDLobby") ??
                                type.GetField("m_SteamIDLobby") ??
                                type.GetField("lobbyID");

            if (ReferenceEquals(lobbyIdField, null))
            {
                Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Could not find lobbyId field via reflection");
                return false;
            }

            var value = lobbyIdField.GetValue(param);

            // Accept ulong or parse from string
            return value switch
            {
                ulong ul => (lobbyId = ul) > 0,
                _ when !ReferenceEquals(value, null) && ulong.TryParse(value.ToString(), out lobbyId) => lobbyId > 0,
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
                    // Removed legacy AcceptP2PSession call
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
                if (ReferenceEquals(OnJoinRequested, null))
                {
                    Debug.LogWarning($"[SteamCallbackManager] TriggerJoinRequested: OnJoinRequested event is null. steamId={steamId}");
                    return;
                }
                Debug.Log($"[SteamCallbackManager] TriggerJoinRequested: Invoking OnJoinRequested for steamId={steamId}");
                OnJoinRequested(steamId);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Exception in TriggerJoinRequested: " + ex.Message);
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

        public static void EnsureSteamCallbacksArePumped()
        {
            try
            {
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] Steamworks assembly not found in EnsureSteamCallbacksArePumped");
                    return;
                }
                if (ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod = steamworksAssembly.GetType("Steamworks.SteamAPI", false)?.GetMethod("RunCallbacks", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                }
                if (!ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod.Invoke(null, null);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] Could not find SteamAPI.RunCallbacks method");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ETGSteamP2P] Exception in EnsureSteamCallbacksArePumped: " + ex.Message);
            }
        }

        public static void LogSteamworksAssemblyInfo()
        {
            try
            {
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    UnityEngine.Debug.LogWarning("[ETGSteamP2P] Steamworks assembly not found in LogSteamworksAssemblyInfo");
                    return;
                }
                UnityEngine.Debug.Log($"[ETGSteamP2P] Steamworks assembly: {steamworksAssembly.FullName} location: {steamworksAssembly.Location}");
                foreach (var type in steamworksAssembly.GetTypes())
                {
                    if (type.Name.Contains("Lobby") || type.Name.Contains("Join") || type.Name.Contains("Callback"))
                    {
                        UnityEngine.Debug.Log($"[ETGSteamP2P] Type: {type.FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ETGSteamP2P] Exception in LogSteamworksAssemblyInfo: " + ex.Message);
            }
        }

        /// <summary>
        /// Steam callback handler for LobbyEnter_t. Handles lobby connection logic for both host and joiners.
        /// </summary>
        private static void OnLobbyEnter(object param)
        {
            Debug.Log($"[SteamCallbackManager] OnLobbyEnter called! param type: {(param == null ? "null" : param.GetType().FullName)}");

            try
            {
                // Extract lobby ID from callback parameter (reflection)
                if (!TryGetLobbyId(param, out ulong lobbyId))
                {
                    Debug.LogWarning("[SteamCallbackManager] OnLobbyEnter: Failed to extract lobby ID");
                    return;
                }

                Debug.Log($"[SteamCallbackManager] OnLobbyEnter: Extracted Lobby ID: {lobbyId} (type: {lobbyId.GetType().FullName})");

                // Get current user's Steam ID
                ulong mySteamId = SteamReflectionHelper.GetSteamID();

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

                // Handle logic for both host and joiners
                if (SteamHostManager.IsCurrentlyHosting)
                {
                    HandleHostLobbyEnter(csteamId, getNumMembersMethod, getMemberByIndexMethod, mySteamId);
                }
                else
                {
                    HandleJoinerLobbyEnter(csteamId, getNumMembersMethod, getMemberByIndexMethod, mySteamId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error in OnLobbyEnter: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles lobby enter logic for the host - notifies about new members
        /// </summary>
        private static void HandleHostLobbyEnter(object csteamId, MethodInfo getNumMembersMethod, MethodInfo getMemberByIndexMethod, ulong hostSteamId)
        {
            Debug.Log("[SteamCallbackManager] HandleHostLobbyEnter: Processing as host");

            // Get current member count
            if (!TryInvokeMethod(getNumMembersMethod, new[] { csteamId }, out int memberCount))
            {
                Debug.LogError("[SteamCallbackManager] Failed to get lobby member count");
                return;
            }

            Debug.Log($"[SteamCallbackManager] Current lobby member count: {memberCount}");

            // List all current members and identify new ones
            var currentMembers = new List<ulong>();
            for (int i = 0; i < memberCount; i++)
            {
                if (TryGetMemberSteamId(getMemberByIndexMethod, csteamId, i, out ulong memberSteamId) && memberSteamId != 0)
                {
                    currentMembers.Add(memberSteamId);

                    // If this member is not the host, they're a joiner
                    if (memberSteamId != hostSteamId)
                    {
                        Debug.Log($"[SteamCallbackManager] Player {memberSteamId} has joined the lobby!");

                        // Notify the host UI/game logic about the new player
                        NotifyHostOfNewPlayer(memberSteamId);

                        // Accept P2P session with the new player
                        AcceptP2PSessionWithPlayer(memberSteamId);
                    }
                }
            }
        }

        /// <summary>
        /// Handles lobby enter logic for joiners - connects to existing members
        /// </summary>
        private static void HandleJoinerLobbyEnter(object csteamId, MethodInfo getNumMembersMethod, MethodInfo getMemberByIndexMethod, ulong joinerSteamId)
        {
            Debug.Log("[SteamCallbackManager] HandleJoinerLobbyEnter: Processing as joiner");

            // Log the join event for analytics/debugging
            SteamHostManager.LogPlayerJoinedViaInviteOrOverlay(joinerSteamId);

            // Connect to all existing lobby members
            ConnectToLobbyMembers(csteamId, getNumMembersMethod, getMemberByIndexMethod, joinerSteamId);
        }

        /// <summary>
        /// Connects to all lobby members except self using Steam P2P networking.
        /// </summary>
        private static void ConnectToLobbyMembers(object csteamId, MethodInfo getNumMembersMethod, MethodInfo getMemberByIndexMethod, ulong mySteamId)
        {
            // Get member count
            if (!TryInvokeMethod(getNumMembersMethod, new[] { csteamId }, out int memberCount))
            {
                Debug.LogError("[SteamCallbackManager] Failed to get lobby member count");
                return;
            }

            Debug.Log($"[SteamCallbackManager] Connecting to {memberCount} lobby members");

            // Connect to each member except self
            for (int i = 0; i < memberCount; i++)
            {
                if (TryGetMemberSteamId(getMemberByIndexMethod, csteamId, i, out ulong memberSteamId) &&
                    memberSteamId != 0 &&
                    memberSteamId != mySteamId)
                {
                    Debug.Log($"[SteamCallbackManager] Attempting to connect to member: {memberSteamId}");

                    // Accept P2P session with this member
                    AcceptP2PSessionWithPlayer(memberSteamId);

                    // Optionally send a handshake packet
                    SendHandshakePacket(memberSteamId);
                }
            }
        }

        /// <summary>
        /// Notifies the host's game logic that a new player has joined
        /// </summary>
        private static void NotifyHostOfNewPlayer(ulong newPlayerSteamId)
        {
            try
            {
                // Get player name if possible
                string playerName = GetPlayerName(newPlayerSteamId);
                Debug.Log($"[SteamCallbackManager] {playerName} has joined!");

                // Notify your game's UI/logic about the new player
                // Replace this with your actual notification method
                if (SteamHostManager.OnPlayerJoined != null)
                {
                    SteamHostManager.OnPlayerJoined.Invoke(newPlayerSteamId, playerName);
                }

                // You might also want to update a player list UI here
                // UpdatePlayerListUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error notifying host of new player: {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts P2P session with a specific player
        /// </summary>
        private static void AcceptP2PSessionWithPlayer(ulong playerSteamId)
        {
            try
            {
                var networking = ETGSteamP2PNetworking.Instance;
                if (networking == null)
                {
                    Debug.LogWarning("[SteamCallbackManager] ETGSteamP2PNetworking.Instance is null");
                    return;
                }

                // Use reflection to call AcceptP2PSessionWithUser if it exists
                var acceptMethod = networking.GetType().GetMethod("AcceptP2PSessionWithUser") ??
                                networking.GetType().GetMethod("AcceptP2PSession");

                if (acceptMethod != null)
                {
                    var playerCSteamId = SteamReflectionHelper.ConvertToCSteamID(playerSteamId);
                    if (playerCSteamId != null)
                    {
                        acceptMethod.Invoke(networking, new[] { playerCSteamId });
                        Debug.Log($"[SteamCallbackManager] Accepted P2P session with player: {playerSteamId}");
                    }
                }
                else
                {
                    Debug.LogWarning("[SteamCallbackManager] Could not find AcceptP2PSession method");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error accepting P2P session: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a handshake packet to establish connection
        /// </summary>
        private static void SendHandshakePacket(ulong targetSteamId)
        {
            try
            {
                var networking = ETGSteamP2PNetworking.Instance;
                if (networking == null) return;

                // Send a simple handshake packet to establish the connection
                // You'll need to implement this based on your networking setup
                // This is just a placeholder for the concept

                Debug.Log($"[SteamCallbackManager] Sending handshake to: {targetSteamId}");

                // Example: networking.SendHandshake(targetSteamId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error sending handshake: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a player's display name from Steam
        /// </summary>
        private static string GetPlayerName(ulong steamId)
        {
            try
            {
                // Use Steam Friends API to get the player name
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                var friendsType = steamworksAssembly?.GetType("Steamworks.SteamFriends", false);
                var getNameMethod = friendsType?.GetMethod("GetFriendPersonaName");

                if (getNameMethod != null)
                {
                    var playerCSteamId = SteamReflectionHelper.ConvertToCSteamID(steamId);
                    if (playerCSteamId != null)
                    {
                        var name = getNameMethod.Invoke(null, new[] { playerCSteamId }) as string;
                        return !string.IsNullOrEmpty(name) ? name : $"Player_{steamId}";
                    }
                }

                return $"Player_{steamId}";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error getting player name: {ex.Message}");
                return $"Player_{steamId}";
            }
        }

        /// <summary>
        /// Steam callback for LobbyDataUpdate_t - handles lobby membership changes
        /// </summary>
        private static void OnLobbyDataUpdate(object param)
        {
            Debug.Log("[SteamCallbackManager] OnLobbyDataUpdate called!");

            try
            {
                if (!TryGetLobbyId(param, out ulong lobbyId))
                {
                    Debug.LogWarning("[SteamCallbackManager] OnLobbyDataUpdate: Failed to extract lobby ID");
                    return;
                }

                // Check if this is our current lobby
                if (SteamHostManager.CurrentLobbyId != lobbyId)
                {
                    Debug.Log("[SteamCallbackManager] OnLobbyDataUpdate: Not our current lobby, ignoring");
                    return;
                }

                Debug.Log($"[SteamCallbackManager] Lobby data updated for lobby: {lobbyId}");

                // Refresh member list and handle any changes
                RefreshLobbyMemberList(lobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error in OnLobbyDataUpdate: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the lobby member list and handles changes
        /// </summary>
        private static void RefreshLobbyMemberList(ulong lobbyId)
        {
            try
            {
                if (!TryGetMatchmakingMethods(out var getNumMembersMethod, out var getMemberByIndexMethod))
                {
                    return;
                }

                var csteamId = SteamReflectionHelper.ConvertToCSteamID(lobbyId);
                if (csteamId == null) return;

                if (!TryInvokeMethod(getNumMembersMethod, new[] { csteamId }, out int memberCount))
                {
                    return;
                }

                Debug.Log($"[SteamCallbackManager] Lobby now has {memberCount} members");

                // Update your game's member list UI here
                // UpdateLobbyMemberListUI(memberCount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamCallbackManager] Error refreshing lobby member list: {ex.Message}");
            }
        }
        // Reference to the ISteamNetworking implementation
        public static ISteamNetworking SteamNetworking { get; set; }
    }
}
