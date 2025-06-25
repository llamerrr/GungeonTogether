using System;
using UnityEngine;

namespace GungeonTogether.Game
{    /// <summary>
    /// Basic GameManager to isolate TypeLoadException
    /// </summary>
    public class BasicGameManager
    {
        // Basic state
        public bool IsActive { get; private set; }
        public bool IsHost { get; private set; }
        public string Status { get; private set; }
        public string CurrentSessionId { get; private set; }
        
        // Simple events without complex types
        public event Action OnSessionStarted;
        public event Action OnSessionStopped;
        public event Action<string> OnSessionJoined;
        public event Action<string> OnSessionJoinFailed;
        
        public BasicGameManager()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            Status = "Initialized";
            IsActive = false;
            IsHost = false;
            CurrentSessionId = null;
            Debug.Log("[MinimalGameManager] Ultra-minimal initialization complete");
        }
        
        public void StartSession()
        {
            try
            {
                IsActive = true;
                IsHost = true;
                CurrentSessionId = GenerateSessionId();
                Status = "Hosting";
                
                Debug.Log($"[MinimalGameManager] Started hosting session: {CurrentSessionId}");
                OnSessionStarted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MinimalGameManager] Error starting session: {e.Message}");
            }
        }
        
        public void JoinSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    Debug.LogError("[MinimalGameManager] Cannot join session: Invalid session ID");
                    OnSessionJoinFailed?.Invoke("Invalid session ID");
                    return;
                }
                
                Debug.Log($"[MinimalGameManager] Attempting to join session: {sessionId}");
                
                IsActive = true;
                IsHost = false;
                CurrentSessionId = sessionId;
                Status = "Connected";
                
                Debug.Log($"[MinimalGameManager] Successfully joined session: {sessionId}");
                OnSessionJoined?.Invoke(sessionId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MinimalGameManager] Failed to join session: {e.Message}");
                OnSessionJoinFailed?.Invoke($"Join failed: {e.Message}");
            }
        }
        
        public void StopSession()
        {
            try
            {
                var wasActive = IsActive;
                var sessionId = CurrentSessionId;
                
                IsActive = false;
                IsHost = false;
                Status = "Stopped";
                CurrentSessionId = null;
                
                if (wasActive)
                {
                    Debug.Log($"[MinimalGameManager] Stopped session: {sessionId}");
                    OnSessionStopped?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MinimalGameManager] Error stopping session: {e.Message}");
            }
        }
        
        private string GenerateSessionId()
        {
            // Simple session ID generation
            return $"session_{DateTime.Now.Ticks % 1000000}";
        }
        
        public void Update()
        {
            // Minimal update logic
        }
    }
}
