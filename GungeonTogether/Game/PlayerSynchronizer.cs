using System;
using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of player states across multiplayer sessions
    /// </summary>
    public class PlayerSynchronizer
    {
        private static PlayerSynchronizer instance;
        public static PlayerSynchronizer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PlayerSynchronizer();
                }
                return instance;
            }
        }

        // Remote player tracking
        private readonly Dictionary<ulong, RemotePlayerState> remotePlayers = new Dictionary<ulong, RemotePlayerState>();
        private readonly Dictionary<ulong, GameObject> remotePlayerObjects = new Dictionary<ulong, GameObject>();
        
        // Local player tracking
        private PlayerController localPlayer;
        private Vector2 lastSentPosition;
        private float lastSentRotation;
        private bool lastSentGrounded;
        private bool lastSentDodgeRolling;
        private const float POSITION_THRESHOLD = 0.1f;
        private const float ROTATION_THRESHOLD = 5f;

        public struct RemotePlayerState
        {
            public ulong SteamId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public bool IsGrounded;
            public bool IsDodgeRolling;
            public float LastUpdateTime;
            public Vector2 TargetPosition;
            public float InterpolationSpeed;
        }

        private PlayerSynchronizer()
        {
            NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.Instance.OnPlayerLeft += OnPlayerLeft;
        }

        /// <summary>
        /// Initialize the player synchronizer
        /// </summary>
        public void Initialize()
        {
            try
            {
                localPlayer = GameManager.Instance?.PrimaryPlayer;
                if (localPlayer != null)
                {
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] Local player found and initialized");
                    
                    // Hook into player events
                    HookPlayerEvents();
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError("[PlayerSync] Could not find local player");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Initialize error: {e.Message}");
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            try
            {
                UpdateLocalPlayer();
                UpdateRemotePlayers();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Update error: {e.Message}");
            }
        }

        #region Local Player

        private void HookPlayerEvents()
        {
            if (localPlayer == null) return;

            try
            {
                // Hook shooting events
                if (localPlayer.CurrentGun != null)
                {
                    // Monitor gun firing - we'll check this in Update instead of hooking directly
                }

                GungeonTogether.Logging.Debug.Log("[PlayerSync] Player events hooked successfully");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to hook player events: {e.Message}");
            }
        }

        private void UpdateLocalPlayer()
        {
            if (localPlayer == null) return;

            try
            {
                var currentPosition = localPlayer.transform.position;
                var currentRotation = localPlayer.transform.eulerAngles.z;
                var currentGrounded = localPlayer.IsGrounded;
                var currentDodgeRolling = localPlayer.IsDodgeRolling;

                // Check if we need to send position update
                bool shouldSendUpdate = false;
                
                if (Vector2.Distance(currentPosition, lastSentPosition) > POSITION_THRESHOLD)
                    shouldSendUpdate = true;
                
                if (Mathf.Abs(currentRotation - lastSentRotation) > ROTATION_THRESHOLD)
                    shouldSendUpdate = true;
                
                if (currentGrounded != lastSentGrounded || currentDodgeRolling != lastSentDodgeRolling)
                    shouldSendUpdate = true;

                if (shouldSendUpdate)
                {
                    NetworkManager.Instance.SendPlayerPosition(
                        currentPosition,
                        localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                        currentRotation,
                        currentGrounded,
                        currentDodgeRolling
                    );

                    lastSentPosition = currentPosition;
                    lastSentRotation = currentRotation;
                    lastSentGrounded = currentGrounded;
                    lastSentDodgeRolling = currentDodgeRolling;
                }

                // Check for shooting
                CheckLocalPlayerShooting();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Local player update error: {e.Message}");
            }
        }

        private void CheckLocalPlayerShooting()
        {
            if (localPlayer?.CurrentGun == null) return;

            try
            {
                // Check if player is currently shooting
                bool isShooting = localPlayer.CurrentGun.IsFiring;
                bool isCharging = localPlayer.CurrentGun.IsCharging;

                if (isShooting || isCharging)
                {
                    var gunAngle = localPlayer.CurrentGun.CurrentAngle;
                    var shootDirection = new Vector2(Mathf.Cos(gunAngle * Mathf.Deg2Rad), Mathf.Sin(gunAngle * Mathf.Deg2Rad));
                    
                    NetworkManager.Instance.SendPlayerShooting(
                        localPlayer.transform.position,
                        shootDirection,
                        localPlayer.CurrentGun.PickupObjectId,
                        isCharging,
                        0f // TODO: Find correct charge time property
                    );
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Shooting check error: {e.Message}");
            }
        }

        #endregion

        #region Remote Players

        private ulong GetLocalSteamId()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.LocalSteamId : 0UL;
        }

        private void UpdateRemotePlayers()
        {
            var currentTime = Time.time;
            var playersToRemove = new List<ulong>();
            ulong localSteamId = GetLocalSteamId();

            foreach (var kvp in remotePlayers)
            {
                var steamId = kvp.Key;
                var playerState = kvp.Value;

                // SKIP local player (host) in timeout check
                if (steamId.Equals(localSteamId))
                    continue;

                // Check for timeout
                if (currentTime - playerState.LastUpdateTime > 5f)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Timeout: Removing remote player {steamId} (last update: {playerState.LastUpdateTime}, now: {currentTime})");
                    playersToRemove.Add(steamId);
                    continue;
                }

                // Interpolate position
                if (remotePlayerObjects.ContainsKey(steamId))
                {
                    var playerObject = remotePlayerObjects[steamId];
                    if (playerObject != null)
                    {
                        // Smooth interpolation to target position
                        var currentPos = playerObject.transform.position;
                        var targetPos = playerState.TargetPosition;
                        var newPos = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * playerState.InterpolationSpeed);
                        playerObject.transform.position = newPos;
                        playerObject.transform.rotation = Quaternion.Euler(0, 0, playerState.Rotation);
                    }
                }
            }

            // Remove timed out players (never remove local host)
            foreach (var steamId in playersToRemove)
            {
                RemoveRemotePlayer(steamId);
            }
        }

        private void OnPlayerJoined(ulong steamId)
        {
            CreateRemotePlayer(steamId);
        }

        private void OnPlayerLeft(ulong steamId)
        {
            RemoveRemotePlayer(steamId);
        }

        private void CreateRemotePlayer(ulong steamId)
        {
            ulong localSteamId = GetLocalSteamId();
            if (steamId.Equals(localSteamId))
                return;

            try
            {
                if (remotePlayerObjects.ContainsKey(steamId)) return;

                // Create a simple visual representation for remote player
                var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}");
                
                // Add sprite renderer for visibility
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = localPlayer?.GetComponent<SpriteRenderer>()?.sprite;
                spriteRenderer.color = Color.cyan; // Different color for remote players
                
                // Position it initially at spawn
                remotePlayerObj.transform.position = Vector2.zero;
                
                remotePlayerObjects[steamId] = remotePlayerObj;
                remotePlayers[steamId] = new RemotePlayerState
                {
                    SteamId = steamId,
                    Position = Vector2.zero,
                    Velocity = Vector2.zero,
                    Rotation = 0f,
                    IsGrounded = true,
                    IsDodgeRolling = false,
                    LastUpdateTime = Time.time,
                    TargetPosition = Vector2.zero,
                    InterpolationSpeed = 10f
                };

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created remote player object for {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create remote player {steamId}: {e.Message}");
            }
        }

        private void RemoveRemotePlayer(ulong steamId)
        {
            ulong localSteamId = GetLocalSteamId();
            if (steamId.Equals(localSteamId))
                return;

            try
            {
                if (remotePlayerObjects.ContainsKey(steamId))
                {
                    var playerObject = remotePlayerObjects[steamId];
                    if (playerObject != null)
                    {
                        UnityEngine.Object.Destroy(playerObject);
                    }
                    remotePlayerObjects.Remove(steamId);
                }

                remotePlayers.Remove(steamId);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Removed remote player {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to remove remote player {steamId}: {e.Message}");
            }
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// Handle received player position data
        /// </summary>
        public void OnPlayerPositionReceived(PlayerPositionData data)
        {
            ulong localSteamId = GetLocalSteamId();
            if (data.PlayerId.Equals(localSteamId))
                return;

            try
            {
                if (remotePlayers.ContainsKey(data.PlayerId))
                {
                    var playerState = remotePlayers[data.PlayerId];
                    playerState.Position = data.Position;
                    playerState.Velocity = data.Velocity;
                    playerState.Rotation = data.Rotation;
                    playerState.IsGrounded = data.IsGrounded;
                    playerState.IsDodgeRolling = data.IsDodgeRolling;
                    playerState.LastUpdateTime = Time.time;
                    playerState.TargetPosition = data.Position;
                    
                    remotePlayers[data.PlayerId] = playerState;
                }
                else
                {
                    // Create new remote player if we don't have them
                    CreateRemotePlayer(data.PlayerId);
                    OnPlayerPositionReceived(data); // Recursively call to handle the data
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Position receive error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle received player shooting data
        /// </summary>
        public void OnPlayerShootingReceived(PlayerShootingData data)
        {
            try
            {
                // Create visual effect for remote player shooting
                CreateShootingEffect(data.Position, data.Direction);
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player {data.PlayerId} shooting from {data.Position} towards {data.Direction}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Shooting receive error: {e.Message}");
            }
        }

        private void CreateShootingEffect(Vector2 position, Vector2 direction)
        {
            try
            {
                // Create a simple line renderer or particle effect to show shooting
                var effectObj = new GameObject("ShootingEffect");
                effectObj.transform.position = position;
                
                var lineRenderer = effectObj.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.yellow;
                lineRenderer.endColor = Color.yellow;
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
                
                var endPosition = position + direction * 2f; // 2 unit long line
                lineRenderer.SetPosition(0, position);
                lineRenderer.SetPosition(1, endPosition);
                
                // Destroy the effect after a short time
                UnityEngine.Object.Destroy(effectObj, 0.1f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Shooting effect error: {e.Message}");
            }
        }

        private void HandleRemotePlayerUpdate(PlayerPositionData data)
        {
            try
            {
                if (remotePlayers.ContainsKey(data.PlayerId))
                {
                    var state = remotePlayers[data.PlayerId];
                    state.Position = data.Position;
                    state.Velocity = data.Velocity;
                    state.Rotation = data.Rotation;
                    state.IsGrounded = data.IsGrounded;
                    state.IsDodgeRolling = data.IsDodgeRolling;
                    state.LastUpdateTime = Time.time;
                    state.TargetPosition = data.Position;
                    remotePlayers[data.PlayerId] = state;

                    UpdateRemotePlayerVisual(data.PlayerId, state);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error updating remote player: {e.Message}");
            }
        }

        private void HandleRemotePlayerShootingInternal(PlayerShootingData data)
        {
            try
            {
                // Create visual effect for remote player shooting
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player {data.PlayerId} fired weapon {data.WeaponId}");
                
                // TODO: Create projectile or visual effect at position and direction
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error handling remote shooting: {e.Message}");
            }
        }

        private void UpdateRemotePlayerVisual(ulong playerId, RemotePlayerState state)
        {
            try
            {
                if (remotePlayerObjects.ContainsKey(playerId))
                {
                    var playerObj = remotePlayerObjects[playerId];
                    if (playerObj != null)
                    {
                        playerObj.transform.position = state.Position;
                        playerObj.transform.eulerAngles = new Vector3(0, 0, state.Rotation);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error updating visual: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Get all remote player states
        /// </summary>
        public Dictionary<ulong, RemotePlayerState> GetRemotePlayers()
        {
            return new Dictionary<ulong, RemotePlayerState>(remotePlayers);
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Cleanup()
        {
            foreach (var kvp in remotePlayerObjects)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            
            remotePlayerObjects.Clear();
            remotePlayers.Clear();
            
            GungeonTogether.Logging.Debug.Log("[PlayerSync] Cleanup complete");
        }

        /// <summary>
        /// Static initialize method
        /// </summary>
        public static void StaticInitialize()
        {
            Instance.Initialize();
        }

        /// <summary>
        /// Static update method
        /// </summary>
        public static void StaticUpdate()
        {
            Instance.Update();
        }

        /// <summary>
        /// Update remote player from network data
        /// </summary>
        public static void UpdateRemotePlayer(PlayerPositionData data)
        {
            Instance.HandleRemotePlayerPosition(data);
        }

        /// <summary>
        /// Handle remote player shooting
        /// </summary>
        public static void HandleRemotePlayerShooting(PlayerShootingData data)
        {
            Instance.HandlePlayerShootingData(data);
        }

        private void HandleRemotePlayerPosition(PlayerPositionData data)
        {
            try
            {
                if (remotePlayers.ContainsKey(data.PlayerId))
                {
                    var playerState = remotePlayers[data.PlayerId];
                    playerState.Position = data.Position;
                    playerState.Velocity = data.Velocity;
                    playerState.Rotation = data.Rotation;
                    playerState.IsGrounded = data.IsGrounded;
                    playerState.IsDodgeRolling = data.IsDodgeRolling;
                    playerState.LastUpdateTime = Time.time;
                    remotePlayers[data.PlayerId] = playerState;
                }
                else
                {
                    // Create new remote player
                    CreateRemotePlayer(data);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error handling remote player position: {e.Message}");
            }
        }

        private void HandlePlayerShootingData(PlayerShootingData data)
        {
            try
            {
                CreateShootingEffect(data.Position, data.Direction);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player {data.PlayerId} shooting");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error handling player shooting: {e.Message}");
            }
        }

        private void CreateRemotePlayer(PlayerPositionData data)
        {
            try
            {
                var remotePlayerState = new RemotePlayerState
                {
                    SteamId = data.PlayerId,
                    Position = data.Position,
                    Velocity = data.Velocity,
                    Rotation = data.Rotation,
                    IsGrounded = data.IsGrounded,
                    IsDodgeRolling = data.IsDodgeRolling,
                    LastUpdateTime = Time.time,
                    TargetPosition = data.Position,
                    InterpolationSpeed = 5f
                };

                remotePlayers[data.PlayerId] = remotePlayerState;
                
                // Create visual representation
                CreateRemotePlayerVisual(data.PlayerId, data.Position);
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created remote player {data.PlayerId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error creating remote player: {e.Message}");
            }
        }

        private void CreateRemotePlayerVisual(ulong playerId, Vector2 position)
        {
            try
            {
                var remotePlayerObj = new GameObject($"RemotePlayer_{playerId}");
                remotePlayerObj.transform.position = position;
                
                // Add sprite renderer for visibility
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
                spriteRenderer.color = Color.green;
                
                // Create a simple player sprite
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        texture.SetPixel(x, y, Color.green);
                    }
                }
                texture.Apply();
                
                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                spriteRenderer.sprite = sprite;
                
                remotePlayerObjects[playerId] = remotePlayerObj;
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created visual for remote player {playerId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error creating remote player visual: {e.Message}");
            }
        }
    }
}
