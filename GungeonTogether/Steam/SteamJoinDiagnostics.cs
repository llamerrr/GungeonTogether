using System;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Comprehensive diagnostics for Steam join functionality
    /// </summary>
    public static class SteamJoinDiagnostics
    {
        private static bool diagnosticsEnabled = true;
        private static float lastDiagnosticTime = 0f;
        
        /// <summary>
        /// Run comprehensive diagnostics on Steam join functionality
        /// </summary>
        public static void RunJoinDiagnostics()
        {
            if (!diagnosticsEnabled)
                return;
                
            try
            {
                // Only run diagnostics every 10 seconds to avoid spam
                if (Time.time - lastDiagnosticTime < 10f)
                    return;
                    
                lastDiagnosticTime = Time.time;
                
                Debug.Log("=== STEAM JOIN DIAGNOSTICS ===");
                Debug.Log($"[Diagnostics] Frame count: {Time.frameCount}, Time: {Time.time:F1}s");
                
                // 1. Check if Steam is available
                CheckSteamAvailability();
                
                // 2. Check Steam Rich Presence
                CheckRichPresence();
                
                // 3. Check Steam Callbacks
                CheckCallbackStatus();
                
                // 4. Check P2P networking
                CheckP2PNetworking();
                
                // 5. Check command line arguments
                CheckCommandLineArgs();
                
                Debug.Log("=== END STEAM DIAGNOSTICS ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamJoinDiagnostics] Error running diagnostics: {ex.Message}");
            }
        }
        
        private static void CheckSteamAvailability()
        {
            try
            {
                var steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                Debug.Log($"[Diagnostics] Steamworks Assembly: {!ReferenceEquals(steamworksAssembly, null)}");
                
                if (!ReferenceEquals(steamworksAssembly, null))
                {
                    var steamApiType = steamworksAssembly.GetType("Steamworks.SteamAPI", false);
                    var steamUserType = steamworksAssembly.GetType("Steamworks.SteamUser", false);
                    
                    Debug.Log($"[Diagnostics] SteamAPI Type: {!ReferenceEquals(steamApiType, null)}");
                    Debug.Log($"[Diagnostics] SteamUser Type: {!ReferenceEquals(steamUserType, null)}");
                    
                    // Check if Steam is initialized
                    if (!ReferenceEquals(steamApiType, null))
                    {
                        var isInitMethod = steamApiType.GetMethod("IsSteamRunning", BindingFlags.Public | BindingFlags.Static);
                        if (!ReferenceEquals(isInitMethod, null))
                        {
                            bool isRunning = (bool)isInitMethod.Invoke(null, null);
                            Debug.Log($"[Diagnostics] Steam is running: {isRunning}");
                        }
                    }
                    
                    // Get current user Steam ID
                    if (!ReferenceEquals(steamUserType, null))
                    {
                        var getSteamIdMethod = steamUserType.GetMethod("GetSteamID", BindingFlags.Public | BindingFlags.Static);
                        if (!ReferenceEquals(getSteamIdMethod, null))
                        {
                            var steamId = getSteamIdMethod.Invoke(null, null);
                            Debug.Log($"[Diagnostics] Current Steam ID: {steamId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Diagnostics] Steam availability check failed: {ex.Message}");
            }
        }
        
        private static void CheckRichPresence()
        {
            try
            {
                Debug.Log("[Diagnostics] Rich Presence check not implemented yet");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Diagnostics] Rich presence check failed: {ex.Message}");
            }
        }
        
        private static void CheckCallbackStatus()
        {
            try
            {
                bool registered = SteamCallbackManager.AreCallbacksRegistered;
                string status = SteamCallbackManager.GetCallbackStatus();
                
                Debug.Log($"[Diagnostics] Callbacks registered: {registered}");
                Debug.Log($"[Diagnostics] Callback status: {status}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Diagnostics] Callback status check failed: {ex.Message}");
            }
        }
        
        private static void CheckP2PNetworking()
        {
            try
            {
                var instance = ETGSteamP2PNetworking.Instance;
                Debug.Log($"[Diagnostics] P2P Instance available: {!ReferenceEquals(instance, null)}");
                
                if (!ReferenceEquals(instance, null))
                {
                    Debug.Log($"[Diagnostics] P2P Is available: {instance.IsAvailable()}");
                    Debug.Log($"[Diagnostics] P2P Is currently hosting: {ETGSteamP2PNetworking.IsCurrentlyHosting}");
                    Debug.Log($"[Diagnostics] P2P Callbacks registered: {ETGSteamP2PNetworking.AreCallbacksRegistered}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Diagnostics] P2P networking check failed: {ex.Message}");
            }
        }
        
        private static void CheckCommandLineArgs()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                Debug.Log($"[Diagnostics] Command line args count: {args.Length}");
                
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Contains("steam") || args[i].Contains("connect") || args[i].Contains("join"))
                    {
                        Debug.Log($"[Diagnostics] Relevant arg {i}: {args[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Diagnostics] Command line check failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Enable or disable diagnostics
        /// </summary>
        public static void SetDiagnosticsEnabled(bool enabled)
        {
            diagnosticsEnabled = enabled;
            Debug.Log($"[SteamJoinDiagnostics] Diagnostics {(enabled ? "enabled" : "disabled")}");
        }
    }
}
