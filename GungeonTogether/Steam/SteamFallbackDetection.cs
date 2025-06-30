using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Handles fallback join detection when Steam callbacks don't work properly
    /// </summary>
    public static class SteamFallbackDetection
    {
        // Flag to track if we're using fallback join detection
        private static bool usingFallbackJoinDetection = false;
        private static float lastFallbackCheck = 0f;
        
        // Enhanced P2P connection management
        private static HashSet<ulong> acceptedSessions = new HashSet<ulong>();
        private static Dictionary<ulong, float> pendingJoinRequests = new Dictionary<ulong, float>();
        
        /// <summary>
        /// Initialize fallback join detection when Steam callbacks don't work
        /// </summary>
        public static void InitializeFallbackJoinDetection()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] 🔄 Initializing fallback join detection system...");
                
                // Since Steam callbacks aren't working, we'll implement active monitoring
                // This will be called in ProcessFallbackDetection() every frame
                Debug.Log("[ETGSteamP2P] ✅ Fallback join detection initialized - will actively monitor for Steam join requests");
                
                // Set up the fallback monitoring flag
                usingFallbackJoinDetection = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error initializing fallback join detection: {e.Message}");
            }
        }
        
        /// <summary>
        /// Process fallback detection if we're using it
        /// </summary>
        public static void ProcessFallbackDetection()
        {
            try
            {
                // If we're using fallback detection, do active monitoring
                if (usingFallbackJoinDetection)
                {
                    // Check for Steam join requests every frame
                    CheckCommandLineForJoinRequests();
                    
                    // Check for P2P connection requests more frequently
                    if (Time.time - lastFallbackCheck > 0.1f) // Every 100ms
                    {
                        MonitorSteamOverlayState();
                        CheckEnvironmentVariablesForJoinRequests();
                        lastFallbackCheck = Time.time;
                    }
                }
            }
            catch (Exception e)
            {
                // Don't spam the log with fallback errors
                if (ReferenceEquals(Time.frameCount % 600, 0)) // Log every 10 seconds at 60fps
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error in fallback detection: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Perform active fallback monitoring for Steam join requests
        /// </summary>
        private static void PerformFallbackJoinDetection()
        {
            try
            {
                // Method 1: Monitor command line arguments for Steam join commands
                CheckCommandLineForJoinRequests();
                
                // Method 3: Monitor for Rich Presence changes (if we can access Steam Friends API)
                CheckRichPresenceForJoinRequests();
                
                // Method 4: Check if Steam is trying to establish new P2P connections
                CheckForNewP2PConnections();
                
                // Method 5: Monitor for Steam overlay state changes
                MonitorSteamOverlayState();
            }
            catch (Exception e)
            {
                // Silently handle errors in fallback detection
                if (ReferenceEquals(Time.frameCount % 1800, 0)) // Log every 30 seconds
                {
                    Debug.LogWarning($"[ETGSteamP2P] Error in fallback join detection: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Check command line arguments for Steam join requests
        /// </summary>
        private static void CheckCommandLineForJoinRequests()
        {
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    
                    // Check for Steam overlay join patterns
                    if (arg.StartsWith("+connect_lobby") || arg.StartsWith("+join_game") || 
                        arg.StartsWith("steam://connect/") || arg.StartsWith("steam://joinlobby/") ||
                        arg.StartsWith("steam://rungameid/") || arg.Contains("connect"))
                    {
                        Debug.Log($"[ETGSteamP2P] 🎮 FALLBACK: Detected Steam join command: {arg}");
                        
                        // Try to extract Steam ID from the argument
                        ExtractAndProcessSteamIdFromArgument(arg);
                    }
                    else if (arg.StartsWith("+connect") && i + 1 < args.Length)
                    {
                        // Check if next argument is a Steam ID
                        if (ulong.TryParse(args[i + 1], out ulong steamId) && steamId > 76561197960265728)
                        {
                            Debug.Log($"[ETGSteamP2P] 🎮 FALLBACK: Detected +connect command with Steam ID: {steamId}");
                            ProcessFallbackJoinRequest(steamId);
                        }
                    }
                    else if (arg.Contains("76561") && ulong.TryParse(arg, out ulong directSteamId))
                    {
                        // Direct Steam ID in arguments
                        Debug.Log($"[ETGSteamP2P] 🎮 FALLBACK: Found direct Steam ID in args: {directSteamId}");
                        ProcessFallbackJoinRequest(directSteamId);
                    }
                }
                
                // Also check environment variables that Steam might set
                CheckEnvironmentVariablesForJoinRequests();
            }
            catch (Exception)
            {
                // Ignore errors in command line parsing
            }
        }
        
        /// <summary>
        /// Check environment variables for Steam join indicators
        /// </summary>
        private static void CheckEnvironmentVariablesForJoinRequests()
        {
            try
            {
                // Check common Steam environment variables
                var steamConnectVar = System.Environment.GetEnvironmentVariable("STEAM_CONNECT");
                var steamJoinVar = System.Environment.GetEnvironmentVariable("STEAM_JOIN");
                
                if (!string.IsNullOrEmpty(steamConnectVar))
                {
                    Debug.Log($"[ETGSteamP2P] 🔍 FALLBACK: Found STEAM_CONNECT env var: {steamConnectVar}");
                    ExtractAndProcessSteamIdFromArgument(steamConnectVar);
                }
                
                if (!string.IsNullOrEmpty(steamJoinVar))
                {
                    Debug.Log($"[ETGSteamP2P] 🔍 FALLBACK: Found STEAM_JOIN env var: {steamJoinVar}");
                    ExtractAndProcessSteamIdFromArgument(steamJoinVar);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Check for new P2P connections being established
        /// </summary>
        private static void CheckForNewP2PConnections()
        {
            try
            {
                // Monitor for new P2P connections that might be from overlay joins
                // This is a lightweight check - we don't actually send packets
                // but we monitor for Steam's internal state changes
                // Implementation would depend on Steam API access
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Monitor Steam overlay state for join indicators
        /// </summary>
        private static void MonitorSteamOverlayState()
        {
            try
            {
                // Check if the Steam overlay is active and if that might signal a join
                // This is a passive monitor - we don't interfere with overlay operations
                // We could potentially detect overlay activity patterns that suggest joins
                // but this requires careful implementation to avoid false positives
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Check Rich Presence for join request indicators
        /// </summary>
        private static void CheckRichPresenceForJoinRequests()
        {
            try
            {
                // Method 1: Check if we can detect Rich Presence changes that indicate joins
                // Monitor our friends list for anyone trying to connect to us
                CheckFriendsForJoinAttempts();
                
                // Method 2: Check if Steam has updated our own Rich Presence in a way that suggests a join
                CheckOwnRichPresenceForJoinIndicators();
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Check friends list for anyone attempting to join
        /// </summary>
        private static void CheckFriendsForJoinAttempts()
        {
            try
            {
                // For now, just prepare for any potential P2P connections
                // This is a lightweight approach that doesn't require friend enumeration
                // We'll rely more on the command line and environment variable detection
                
                // Check if we have any friends currently in-game by using the existing infrastructure
                // This is a placeholder for more sophisticated friend monitoring
            }
            catch (Exception)
            {
                // Ignore errors in friend checking
            }
        }
        
        /// <summary>
        /// Check our own Rich Presence for join indicators
        /// </summary>
        private static void CheckOwnRichPresenceForJoinIndicators()
        {
            try
            {
                // This could detect if Steam has modified our Rich Presence in response to join requests
                // Implementation depends on specific Steam API behavior
                // For now, this is a placeholder for potential future enhancement
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
        
        /// <summary>
        /// Extract Steam ID from command line argument and process join request
        /// </summary>
        private static void ExtractAndProcessSteamIdFromArgument(string arg)
        {
            try
            {
                Debug.Log($"[ETGSteamP2P] 🔍 FALLBACK: Analyzing argument: {arg}");
                
                // Try to extract Steam ID from various argument formats
                var patterns = new string[] {
                    @"steam://connect/(\d+)",
                    @"steam://joinlobby/\d+/(\d+)",
                    @"steam://rungameid/\d+//(\d+)",
                    @"\+connect_lobby\s+(\d+)",
                    @"\+join_game\s+(\d+)",
                    @"connect=(\d+)",
                    @"steamid=(\d+)",
                    @"host=(\d+)"
                };
                
                // First try regex patterns
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(arg, pattern);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        if (ulong.TryParse(match.Groups[1].Value, out ulong steamId) && steamId > 76561197960265728)
                        {
                            Debug.Log($"[ETGSteamP2P] 🎯 FALLBACK: Extracted Steam ID from pattern '{pattern}': {steamId}");
                            ProcessFallbackJoinRequest(steamId);
                            return;
                        }
                    }
                }
                
                // Try to find any number that looks like a Steam ID
                var allNumbers = Regex.Matches(arg, @"\d+");
                foreach (Match match in allNumbers)
                {
                    if (ulong.TryParse(match.Value, out ulong steamId) && steamId > 76561197960265728)
                    {
                        Debug.Log($"[FallbackDetection] 🎯 FALLBACK: Found potential Steam ID in argument: {steamId}");
                        ProcessFallbackJoinRequest(steamId);
                        return;
                    }
                }
                
                // Special case: if the argument is just "+connect_lobby" without parameters,
                // this might be a Steam Rich Presence join where Steam didn't pass the Steam ID correctly
                if (arg.Equals("+connect_lobby") || arg.Equals("+join_game"))
                {
                    Debug.Log($"[FallbackDetection] 🔍 FALLBACK: Empty {arg} command detected - this suggests Steam Rich Presence join");
                    Debug.Log($"[FallbackDetection] 💡 FALLBACK: The host's Rich Presence 'connect' field might be misconfigured");
                    Debug.Log($"[FallbackDetection] 💡 FALLBACK: Host should set Rich Presence 'connect' to their Steam ID, not session ID");
                    
                    // Try to get the Steam ID from other sources or recent host data
                    TryRecoverSteamIdFromRecent();
                    return;
                }
                
                Debug.Log($"[FallbackDetection] ❌ FALLBACK: Could not extract Steam ID from: {arg}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FallbackDetection] Error extracting Steam ID from argument: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to recover Steam ID from recent host discovery or other sources
        /// when Steam command line parsing fails
        /// </summary>
        private static void TryRecoverSteamIdFromRecent()
        {
            try
            {
                Debug.Log("[ETGSteamP2P] 🔍 FALLBACK: Attempting to recover Steam ID from recent data...");
                
                // Try to get the most recent host from SteamHostManager
                var availableHosts = SteamHostManager.GetAvailableHosts();
                if (!ReferenceEquals(availableHosts, null) && availableHosts.Length > 0)
                {
                    // Get the first available host (since we don't have timestamp info in the array)
                    var hostSteamId = availableHosts[0];
                    
                    if (hostSteamId != 0)
                    {
                        Debug.Log($"[ETGSteamP2P] 🎯 FALLBACK: Found recent host Steam ID: {hostSteamId}");
                        ProcessFallbackJoinRequest(hostSteamId);
                        return;
                    }
                }
                
                Debug.Log("[ETGSteamP2P] ❌ FALLBACK: No recent host data available");
                Debug.Log("[ETGSteamP2P] 💡 FALLBACK: User should use the in-game multiplayer menu instead");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error recovering Steam ID from recent data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Process a fallback join request
        /// </summary>
        private static void ProcessFallbackJoinRequest(ulong hostSteamId)
        {
            try
            {
                if (ReferenceEquals(hostSteamId, 0))
                    return;
                
                // Prevent duplicate processing
                if (ReferenceEquals(SteamHostManager.GetLastInviterSteamId(), hostSteamId) && Time.time - lastFallbackCheck < 5.0f)
                    return;
                
                Debug.Log($"[ETGSteamP2P] 🚀 FALLBACK: Processing join request for Steam ID: {hostSteamId}");
                
                // Get our own Steam ID to avoid self-connection
                ulong mySteamId = SteamReflectionHelper.GetSteamID();
                
                if (object.Equals(hostSteamId, mySteamId))
                {
                    Debug.Log("[ETGSteamP2P] 🚫 FALLBACK: Ignoring self-connection attempt");
                    return;
                }
                
                // Set the invite info so the main system can handle it
                SteamHostManager.SetInviteInfo(hostSteamId);
                
                // Pre-accept the P2P session for this host
                if (!acceptedSessions.Contains(hostSteamId))
                {
                    acceptedSessions.Add(hostSteamId);
                    Debug.Log($"[ETGSteamP2P] 🤝 FALLBACK: Pre-accepted P2P session for host: {hostSteamId}");
                }
                
                // Fire the join requested event
                SteamCallbackManager.TriggerJoinRequested(hostSteamId);
                
                Debug.Log($"[ETGSteamP2P] ✅ FALLBACK: Successfully processed join request for host: {hostSteamId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ETGSteamP2P] Error processing fallback join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Check if a P2P session has been pre-accepted
        /// </summary>
        public static bool IsSessionAccepted(ulong steamId)
        {
            return acceptedSessions.Contains(steamId);
        }
        
        /// <summary>
        /// Add a Steam ID to the accepted sessions list
        /// </summary>
        public static void AcceptSession(ulong steamId)
        {
            acceptedSessions.Add(steamId);
        }
        
        /// <summary>
        /// Remove a Steam ID from the accepted sessions list
        /// </summary>
        public static void RemoveAcceptedSession(ulong steamId)
        {
            acceptedSessions.Remove(steamId);
        }
        
        /// <summary>
        /// Get the status of fallback detection
        /// </summary>
        public static string GetFallbackStatus()
        {
            var status = $"[ETGSteamP2P] Fallback Detection Status:\n";
            status += $"  Using Fallback Detection: {usingFallbackJoinDetection}\n";
            status += $"  Last Fallback Check: {Time.time - lastFallbackCheck:F1}s ago\n";
            status += $"  Accepted Sessions: {acceptedSessions.Count}\n";
            status += $"  Pending Join Requests: {pendingJoinRequests.Count}\n";
            
            return status;
        }
        
        public static bool IsUsingFallbackDetection => usingFallbackJoinDetection;
    }
}
