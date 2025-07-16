using System;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Simple and reliable Steam join system that doesn't rely on complex callbacks
    /// </summary>
    public static class SimpleSteamJoinSystem
    {
        private static bool initialized = false;

        /// <summary>
        /// Initialize the simple join system
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;

            initialized = true;
            GungeonTogether.Logging.Debug.Log("[SimpleSteamJoin] Initialized simple Steam join system");

            // Proactively initialize P2P networking so we can detect join requests
            EnsureP2PNetworkingInitialized();
        }

        /// <summary>
        /// Ensure P2P networking is initialized for join detection
        /// </summary>
        private static void EnsureP2PNetworkingInitialized()
        {
            try
            {
                // If Instance is already available, we're good
                if (!ReferenceEquals(ETGSteamP2PNetworking.Instance, null))
                {
                    GungeonTogether.Logging.Debug.Log("[SimpleSteamJoin] P2P networking already initialized");
                    return;
                }

                // Try to create the P2P networking instance using the factory
                GungeonTogether.Logging.Debug.Log("[SimpleSteamJoin] Initializing P2P networking for join detection...");
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();

                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    GungeonTogether.Logging.Debug.Log("[SimpleSteamJoin] P2P networking initialized successfully");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SimpleSteamJoin] P2P networking not available - will try again later");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SimpleSteamJoin] Error initializing P2P networking: {ex.Message}");
            }
        }

    }
}
