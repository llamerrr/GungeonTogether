using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GungeonTogether.Steam;
using UnityEngine.SceneManagement;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of player states across multiplayer sessions
    /// </summary>
    public class PlayerSynchroniser
    {
        private static PlayerSynchroniser instance;
        public static PlayerSynchroniser Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PlayerSynchroniser();
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
        private float lastPositionSentTime = 0f;
        private const float HEARTBEAT_INTERVAL = 1f; // Send heartbeat every 1 second
        private const float TIMEOUT_MULTIPLIER = 6f; // Match NetworkManager
        
        // Logging spam reduction
        private float lastLogTime = 0f;
        private const float LOG_THROTTLE_INTERVAL = 5f; // Only log every 5 seconds for routine updates
        private Vector2 lastLoggedPosition;
        private string lastLoggedMap;

        // Add a field to track the current map/scene for both local and remote players
        private string localMapName;

        // Debug counters for networking
        public static int LastUpdateSentFrame = -1;
        public static int LastUpdateReceivedFrame = -1;
        public static float LastUpdateSentTime = -1f;
        public static float LastUpdateReceivedTime = -1f;

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
            public string MapName; // Track the map/scene name
        }

        private PlayerSynchroniser()
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
                var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
                var isHost = NetworkManager.Instance?.IsHost() ?? false;
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][INIT] Called for SteamId={localSteamId}, IsHost={isHost}");
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Starting PlayerSynchroniser initialization...");
                localPlayer = GameManager.Instance?.PrimaryPlayer;
                localMapName = SceneManager.GetActiveScene().name;
                if (localPlayer != null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Local player found and initialized. Player name: {localPlayer.name}, Position: {localPlayer.transform.position}, Map: {localMapName}");
                    HookPlayerEvents();
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError("[PlayerSync] Could not find local player - GameManager.Instance.PrimaryPlayer is null");
                    if (GameManager.Instance == null)
                    {
                        GungeonTogether.Logging.Debug.LogError("[PlayerSync] GameManager.Instance is null");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Initialize error: " + e.Message);
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
            var isHost = NetworkManager.Instance?.IsHost() ?? false;
            try
            {
                // Try to re-initialize local player if it's still null
                if (localPlayer == null)
                {
                    TryReinitializeLocalPlayer();
                }
                
                // Log occasionally to verify Update is being called
                if (Time.frameCount % 600 == 0) // Every 10 seconds at 60fps
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Update running - Local player: {(localPlayer != null ? "Found" : "NULL")}, Remote players: {remotePlayers.Count}");
                }
                
                UpdateLocalPlayer();
                UpdateRemotePlayers();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Update error: " + e.Message);
            }
        }

        /// <summary>
        /// Try to re-initialize local player if it becomes available
        /// </summary>
        private void TryReinitializeLocalPlayer()
        {
            try
            {
                if (GameManager.Instance?.PrimaryPlayer != null)
                {
                    localPlayer = GameManager.Instance.PrimaryPlayer;
                    localMapName = SceneManager.GetActiveScene().name;
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] RE-INITIALIZED local player! Player name: {localPlayer.name}, Position: {localPlayer.transform.position}, Map: {localMapName}");
                    HookPlayerEvents();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] TryReinitializeLocalPlayer error: " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to hook player events: " + e.Message);
            }
        }

        private void UpdateLocalPlayer()
        {
            if (localPlayer == null)
            {
                if (Time.frameCount % 300 == 0)
                {
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] UpdateLocalPlayer: localPlayer is null, skipping sync.");
                }
                return;
            }
            try
            {
                var currentPosition = localPlayer.transform.position;
                var currentRotation = localPlayer.transform.eulerAngles.z;
                var currentGrounded = localPlayer.IsGrounded;
                var currentDodgeRolling = localPlayer.IsDodgeRolling;
                localMapName = SceneManager.GetActiveScene().name;
                bool shouldSendUpdate = false;
                // Send if moved, rotated, state changed, or at least every 2 seconds
                if (Vector2.Distance(currentPosition, lastSentPosition) > POSITION_THRESHOLD ||
                    Mathf.Abs(currentRotation - lastSentRotation) > ROTATION_THRESHOLD ||
                    currentGrounded != lastSentGrounded ||
                    currentDodgeRolling != lastSentDodgeRolling ||
                    (Time.time - lastPositionSentTime > 2.0f))
                {
                    shouldSendUpdate = true;
                }
                if (shouldSendUpdate)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][JOINER] Attempting to send position: pos={currentPosition} rot={currentRotation} map={localMapName}");
                    NetworkManager.Instance.SendPlayerPositionWithMap(
                        currentPosition,
                        localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                        currentRotation,
                        currentGrounded,
                        currentDodgeRolling,
                        localMapName
                    );
                    lastSentPosition = currentPosition;
                    lastSentRotation = currentRotation;
                    lastSentGrounded = currentGrounded;
                    lastSentDodgeRolling = currentDodgeRolling;
                    lastPositionSentTime = Time.time;
                    // Track last update sent
                    LastUpdateSentFrame = Time.frameCount;
                    LastUpdateSentTime = Time.time;
                }
                CheckLocalPlayerShooting();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync][JOINER] Local player update error: " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting check error: " + e.Message);
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
                float timeoutDuration = HEARTBEAT_INTERVAL * TIMEOUT_MULTIPLIER;
                if (currentTime - playerState.LastUpdateTime > timeoutDuration)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Timeout: Removing remote player {steamId} (last update: {playerState.LastUpdateTime}, now: {currentTime})");
                    playersToRemove.Add(steamId);
                    continue;
                }

                // Interpolate position and update behavior
                if (remotePlayerObjects.ContainsKey(steamId))
                {
                    var playerObject = remotePlayerObjects[steamId];
                    if (playerObject != null)
                    {
                        // Use RemotePlayerBehavior if available, otherwise fallback to simple updates
                        var remoteBehavior = playerObject.GetComponent<RemotePlayerBehavior>();
                        if (remoteBehavior != null)
                        {
                            // Let the behavior component handle the update with full state
                            remoteBehavior.UpdateFromNetworkData(
                                playerState.TargetPosition, 
                                playerState.Velocity, 
                                playerState.Rotation, 
                                playerState.IsGrounded, 
                                playerState.IsDodgeRolling
                            );
                        }
                        else
                        {
                            // Fallback to simple position interpolation
                            var currentPos = playerObject.transform.position;
                            var targetPos = playerState.TargetPosition;
                            var newPos = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * playerState.InterpolationSpeed);
                            playerObject.transform.position = newPos;
                            playerObject.transform.rotation = Quaternion.Euler(0, 0, playerState.Rotation);
                        }
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
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] OnPlayerJoined called. Local SteamId: {GetLocalSteamId()}, Event SteamId: {steamId}");
            // If we are the host, send our current map/scene to the new player
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Host sending map sync to new player {steamId}: {localMapName}");
                NetworkManager.Instance.SendMapSync(steamId, localMapName);
            }
            CreateRemotePlayer(steamId, localMapName);
        }

        private void OnPlayerLeft(ulong steamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player left event: {steamId}");
            RemoveRemotePlayer(steamId);
        }

        private void CreateRemotePlayer(ulong steamId, string mapName = "Unknown")
        {
            ulong localSteamId = GetLocalSteamId();
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] CreateRemotePlayer called. Local SteamId: {localSteamId}, Remote SteamId: {steamId}, Map: {mapName}");
            if (steamId.Equals(localSteamId))
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Skipping CreateRemotePlayer for local player.");
                return;
            }
            // DEBUG: Log all connected players for troubleshooting
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Connected players: {string.Join(", ", remotePlayers.Keys.Select(k => k.ToString()).ToArray())}");
            try
            {
                if (remotePlayerObjects.ContainsKey(steamId))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player object already exists for {steamId}");
                    return;
                }
                
                // Create a more realistic remote player representation
                var remotePlayerObj = CreateRemotePlayerLikeObject(steamId, mapName);
                
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
                    InterpolationSpeed = 10f,
                    MapName = mapName
                };
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created remote player object for {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to create remote player " + steamId + ": " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to remove remote player " + steamId + ": " + e.Message);
            }
        }

        /// <summary>
        /// Creates a more realistic remote player representation that mimics actual PlayerController behavior
        /// </summary>
        private GameObject CreateRemotePlayerLikeObject(ulong steamId, string mapName)
        {
            var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}");
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Creating advanced remote player for {steamId} in map {mapName}");
            
            try
            {
                // Add basic components that make it behave more like a PlayerController
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
                var animator = remotePlayerObj.AddComponent<tk2dSpriteAnimator>();
                
                // Try to copy sprite and animation from local player
                if (localPlayer != null)
                {
                    // Copy sprite renderer properties
                    var localSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
                    {
                        spriteRenderer.sprite = localSpriteRenderer.sprite;
                        spriteRenderer.sortingLayerName = localSpriteRenderer.sortingLayerName;
                        spriteRenderer.sortingOrder = localSpriteRenderer.sortingOrder;
                        spriteRenderer.color = new Color(0.8f, 0.8f, 1.0f, 0.9f); // Slightly transparent and blue-tinted
                    }
                    
                    // Try to copy animation component
                    var localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                    if (localAnimator != null && localAnimator.Library != null)
                    {
                        animator.Library = localAnimator.Library;
                        // Start with idle animation if available
                        try
                        {
                            var idleClips = new string[] { "idle_south", "idle", "player_idle_south", "player_idle" };
                            foreach (var clipName in idleClips)
                            {
                                if (animator.GetClipByName(clipName) != null)
                                {
                                    animator.Play(clipName);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Could not start idle animation for remote player: {ex.Message}");
                        }
                    }
                }
                
                // Fallback if we can't copy from local player
                if (spriteRenderer.sprite == null)
                {
                    CreateFallbackSprite(spriteRenderer);
                }
                
                // Add a simple collider for interaction (but don't make it interfere with physics)
                var collider = remotePlayerObj.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true; // Don't interfere with game physics
                
                // Add a component to handle remote player behavior
                var remotePlayerBehavior = remotePlayerObj.AddComponent<RemotePlayerBehavior>();
                remotePlayerBehavior.Initialize(steamId);
                
                // Position it initially at spawn
                remotePlayerObj.transform.position = Vector2.zero;
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully created advanced remote player for {steamId}");
                return remotePlayerObj;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create advanced remote player for {steamId}: {e.Message}");
                // Fallback to simple sprite if advanced creation fails
                return CreateSimpleRemotePlayer(steamId);
            }
        }
        
        /// <summary>
        /// Creates a simple fallback remote player representation
        /// </summary>
        private GameObject CreateSimpleRemotePlayer(ulong steamId)
        {
            var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}_Simple");
            var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
            CreateFallbackSprite(spriteRenderer);
            return remotePlayerObj;
        }
        
        /// <summary>
        /// Creates a simple sprite as fallback when player sprites are not available
        /// </summary>
        private void CreateFallbackSprite(SpriteRenderer spriteRenderer)
        {
            var texture = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    texture.SetPixel(x, y, Color.cyan);
            texture.Apply();
            
            var fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
            spriteRenderer.sprite = fallbackSprite;
            spriteRenderer.color = Color.cyan;
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// Handle received player position data
        /// </summary>
        public void OnPlayerPositionReceived(PlayerPositionData data)
        {
            ulong localSteamId = GetLocalSteamId();
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnPlayerPositionReceived called. Local SteamId: {localSteamId}, Data PlayerId: {data.PlayerId}");
            if (data.PlayerId.Equals(localSteamId))
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Skipping position packet from self: {data.PlayerId}");
                return;
            }
            try
            {
                // Track last update received (robust, never for self)
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Processing position packet from remote player: {data.PlayerId}");
                OnAnyRemotePacketReceived(data.PlayerId);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Received position for remote player {data.PlayerId}: pos={data.Position} rot={data.Rotation} grounded={data.IsGrounded} dodge={data.IsDodgeRolling}");
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
                    playerState.MapName = data.MapName ?? "Unknown";
                    remotePlayers[data.PlayerId] = playerState;
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player {data.PlayerId} not found in remotePlayers. Creating...");
                    CreateRemotePlayer(data.PlayerId, data.MapName);
                    OnPlayerPositionReceived(data);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Position receive error: " + e.Message);
            }
        }

        /// <summary>
        /// Handle received player shooting data
        /// </summary>
        public void OnPlayerShootingReceived(PlayerShootingData data)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnPlayerShootingReceived called for player: {data.PlayerId}");
                // Track last update received (robust, never for self)
                OnAnyRemotePacketReceived(data.PlayerId);
                CreateShootingEffect(data.Position, data.Direction);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player {data.PlayerId} shooting from {data.Position} towards {data.Direction}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting receive error: " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting effect error: " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error updating remote player: " + e.Message);
            }
        }

        private void HandleRemotePlayerShootingInternal(PlayerShootingData data)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player {data.PlayerId} fired weapon {data.WeaponId}");
                // TODO: Create projectile or visual effect at position and direction
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error handling remote shooting: " + e.Message);
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
                        if (state.MapName == localMapName)
                        {
                            playerObj.SetActive(true);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Rendering remote player {playerId} at pos={state.Position} rot={state.Rotation} in map {state.MapName}");
                            playerObj.transform.position = state.Position;
                            playerObj.transform.eulerAngles = new Vector3(0, 0, state.Rotation);
                        }
                        else
                        {
                            playerObj.SetActive(false);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Hiding remote player {playerId} (map mismatch: {state.MapName} != {localMapName})");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error updating visual: " + e.Message);
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
            // Track for debug UI
            LastStaticUpdateFrame = Time.frameCount;
            var session = GungeonTogetherMod.Instance?._sessionManager;
            if (session == null || !session.IsActive)
                LastStaticUpdateRole = "Singleplayer";
            else if (session.IsHost)
                LastStaticUpdateRole = "HOST";
            else
                LastStaticUpdateRole = "JOINER";

            var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
            var isHost = NetworkManager.Instance?.IsHost() ?? false;
            Instance.Update();
        }

        /// <summary>
        /// Tracks the last frame and role (HOST/JOINER) when StaticUpdate() was called, for debug UI.
        /// </summary>
        public static int LastStaticUpdateFrame = -1;
        public static string LastStaticUpdateRole = "Unknown";

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
                    playerState.MapName = data.MapName ?? "Unknown";
                    remotePlayers[data.PlayerId] = playerState;
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Updated remote player {data.PlayerId} to pos={data.Position} rot={data.Rotation} map={playerState.MapName}");
                    UpdateRemotePlayerVisual(data.PlayerId, playerState);
                }
                else
                {
                    CreateRemotePlayer(data.PlayerId, data.MapName);
                    if (remotePlayers.ContainsKey(data.PlayerId))
                    {
                        UpdateRemotePlayerVisual(data.PlayerId, remotePlayers[data.PlayerId]);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error handling remote player position: " + e.Message);
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
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error handling player shooting: " + e.Message);
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
                    InterpolationSpeed = 5f,
                    MapName = data.MapName ?? "Unknown"
                };
                remotePlayers[data.PlayerId] = remotePlayerState;
                
                // Create the advanced remote player object
                var remotePlayerObj = CreateRemotePlayerLikeObject(data.PlayerId, data.MapName ?? "Unknown");
                remotePlayerObjects[data.PlayerId] = remotePlayerObj;
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created remote player {data.PlayerId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error creating remote player: " + e.Message);
            }
        }

        // Call this from any handler that receives a packet from a remote player.
        // This will NOT update the timer if the sender is the local player (should never happen).
        public static void OnAnyRemotePacketReceived(ulong senderSteamId)
        {
            ulong localSteamId = NetworkManager.Instance != null ? NetworkManager.Instance.LocalSteamId : 0UL;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnAnyRemotePacketReceived called: sender={senderSteamId}, local={localSteamId}");
            
            if (senderSteamId == 0UL) 
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync][DEBUG] Ignoring packet with invalid sender ID: {senderSteamId}");
                return; // Defensive: ignore invalid sender
            }
            if (senderSteamId == localSteamId) 
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Ignoring packet from self: {senderSteamId}");
                return; // Never update for self
            }
            
            LastUpdateReceivedFrame = Time.frameCount;
            LastUpdateReceivedTime = Time.time;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Updated receive counters: frame={LastUpdateReceivedFrame}, time={LastUpdateReceivedTime:F2}");
        }

        // Handler for map sync packet (to be called from NetworkManager when received)
        public void OnMapSyncReceived(string mapName, ulong senderSteamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnMapSyncReceived called: map={mapName}, sender={senderSteamId}");
            // Track last update received (robust, never for self)
            OnAnyRemotePacketReceived(senderSteamId);
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] OnMapSyncReceived called with map: {mapName} (localMapName={localMapName})");
            if (localMapName != mapName)
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Map mismatch - Current: {localMapName}, Host: {mapName}");
                // Defer scene load until GameManager.Instance and PrimaryPlayer are available
                Instance.DeferSceneLoadIfNeeded(mapName);
            }
            else
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Already in correct map: {mapName}");
            }
        }

        // Helper to defer scene load until player is ready
        private void DeferSceneLoadIfNeeded(string mapName)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Checking if scene load can proceed for map: {mapName}");
            GungeonTogether.GungeonTogetherCoroutineRunner.RunCoroutine(WaitForPlayerAndLoadScene(mapName));
        }

        private System.Collections.IEnumerator WaitForPlayerAndLoadScene(string mapName)
        {
            float timeout = 10f;
            float elapsed = 0f;
            while ((GameManager.Instance == null || GameManager.Instance.PrimaryPlayer == null) && elapsed < timeout)
            {
                if (GameManager.Instance == null)
                    GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] Waiting for GameManager.Instance...");
                else if (GameManager.Instance.PrimaryPlayer == null)
                    GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] Waiting for PrimaryPlayer...");
                yield return null;
                elapsed += UnityEngine.Time.unscaledDeltaTime;
            }
            if (GameManager.Instance == null || GameManager.Instance.PrimaryPlayer == null)
            {
                GungeonTogether.Logging.Debug.LogWarning("[PlayerSync][MAPSYNC] Timeout waiting for GameManager/PrimaryPlayer. Scene load may fail.");
            }
            else
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] GameManager and PrimaryPlayer are ready. Proceeding with scene load if needed.");
            }
            
            // Update local map name first
            localMapName = mapName;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Attempting to sync to scene: {mapName}");
            
            // Enhanced scene forcing logic for different scenarios
            try
            {
                if (ForceSceneTransition(mapName))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Successfully initiated scene transition to {mapName}");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync][MAPSYNC] Failed to force scene transition to {mapName}");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync][MAPSYNC] Exception during scene transition: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Enhanced scene forcing logic that handles all major scene transitions
        /// </summary>
        private bool ForceSceneTransition(string targetScene)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] ForceSceneTransition to {targetScene}");
            
            if (string.IsNullOrEmpty(targetScene))
            {
                GungeonTogether.Logging.Debug.LogWarning("[PlayerSync] Target scene is null or empty");
                return false;
            }
            
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Current scene: {currentScene}, Target: {targetScene}");
            
            // If already in target scene, no need to transition
            if (string.Equals(currentScene, targetScene, StringComparison.OrdinalIgnoreCase))
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Already in target scene");
                return true;
            }
            
            // Handle specific scene transitions
            if (targetScene.Equals("tt_foyer", StringComparison.OrdinalIgnoreCase))
            {
                return ForceToFoyer();
            }
            else if (targetScene.StartsWith("tt_") || targetScene.Contains("dungeon") || targetScene.Contains("Dungeon"))
            {
                return ForceToDungeon(targetScene);
            }
            else
            {
                // Generic scene load for other scenes
                return ForceGenericSceneLoad(targetScene);
            }
        }
        
        /// <summary>
        /// Force transition to the foyer
        /// </summary>
        private bool ForceToFoyer()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerSync] Forcing transition to foyer");
            
            try
            {
                if (GameManager.Instance != null)
                {
                    // Try using GameManager's ReturnToFoyer method first
                    var gameManagerType = GameManager.Instance.GetType();
                    var returnToFoyerMethod = gameManagerType.GetMethod("ReturnToFoyer", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (returnToFoyerMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.ReturnToFoyer()");
                        returnToFoyerMethod.Invoke(GameManager.Instance, null);
                        return true;
                    }
                    
                    // Try DoMainMenu method as alternative
                    var doMainMenuMethod = gameManagerType.GetMethod("DoMainMenu", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (doMainMenuMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.DoMainMenu()");
                        doMainMenuMethod.Invoke(GameManager.Instance, null);
                        return true;
                    }
                }
                
                // Fallback to direct scene load
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Using direct scene load for foyer");
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("tt_foyer");
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error forcing foyer transition: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Force transition to a dungeon scene
        /// </summary>
        private bool ForceToDungeon(string dungeonScene)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Forcing transition to dungeon: {dungeonScene}");
            
            try
            {
                // For dungeon scenes, try to use the game's proper dungeon loading mechanism
                if (GameManager.Instance != null)
                {
                    // Try to find and use LoadLevel or similar method
                    var gameManagerType = GameManager.Instance.GetType();
                    var loadLevelMethod = gameManagerType.GetMethod("LoadLevel", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (loadLevelMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.LoadLevel()");
                        // This method might require specific parameters, so use reflection carefully
                        var parameters = loadLevelMethod.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        {
                            loadLevelMethod.Invoke(GameManager.Instance, new object[] { dungeonScene });
                            return true;
                        }
                    }
                }
                
                // Fallback to direct scene load
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Using direct scene load for dungeon");
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(dungeonScene);
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error forcing dungeon transition: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Force generic scene load for any other scene
        /// </summary>
        private bool ForceGenericSceneLoad(string sceneName)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Forcing generic scene load: {sceneName}");
            
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error in generic scene load: {ex.Message}");
                return false;
            }
        }
    }
}
