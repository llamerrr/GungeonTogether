using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of enemy states, paths, and actions across multiplayer sessions
    /// Only the host controls enemies and sends updates to clients
    /// </summary>
    public class EnemySynchronizer
    {
        private static EnemySynchronizer instance;
        public static EnemySynchronizer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EnemySynchronizer();
                }
                return instance;
            }
        }

        // Enemy tracking
        private readonly Dictionary<int, AIActor> trackedEnemies = new Dictionary<int, AIActor>();
        private readonly Dictionary<int, RemoteEnemyState> remoteEnemyStates = new Dictionary<int, RemoteEnemyState>();
        private readonly Dictionary<int, GameObject> remoteEnemyObjects = new Dictionary<int, GameObject>();

        private bool isHost;
        private float lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.1f; // 10 FPS for enemy updates
        private const float POSITION_THRESHOLD = 0.2f;

        public struct EnemyNetworkData
        {
            public int EnemyId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float Health;
            public float MaxHealth;
            public int AnimationState;
            public bool IsActive;
            public bool IsMoving;
            public Vector2 TargetPosition;
            public float LastUpdateTime;
        }

        public struct RemoteEnemyState
        {
            public int EnemyId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float Health;
            public float MaxHealth;
            public int AnimationState;
            public bool IsActive;
            public bool IsMoving;
            public float LastUpdateTime;
            public Vector2 TargetPosition;
        }

        private EnemySynchronizer()
        {
        }

        /// <summary>
        /// Initialize the enemy synchronizer
        /// </summary>
        public void Initialize(bool asHost)
        {
            isHost = asHost;

            if (isHost)
            {
                GungeonTogether.Logging.Debug.Log("[EnemySync] Initialized as HOST - will control enemies");
                HookEnemyEvents();
            }
            else
            {
                GungeonTogether.Logging.Debug.Log("[EnemySync] Initialized as CLIENT - will receive enemy updates");
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            if (isHost)
            {
                UpdateHostEnemies();
            }
            else
            {
                UpdateClientEnemies();
            }
        }

        #region Host Logic

        private void HookEnemyEvents()
        {
            try
            {
                // Hook into room events to track enemies
                if (GameManager.Instance != null)
                {
                    // We'll track enemies through room changes and enemy spawning
                    GungeonTogether.Logging.Debug.Log("[EnemySync] Enemy event hooks installed");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Failed to hook enemy events: {e.Message}");
            }
        }

        private void UpdateHostEnemies()
        {
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL) return;

            try
            {
                // Get current room and update enemy tracking
                var currentRoom = GameManager.Instance?.Dungeon?.data?.GetAbsoluteRoomFromPosition(
                    GameManager.Instance?.PrimaryPlayer?.CurrentRoom?.area?.basePosition ?? IntVector2.Zero);

                if (currentRoom != null)
                {
                    UpdateRoomEnemies(currentRoom);
                }

                lastUpdateTime = Time.time;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Host enemy update error: {e.Message}");
            }
        }

        private void UpdateRoomEnemies(object room) // RoomHandler - Will be properly typed when ETG references are available
        {
            try
            {
                // TODO: Implement room enemy tracking when ETG types are available
                // var activeEnemies = room.GetActiveEnemies();
                // if (activeEnemies != null)
                // {
                //     foreach (var enemy in activeEnemies)
                //     {
                //         if (enemy != null && enemy.aiActor != null)
                //         {
                //             var enemyId = enemy.GetInstanceID();
                //             
                //             // Track new enemies
                //             if (!trackedEnemies.ContainsKey(enemyId))
                //             {
                //                 trackedEnemies[enemyId] = enemy.aiActor;
                //                 OnEnemySpawned(enemy.aiActor);
                //             }
                //
                //             // Send state updates for existing enemies
                //             UpdateEnemyState(enemy.aiActor, enemyId);
                //         }
                //     }
                // }

                // Check for destroyed enemies
                CheckForDestroyedEnemies();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Room enemy update error: {e.Message}");
            }
        }

        private void UpdateEnemyState(AIActor enemy, int enemyId)
        {
            try
            {
                if (enemy == null || !enemy.isActiveAndEnabled) return;

                var position = enemy.transform.position;
                var velocity = enemy.specRigidbody?.Velocity ?? Vector2.zero;
                var rotation = enemy.transform.eulerAngles.z;
                var health = enemy.healthHaver?.GetCurrentHealth() ?? 0f;
                var maxHealth = enemy.healthHaver?.GetMaxHealth() ?? 0f;
                var animationState = 0; // TODO: Get correct animation state value

                // Send state update
                NetworkManager.Instance.SendEnemyState(
                    enemyId,
                    position,
                    velocity,
                    rotation,
                    health,
                    animationState,
                    enemy.isActiveAndEnabled
                );

                // Check for path updates
                UpdateEnemyPath(enemy, enemyId);

                // Check for shooting
                CheckEnemyShooting(enemy, enemyId);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy state update error: {e.Message}");
            }
        }

        public void UpdateEnemyPath(AIActor enemy, int enemyId)
        {
            try
            {
                // TODO: Find correct pathfinding method in ETG
                /*
                if (enemy.MovementSpeed > 0 && enemy.PathfindToPosition != null)
                {
                    // Get path information
                    var pathPoints = new Vector2[] { enemy.PathfindToPosition.Value };
                    
                    NetworkManager.Instance.SendEnemyPath(
                        enemyId,
                        pathPoints,
                        0,
                        enemy.MovementSpeed,
                        true
                    );
                }
                */
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy path update error: {e.Message}");
            }
        }

        private void CheckEnemyShooting(AIActor enemy, int enemyId)
        {
            try
            {
                // Check if enemy is shooting
                if (enemy.behaviorSpeculator != null)
                {
                    // Look for shooting behaviors
                    var shootBehaviors = enemy.behaviorSpeculator.AttackBehaviors;
                    if (shootBehaviors != null)
                    {
                        foreach (var behavior in shootBehaviors)
                        {
                            // Check if this behavior is currently active/firing
                            if (behavior != null && behavior.IsReady())
                            {
                                var targetPosition = GameManager.Instance?.PrimaryPlayer?.transform.position ?? Vector2.zero;

                                NetworkManager.Instance.SendEnemyShooting(
                                    enemyId,
                                    enemy.transform.position,
                                    targetPosition,
                                    0 // projectile type - would need to determine this
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy shooting check error: {e.Message}");
            }
        }

        private void CheckForDestroyedEnemies()
        {
            var enemiesToRemove = new List<int>();

            foreach (var kvp in trackedEnemies)
            {
                if (kvp.Value == null || !kvp.Value.isActiveAndEnabled)
                {
                    enemiesToRemove.Add(kvp.Key);
                }
            }

            foreach (var enemyId in enemiesToRemove)
            {
                OnEnemyDestroyed(enemyId);
                trackedEnemies.Remove(enemyId);
            }
        }

        private void OnEnemySpawned(AIActor enemy)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Enemy spawned: {enemy.name} at {enemy.transform.position}");

                // Send spawn notification to clients
                var enemyId = enemy.GetInstanceID();
                NetworkManager.Instance.SendEnemyState(
                    enemyId,
                    enemy.transform.position,
                    Vector2.zero,
                    enemy.transform.eulerAngles.z,
                    enemy.healthHaver?.GetCurrentHealth() ?? 0f,
                    0,
                    true
                );
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy spawn notification error: {e.Message}");
            }
        }

        private void OnEnemyDestroyed(int enemyId)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Enemy destroyed: {enemyId}");

                // Send death notification to clients
                NetworkManager.Instance.SendEnemyState(
                    enemyId,
                    Vector2.zero,
                    Vector2.zero,
                    0f,
                    0f,
                    0,
                    false
                );
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy death notification error: {e.Message}");
            }
        }

        #endregion

        #region Client Logic

        private void UpdateClientEnemies()
        {
            try
            {
                var currentTime = Time.time;
                var enemiesToRemove = new List<int>();

                foreach (var kvp in remoteEnemyStates)
                {
                    var enemyId = kvp.Key;
                    var enemyData = kvp.Value;

                    // Check for timeout
                    if (currentTime - enemyData.LastUpdateTime > 5f)
                    {
                        enemiesToRemove.Add(enemyId);
                        continue;
                    }

                    // Update visual representation
                    UpdateRemoteEnemyVisual(enemyId, enemyData);
                }

                // Remove timed out enemies
                foreach (var enemyId in enemiesToRemove)
                {
                    RemoveRemoteEnemy(enemyId);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Client enemy update error: {e.Message}");
            }
        }

        private void UpdateRemoteEnemyVisual(int enemyId, RemoteEnemyState enemyData)
        {
            try
            {
                if (remoteEnemyObjects.ContainsKey(enemyId))
                {
                    var enemyObj = remoteEnemyObjects[enemyId];
                    if (enemyObj != null)
                    {
                        // Interpolate position
                        var currentPos = enemyObj.transform.position;
                        var targetPos = enemyData.TargetPosition;
                        var newPos = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * 5f);

                        enemyObj.transform.position = newPos;
                        enemyObj.transform.rotation = Quaternion.Euler(0, 0, enemyData.Rotation);

                        // Update health visual indicator (optional)
                        UpdateEnemyHealthVisual(enemyObj, enemyData.Health, enemyData.MaxHealth);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Remote enemy visual update error: {e.Message}");
            }
        }

        private void UpdateEnemyHealthVisual(GameObject enemyObj, float health, float maxHealth)
        {
            try
            {
                // Create or update health bar (simple implementation)
                var healthBarObj = enemyObj.transform.Find("HealthBar");
                if (healthBarObj == null && health > 0 && maxHealth > 0)
                {
                    // Create health bar
                    var healthBar = new GameObject("HealthBar");
                    healthBar.transform.SetParent(enemyObj.transform);
                    healthBar.transform.localPosition = Vector3.up * 0.5f;

                    var renderer = healthBar.AddComponent<LineRenderer>();
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.startColor = Color.red;
                    renderer.endColor = Color.red;
                    renderer.startWidth = 0.1f;
                    renderer.endWidth = 0.1f;
                    renderer.positionCount = 2;
                    renderer.useWorldSpace = false;
                }

                if (healthBarObj != null)
                {
                    var renderer = healthBarObj.GetComponent<LineRenderer>();
                    if (renderer != null && maxHealth > 0)
                    {
                        float healthPercent = health / maxHealth;
                        renderer.SetPosition(0, Vector3.left * 0.2f);
                        renderer.SetPosition(1, Vector3.left * 0.2f + Vector3.right * (0.4f * healthPercent));

                        // Hide if at full health or dead
                        renderer.enabled = health > 0 && health < maxHealth;
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Health visual update error: {e.Message}");
            }
        }

        private void CreateRemoteEnemy(int enemyId, EnemyNetworkData enemyData)
        {
            try
            {
                if (remoteEnemyObjects.ContainsKey(enemyId)) return;

                // Create a simple visual representation for remote enemy
                var remoteEnemyObj = new GameObject($"RemoteEnemy_{enemyId}");

                // Add sprite renderer for visibility
                var spriteRenderer = remoteEnemyObj.AddComponent<SpriteRenderer>();
                spriteRenderer.color = Color.red; // Red for enemies

                // Create a simple square sprite
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        texture.SetPixel(x, y, Color.red);
                    }
                }
                texture.Apply();

                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                spriteRenderer.sprite = sprite;

                // Position it
                remoteEnemyObj.transform.position = enemyData.Position;

                remoteEnemyObjects[enemyId] = remoteEnemyObj;

                GungeonTogether.Logging.Debug.Log($"[EnemySync] Created remote enemy visual for {enemyId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Failed to create remote enemy {enemyId}: {e.Message}");
            }
        }

        private void RemoveRemoteEnemy(int enemyId)
        {
            try
            {
                if (remoteEnemyObjects.ContainsKey(enemyId))
                {
                    var enemyObj = remoteEnemyObjects[enemyId];
                    if (enemyObj != null)
                    {
                        UnityEngine.Object.Destroy(enemyObj);
                    }
                    remoteEnemyObjects.Remove(enemyId);
                }

                remoteEnemyStates.Remove(enemyId);
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Removed remote enemy {enemyId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Failed to remove remote enemy {enemyId}: {e.Message}");
            }
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// Handle received enemy state data
        /// </summary>
        public void OnEnemyStateReceived(EnemyStateData data)
        {
            if (isHost) return; // Host doesn't need to receive enemy updates

            try
            {
                var networkData = new RemoteEnemyState
                {
                    EnemyId = data.EnemyId,
                    Position = data.Position,
                    Velocity = data.Velocity,
                    Rotation = data.Rotation,
                    Health = data.Health,
                    MaxHealth = 100f, // Default, could be sent separately
                    AnimationState = data.AnimationState,
                    IsActive = data.IsActive,
                    IsMoving = data.Velocity.magnitude > 0.1f,
                    TargetPosition = data.Position,
                    LastUpdateTime = Time.time
                };

                if (!remoteEnemyStates.ContainsKey(data.EnemyId))
                {
                    // New enemy - use the existing CreateRemoteEnemy method that takes EnemyPositionData
                    var positionData = new EnemyPositionData
                    {
                        EnemyId = data.EnemyId,
                        Position = data.Position,
                        Velocity = data.Velocity,
                        Rotation = data.Rotation,
                        Health = data.Health,
                        AnimationState = data.AnimationState,
                        IsActive = data.IsActive
                    };
                    CreateRemoteEnemy(positionData);
                }

                remoteEnemyStates[data.EnemyId] = networkData;

                // If enemy is dead, remove it
                if (!data.IsActive || data.Health <= 0)
                {
                    RemoveRemoteEnemy(data.EnemyId);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy state receive error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle received enemy path data
        /// </summary>
        public void OnEnemyPathReceived(EnemyPathData data)
        {
            if (isHost) return;

            try
            {
                if (remoteEnemyStates.ContainsKey(data.EnemyId))
                {
                    var enemyData = remoteEnemyStates[data.EnemyId];

                    // Update target position based on path
                    if (data.PathPoints != null && data.PathPoints.Length > 0)
                    {
                        enemyData.TargetPosition = data.PathPoints[data.CurrentPathIndex % data.PathPoints.Length];
                        enemyData.IsMoving = data.IsPatrolling;
                        remoteEnemyStates[data.EnemyId] = enemyData;
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy path receive error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle received enemy shooting data
        /// </summary>
        public void OnEnemyShootingReceived(EnemyShootingData data)
        {
            if (isHost) return;

            try
            {
                // Create visual effect for enemy shooting
                CreateEnemyShootingEffect(data.Position, data.TargetPosition);

                GungeonTogether.Logging.Debug.Log($"[EnemySync] Enemy {data.EnemyId} shooting from {data.Position} to {data.TargetPosition}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy shooting receive error: {e.Message}");
            }
        }

        private void CreateEnemyShootingEffect(Vector2 fromPosition, Vector2 toPosition)
        {
            try
            {
                // Create a visual line showing the shot
                var effectObj = new GameObject("EnemyShootingEffect");
                effectObj.transform.position = fromPosition;

                var lineRenderer = effectObj.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;

                lineRenderer.SetPosition(0, fromPosition);
                lineRenderer.SetPosition(1, toPosition);

                // Destroy the effect after a short time
                UnityEngine.Object.Destroy(effectObj, 0.15f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Enemy shooting effect error: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Get all remote enemy states
        /// </summary>
        public Dictionary<int, RemoteEnemyState> GetRemoteEnemies()
        {
            return new Dictionary<int, RemoteEnemyState>(remoteEnemyStates);
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Cleanup()
        {
            foreach (var kvp in remoteEnemyObjects)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }

            trackedEnemies.Clear();
            remoteEnemyStates.Clear();
            remoteEnemyObjects.Clear();

            GungeonTogether.Logging.Debug.Log("[EnemySync] Cleanup complete");
        }

        #region Static Methods

        /// <summary>
        /// Static initialize method
        /// </summary>
        public static void StaticInitialize()
        {
            Instance.Initialize(true); // Default to host
        }

        /// <summary>
        /// Static update method
        /// </summary>
        public static void StaticUpdate()
        {
            Instance.Update();
        }

        /// <summary>
        /// Update enemy position from network data
        /// </summary>
        public static void UpdateEnemyPosition(EnemyPositionData data)
        {
            Instance.HandleEnemyPositionUpdate(data);
        }

        /// <summary>
        /// Handle enemy path data
        /// </summary>
        public static void HandleEnemyPath(EnemyPathData data)
        {
            Instance.HandleEnemyPathUpdate(data);
        }

        /// <summary>
        /// Handle enemy shooting data
        /// </summary>
        public static void HandleEnemyShooting(EnemyShootingData data)
        {
            Instance.HandleEnemyShootingUpdate(data);
        }

        public static void HandleEnemySpawn(EnemySpawnData data)
        {
            Instance.HandleEnemySpawnInternal(data);
        }

        private void HandleEnemyPositionUpdate(EnemyPositionData data)
        {
            try
            {
                if (remoteEnemyStates.ContainsKey(data.EnemyId))
                {
                    var enemyState = remoteEnemyStates[data.EnemyId];
                    enemyState.Position = data.Position;
                    enemyState.Velocity = data.Velocity;
                    enemyState.Rotation = data.Rotation;
                    enemyState.Health = data.Health;
                    enemyState.AnimationState = data.AnimationState;
                    enemyState.IsActive = data.IsActive;
                    enemyState.LastUpdateTime = Time.time;
                    remoteEnemyStates[data.EnemyId] = enemyState;
                }
                else
                {
                    // Create new remote enemy
                    CreateRemoteEnemy(data);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error handling enemy position update: {e.Message}");
            }
        }

        private void HandleEnemyPathUpdate(EnemyPathData data)
        {
            try
            {
                // TODO: Implement enemy path visualization
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Enemy {data.EnemyId} path update received");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error handling enemy path: {e.Message}");
            }
        }

        private void HandleEnemyShootingUpdate(EnemyShootingData data)
        {
            try
            {
                CreateEnemyShootingEffect(data.Position, data.TargetPosition);
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Enemy {data.EnemyId} shooting");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error handling enemy shooting: {e.Message}");
            }
        }

        private void HandleEnemySpawnInternal(EnemySpawnData data)
        {
            try
            {
                if (remoteEnemyStates.ContainsKey(data.EnemyId)) return; // already exists
                var st = new RemoteEnemyState
                {
                    EnemyId = data.EnemyId,
                    Position = data.Position,
                    Velocity = Vector2.zero,
                    Rotation = data.Rotation,
                    Health = data.MaxHealth,
                    MaxHealth = data.MaxHealth,
                    AnimationState = 0,
                    IsActive = true,
                    LastUpdateTime = Time.time,
                    TargetPosition = data.Position
                };
                remoteEnemyStates[data.EnemyId] = st;
                CreateRemoteEnemyVisual(data.EnemyId, data.Position);
                GungeonTogether.Logging.Debug.Log($"[EnemySync] Minimal spawn created for enemy {data.EnemyId} type {data.EnemyType}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error handling minimal enemy spawn: {e.Message}");
            }
        }

        private void CreateRemoteEnemy(EnemyPositionData data)
        {
            try
            {
                var remoteEnemyState = new RemoteEnemyState
                {
                    EnemyId = data.EnemyId,
                    Position = data.Position,
                    Velocity = data.Velocity,
                    Rotation = data.Rotation,
                    Health = data.Health,
                    MaxHealth = data.Health, // Assume starting health is max
                    AnimationState = data.AnimationState,
                    IsActive = data.IsActive,
                    LastUpdateTime = Time.time,
                    TargetPosition = data.Position
                };

                remoteEnemyStates[data.EnemyId] = remoteEnemyState;

                // Create visual representation
                CreateRemoteEnemyVisual(data.EnemyId, data.Position);

                GungeonTogether.Logging.Debug.Log($"[EnemySync] Created remote enemy {data.EnemyId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error creating remote enemy: {e.Message}");
            }
        }

        private void CreateRemoteEnemyVisual(int enemyId, Vector2 position)
        {
            try
            {
                var remoteEnemyObj = new GameObject($"RemoteEnemy_{enemyId}");
                remoteEnemyObj.transform.position = position;

                // Add sprite renderer for visibility
                var spriteRenderer = remoteEnemyObj.AddComponent<SpriteRenderer>();
                spriteRenderer.color = Color.red;

                // Create a simple enemy sprite
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        texture.SetPixel(x, y, Color.red);
                    }
                }
                texture.Apply();

                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                spriteRenderer.sprite = sprite;

                remoteEnemyObjects[enemyId] = remoteEnemyObj;

                GungeonTogether.Logging.Debug.Log($"[EnemySync] Created visual for remote enemy {enemyId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[EnemySync] Error creating remote enemy visual: {e.Message}");
            }
        }

        #endregion
    }
}
