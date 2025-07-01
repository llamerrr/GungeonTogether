using System;

namespace GungeonTogether.Logging
{
    /// <summary>
    /// Global Debug class to handle logging for GungeonTogether
    /// This provides Debug.Log, Debug.LogError, and Debug.LogWarning methods
    /// that work throughout the entire codebase without namespace imports
    /// </summary>
    public static class Debug
    {
        public static void Log(object message)
        {
            UnityEngine.Debug.Log($"[GungeonTogether] {message}");
        }

        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError($"[GungeonTogether] {message}");
        }

        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning($"[GungeonTogether] {message}");
        }

        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }
    }
}