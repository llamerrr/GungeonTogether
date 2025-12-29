using BepInEx.Logging;

namespace GungeonTogether.Systems.Logging
{
    public static class Logger
    {
        private static ManualLogSource _logSource;

        public static void Initialise(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        public static void LogInfo(object data) => _logSource?.LogInfo(data);
        public static void LogWarning(object data) => _logSource?.LogWarning(data);
        public static void LogError(object data) => _logSource?.LogError(data);
        public static void LogDebug(object data) => _logSource?.LogDebug(data);
    }
}
