using System;
using System.Reflection;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Factory for creating Steam networking instances using reflection to avoid TypeLoadException
    /// </summary>
    public static class SteamNetworkingFactory
    {
        /// <summary>
        /// Try to create a Steam networking instance using reflection
        /// Returns null if the type cannot be loaded or instantiated
        /// </summary>
        public static ISteamNetworking TryCreateSteamNetworking()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[SteamFactory] Attempting to create Steam networking via reflection...");

                // Get the current assembly
                var assembly = Assembly.GetExecutingAssembly();

                // Try to get the ETGSteamP2PNetworking type
                var steamNetType = assembly.GetType("GungeonTogether.Steam.ETGSteamP2PNetworking", false);

                if (object.ReferenceEquals(steamNetType, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamFactory] ETGSteamP2PNetworking type not found");
                    return null;
                }

                // Try to create an instance
                var instance = Activator.CreateInstance(steamNetType);

                if (object.ReferenceEquals(instance, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamFactory] Failed to create ETGSteamP2PNetworking instance");
                    return null;
                }

                // Check if it implements ISteamNetworking
                if (instance is ISteamNetworking steamNet)
                {
                    GungeonTogether.Logging.Debug.Log("[SteamFactory] Successfully created Steam networking instance");
                    return steamNet;
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamFactory] Created instance does not implement ISteamNetworking");
                    return null;
                }
            }
            catch (TypeLoadException e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamFactory] TypeLoadException when creating Steam networking: {e.Message}");
                return null;
            }
            catch (ReflectionTypeLoadException e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamFactory] ReflectionTypeLoadException when creating Steam networking: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamFactory] General exception when creating Steam networking: {e.Message}");
                return null;
            }
        }
    }
}
