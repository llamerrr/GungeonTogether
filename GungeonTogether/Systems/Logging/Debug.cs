namespace GungeonTogether.Systems.Logging
{
    public static class Debug
    {
        public static void Log(object message) => Logger.LogDebug(message);
        public static void LogWarning(object message) => Logger.LogWarning(message);
        public static void LogError(object message) => Logger.LogError(message);
    }
}
