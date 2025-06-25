using System;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Simplified GameManager for testing TypeLoadException issue
    /// </summary>
    public class SimpleGameManager
    {
        private bool isInitialized = false;
        
        // Game state
        public bool IsMultiplayerActive { get; private set; }
        public bool IsHost { get; private set; }
        
        // Events
        public event Action OnMultiplayerStarted;
        public event Action OnMultiplayerStopped;
        public event Action<string> OnConnectionFailed;
        
        public SimpleGameManager()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            try
            {
                Debug.Log("Initializing SimpleGameManager...");
                isInitialized = true;
                Debug.Log("SimpleGameManager initialized successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize SimpleGameManager: {e.Message}");
            }
        }
        
        public void StartHosting()
        {
            if (!isInitialized)
            {
                Debug.LogError("SimpleGameManager not initialized!");
                return;
            }
            
            try
            {
                Debug.Log("Starting multiplayer session as host...");
                
                IsHost = true;
                IsMultiplayerActive = true;
                
                OnMultiplayerStarted?.Invoke();
                Debug.Log("Multiplayer session started as host!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start hosting: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to start hosting: {e.Message}");
            }
        }
          public void JoinSession(string hostSteamId)
        {
            if (!isInitialized)
            {
                Debug.LogError("SimpleGameManager not initialized!");
                return;
            }
            
            try
            {
                Debug.Log($"Joining multiplayer session: {hostSteamId}");
                
                IsHost = false;
                IsMultiplayerActive = true;
                
                OnMultiplayerStarted?.Invoke();
                Debug.Log("Joined multiplayer session!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join session: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to join session: {e.Message}");
            }
        }
        
        public void StopMultiplayer()
        {
            if (!IsMultiplayerActive) return;
            
            try
            {
                Debug.Log("Stopping multiplayer session...");
                
                IsHost = false;
                IsMultiplayerActive = false;
                
                OnMultiplayerStopped?.Invoke();
                Debug.Log("Multiplayer session stopped!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping multiplayer: {e.Message}");
            }
        }
        
        public void Update()
        {
            if (!isInitialized) return;
            // Simple update logic for testing
        }
        
        public void Dispose()
        {
            StopMultiplayer();
        }
    }
}
