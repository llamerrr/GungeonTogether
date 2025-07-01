using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of player positions, animations, and states across multiplayer sessions.
    /// Ready for real networking implementation
    /// </summary>
    public class PlayerSynchronizer
    {
        private object gameManager;
        
        // Player tracking
        private PlayerController localPlayer;
        private Dictionary<ulong, RemotePlayer> remotePlayers;
        private float lastSyncTime;
        private const float SYNC_INTERVAL = 0.1f; // 10 FPS for position updates
        
        // Player data for networking
        public struct PlayerState
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool isMoving;
            public float facing;
            public string currentAnimation;
            public string currentRoom;
            public int health;
            public int armor;
        }
        
        private class RemotePlayer
        {
            public ulong steamId;
            public PlayerState state;
            public GameObject gameObject;
            public PlayerController controller;
            public float lastUpdateTime;
        }
        
        public PlayerSynchronizer(object gameManager)
        {
            this.gameManager = gameManager;
            this.remotePlayers = new Dictionary<ulong, RemotePlayer>();
            
            GungeonTogether.Logging.Debug.Log("[PlayerSync] PlayerSynchronizer initialized (ready for networking implementation)");
        }
        
        /// <summary>
        /// Update player synchronization - call this every frame
        /// </summary>
        public void Update()
        {
            try
            {
                // Only update if we have a valid game manager
                if (ReferenceEquals(gameManager, null))
                {
                    return;
                }
                
                UpdateLocalPlayer();
                UpdateRemotePlayers();
                
                // Send updates at regular intervals (but not too frequently to avoid sync issues)
                if (Time.time - lastSyncTime >= SYNC_INTERVAL)
                {
                    BroadcastPlayerUpdate();
                    lastSyncTime = Time.time;
                }
            }
            catch (Exception e)
            {
                // Don't log every frame to avoid spam - only log once per second
                if (Time.time - lastSyncTime >= 1.0f)
                {
                    GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error in Update: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Find and track the local player
        /// </summary>
        private void UpdateLocalPlayer()
        {
            try
            {
                if (ReferenceEquals(localPlayer, null))
                {
                    // Try to find the local player
                    localPlayer = GameManager.Instance?.PrimaryPlayer;
                    
                    if (!ReferenceEquals(localPlayer, null))
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Local player found and tracked");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error updating local player: {e.Message}");
            }
        }
        
        /// <summary>
        /// Update positions and states of remote players
        /// </summary>
        private void UpdateRemotePlayers()
        {
            // TODO: Implement remote player interpolation and state updates
            // This will be filled in when we add real networking
        }
        
        /// <summary>
        /// Broadcast local player update to all connected players
        /// </summary>
        private void BroadcastPlayerUpdate()
        {
            try
            {
                if (ReferenceEquals(localPlayer, null)) return;

                var playerState = GetLocalPlayerState();

                // TODO: Send player state over network using real Steam P2P
                
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error broadcasting player update: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get current state of local player
        /// </summary>
        private PlayerState GetLocalPlayerState()
        {
            var state = new PlayerState();
            
            if (!ReferenceEquals(localPlayer, null))
            {
                state.position = localPlayer.transform.position;
                state.velocity = Vector2.zero; // TODO: Get actual velocity
                state.isMoving = localPlayer.IsInCombat; // Use available property as placeholder
                
                // Use safe reflection to access gun angle
                state.facing = GetPlayerFacing(localPlayer);
                
                state.currentAnimation = "default"; // TODO: Get actual animation state
                state.currentRoom = GetCurrentRoomName();
                state.health = (int)(localPlayer.healthHaver?.GetCurrentHealth() ?? 0f);
                state.armor = (int)(localPlayer.healthHaver?.Armor ?? 0f);
            }
            
            return state;
        }
        
        /// <summary>
        /// Safely get player facing direction using reflection if needed
        /// </summary>
        private float GetPlayerFacing(PlayerController player)
        {
            try
            {
                // Try to access the field using reflection
                var fieldInfo = typeof(PlayerController).GetField("m_currentGunAngle", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Public);
                
                if (!object.ReferenceEquals(fieldInfo, null))
                {
                    object value = fieldInfo.GetValue(player);
                    if (!object.ReferenceEquals(value, null))
                    {
                        return (float)value;
                    }
                }
                
                // Fallback: use transform rotation
                return player.transform.eulerAngles.z;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Could not get player facing: {e.Message}");
                // Fallback: use transform rotation
                return player.transform.eulerAngles.z;
            }
        }
        
        /// <summary>
        /// Get the name/ID of the current room
        /// </summary>
        private string GetCurrentRoomName()
        {
            try
            {
                var currentRoom = GameManager.Instance?.Dungeon?.data?.GetAbsoluteRoomFromPosition(localPlayer.transform.position.IntXY());
                return currentRoom?.GetRoomName() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
        
        /// <summary>
        /// Handle incoming player update from network
        /// </summary>
        public void OnPlayerUpdate(ulong steamId, PlayerState state)
        {
            try
            {
                if (!remotePlayers.ContainsKey(steamId))
                {
                    CreateRemotePlayer(steamId);
                }
                
                var remotePlayer = remotePlayers[steamId];
                remotePlayer.state = state;
                remotePlayer.lastUpdateTime = Time.time;
                
                // Update visual representation
                UpdateRemotePlayerVisuals(remotePlayer);
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Updated remote player {steamId} at {state.position}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error handling player update: {e.Message}");
            }
        }
        
        /// <summary>
        /// Create a visual representation for a remote player
        /// </summary>
        private void CreateRemotePlayer(ulong steamId)
        {
            try
            {
                // TODO: Create actual remote player GameObject
                // For now, just track in dictionary
                var remotePlayer = new RemotePlayer
                {
                    steamId = steamId,
                    gameObject = null, // Will create when we have proper player prefabs
                    controller = null,
                    lastUpdateTime = Time.time
                };
                
                remotePlayers[steamId] = remotePlayer;
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created remote player tracking for {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error creating remote player: {e.Message}");
            }
        }
        
        /// <summary>
        /// Update visual representation of remote player
        /// </summary>
        private void UpdateRemotePlayerVisuals(RemotePlayer remotePlayer)
        {
            try
            {
                // TODO: Update remote player GameObject position, animation, etc.
                // This will be implemented when we have proper remote player visuals
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error updating remote player visuals: {e.Message}");
            }
        }
        
        /// <summary>
        /// Remove a disconnected player
        /// </summary>
        public void OnPlayerDisconnected(ulong steamId)
        {
            try
            {
                if (remotePlayers.ContainsKey(steamId))
                {
                    var remotePlayer = remotePlayers[steamId];
                    
                    // Destroy visual representation
                    if (!ReferenceEquals(remotePlayer.gameObject, null))
                    {
                        UnityEngine.Object.Destroy(remotePlayer.gameObject);
                    }
                    
                    remotePlayers.Remove(steamId);
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Removed disconnected player {steamId}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error removing disconnected player: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get count of connected remote players
        /// </summary>
        public int GetRemotePlayerCount()
        {
            return remotePlayers.Count;
        }
        
        /// <summary>
        /// Cleanup when session ends
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Remove all remote players
                foreach (var remotePlayer in remotePlayers.Values)
                {
                    if (!ReferenceEquals(remotePlayer.gameObject, null))
                    {
                        UnityEngine.Object.Destroy(remotePlayer.gameObject);
                    }
                }
                
                remotePlayers.Clear();
                localPlayer = null;
                
                GungeonTogether.Logging.Debug.Log("[PlayerSync] PlayerSynchronizer cleaned up");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error during cleanup: {e.Message}");
            }
        }
    }
}
