using System;
using System.Collections.Generic;
using System.Reflection;
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
            // Prevent double initialization
            if (earlyInitialized)
            {
                Debug.Log("[ETGSteamP2P] Steam callbacks already initialized early, skipping...");
                return;
            }
            
            earlyInitialized = true;
            
            try
            {
                Debug.Log("[ETGSteamP2P] 🔄 Initializing Steam callbacks for invite and overlay join support...");
                
                // Use the cached Steamworks assembly from SteamReflectionHelper
                Assembly steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Cannot initialize Steam callbacks - Steamworks assembly not available");
                    return;
                }
                
                // Initialize Steam command line monitoring immediately
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
                
                Debug.Log($"[ETGSteamP2P] 🔍 Callback types found:");
                Debug.Log($"  Callback base: {callbackBaseType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameOverlayActivated_t: {gameOverlayActivatedType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameLobbyJoinRequested_t: {gameLobbyJoinRequestedType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameRichPresenceJoinRequested_t: {gameJoinRequestedType?.FullName ?? "NOT FOUND"}");
                
                // Let's explore what callback-related types are actually available
                Debug.Log("[ETGSteamP2P] 🔍 Exploring available callback types in Steamworks assembly...");
                try
                {
                    var allTypes = steamworksAssembly.GetTypes();
                    var callbackTypes = new System.Collections.Generic.List<string>();
                    
                    foreach (var type in allTypes)
                    {
                        var typeName = type.FullName ?? type.Name;
                        if (typeName.Contains("Callback") || typeName.Contains("_t"))
                        {
                            callbackTypes.Add(typeName);
                        }
                    }
                    
                    Debug.Log($"[ETGSteamP2P] Found {callbackTypes.Count} callback/struct types:");
                    for (int i = 0; i < callbackTypes.Count && i < 20; i++) // Limit to first 20 to avoid spam
                    {
                        Debug.Log($"[ETGSteamP2P]   {i + 1}: {callbackTypes[i]}");
                    }
                    
                    if (callbackTypes.Count > 20)
                    {
                        Debug.Log($"[ETGSteamP2P]   ... and {callbackTypes.Count - 20} more");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not enumerate callback types: {ex.Message}");
                }
                
                // Try to find SteamAPI.RunCallbacks method for processing callbacks
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
                
                // If we can't find the callback base, implement a fallback system
                if (ReferenceEquals(callbackBaseType, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] ⚠️ Steam callback base type not found - implementing fallback polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true; // Mark as registered so we use the fallback
                    return;
                }
                
                // DISABLE complex callback registration due to IL2CPP limitations
                // The reflection-based callback registration causes "Method not found" errors in IL2CPP
                Debug.LogWarning("[ETGSteamP2P] ⚠️ Skipping complex Steam callback registration due to IL2CPP limitations");
                Debug.Log("[ETGSteamP2P] 🔄 Using fallback join detection system instead");
                SteamFallbackDetection.InitializeFallbackJoinDetection();
                joinCallbacksRegistered = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] ❌ Failed to initialize Steam callbacks: {e.Message}");
                Debug.LogError($"[ETGSteamP2P] Stack trace: {e.StackTrace}");
                
                // Fall back to polling system
                SteamFallbackDetection.InitializeFallbackJoinDetection();
                joinCallbacksRegistered = true;
            }
        }
        
        /// <summary>
        /// Try to register a Steam callback with error handling
        /// </summary>
        private static bool TryRegisterCallback(Assembly steamworksAssembly, Type callbackBaseType, Type callbackDataType, string handlerMethodName)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] Registering callback for {callbackDataType.Name}...");
                
                // Try to create a callback for the specified type
                Type genericCallbackType = null;
                
                // Try different callback type patterns
                if (callbackBaseType.IsGenericTypeDefinition)
                {
                    Debug.Log($"[ETGSteamP2P] Using generic type definition: {callbackBaseType.Name}");
                    genericCallbackType = callbackBaseType.MakeGenericType(callbackDataType);
                }
                else
                {
                    // Look for generic callback types
                    var genericCallback = steamworksAssembly.GetType("Steamworks.Callback`1", false);
                    if (!ReferenceEquals(genericCallback, null))
                    {
                        Debug.Log($"[ETGSteamP2P] Found Callback`1 type, making generic with {callbackDataType.Name}");
                        genericCallbackType = genericCallback.MakeGenericType(callbackDataType);
                    }
                    else
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not find Callback`1 type in assembly");
                        return false;
                    }
                }
                
                if (ReferenceEquals(genericCallbackType, null))
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not create generic callback type for {callbackDataType.Name}");
                    return false;
                }
                
                Debug.Log($"[ETGSteamP2P] Created generic callback type: {genericCallbackType.FullName}");
                
                // Try multiple delegate type patterns - different Steamworks.NET versions use different patterns
                Type delegateType = null;
                object delegateInstance = null;
                
                // Pattern 1: Try nested DispatchDelegate
                var nestedDelegateType = steamworksAssembly.GetType("Steamworks.Callback`1+DispatchDelegate", false);
                if (!ReferenceEquals(nestedDelegateType, null))
                {
                    delegateType = nestedDelegateType.MakeGenericType(callbackDataType);
                    Debug.Log($"[ETGSteamP2P] Found nested DispatchDelegate type: {delegateType.FullName}");
                }
                
                // Pattern 2: Try standalone delegate types
                if (ReferenceEquals(delegateType, null))
                {
                    var standaloneDelegate = steamworksAssembly.GetType("Steamworks.DispatchDelegate", false) ??
                                           steamworksAssembly.GetType("Steamworks.CallbackDispatchDelegate", false) ??
                                           steamworksAssembly.GetType("Steamworks.SteamAPICall_t+CallbackDispatchDelegate", false);
                                           
                    if (!ReferenceEquals(standaloneDelegate, null))
                    {
                        if (standaloneDelegate.IsGenericTypeDefinition)
                        {
                            delegateType = standaloneDelegate.MakeGenericType(callbackDataType);
                        }
                        else
                        {
                            delegateType = standaloneDelegate;
                        }
                        Debug.Log($"[ETGSteamP2P] Found standalone delegate type: {delegateType.FullName}");
                    }
                }
                
                // Pattern 3: Try Action<T> delegates (common in newer versions)
                if (ReferenceEquals(delegateType, null))
                {
                    delegateType = typeof(System.Action<>).MakeGenericType(callbackDataType);
                    Debug.Log($"[ETGSteamP2P] Using Action<T> delegate type: {delegateType.FullName}");
                }
                
                // Create the callback handler method
                var handleMethod = typeof(SteamCallbackManager).GetMethod(handlerMethodName, 
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (!ReferenceEquals(handleMethod, null))
                {
                    Debug.Log($"[ETGSteamP2P] Found handler method: {handlerMethodName}");
                    
                    try
                    {
                        delegateInstance = System.Delegate.CreateDelegate(delegateType, handleMethod);
                        Debug.Log($"[ETGSteamP2P] ✅ Created delegate instance");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not create delegate: {ex.Message}");
                        return false;
                    }
                    
                    // Create the callback instance - try multiple constructor patterns
                    var constructors = genericCallbackType.GetConstructors();
                    Debug.Log($"[ETGSteamP2P] Found {constructors.Length} constructors for {genericCallbackType.Name}");
                    
                    object callbackInstance = null;
                    bool constructorFound = false;
                    
                    // Try different constructor patterns in order of likelihood
                    foreach (var constructor in constructors)
                    {
                        var parameters = constructor.GetParameters();
                        Debug.Log($"[ETGSteamP2P] Trying constructor with {parameters.Length} parameters:");
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Debug.Log($"[ETGSteamP2P]   Param {i}: {parameters[i].ParameterType.Name} ({parameters[i].ParameterType.FullName})");
                        }
                        
                        // Try constructor with Action<T> parameter (most common in newer versions)
                        if (parameters.Length == 1)
                        {
                            var paramType = parameters[0].ParameterType;
                            if (paramType.IsGenericType && object.Equals(paramType.GetGenericTypeDefinition(), typeof(System.Action<>)))
                            {
                                Debug.Log($"[ETGSteamP2P] Trying constructor with Action<T> parameter");
                                try
                                {
                                    var actionDelegate = System.Delegate.CreateDelegate(paramType, handleMethod);
                                    callbackInstance = constructor.Invoke(new object[] { actionDelegate });
                                    constructorFound = true;
                                    Debug.Log($"[ETGSteamP2P] ✅ Successfully created callback instance with Action<T> constructor");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[ETGSteamP2P] Action<T> constructor failed: {ex.Message}");
                                }
                            }
                            // Try constructor with compatible delegate parameter
                            else if (paramType.IsAssignableFrom(delegateType) || delegateType.IsAssignableFrom(paramType))
                            {
                                Debug.Log($"[ETGSteamP2P] Trying constructor with compatible delegate parameter");
                                try
                                {
                                    callbackInstance = constructor.Invoke(new object[] { delegateInstance });
                                    constructorFound = true;
                                    Debug.Log($"[ETGSteamP2P] ✅ Successfully created callback instance with delegate constructor");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[ETGSteamP2P] Delegate constructor failed: {ex.Message}");
                                }
                            }
                        }
                        
                        // Try parameterless constructor with post-construction delegate assignment
                        if (!constructorFound && parameters.Length == 0)
                        {
                            Debug.Log($"[ETGSteamP2P] Trying parameterless constructor");
                            try
                            {
                                callbackInstance = constructor.Invoke(new object[0]);
                                
                                // Try to set the delegate after construction using various field/property names
                                var possibleFields = new string[] { "m_Func", "Func", "m_pCallback", "m_Callback", "callback", "Callback" };
                                bool delegateSet = false;
                                
                                foreach (var fieldName in possibleFields)
                                {
                                    var field = genericCallbackType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (!ReferenceEquals(field, null))
                                    {
                                        try
                                        {
                                            field.SetValue(callbackInstance, delegateInstance);
                                            constructorFound = true;
                                            delegateSet = true;
                                            Debug.Log($"[ETGSteamP2P] ✅ Successfully created callback instance with parameterless constructor + field '{fieldName}' assignment");
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogWarning($"[ETGSteamP2P] Failed to set field '{fieldName}': {ex.Message}");
                                        }
                                    }
                                }
                                
                                // Try properties if fields didn't work
                                if (!delegateSet)
                                {
                                    Debug.LogWarning($"[ETGSteamP2P] Could not set delegate via fields, trying alternative approaches...");
                                    
                                    // IL2CPP doesn't support PropertyInfo.SetValue reliably
                                    // Skip property-based assignment and rely on fallback detection
                                    Debug.LogWarning($"[ETGSteamP2P] Property assignment skipped due to IL2CPP limitations");
                                }
                                
                                if (constructorFound) break;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ETGSteamP2P] Parameterless constructor failed: {ex.Message}");
                            }
                        }
                        
                        // Try constructor with multiple parameters (callback + object/params)
                        if (!constructorFound && parameters.Length >= 2)
                        {
                            Debug.Log($"[ETGSteamP2P] Trying constructor with {parameters.Length} parameters");
                            try
                            {
                                var args = new object[parameters.Length];
                                args[0] = delegateInstance;
                                for (int i = 1; i < parameters.Length; i++)
                                {
                                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                                }
                                
                                callbackInstance = constructor.Invoke(args);
                                constructorFound = true;
                                Debug.Log($"[ETGSteamP2P] ✅ Successfully created callback instance with {parameters.Length}-parameter constructor");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ETGSteamP2P] {parameters.Length}-parameter constructor failed: {ex.Message}");
                            }
                        }
                    }
                    
                    if (!constructorFound)
                    {
                        Debug.LogWarning($"[ETGSteamP2P] Could not find suitable constructor for {genericCallbackType.Name}");
                        return false;
                    }
                    
                    if (!ReferenceEquals(callbackInstance, null))
                    {
                        // Store the callback handle based on the type
                        if (callbackDataType.Name.Contains("Lobby"))
                        {
                            lobbyCallbackHandle = callbackInstance;
                            Debug.Log($"[ETGSteamP2P] Stored lobby callback handle");
                        }
                        else if (callbackDataType.Name.Contains("Overlay"))
                        {
                            overlayCallbackHandle = callbackInstance;
                            Debug.Log($"[ETGSteamP2P] Stored overlay callback handle");
                        }
                        else if (callbackDataType.Name.Contains("RichPresence") || callbackDataType.Name.Contains("Join"))
                        {
                            steamCallbackHandle = callbackInstance;
                            Debug.Log($"[ETGSteamP2P] Stored rich presence callback handle");
                        }
                        
                        Debug.Log($"[ETGSteamP2P] Successfully registered {callbackDataType.Name} callback!");
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not find handler method: {handlerMethodName}");
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Exception while registering {callbackDataType.Name} callback: {e.Message}");
                Debug.LogWarning($"[ETGSteamP2P] Stack trace: {e.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Handle Rich Presence join requests (when friends click "Join Game")
        /// </summary>
        private static void HandleRichPresenceJoinRequest(object joinData)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Rich Presence join request received!");
                
                if (ReferenceEquals(joinData, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] Rich Presence join request data is null");
                    return;
                }
                
                // Extract Steam ID from the join request
                var dataType = joinData.GetType();
                var friendIdField = dataType.GetField("m_steamIDFriend") ?? dataType.GetField("steamIDFriend");
                var connectField = dataType.GetField("m_rgchConnect") ?? dataType.GetField("rgchConnect");
                
                string hostSteamId = "unknown";
                string connectString = "";
                
                if (!ReferenceEquals(friendIdField, null))
                {
                    var friendId = friendIdField.GetValue(joinData);
                    hostSteamId = friendId.ToString();
                    Debug.Log($"[ETGSteamP2P] Rich Presence join from friend: {hostSteamId}");
                }
                
                if (!ReferenceEquals(connectField, null))
                {
                    var connect = connectField.GetValue(joinData);
                    connectString = connect?.ToString() ?? "";
                    Debug.Log($"[ETGSteamP2P] Rich Presence connect string: {connectString}");
                }
                
                // Parse host Steam ID from connect string if available
                if (!string.IsNullOrEmpty(connectString) && ulong.TryParse(connectString, out ulong parsedSteamId))
                {
                    hostSteamId = parsedSteamId.ToString();
                    SteamHostManager.SetInviteInfo(parsedSteamId);
                }
                
                // Check if we have subscribers to handle the event immediately
                if (OnJoinRequested is not null && OnJoinRequested.GetInvocationList().Length > 0)
                {
                    // Fire event for session manager to handle the join
                    Debug.Log($"[ETGSteamP2P] Firing OnJoinRequested event for Steam ID: {hostSteamId}");
                    OnJoinRequested?.Invoke(ulong.Parse(hostSteamId));
                }
                else
                {
                    // Queue the request for later processing
                    Debug.Log($"[ETGSteamP2P] No event subscribers yet, queueing join request for Steam ID: {hostSteamId}");
                    pendingJoinRequests.Enqueue(ulong.Parse(hostSteamId));
                }
                
                Debug.Log($"[ETGSteamP2P] Rich Presence join request processed successfully for host: {hostSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error handling Rich Presence join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle overlay state changes
        /// </summary>
        private static void HandleOverlayActivated(object overlayData)
        {
            try
            {
                if (ReferenceEquals(overlayData, null))
                    return;
                
                var dataType = overlayData.GetType();
                var activeField = dataType.GetField("m_bActive") ?? dataType.GetField("bActive");
                
                if (!ReferenceEquals(activeField, null))
                {
                    var isActive = activeField.GetValue(overlayData);
                    bool active = Convert.ToBoolean(isActive);
                    
                    Debug.Log($"[ETGSteamP2P] Steam overlay {(active ? "opened" : "closed")}");
                    OnOverlayActivated?.Invoke(active);
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
        private static void HandleLobbyJoinRequest(object lobbyData)
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Steam lobby join requested! (Overlay 'Join Game' clicked)");
                
                // Extract Steam ID from lobby data
                var friendIdField = lobbyData.GetType().GetField("m_steamIDFriend");
                var lobbyIdField = lobbyData.GetType().GetField("m_steamIDLobby");
                
                string hostSteamId = "unknown";
                
                if (!ReferenceEquals(friendIdField, null))
                {
                    var friendId = friendIdField.GetValue(lobbyData);
                    hostSteamId = friendId.ToString();
                    Debug.Log($"[ETGSteamP2P] Join request from friend: {hostSteamId}");
                }
                
                if (!ReferenceEquals(lobbyIdField, null))
                {
                    var lobbyId = lobbyIdField.GetValue(lobbyData);
                    Debug.Log($"[ETGSteamP2P] Join request for lobby: {lobbyId}");
                }
                
                // Check if we have subscribers to handle the event immediately
                if (OnOverlayJoinRequested is not null && OnOverlayJoinRequested.GetInvocationList().Length > 0)
                {
                    // Fire event for session manager to handle the join
                    OnOverlayJoinRequested?.Invoke(hostSteamId);
                    Debug.Log($"[ETGSteamP2P] Overlay join request fired immediately for host: {hostSteamId}");
                }
                else
                {
                    // Queue the request for later processing
                    Debug.Log($"[ETGSteamP2P] No overlay event subscribers yet, queueing join request for host: {hostSteamId}");
                    pendingOverlayJoinRequests.Enqueue(hostSteamId);
                }
                
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
                // Run normal Steam callbacks if available
                if (!ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod.Invoke(null, null);
                }
                
                // Process any pending join requests from early initialization
                if (pendingJoinRequests.Count > 0 || pendingOverlayJoinRequests.Count > 0)
                {
                    ProcessPendingJoinRequests();
                }
                
                // If we're using fallback detection, do active monitoring
                SteamFallbackDetection.ProcessFallbackDetection();
                
                // Also check for Steam Rich Presence changes that might indicate join requests
                CheckForSteamJoinRequests();
            }
            catch (Exception e)
            {
                // Don't spam the log with callback errors
                if (ReferenceEquals(Time.frameCount % 300, 0)) // Log every 5 seconds at 60fps
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
                if (ReferenceEquals(Time.frameCount % 60, 0)) // Check every second at 60fps
                {
                    // Periodic check for join requests
                }
                
                if (ReferenceEquals(Time.frameCount % 180, 0)) // Check every 3 seconds
                {
                    // Less frequent comprehensive check
                }
            }
            catch (Exception e)
            {
                if (ReferenceEquals(Time.frameCount % 1800, 0)) // Log every 30 seconds
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error checking for Steam join requests: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Get the status of Steam callback initialization for debugging
        /// </summary>
        public static string GetCallbackStatus()
        {
            try
            {
                var status = $"[ETGSteamP2P] Callback Status:\n";
                status += $"  Callbacks Registered: {joinCallbacksRegistered}\n";
                status += $"  Using Fallback Detection: {SteamFallbackDetection.IsUsingFallbackDetection}\n";
                status += $"  RunCallbacks Method: {(!ReferenceEquals(runCallbacksMethod, null) ? "✅" : "❌")}\n";
                status += $"  Lobby Callback Handle: {(!ReferenceEquals(lobbyCallbackHandle, null) ? "✅" : "❌")}\n";
                status += $"  Overlay Callback Handle: {(!ReferenceEquals(overlayCallbackHandle, null) ? "✅" : "❌")}\n";
                status += $"  Steam Callback Handle: {(!ReferenceEquals(steamCallbackHandle, null) ? "✅" : "❌")}\n";
                
                return status;
            }
            catch (Exception e)
            {
                return $"[ETGSteamP2P] Error getting callback status: {e.Message}";
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
        /// Trigger a join requested event (for internal use by fallback detection)
        /// </summary>
        public static void TriggerJoinRequested(ulong steamId)
        {
            try
            {
                OnJoinRequested?.Invoke(steamId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error triggering join requested event: {e.Message}");
            }
        }
        
        public static bool AreCallbacksRegistered => joinCallbacksRegistered;
        
        /// <summary>
        /// Start monitoring Steam command line for early join requests
        /// </summary>
        private static void StartSteamCommandLineMonitoring()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] Starting Steam command line monitoring for early join detection...");
                
                // Check command line arguments immediately
                string[] args = System.Environment.GetCommandLineArgs();
                Debug.Log($"[ETGSteamP2P] Checking {args.Length} command line arguments for Steam join requests...");
                
                for (int i = 0; i < args.Length; i++)
                {
                    // Check for various Steam join patterns
                    if (args[i].StartsWith("+connect") && i + 1 < args.Length)
                    {
                        string target = args[i + 1];
                        if (ulong.TryParse(target, out ulong steamId))
                        {
                            Debug.Log($"[ETGSteamP2P] Found Steam connect command for Steam ID: {steamId}");
                            pendingJoinRequests.Enqueue(steamId);
                        }
                    }
                    else if (args[i].StartsWith("+connect_lobby") && i + 1 < args.Length)
                    {
                        string target = args[i + 1];
                        if (ulong.TryParse(target, out ulong lobbyId))
                        {
                            Debug.Log($"[ETGSteamP2P] Found Steam lobby connect for lobby: {lobbyId}");
                            pendingJoinRequests.Enqueue(lobbyId);
                        }
                    }
                    else if (args[i].Contains("steam://joinlobby/") || args[i].Contains("steam://rungame/"))
                    {
                        Debug.Log($"[ETGSteamP2P] Found Steam URL in command line: {args[i]}");
                        // Try to extract Steam ID from Steam URLs
                        TryExtractSteamIdFromUrl(args[i]);
                    }
                }
                
                if (pendingJoinRequests.Count > 0)
                {
                    Debug.Log($"[ETGSteamP2P] Found {pendingJoinRequests.Count} pending Steam join requests from command line");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error in Steam command line monitoring: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to extract Steam ID from Steam URLs
        /// </summary>
        private static void TryExtractSteamIdFromUrl(string url)
        {
            try
            {
                // Extract numbers from Steam URLs that might contain Steam IDs
                var matches = System.Text.RegularExpressions.Regex.Matches(url, @"\d{17}");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (ulong.TryParse(match.Value, out ulong steamId) && steamId > 76561197960265728)
                    {
                        Debug.Log($"[ETGSteamP2P] Extracted Steam ID from URL: {steamId}");
                        pendingJoinRequests.Enqueue(steamId);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Error extracting Steam ID from URL: {e.Message}");
            }
        }
        
        /// <summary>
        /// Process pending join requests that arrived before the game was ready
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
                    Debug.Log($"[ETGSteamP2P] Processing pending overlay join request for host: {hostSteamId}");
                    OnOverlayJoinRequested?.Invoke(hostSteamId);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error processing pending join requests: {e.Message}");
            }
        }
    }
}
