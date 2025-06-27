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
                Debug.Log("[ETGSteamP2P] Steam callbacks already initialized early, skipping...");
                return;
            }
            
            earlyInitialized = true;
            
            try
            {
                Debug.Log("[ETGSteamP2P] 🔄 Initializing Steam callbacks for invite and overlay join support...");
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
                
                Debug.Log("[ETGSteamP2P] 🔍 Callback types found:");
                Debug.Log("  Callback base: " + ((!ReferenceEquals(callbackBaseType, null) ? callbackBaseType.FullName : null) ?? "NOT FOUND"));
                Debug.Log("  GameOverlayActivated_t: " + ((!ReferenceEquals(gameOverlayActivatedType, null) ? gameOverlayActivatedType.FullName : null) ?? "NOT FOUND"));
                Debug.Log("  GameLobbyJoinRequested_t: " + ((!ReferenceEquals(gameLobbyJoinRequestedType, null) ? gameLobbyJoinRequestedType.FullName : null) ?? "NOT FOUND"));
                Debug.Log("  GameRichPresenceJoinRequested_t: " + ((!ReferenceEquals(gameJoinRequestedType, null) ? gameJoinRequestedType.FullName : null) ?? "NOT FOUND"));
                
                Debug.Log("[ETGSteamP2P] 🔍 Exploring available callback types in Steamworks assembly...");
                try
                {
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
                    Debug.Log($"[ETGSteamP2P] Found {callbackTypes.Count} callback/struct types:");
                    int count = 0;
                    while (count < callbackTypes.Count && count < 20)
                    {
                        Debug.Log($"[ETGSteamP2P]   {count + 1}: {callbackTypes[count]}");
                        count++;
                    }
                    if (callbackTypes.Count > 20)
                    {
                        Debug.Log($"[ETGSteamP2P]   ... and {callbackTypes.Count - 20} more");
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
                    if (!ReferenceEquals(runCallbacksMethod, null))
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Found SteamAPI.RunCallbacks method");
                    }
                    else
                    {
                        Debug.LogWarning("[ETGSteamP2P] ⚠️ SteamAPI.RunCallbacks method not found");
                    }
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] ⚠️ SteamAPI type not found");
                }

                if (ReferenceEquals(callbackBaseType, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] ⚠️ Steam callback base type not found - implementing fallback polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true;
                }
                else
                {
                    Debug.Log("[ETGSteamP2P] 🔄 Attempting to register Steam callbacks for P2P join detection...");
                    bool registered = false;
                    
                    bool richPresenceRegistered = TryRegisterCallback(steamworksAssembly, callbackBaseType, gameJoinRequestedType, "OnGameRichPresenceJoinRequested");
                    if (richPresenceRegistered)
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameRichPresenceJoinRequested_t callback");
                        registered = true;
                    }
                    
                    bool lobbyRegistered = TryRegisterCallback(steamworksAssembly, callbackBaseType, gameLobbyJoinRequestedType, "OnGameLobbyJoinRequested");
                    if (lobbyRegistered)
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameLobbyJoinRequested_t callback");
                        registered = true;
                    }
                    
                    if (registered)
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Steam callbacks registered successfully - join requests should work");
                        joinCallbacksRegistered = true;
                    }
                    else
                    {
                        Debug.LogWarning("[ETGSteamP2P] ⚠️ Failed to register Steam callbacks - falling back to manual detection");
                        SteamFallbackDetection.InitializeFallbackJoinDetection();
                        joinCallbacksRegistered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[ETGSteamP2P] ❌ Failed to initialize Steam callbacks: " + ex.Message);
                Debug.LogError("[ETGSteamP2P] Stack trace: " + ex.StackTrace);
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
                
                Debug.Log("[ETGSteamP2P] Registering callback for " + callbackDataType.Name + "...");
                Type genericCallbackType = null;
                
                if (callbackBaseType.IsGenericTypeDefinition)
                {
                    Debug.Log("[ETGSteamP2P] Using generic type definition: " + callbackBaseType.Name);
                    genericCallbackType = callbackBaseType.MakeGenericType(new Type[] { callbackDataType });
                }
                else
                {
                    Type callbackGenericType = steamworksAssembly.GetType("Steamworks.Callback`1", false);
                    if (ReferenceEquals(callbackGenericType, null))
                    {
                        Debug.LogWarning("[ETGSteamP2P] Could not find Callback`1 type in assembly");
                        return false;
                    }
                    Debug.Log("[ETGSteamP2P] Found Callback`1 type, making generic with " + callbackDataType.Name);
                    genericCallbackType = callbackGenericType.MakeGenericType(new Type[] { callbackDataType });
                }
                
                if (ReferenceEquals(genericCallbackType, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Could not create generic callback type for " + callbackDataType.Name);
                    return false;
                }
                
                Debug.Log("[ETGSteamP2P] Created generic callback type: " + genericCallbackType.FullName);
                
                // Find the appropriate delegate type
                Type delegateType = null;
                object delegateInstance = null;
                
                // Try nested DispatchDelegate first
                Type nestedDispatchDelegate = steamworksAssembly.GetType("Steamworks.Callback`1+DispatchDelegate", false);
                if (!ReferenceEquals(nestedDispatchDelegate, null))
                {
                    delegateType = nestedDispatchDelegate.MakeGenericType(new Type[] { callbackDataType });
                    Debug.Log("[ETGSteamP2P] Found nested DispatchDelegate type: " + delegateType.FullName);
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
                        Debug.Log("[ETGSteamP2P] Found standalone delegate type: " + delegateType.FullName);
                    }
                }
                
                // Fallback to Action<T>
                if (ReferenceEquals(delegateType, null))
                {
                    delegateType = typeof(Action<>).MakeGenericType(new Type[] { callbackDataType });
                    Debug.Log("[ETGSteamP2P] Using Action<T> delegate type: " + delegateType.FullName);
                }
                
                // Find our handler method
                MethodInfo handlerMethod = typeof(SteamCallbackManager).GetMethod(handlerMethodName, BindingFlags.NonPublic | BindingFlags.Static);
                if (ReferenceEquals(handlerMethod, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Could not find handler method: " + handlerMethodName);
                    return false;
                }
                
                Debug.Log("[ETGSteamP2P] Found handler method: " + handlerMethodName);
                
                try
                {
                    delegateInstance = Delegate.CreateDelegate(delegateType, handlerMethod);
                    Debug.Log("[ETGSteamP2P] ✅ Created delegate instance");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ETGSteamP2P] Could not create delegate: " + ex.Message);
                    return false;
                }
                
                // Try different constructor approaches
                ConstructorInfo[] constructors = genericCallbackType.GetConstructors();
                Debug.Log($"[ETGSteamP2P] Found {constructors.Length} constructors for {genericCallbackType.Name}");
                
                object callbackInstance = null;
                bool success = false;
                
                foreach (ConstructorInfo constructor in constructors)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    Debug.Log($"[ETGSteamP2P] Trying constructor with {parameters.Length} parameters:");
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Debug.Log($"[ETGSteamP2P]   Param {i}: {parameters[i].ParameterType.Name} ({parameters[i].ParameterType.FullName})");
                    }
                    
                    // Try single parameter constructor
                    if (parameters.Length == 1)
                    {
                        Type parameterType = parameters[0].ParameterType;
                        
                        // Try Action<T> parameter
                        if (parameterType.IsGenericType && ReferenceEquals(parameterType.GetGenericTypeDefinition(), typeof(Action<>)))
                        {
                            Debug.Log("[ETGSteamP2P] Trying constructor with Action<T> parameter");
                            try
                            {
                                Delegate actionDelegate = Delegate.CreateDelegate(parameterType, handlerMethod);
                                callbackInstance = constructor.Invoke(new object[] { actionDelegate });
                                success = true;
                                Debug.Log("[ETGSteamP2P] ✅ Successfully created callback instance with Action<T> constructor");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[ETGSteamP2P] Action<T> constructor failed: " + ex.Message);
                            }
                        }
                        // Try compatible delegate parameter
                        else if (parameterType.IsAssignableFrom(delegateType) || delegateType.IsAssignableFrom(parameterType))
                        {
                            Debug.Log("[ETGSteamP2P] Trying constructor with compatible delegate parameter");
                            try
                            {
                                callbackInstance = constructor.Invoke(new object[] { delegateInstance });
                                success = true;
                                Debug.Log("[ETGSteamP2P] ✅ Successfully created callback instance with delegate constructor");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[ETGSteamP2P] Delegate constructor failed: " + ex.Message);
                            }
                        }
                    }
                    // Try parameterless constructor with field assignment
                    else if (!success && parameters.Length == 0)
                    {
                        Debug.Log("[ETGSteamP2P] Trying parameterless constructor");
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
                                        Debug.Log("[ETGSteamP2P] ✅ Successfully created callback instance with parameterless constructor + field '" + fieldName + "' assignment");
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning("[ETGSteamP2P] Failed to set field '" + fieldName + "': " + ex.Message);
                                    }
                                }
                            }
                            
                            if (!fieldSet)
                            {
                                Debug.LogWarning("[ETGSteamP2P] Could not set delegate via fields, trying alternative approaches...");
                            }
                            
                            if (success)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[ETGSteamP2P] Parameterless constructor failed: " + ex.Message);
                        }
                    }
                    // Try multi-parameter constructor
                    else if (!success && parameters.Length >= 2)
                    {
                        Debug.Log($"[ETGSteamP2P] Trying constructor with {parameters.Length} parameters");
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
                            Debug.Log($"[ETGSteamP2P] ✅ Successfully created callback instance with {parameters.Length}-parameter constructor");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ETGSteamP2P] {parameters.Length}-parameter constructor failed: {ex.Message}");
                        }
                    }
                }
                
                if (!success)
                {
                    Debug.LogWarning("[ETGSteamP2P] Could not find suitable constructor for " + genericCallbackType.Name);
                    return false;
                }
                
                if (!ReferenceEquals(callbackInstance, null))
                {
                    // Store callback instance to prevent GC
                    if (callbackDataType.Name.Contains("Lobby"))
                    {
                        lobbyCallbackHandle = callbackInstance;
                        Debug.Log("[ETGSteamP2P] Stored lobby callback handle");
                    }
                    else if (callbackDataType.Name.Contains("Overlay"))
                    {
                        overlayCallbackHandle = callbackInstance;
                        Debug.Log("[ETGSteamP2P] Stored overlay callback handle");
                    }
                    else if (callbackDataType.Name.Contains("RichPresence") || callbackDataType.Name.Contains("Join"))
                    {
                        steamCallbackHandle = callbackInstance;
                        Debug.Log("[ETGSteamP2P] Stored rich presence callback handle");
                    }
                    
                    Debug.Log("[ETGSteamP2P] Successfully registered " + callbackDataType.Name + " callback!");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ETGSteamP2P] Exception while registering " + callbackDataType.Name + " callback: " + ex.Message);
                Debug.LogWarning("[ETGSteamP2P] Stack trace: " + ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Steam callback handler for GameRichPresenceJoinRequested_t
        /// </summary>
        private static void OnGameRichPresenceJoinRequested(object param)
        {
            try
            {
                Debug.Log("[SteamCallbackManager] GameRichPresenceJoinRequested callback triggered!");
                
                if (ReferenceEquals(param, null))
                {
                    Debug.LogWarning("[SteamCallbackManager] Rich Presence join request parameter is null");
                    return;
                }
                
                Type paramType = null;
                try
                {
                    paramType = param.GetType();
                    Debug.Log("[SteamCallbackManager] Rich Presence join request parameter type: " + paramType.FullName);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[SteamCallbackManager] Error getting parameter type: " + ex.Message);
                    return;
                }
                
                if (ReferenceEquals(paramType, null))
                {
                    Debug.LogWarning("[SteamCallbackManager] Parameter type is null");
                    return;
                }
                
                FieldInfo steamIdField = null;
                try
                {
                    FieldInfo[] fields = paramType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    StringBuilder fieldList = new StringBuilder();
                    foreach (FieldInfo field in fields)
                    {
                        if (fieldList.Length > 0)
                        {
                            fieldList.Append(", ");
                        }
                        fieldList.Append(field.Name + "(" + field.FieldType.Name + ")");
                    }
                    Debug.Log($"[SteamCallbackManager] Available fields in {paramType.Name}: {fieldList}");
                    
                    string[] steamIdFieldNames = {
                        "m_steamIDFriend", "steamIDFriend", "m_steamID", "steamID",
                        "m_ulSteamIDFriend", "ulSteamIDFriend"
                    };
                    
                    foreach (string fieldName in steamIdFieldNames)
                    {
                        steamIdField = paramType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (!ReferenceEquals(steamIdField, null))
                        {
                            Debug.Log($"[SteamCallbackManager] Found Steam ID field: {fieldName} (Type: {steamIdField.FieldType.Name})");
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
                        Debug.Log($"[SteamCallbackManager] Steam ID value: {steamIdValue?.ToString() ?? "null"} (Type: {steamIdValue?.GetType().FullName ?? "null"})");
                        
                        ulong extractedSteamId = 0;
                        
                        if (!ReferenceEquals(steamIdValue, null))
                        {
                            // Try direct cast to ulong
                            if (steamIdValue is ulong)
                            {
                                extractedSteamId = (ulong)steamIdValue;
                                Debug.Log($"[SteamCallbackManager] Extracted as ulong: {extractedSteamId}");
                            }
                            // Try cast from long
                            else if (steamIdValue is long)
                            {
                                extractedSteamId = (ulong)(long)steamIdValue;
                                Debug.Log($"[SteamCallbackManager] Extracted as long->ulong: {extractedSteamId}");
                            }
                            // Try cast from uint
                            else if (steamIdValue is uint)
                            {
                                extractedSteamId = (ulong)(uint)steamIdValue;
                                Debug.Log($"[SteamCallbackManager] Extracted as uint->ulong: {extractedSteamId}");
                            }
                            // Try cast from int
                            else if (steamIdValue is int)
                            {
                                extractedSteamId = (ulong)(long)(int)steamIdValue;
                                Debug.Log($"[SteamCallbackManager] Extracted as int->ulong: {extractedSteamId}");
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
                                        Debug.Log($"[SteamCallbackManager] Found internal field {internalFieldName}: {internalValue}");
                                        
                                        if (internalValue is ulong)
                                        {
                                            extractedSteamId = (ulong)internalValue;
                                            Debug.Log($"[SteamCallbackManager] Extracted from internal field: {extractedSteamId}");
                                            break;
                                        }
                                        else if (internalValue is long)
                                        {
                                            extractedSteamId = (ulong)(long)internalValue;
                                            Debug.Log($"[SteamCallbackManager] Extracted from internal field as long->ulong: {extractedSteamId}");
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
                                        Debug.Log($"[SteamCallbackManager] Extracted by parsing ToString(): {extractedSteamId}");
                                    }
                                }
                            }
                        }
                        
                        if (extractedSteamId != 0 && extractedSteamId > 76561197960265728UL)
                        {
                            Debug.Log($"[SteamCallbackManager] ✅ Rich Presence join request from Steam ID: {extractedSteamId}");
                            
                            if (!pendingJoinRequests.Contains(extractedSteamId))
                            {
                                pendingJoinRequests.Enqueue(extractedSteamId);
                                Debug.Log($"[SteamCallbackManager] Added {extractedSteamId} to pending join requests");
                            }
                            
                            ETGSteamP2PNetworking.TriggerOverlayJoinEvent(extractedSteamId.ToString());
                        }
                        else
                        {
                            Debug.LogWarning($"[SteamCallbackManager] Extracted Steam ID {extractedSteamId} is invalid or zero");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[SteamCallbackManager] Error extracting Steam ID from Rich Presence: " + ex.Message);
                    }
                }
                else
                {
                    Debug.LogWarning("[SteamCallbackManager] Could not find Steam ID field in Rich Presence join request");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error handling Rich Presence join request: " + ex.Message);
                Debug.LogError("[SteamCallbackManager] Stack trace: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// Steam callback handler for GameLobbyJoinRequested_t
        /// </summary>
        private static void OnGameLobbyJoinRequested(object param)
        {
            try
            {
                Debug.Log("[SteamCallbackManager] GameLobbyJoinRequested callback triggered!");
                
                ulong steamId = TryDetectSteamIdFromSteamAPI();
                if (steamId != 0)
                {
                    Debug.Log($"[SteamCallbackManager] ✅ Detected Steam ID from Steam API: {steamId}");
                    if (!pendingJoinRequests.Contains(steamId))
                    {
                        pendingJoinRequests.Enqueue(steamId);
                        Debug.Log($"[SteamCallbackManager] Added {steamId} to pending join requests");
                    }
                    ETGSteamP2PNetworking.TriggerOverlayJoinEvent(steamId.ToString());
                }
                else
                {
                    steamId = TryGetInviteHostSteamId();
                    if (steamId != 0)
                    {
                        Debug.Log($"[SteamCallbackManager] ✅ Detected invite host Steam ID: {steamId}");
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(steamId.ToString());
                    }
                    else if (pendingJoinRequests.Count > 0)
                    {
                        ulong queuedSteamId = pendingJoinRequests.Dequeue();
                        Debug.Log($"[SteamCallbackManager] ✅ Using queued Steam ID from pending requests: {queuedSteamId}");
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(queuedSteamId.ToString());
                    }
                    else
                    {
                        Debug.Log("[SteamCallbackManager] ✅ Lobby join request detected - triggering generic overlay join event");
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent("0");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SteamCallbackManager] Error handling Lobby join request: " + ex.Message);
                Debug.LogError("[SteamCallbackManager] Stack trace: " + ex.StackTrace);
                try
                {
                    Debug.Log("[SteamCallbackManager] ✅ Exception fallback - triggering generic overlay join event");
                    ETGSteamP2PNetworking.TriggerOverlayJoinEvent("0");
                }
                catch (Exception ex2)
                {
                    Debug.LogError("[SteamCallbackManager] Even fallback failed: " + ex2.Message);
                }
            }
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
                            pendingJoinRequests.Enqueue(lobbyId);
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
                Debug.Log("[SteamCallbackManager] Attempting to detect invite host Steam ID...");
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                Debug.Log($"[SteamCallbackManager] Checking {commandLineArgs.Length} command line arguments for invite info...");
                
                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    Debug.Log($"[SteamCallbackManager] Arg {i}: {commandLineArgs[i]}");
                    
                    if (commandLineArgs[i].Contains("steam://joinlobby/"))
                    {
                        Debug.Log("[SteamCallbackManager] Found Steam lobby invite URL: " + commandLineArgs[i]);
                        string[] parts = commandLineArgs[i].Split('/');
                        if (parts.Length >= 5)
                        {
                            string steamIdStr = parts[4];
                            if (ulong.TryParse(steamIdStr, out ulong steamId) && steamId > 76561197960265728UL)
                            {
                                Debug.Log($"[SteamCallbackManager] ✅ Extracted inviter Steam ID from URL: {steamId}");
                                return steamId;
                            }
                        }
                    }
                    else if (commandLineArgs[i].StartsWith("+connect") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        Debug.Log("[SteamCallbackManager] Found connect command with target: " + target);
                        if (ulong.TryParse(target, out ulong steamId) && steamId > 76561197960265728UL)
                        {
                            Debug.Log($"[SteamCallbackManager] ✅ Extracted Steam ID from connect command: {steamId}");
                            return steamId;
                        }
                    }
                    else if (commandLineArgs[i].StartsWith("+connect_lobby") && i + 1 < commandLineArgs.Length)
                    {
                        string target = commandLineArgs[i + 1];
                        Debug.Log("[SteamCallbackManager] Found lobby connect command with target: " + target);
                        if (ulong.TryParse(target, out ulong lobbyId) && lobbyId > 76561197960265728UL)
                        {
                            Debug.Log($"[SteamCallbackManager] ✅ Extracted lobby ID from connect command: {lobbyId}");
                            return lobbyId;
                        }
                    }
                }
                
                Debug.LogWarning("[SteamCallbackManager] Could not detect invite host Steam ID from any source");
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
                Debug.Log("[SteamCallbackManager] Attempting to detect Steam ID from Steam API...");
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[SteamCallbackManager] No Steamworks assembly found");
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
                            Debug.Log($"[SteamCallbackManager] Found {friendCount} Steam friends");
                            
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
                                                    Debug.Log($"[SteamCallbackManager] Using first friend as potential inviter: {steamId}");
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
