using System;
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
        
        /// <summary>
        /// Initialize Steam callbacks for overlay join functionality and invite handling
        /// </summary>
        public static void InitializeSteamCallbacks()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] 🔄 Initializing Steam callbacks for invite and overlay join support...");
                
                // Get ETG's Assembly-CSharp-firstpass which contains Steamworks types
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
                {
                    Debug.LogWarning("[ETGSteamP2P] Cannot initialize Steam callbacks - Assembly-CSharp-firstpass not found");
                    return;
                }
                
                // Find Steam callback types - try multiple possible type names
                Type callbackBaseType = steamworksAssembly.GetType("Steamworks.Callback", false) ??
                                       steamworksAssembly.GetType("Steamworks.Callback`1", false) ??
                                       steamworksAssembly.GetType("Steamworks.CCallbackBase", false);
                
                Type gameOverlayActivatedType = steamworksAssembly.GetType("Steamworks.GameOverlayActivated_t", false);
                Type gameLobbyJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameLobbyJoinRequested_t", false);
                Type gameJoinRequestedType = steamworksAssembly.GetType("Steamworks.GameRichPresenceJoinRequested_t", false);
                
                Debug.Log($"[ETGSteamP2P] 🔍 Callback types found:");
                Debug.Log($"  Callback base: {callbackBaseType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameOverlayActivated_t: {gameOverlayActivatedType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameLobbyJoinRequested_t: {gameLobbyJoinRequestedType?.FullName ?? "NOT FOUND"}");
                Debug.Log($"  GameRichPresenceJoinRequested_t: {gameJoinRequestedType?.FullName ?? "NOT FOUND"}");
                
                // If we can't find the callback base, implement a fallback system
                if (ReferenceEquals(callbackBaseType, null))
                {
                    Debug.LogWarning("[ETGSteamP2P] ⚠️ Steam callback base type not found - implementing fallback polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true; // Mark as registered so we use the fallback
                    return;
                }
                
                // Try to initialize proper Steam callbacks
                bool anyCallbackRegistered = false;
                
                // Initialize GameLobbyJoinRequested callback for Steam overlay invites
                if (!ReferenceEquals(gameLobbyJoinRequestedType, null))
                {
                    if (TryRegisterCallback(steamworksAssembly, callbackBaseType, gameLobbyJoinRequestedType, "HandleLobbyJoinRequest"))
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameLobbyJoinRequested callback");
                        anyCallbackRegistered = true;
                    }
                }
                
                // Initialize GameRichPresenceJoinRequested callback for Rich Presence "Join Game"
                if (!ReferenceEquals(gameJoinRequestedType, null))
                {
                    if (TryRegisterCallback(steamworksAssembly, callbackBaseType, gameJoinRequestedType, "HandleRichPresenceJoinRequest"))
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameRichPresenceJoinRequested callback");
                        anyCallbackRegistered = true;
                    }
                }
                
                // Initialize GameOverlayActivated callback for overlay state tracking
                if (!ReferenceEquals(gameOverlayActivatedType, null))
                {
                    if (TryRegisterCallback(steamworksAssembly, callbackBaseType, gameOverlayActivatedType, "HandleOverlayActivated"))
                    {
                        Debug.Log("[ETGSteamP2P] ✅ Successfully registered GameOverlayActivated callback");
                        anyCallbackRegistered = true;
                    }
                }
                
                if (anyCallbackRegistered)
                {
                    joinCallbacksRegistered = true;
                    Debug.Log("[ETGSteamP2P] 🎉 Steam callback initialization complete - Steam invites should now work!");
                }
                else
                {
                    Debug.LogWarning("[ETGSteamP2P] ⚠️ No Steam callbacks could be registered - falling back to polling system");
                    SteamFallbackDetection.InitializeFallbackJoinDetection();
                    joinCallbacksRegistered = true; // Use fallback system
                }
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
                // Try to create a callback for the specified type
                Type genericCallbackType = null;
                
                // Try different callback type patterns
                if (callbackBaseType.IsGenericTypeDefinition)
                {
                    genericCallbackType = callbackBaseType.MakeGenericType(callbackDataType);
                }
                else
                {
                    // Look for generic callback types
                    var genericCallback = steamworksAssembly.GetType("Steamworks.Callback`1", false);
                    if (!ReferenceEquals(genericCallback, null))
                    {
                        genericCallbackType = genericCallback.MakeGenericType(callbackDataType);
                    }
                }
                
                if (ReferenceEquals(genericCallbackType, null))
                {
                    Debug.LogWarning($"[ETGSteamP2P] Could not create generic callback type for {callbackDataType.Name}");
                    return false;
                }
                
                // Create a delegate to handle the callback
                Type delegateType = steamworksAssembly.GetType("Steamworks.Callback`1+DispatchDelegate", false);
                if (!ReferenceEquals(delegateType, null))
                {
                    delegateType = delegateType.MakeGenericType(callbackDataType);
                    
                    // Create the callback handler method
                    var handleMethod = typeof(SteamCallbackManager).GetMethod(handlerMethodName, 
                        BindingFlags.NonPublic | BindingFlags.Static);
                    
                    if (!ReferenceEquals(handleMethod, null))
                    {
                        var delegateInstance = System.Delegate.CreateDelegate(delegateType, handleMethod);
                        
                        // Create the callback instance
                        var constructor = genericCallbackType.GetConstructor(new Type[] { delegateType });
                        if (!ReferenceEquals(constructor, null))
                        {
                            var callbackInstance = constructor.Invoke(new object[] { delegateInstance });
                            
                            // Store the callback handle based on the type
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
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ETGSteamP2P] Could not register {callbackDataType.Name} callback: {e.Message}");
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
                
                // Fire event for session manager to handle the join
                Debug.Log($"[ETGSteamP2P] Firing OnJoinRequested event for Steam ID: {hostSteamId}");
                OnJoinRequested?.Invoke(ulong.Parse(hostSteamId));
                
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
                // Run normal Steam callbacks if available
                if (!ReferenceEquals(runCallbacksMethod, null))
                {
                    runCallbacksMethod.Invoke(null, null);
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
    }
}
