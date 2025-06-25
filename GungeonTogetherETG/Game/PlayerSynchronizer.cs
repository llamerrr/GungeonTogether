using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Packet.Data;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of player positions, animations, and states across multiplayer sessions.
    /// </summary>
    public class PlayerSynchronizer
    {
        private GameManager gameManager;
        
        // Player tracking
        private PlayerController localPlayer;
        private Dictionary<ushort, RemotePlayerData> remotePlayers;
        private float lastUpdateTime;
        private const float UPDATE_RATE = 1f / 20f; // 20 updates per second
        
        // Remote player data storage
        private class RemotePlayerData
        {
            public ushort ClientId;
            public Vector2 Position;
            public Vector2 Velocity;
            public bool IsFacingRight;
            public bool IsGrounded;
            public bool IsRolling;
            public bool IsShooting;
            public float AimDirection;
            public string CurrentAnimation;
            public string CurrentRoom;
            public GameObject PlayerObject;
            public tk2dSpriteAnimator Animator;
            public float LastUpdateTime;
        }
          public PlayerSynchronizer(GameManager gameManager)
        {
            this.gameManager = gameManager;
            this.remotePlayers = new Dictionary<ushort, RemotePlayerData>();
            
            // Subscribe to multiplayer events
            gameManager.OnMultiplayerStarted += OnMultiplayerStarted;
            gameManager.OnMultiplayerStopped += OnMultiplayerStopped;
        }
          private void OnMultiplayerStarted()
        {
            Debug.Log("[PlayerSync] Player synchronization started");
            
            // Find the local player
            FindLocalPlayer();
            
            // Hook into player events
            HookPlayerEvents();
        }
        
        private void OnMultiplayerStopped()
        {
            Debug.Log("[PlayerSync] Player synchronization stopped");
            
            // Clean up remote players
            CleanupRemotePlayers();
            
            // Unhook events
            UnhookPlayerEvents();
        }
        
        private void FindLocalPlayer()
        {            try
            {
                // Try to get the primary player from the actual ETG GameManager
                var etgGameManager = global::GameManager.Instance;
                if (etgGameManager != null && etgGameManager.PrimaryPlayer != null)
                {
                    localPlayer = etgGameManager.PrimaryPlayer;
                    Debug.Log("[PlayerSync] Found local player via GameManager.Instance.PrimaryPlayer");
                    return;
                }
                
                // Fallback: search for PlayerController in scene
                PlayerController[] players = UnityEngine.Object.FindObjectsOfType<PlayerController>();                if (players != null && players.Length > 0)
                {
                    localPlayer = players[0]; // Take the first player found
                    Debug.Log($"[PlayerSync] Found local player via FindObjectsOfType (found {players.Length} players)");
                    return;
                }
                
                Debug.LogWarning("[PlayerSync] Could not find local player - will retry later");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerSync] Error finding local player: {e.Message}");
            }
        }
        
        private void HookPlayerEvents()
        {
            try
            {
                // We'll hook into game events here once we have access to them
                // For now, we'll rely on Update() calls to track player state
                Debug.Log("[PlayerSync] Player event hooks installed");
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Failed to hook player events: {e.Message}");
            }
        }
        
        private void UnhookPlayerEvents()
        {
            try
            {
                // Unhook any events we've registered
                Debug.Log("[PlayerSync] Player event hooks removed");
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Failed to unhook player events: {e.Message}");
            }
        }
        
        public void Update()
        {
            if (!gameManager.IsMultiplayerActive)
                return;
                
            // Try to find local player if we don't have it
            if (localPlayer == null)
            {
                FindLocalPlayer();
                return;
            }
            
            // Send player updates at regular intervals
            if (Time.time - lastUpdateTime >= UPDATE_RATE)
            {
                SendPlayerUpdate();
                lastUpdateTime = Time.time;
            }
            
            // Update remote player interpolation
            UpdateRemotePlayers();
        }
        
        private void SendPlayerUpdate()
        {
            try
            {                if (localPlayer == null || !gameManager.IsMultiplayerActive)
                    return;
                
                // Get current player state
                var packet = new PlayerUpdatePacket
                {
                    Position = localPlayer.CenterPosition,
                    Velocity = localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                    IsFacingRight = !localPlayer.sprite.FlipX, // Use sprite flip to determine facing direction
                    IsGrounded = localPlayer.IsGrounded,
                    IsRolling = localPlayer.IsDodgeRolling,
                    IsShooting = localPlayer.IsFiring,
                    AimDirection = localPlayer.m_currentGunAngle,
                    CurrentAnimation = GetCurrentAnimation(),
                    CurrentRoom = GetCurrentRoomName()
                };
                
                // Send to all connected clients
                if (gameManager.IsHost)
                {
                    gameManager.ServerManager?.BroadcastPacket(packet);
                }
                else
                {
                    gameManager.ClientManager?.SendPacket(packet);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Error sending player update: {e.Message}");
            }
        }
        
        private string GetCurrentAnimation()
        {
            try
            {
                if (localPlayer?.spriteAnimator != null)
                {
                    var currentClip = localPlayer.spriteAnimator.CurrentClip;
                    return currentClip?.name ?? "idle";
                }
            }
            catch (Exception e)
            {
                Debug.Log("[PlayerSync] " + $"Error getting animation: {e.Message}");
            }
            return "idle";
        }
        
        private string GetCurrentRoomName()
        {
            try
            {
                if (localPlayer?.CurrentRoom != null)
                {
                    return localPlayer.CurrentRoom.GetRoomName();
                }
            }
            catch (Exception e)
            {
                Debug.Log("[PlayerSync] " + $"Error getting room name: {e.Message}");
            }
            return "unknown";
        }
        
        public void OnPlayerUpdateReceived(ushort clientId, PlayerUpdatePacket packet)
        {
            try
            {
                // Don't process our own updates
                if (gameManager.IsHost && clientId == 0) // Host is always client 0
                    return;
                    
                if (!gameManager.IsHost && clientId == gameManager.ClientManager?.ClientId)
                    return;
                
                // Update or create remote player data
                if (!remotePlayers.ContainsKey(clientId))
                {
                    CreateRemotePlayer(clientId);
                }
                
                var remotePlayer = remotePlayers[clientId];
                remotePlayer.Position = packet.Position;
                remotePlayer.Velocity = packet.Velocity;
                remotePlayer.IsFacingRight = packet.IsFacingRight;
                remotePlayer.IsGrounded = packet.IsGrounded;
                remotePlayer.IsRolling = packet.IsRolling;
                remotePlayer.IsShooting = packet.IsShooting;
                remotePlayer.AimDirection = packet.AimDirection;
                remotePlayer.CurrentAnimation = packet.CurrentAnimation;
                remotePlayer.CurrentRoom = packet.CurrentRoom;
                remotePlayer.LastUpdateTime = Time.time;
                
                Debug.Log("[PlayerSync] " + $"Updated remote player {clientId} at position {packet.Position}");
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Error processing player update for client {clientId}: {e.Message}");
            }
        }
        
        private void CreateRemotePlayer(ushort clientId)
        {
            try
            {
                Debug.Log("[PlayerSync] " + $"Creating remote player for client {clientId}");
                
                // Create a visual representation of the remote player
                // For now, we'll create a simple sprite-based representation
                GameObject remotePlayerObj = new GameObject($"RemotePlayer_{clientId}");
                
                // Add a sprite renderer to make the player visible
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();                // Try to get the player sprite from the local player
                if (localPlayer?.sprite != null && localPlayer.sprite.renderer != null)
                {
                    // Try to copy material and settings, but create our own sprite
                    spriteRenderer.material = localPlayer.sprite.renderer.material;
                    // We'll create a fallback sprite since accessing the current sprite is complex
                }
                
                // Create a simple colored square for the remote player
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        texture.SetPixel(x, y, Color.cyan); // Different color for remote players
                    }
                }
                texture.Apply();
                spriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
                
                // Add animator if possible
                tk2dSpriteAnimator animator = null;
                if (localPlayer?.spriteAnimator != null)
                {
                    animator = remotePlayerObj.AddComponent<tk2dSpriteAnimator>();
                    // Copy animation library if available
                    if (localPlayer.spriteAnimator.Library != null)
                    {
                        animator.Library = localPlayer.spriteAnimator.Library;
                    }
                }
                
                var remotePlayer = new RemotePlayerData
                {
                    ClientId = clientId,
                    PlayerObject = remotePlayerObj,
                    Animator = animator,
                    LastUpdateTime = Time.time
                };
                
                remotePlayers[clientId] = remotePlayer;
                
                Debug.Log("[PlayerSync] " + $"Remote player {clientId} created successfully");
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Failed to create remote player {clientId}: {e.Message}");
            }
        }
        
        private void UpdateRemotePlayers()
        {
            try
            {
                foreach (var kvp in remotePlayers)
                {
                    var remotePlayer = kvp.Value;
                    
                    // Check if the remote player is still active
                    if (Time.time - remotePlayer.LastUpdateTime > 5f) // 5 second timeout
                    {
                        Debug.Log("[PlayerSync] " + $"Remote player {remotePlayer.ClientId} timed out, removing");
                        RemoveRemotePlayer(remotePlayer.ClientId);
                        continue;
                    }
                    
                    // Update visual representation
                    if (remotePlayer.PlayerObject != null)
                    {
                        // Smooth interpolation to the target position
                        var currentPos = remotePlayer.PlayerObject.transform.position;
                        var targetPos = new Vector3(remotePlayer.Position.x, remotePlayer.Position.y, currentPos.z);
                        remotePlayer.PlayerObject.transform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * 10f);
                        
                        // Update sprite facing direction
                        var spriteRenderer = remotePlayer.PlayerObject.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            spriteRenderer.flipX = !remotePlayer.IsFacingRight;
                        }
                        
                        // Update animation if available
                        if (remotePlayer.Animator != null && !string.IsNullOrEmpty(remotePlayer.CurrentAnimation))
                        {
                            try
                            {
                                if (remotePlayer.Animator.CurrentClip?.name != remotePlayer.CurrentAnimation)
                                {
                                    remotePlayer.Animator.Play(remotePlayer.CurrentAnimation);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.Log("[PlayerSync] " + $"Could not play animation '{remotePlayer.CurrentAnimation}': {e.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Error updating remote players: {e.Message}");
            }
        }
        
        public void OnClientDisconnected(ushort clientId)
        {
            RemoveRemotePlayer(clientId);
        }
        
        private void RemoveRemotePlayer(ushort clientId)
        {
            try
            {
                if (remotePlayers.ContainsKey(clientId))
                {
                    var remotePlayer = remotePlayers[clientId];
                    
                    if (remotePlayer.PlayerObject != null)
                    {
                        UnityEngine.Object.Destroy(remotePlayer.PlayerObject);
                    }
                    
                    remotePlayers.Remove(clientId);
                    Debug.Log("[PlayerSync] " + $"Removed remote player {clientId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Error removing remote player {clientId}: {e.Message}");
            }
        }
        
        private void CleanupRemotePlayers()
        {
            try
            {
                foreach (var kvp in remotePlayers)
                {
                    if (kvp.Value.PlayerObject != null)
                    {
                        UnityEngine.Object.Destroy(kvp.Value.PlayerObject);
                    }
                }
                
                remotePlayers.Clear();
                Debug.Log("[PlayerSync] All remote players cleaned up");
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSync] " + $"Error cleaning up remote players: {e.Message}");
            }
        }
    }
}
