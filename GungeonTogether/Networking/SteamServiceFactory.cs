using System;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Networking
{
    /// <summary>
    /// Factory for creating Steam service instances with runtime detection
    /// </summary>
    public static class SteamServiceFactory
    {
        private static ISteamService _instance;
        private static bool _initialized = false;
        
        /// <summary>
        /// Gets or creates a Steam service instance
        /// </summary>
        public static ISteamService GetSteamService()
        {
            if (!_initialized)
            {
                _instance = CreateSteamService();
                _initialized = true;
            }
            return _instance;
        }
        
        private static ISteamService CreateSteamService()
        {
            Debug.Log("[SteamFactory] Detecting Steam availability...");
            
            // Try to detect if we're running in an environment with Steam support
            if (HasSteamSupport())
            {
                Debug.Log("[SteamFactory] Steam environment detected, attempting to create real Steam service...");
                try
                {
                    return CreateRealSteamService();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SteamFactory] Failed to create real Steam service: {e.Message}");
                    Debug.LogWarning("[SteamFactory] Falling back to mock service...");
                }
            }
            else
            {
                Debug.Log("[SteamFactory] No Steam environment detected, using mock service...");
            }
            
            return new MockSteamService();
        }
        
        private static bool HasSteamSupport()
        {
            try
            {
                // Check if we can find Steam-related types using reflection
                // This avoids TypeLoadException by not directly referencing Steam types
                
                // Look for SteamManager or similar Steam types
                var steamManagerType = FindTypeByName("SteamManager");
                if (steamManagerType != null)
                {
                    // Check if SteamManager is initialized
                    var initializedProperty = steamManagerType.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Static);
                    if (initializedProperty != null)
                    {
                        bool isInitialized = (bool)initializedProperty.GetValue(null);
                        Debug.Log($"[SteamFactory] SteamManager.Initialized = {isInitialized}");
                        return isInitialized;
                    }
                }
                
                // Check for Steamworks namespace
                var steamworksAssembly = FindAssemblyByName("Steamworks.NET");
                if (steamworksAssembly != null)
                {
                    Debug.Log("[SteamFactory] Found Steamworks.NET assembly");
                    return true;
                }
                
                // Check if we're running in Steam environment
                bool runningSteam = Environment.GetCommandLineArgs().Length > 0 && 
                                   Environment.GetCommandLineArgs()[0].ToLower().Contains("steam");
                if (runningSteam)
                {
                    Debug.Log("[SteamFactory] Detected Steam command line");
                    return true;
                }
                
                Debug.Log("[SteamFactory] No Steam indicators found");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamFactory] Error checking Steam support: {e.Message}");
                return false;
            }
        }
        
        private static ISteamService CreateRealSteamService()
        {
            // In the future, we'll use reflection to create a real Steam service
            // For now, this will throw to trigger fallback to mock service
            throw new NotImplementedException("Real Steam service not yet implemented - using mock service");
            
            // Future implementation would look like:
            // var steamServiceType = FindTypeByName("GungeonTogether.Networking.RealSteamService");
            // return (ISteamService)Activator.CreateInstance(steamServiceType);
        }
        
        private static Type FindTypeByName(string typeName)
        {
            try
            {
                // Search all loaded assemblies for the type
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(typeName, false);
                    if (type != null) return type;
                    
                    // Also search for partial matches
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name.Contains(typeName) || t.FullName.Contains(typeName))
                        {
                            return t;
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private static Assembly FindAssemblyByName(string assemblyName)
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains(assemblyName))
                    {
                        return assembly;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
