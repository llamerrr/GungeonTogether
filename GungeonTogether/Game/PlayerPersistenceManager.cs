using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages persistence of remote players across scene transitions
    /// Ensures multiplayer sessions remain active when transitioning between levels
    /// </summary>
    public class PlayerPersistenceManager
    {
        private static PlayerPersistenceManager instance;
        public static PlayerPersistenceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PlayerPersistenceManager();
                }
                return instance;
            }
        }

        // Persistent data structures
        private readonly Dictionary<ulong, PersistentPlayerData> persistentPlayers = new Dictionary<ulong, PersistentPlayerData>();
        private readonly Dictionary<ulong, GameObject> persistentPlayerObjects = new Dictionary<ulong, GameObject>();
        
        // Scene transition state
        private bool isTransitioning = false;
        private string previousScene = "";
        private string targetScene = "";
        private float transitionStartTime = 0f;
        
        // Event handlers
        public event Action<string, string> OnSceneTransitionStarted; // oldScene, newScene
        public event Action<string> OnSceneTransitionCompleted; // newScene
        public event Action<ulong> OnPlayerPersisted; // playerId
        public event Action<ulong> OnPlayerRestored; // playerId

        /// <summary>
        /// Data structure to store persistent player information across scene transitions
        /// </summary>
        [Serializable]
        public struct PersistentPlayerData
        {
            public ulong SteamId;
            public Vector2 LastKnownPosition;
            public Vector2 LastKnownVelocity;
            public float LastKnownRotation;
            public bool IsGrounded;
            public bool IsDodgeRolling;
            public int CharacterId;
            public string CharacterName;
            public string LastMapName;
            public float LastUpdateTime;
            public bool IsConnected;
            
            // Animation state preservation
            public PlayerAnimationState AnimationState;
            public Vector2 MovementDirection;
            public bool IsRunning;
            public bool IsFalling;
            public bool IsTakingDamage;
            public bool IsDead;
            public string CurrentAnimationName;
        }

        private PlayerPersistenceManager()
        {
            // Subscribe to scene loading events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] PlayerPersistenceManager initialized");
        }

        /// <summary>
        /// Initialize the persistence system
        /// </summary>
        public void Initialize()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] Initializing player persistence system");
            
            // Store current scene as baseline
            previousScene = SceneManager.GetActiveScene().name;
            
            // Hook into NetworkManager events for player management
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
                NetworkManager.Instance.OnPlayerLeft += OnPlayerLeft;
            }
        }

        /// <summary>
        /// Persist a player's data for scene transitions
        /// </summary>
        public void PersistPlayer(ulong steamId, PlayerSynchroniser.RemotePlayerState playerState)
        {
            // Check if player is already persisted to avoid redundant calls
            bool isNewPlayer = !persistentPlayers.ContainsKey(steamId);
            bool needsObjectPersistence = false;
            
            var persistentData = new PersistentPlayerData
            {
                SteamId = steamId,
                LastKnownPosition = playerState.Position,
                LastKnownVelocity = playerState.Velocity,
                LastKnownRotation = playerState.Rotation,
                IsGrounded = playerState.IsGrounded,
                IsDodgeRolling = playerState.IsDodgeRolling,
                CharacterId = playerState.CharacterId,
                CharacterName = playerState.CharacterName,
                LastMapName = playerState.MapName,
                LastUpdateTime = Time.time,
                IsConnected = true,
                AnimationState = playerState.AnimationState,
                MovementDirection = playerState.MovementDirection,
                IsRunning = playerState.IsRunning,
                IsFalling = playerState.IsFalling,
                IsTakingDamage = playerState.IsTakingDamage,
                IsDead = playerState.IsDead,
                CurrentAnimationName = playerState.CurrentAnimationName
            };

            persistentPlayers[steamId] = persistentData;
            
            // Only mark player object as persistent if it's not already marked and exists
            if (PlayerSynchroniser.Instance != null && !persistentPlayerObjects.ContainsKey(steamId))
            {
                var playerObjects = PlayerSynchroniser.Instance.GetRemotePlayerObjects();
                if (playerObjects.ContainsKey(steamId) && playerObjects[steamId] != null)
                {
                    var playerObj = playerObjects[steamId];
                    UnityEngine.Object.DontDestroyOnLoad(playerObj);
                    persistentPlayerObjects[steamId] = playerObj;
                    needsObjectPersistence = true;
                    
                    GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Marked player {steamId} object as persistent");
                }
            }
            
            // Only fire events and show notifications for new players or when object persistence is applied
            if (isNewPlayer || needsObjectPersistence)
            {
                OnPlayerPersisted?.Invoke(steamId);
                GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] ✅ Persisted player {steamId} for scene transitions");
                
                // Only show UI notification for new players (reduce spam)
                if (isNewPlayer)
                {
                    try
                    {
                        UI.MultiplayerUIManager.ShowNotification($"💾 Remote player data saved", 2f);
                    }
                    catch { /* UI might not be available */ }
                }
            }
        }



        /// <summary>
        /// Check if a player is already persisted
        /// </summary>
        public bool IsPlayerPersisted(ulong steamId)
        {
            return persistentPlayers.ContainsKey(steamId);
        }

        /// <summary>
        /// Restore a player's data after scene transition
        /// </summary>
        public bool RestorePlayer(ulong steamId)
        {
            if (!persistentPlayers.ContainsKey(steamId))
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerPersistence] No persistent data found for player {steamId}");
                return false;
            }

            var persistentData = persistentPlayers[steamId];
            
            // Convert back to RemotePlayerState
            var restoredState = new PlayerSynchroniser.RemotePlayerState
            {
                SteamId = persistentData.SteamId,
                Position = persistentData.LastKnownPosition,
                Velocity = persistentData.LastKnownVelocity,
                Rotation = persistentData.LastKnownRotation,
                IsGrounded = persistentData.IsGrounded,
                IsDodgeRolling = persistentData.IsDodgeRolling,
                LastUpdateTime = Time.time, // Use current time for restoration
                TargetPosition = persistentData.LastKnownPosition,
                InterpolationSpeed = 5f, // Default interpolation speed
                MapName = SceneManager.GetActiveScene().name, // Update to current scene
                CharacterId = persistentData.CharacterId,
                CharacterName = persistentData.CharacterName,
                AnimationState = persistentData.AnimationState,
                MovementDirection = persistentData.MovementDirection,
                IsRunning = persistentData.IsRunning,
                IsFalling = persistentData.IsFalling,
                IsTakingDamage = persistentData.IsTakingDamage,
                IsDead = persistentData.IsDead,
                CurrentAnimationName = persistentData.CurrentAnimationName
            };

            // Restore the player through PlayerSynchroniser
            if (PlayerSynchroniser.Instance != null)
            {
                // Check if player object already exists and is persistent
                if (persistentPlayerObjects.ContainsKey(steamId) && persistentPlayerObjects[steamId] != null)
                {
                    // Player object survived scene transition, just update state
                    GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Player {steamId} object survived scene transition, updating state");
                    PlayerSynchroniser.Instance.UpdateRemotePlayerState(steamId, restoredState);
                }
                else
                {
                    // Player object was destroyed, recreate it
                    GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Recreating player {steamId} after scene transition");
                    PlayerSynchroniser.Instance.CreateRemotePlayerFromPersistentData(steamId, restoredState);
                }
                
                OnPlayerRestored?.Invoke(steamId);
                GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] ✅ Restored player {steamId} in scene {SceneManager.GetActiveScene().name}");
                
                // Show UI notification
                try
                {
                    UI.MultiplayerUIManager.ShowNotification($"🔄 Remote player restored in new scene", 2f);
                }
                catch { /* UI might not be available */ }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Handle scene loading events
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string newSceneName = scene.name;
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] === SCENE LOADED: {newSceneName} (mode: {mode}) ===");
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Persistent players count: {persistentPlayers.Count}");
            
            if (isTransitioning)
            {
                targetScene = newSceneName;
                CompleteSceneTransition();
            }
            else
            {
                // Scene loaded without explicit transition (e.g., initial load)
                previousScene = newSceneName;
            }
        }

        /// <summary>
        /// Handle scene unloading events
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            string unloadedSceneName = scene.name;
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] === SCENE UNLOADED: {unloadedSceneName} ===");
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Will persist {persistentPlayers.Count} players");
            
            if (!isTransitioning && unloadedSceneName == previousScene)
            {
                // Start scene transition
                StartSceneTransition(unloadedSceneName);
            }
        }

        /// <summary>
        /// Start a scene transition
        /// </summary>
        private void StartSceneTransition(string oldScene)
        {
            if (isTransitioning)
            {
                GungeonTogether.Logging.Debug.LogWarning("[PlayerPersistence] Scene transition already in progress");
                return;
            }

            isTransitioning = true;
            previousScene = oldScene;
            transitionStartTime = Time.time;
            
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Starting scene transition from {oldScene}");
            
            // Persist all current remote players
            PersistAllRemotePlayers();
            
            OnSceneTransitionStarted?.Invoke(oldScene, targetScene);
        }

        /// <summary>
        /// Complete a scene transition
        /// </summary>
        private void CompleteSceneTransition()
        {
            if (!isTransitioning)
            {
                return;
            }

            float transitionTime = Time.time - transitionStartTime;
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Completing scene transition to {targetScene} (took {transitionTime:F2}s)");
            
            // Wait a frame to ensure scene is fully loaded, then restore players
            GungeonTogetherCoroutineRunner.RunCoroutine(RestorePlayersAfterSceneLoad());
            
            isTransitioning = false;
            previousScene = targetScene;
            
            OnSceneTransitionCompleted?.Invoke(targetScene);
        }

        /// <summary>
        /// Coroutine to restore players after scene load is complete
        /// </summary>
        private System.Collections.IEnumerator RestorePlayersAfterSceneLoad()
        {
            // Wait a few frames for scene to be fully initialized
            yield return null;
            yield return null;
            yield return null;
            
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Restoring {persistentPlayers.Count} persistent players");
            
            // Restore all persistent players
            foreach (var steamId in new List<ulong>(persistentPlayers.Keys))
            {
                try
                {
                    RestorePlayer(steamId);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[PlayerPersistence] Failed to restore player {steamId}: {e.Message}");
                }
                
                // Small delay between restorations to avoid overwhelming the system
                yield return new WaitForSeconds(0.1f);
            }
            
            // Refresh outlines for all restored players
            RemotePlayerOutlineManager.Instance.RefreshAllOutlines();
            
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] Player restoration complete");
        }

        /// <summary>
        /// Persist all current remote players
        /// </summary>
        private void PersistAllRemotePlayers()
        {
            if (PlayerSynchroniser.Instance == null)
            {
                return;
            }

            var remotePlayers = PlayerSynchroniser.Instance.GetRemotePlayers();
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Persisting {remotePlayers.Count} remote players for scene transition");
            
            foreach (var kvp in remotePlayers)
            {
                try
                {
                    PersistPlayer(kvp.Key, kvp.Value);
                }
                catch (Exception e)
                {
                    GungeonTogether.Logging.Debug.LogError($"[PlayerPersistence] Failed to persist player {kvp.Key}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Handle new player joining during active session
        /// </summary>
        private void OnPlayerJoined(ulong steamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Player {steamId} joined during active session");
            // New players will be handled by normal PlayerSynchroniser flow
            // We only persist existing players during scene transitions
        }

        /// <summary>
        /// Handle player leaving during active session
        /// </summary>
        private void OnPlayerLeft(ulong steamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerPersistence] Player {steamId} left, removing from persistence");
            
            // Clean up persistent data for disconnected players
            if (persistentPlayers.ContainsKey(steamId))
            {
                persistentPlayers.Remove(steamId);
            }
            
            if (persistentPlayerObjects.ContainsKey(steamId))
            {
                var playerObj = persistentPlayerObjects[steamId];
                if (playerObj != null)
                {
                    UnityEngine.Object.Destroy(playerObj);
                }
                persistentPlayerObjects.Remove(steamId);
            }
        }

        /// <summary>
        /// Force persist a specific player (useful for manual persistence)
        /// </summary>
        public void ForcePersistPlayer(ulong steamId)
        {
            if (PlayerSynchroniser.Instance == null)
            {
                return;
            }

            var remotePlayers = PlayerSynchroniser.Instance.GetRemotePlayers();
            if (remotePlayers.ContainsKey(steamId))
            {
                PersistPlayer(steamId, remotePlayers[steamId]);
            }
        }

        /// <summary>
        /// Update player data with smart persistence (throttled to avoid spam)
        /// </summary>
        public void UpdatePlayerData(ulong steamId, PlayerSynchroniser.RemotePlayerState playerState)
        {
            // Check if we should persist this update
            bool shouldPersist = false;
            
            if (!persistentPlayers.ContainsKey(steamId))
            {
                // New player - always persist
                shouldPersist = true;
            }
            else
            {
                var existingData = persistentPlayers[steamId];
                
                // Check for significant changes that warrant persistence
                if (existingData.CharacterId != playerState.CharacterId ||
                    existingData.CharacterName != playerState.CharacterName ||
                    existingData.LastMapName != playerState.MapName ||
                    (Time.time - existingData.LastUpdateTime) > 30f) // Throttle to max once per 30 seconds for position-only updates
                {
                    shouldPersist = true;
                }
            }
            
            if (shouldPersist)
            {
                PersistPlayer(steamId, playerState);
            }
            else if (persistentPlayers.ContainsKey(steamId))
            {
                // Just update the data silently for existing persistent players
                var persistentData = new PersistentPlayerData
                {
                    SteamId = steamId,
                    LastKnownPosition = playerState.Position,
                    LastKnownVelocity = playerState.Velocity,
                    LastKnownRotation = playerState.Rotation,
                    IsGrounded = playerState.IsGrounded,
                    IsDodgeRolling = playerState.IsDodgeRolling,
                    CharacterId = playerState.CharacterId,
                    CharacterName = playerState.CharacterName,
                    LastMapName = playerState.MapName,
                    LastUpdateTime = Time.time,
                    IsConnected = true,
                    AnimationState = playerState.AnimationState,
                    MovementDirection = playerState.MovementDirection,
                    IsRunning = playerState.IsRunning,
                    IsFalling = playerState.IsFalling,
                    IsTakingDamage = playerState.IsTakingDamage,
                    IsDead = playerState.IsDead,
                    CurrentAnimationName = playerState.CurrentAnimationName
                };

                persistentPlayers[steamId] = persistentData;
            }
        }

        /// <summary>
        /// Get persistent player data (for debugging)
        /// </summary>
        public Dictionary<ulong, PersistentPlayerData> GetPersistentPlayers()
        {
            return new Dictionary<ulong, PersistentPlayerData>(persistentPlayers);
        }

        /// <summary>
        /// Clean up all persistent data (call when ending multiplayer session)
        /// </summary>
        public void ClearAllPersistentData()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] Clearing all persistent player data");
            
            // Destroy any persistent objects
            foreach (var kvp in persistentPlayerObjects)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            
            persistentPlayers.Clear();
            persistentPlayerObjects.Clear();
            
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] Persistent data cleared");
        }

        /// <summary>
        /// Cleanup and unsubscribe from events
        /// </summary>
        public void Shutdown()
        {
            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            
            // Unsubscribe from NetworkManager events
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.Instance.OnPlayerLeft -= OnPlayerLeft;
            }
            
            // Clean up all persistent data
            ClearAllPersistentData();
            
            GungeonTogether.Logging.Debug.Log("[PlayerPersistence] PlayerPersistenceManager shutdown complete");
        }
    }
}
