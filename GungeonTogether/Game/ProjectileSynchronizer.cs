using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Handles synchronization of projectiles across multiplayer sessions
    /// </summary>
    public class ProjectileSynchronizer
    {
        private static ProjectileSynchronizer instance;
        public static ProjectileSynchronizer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ProjectileSynchronizer();
                }
                return instance;
            }
        }

        // Projectile tracking
        private readonly Dictionary<int, Projectile> localProjectiles = new Dictionary<int, Projectile>();
        private readonly Dictionary<int, RemoteProjectileData> remoteProjectiles = new Dictionary<int, RemoteProjectileData>();
        private readonly Dictionary<int, GameObject> remoteProjectileObjects = new Dictionary<int, GameObject>();

        private bool isInitialized;
        private float lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.05f; // 20 FPS for projectile updates
        private int nextProjectileId = 1;

        public struct RemoteProjectileData
        {
            public int ProjectileId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Damage;
            public int ProjectileType;
            public ulong OwnerId;
            public bool IsPlayerProjectile;
            public float SpawnTime;
            public float LastUpdateTime;
            public Vector2 TargetPosition;
        }

        private ProjectileSynchronizer()
        {
        }

        /// <summary>
        /// Initialize the projectile synchronizer
        /// </summary>
        public void Initialize()
        {
            try
            {
                isInitialized = true;
                HookProjectileEvents();
                GungeonTogether.Logging.Debug.Log("[ProjectileSync] Projectile synchronizer initialized");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Initialize error: {e.Message}");
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            if (!isInitialized) return;

            try
            {
                UpdateLocalProjectiles();
                UpdateRemoteProjectiles();
                CleanupOldProjectiles();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Update error: {e.Message}");
            }
        }

        #region Local Projectiles

        private void HookProjectileEvents()
        {
            try
            {
                // Hook into projectile spawning
                // We'll monitor existing projectiles in the scene instead of hooking events directly
                GungeonTogether.Logging.Debug.Log("[ProjectileSync] Projectile event hooks installed");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Failed to hook projectile events: {e.Message}");
            }
        }

        private void UpdateLocalProjectiles()
        {
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL) return;

            try
            {
                // Find all active projectiles in the scene
                var allProjectiles = UnityEngine.Object.FindObjectsOfType<Projectile>();
                var currentProjectileIds = new HashSet<int>();

                foreach (var projectile in allProjectiles)
                {
                    if (projectile != null && projectile.isActiveAndEnabled)
                    {
                        var projectileId = projectile.GetInstanceID();
                        currentProjectileIds.Add(projectileId);

                        // Track new projectiles
                        if (!localProjectiles.ContainsKey(projectileId))
                        {
                            localProjectiles[projectileId] = projectile;
                            OnLocalProjectileSpawned(projectile, projectileId);
                        }
                        else
                        {
                            // Update existing projectile
                            UpdateLocalProjectileState(projectile, projectileId);
                        }
                    }
                }

                // Remove destroyed projectiles
                var projectilesToRemove = new List<int>();
                foreach (var kvp in localProjectiles)
                {
                    if (!currentProjectileIds.Contains(kvp.Key) || kvp.Value == null)
                    {
                        projectilesToRemove.Add(kvp.Key);
                    }
                }

                foreach (var projectileId in projectilesToRemove)
                {
                    OnLocalProjectileDestroyed(projectileId);
                    localProjectiles.Remove(projectileId);
                }

                lastUpdateTime = Time.time;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Local projectile update error: {e.Message}");
            }
        }

        private void OnLocalProjectileSpawned(Projectile projectile, int projectileId)
        {
            try
            {
                bool isPlayerProjectile = IsPlayerProjectile(projectile);

                // Send spawn notification
                NetworkManager.Instance.SendProjectileSpawn(
                    projectileId,
                    projectile.transform.position,
                    projectile.specRigidbody?.Velocity ?? Vector2.zero,
                    projectile.baseData.damage,
                    GetProjectileType(projectile),
                    isPlayerProjectile
                );

                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Local projectile spawned: {projectileId} (Player: {isPlayerProjectile})");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Local projectile spawn error: {e.Message}");
            }
        }

        private void UpdateLocalProjectileState(Projectile projectile, int projectileId)
        {
            try
            {
                // For now, we mainly track spawn/destroy rather than continuous updates
                // Projectiles are usually short-lived and move predictably
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Local projectile state update error: {e.Message}");
            }
        }

        private void OnLocalProjectileDestroyed(int projectileId)
        {
            try
            {
                // Send destroy notification
                var packet = new NetworkPacket(PacketType.ProjectileDestroy,
                    SteamReflectionHelper.GetLocalSteamId(),
                    BitConverter.GetBytes(projectileId));

                NetworkManager.Instance.SendToAll(PacketType.ProjectileDestroy, BitConverter.GetBytes(projectileId));

                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Local projectile destroyed: {projectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Local projectile destroy error: {e.Message}");
            }
        }

        private bool IsPlayerProjectile(Projectile projectile)
        {
            try
            {
                // Check if the projectile owner is a player
                if (projectile.Owner != null)
                {
                    return projectile.Owner is PlayerController;
                }

                // Fallback: check projectile source
                return projectile.PossibleSourceGun != null &&
                       projectile.PossibleSourceGun.CurrentOwner is PlayerController;
            }
            catch
            {
                return false;
            }
        }

        private int GetProjectileType(Projectile projectile)
        {
            try
            {
                // Return a simple type identifier based on projectile properties
                if (projectile.baseData.damage > 50f)
                    return 2; // Heavy projectile
                else if (projectile.baseData.speed > 20f)
                    return 1; // Fast projectile
                else
                    return 0; // Normal projectile
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Remote Projectiles

        private void UpdateRemoteProjectiles()
        {
            try
            {
                var currentTime = Time.time;
                var projectilesToRemove = new List<int>();

                foreach (var kvp in remoteProjectiles)
                {
                    var projectileId = kvp.Key;
                    var projectileData = kvp.Value;

                    // Check for timeout (projectiles have short lifespan)
                    if (currentTime - projectileData.SpawnTime > 10f ||
                        currentTime - projectileData.LastUpdateTime > 2f)
                    {
                        projectilesToRemove.Add(projectileId);
                        continue;
                    }

                    // Update visual representation
                    UpdateRemoteProjectileVisual(projectileId, projectileData);
                }

                // Remove timed out projectiles
                foreach (var projectileId in projectilesToRemove)
                {
                    RemoveRemoteProjectile(projectileId);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Remote projectile update error: {e.Message}");
            }
        }

        private void UpdateRemoteProjectileVisual(int projectileId, RemoteProjectileData projectileData)
        {
            try
            {
                if (remoteProjectileObjects.ContainsKey(projectileId))
                {
                    var projectileObj = remoteProjectileObjects[projectileId];
                    if (projectileObj != null)
                    {
                        // Move projectile based on velocity
                        var currentTime = Time.time;
                        var deltaTime = currentTime - projectileData.LastUpdateTime;
                        var newPosition = projectileData.Position + projectileData.Velocity * deltaTime;

                        projectileObj.transform.position = newPosition;

                        // Update the stored position
                        var updatedData = projectileData;
                        updatedData.Position = newPosition;
                        remoteProjectiles[projectileId] = updatedData;
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Remote projectile visual update error: {e.Message}");
            }
        }

        private void CreateRemoteProjectile(int projectileId, RemoteProjectileData projectileData)
        {
            try
            {
                if (remoteProjectileObjects.ContainsKey(projectileId)) return;

                // Create a simple visual representation for remote projectile
                var remoteProjectileObj = new GameObject($"RemoteProjectile_{projectileId}");

                // Add sprite renderer for visibility
                var spriteRenderer = remoteProjectileObj.AddComponent<SpriteRenderer>();

                // Different colors for different projectile types/owners
                Color projectileColor = projectileData.IsPlayerProjectile ? Color.blue : new Color(1f, 0.5f, 0f); // Orange
                spriteRenderer.color = projectileColor;

                // Create a simple circular sprite
                var texture = new Texture2D(8, 8);
                var center = new Vector2(4, 4);
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        if (distance <= 3f)
                        {
                            texture.SetPixel(x, y, projectileColor);
                        }
                        else
                        {
                            texture.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                texture.Apply();

                var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
                spriteRenderer.sprite = sprite;

                // Position it
                remoteProjectileObj.transform.position = projectileData.Position;

                // Add a trail effect
                var trailRenderer = remoteProjectileObj.AddComponent<TrailRenderer>();
                trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
                trailRenderer.startColor = projectileColor;
                trailRenderer.endColor = projectileColor;
                trailRenderer.startWidth = 0.1f;
                trailRenderer.endWidth = 0.01f;
                trailRenderer.time = 0.2f;

                remoteProjectileObjects[projectileId] = remoteProjectileObj;

                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Created remote projectile visual for {projectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Failed to create remote projectile {projectileId}: {e.Message}");
            }
        }

        private void RemoveRemoteProjectile(int projectileId)
        {
            try
            {
                if (remoteProjectileObjects.ContainsKey(projectileId))
                {
                    var projectileObj = remoteProjectileObjects[projectileId];
                    if (projectileObj != null)
                    {
                        // Create a small explosion effect
                        CreateProjectileDestroyEffect(projectileObj.transform.position);
                        UnityEngine.Object.Destroy(projectileObj);
                    }
                    remoteProjectileObjects.Remove(projectileId);
                }

                remoteProjectiles.Remove(projectileId);
                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Removed remote projectile {projectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Failed to remove remote projectile {projectileId}: {e.Message}");
            }
        }

        private void CreateProjectileDestroyEffect(Vector2 position)
        {
            try
            {
                // Create a small particle effect for projectile destruction
                var effectObj = new GameObject("ProjectileDestroyEffect");
                effectObj.transform.position = position;

                // Simple expanding circle effect
                var spriteRenderer = effectObj.AddComponent<SpriteRenderer>();
                spriteRenderer.color = Color.white;

                // Create explosion texture
                var texture = new Texture2D(16, 16);
                var center = new Vector2(8, 8);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Clamp01(1f - distance / 8f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 0.5f, alpha));
                    }
                }
                texture.Apply();

                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                spriteRenderer.sprite = sprite;

                // Animate and destroy
                StartCoroutine(AnimateExplosion(effectObj, spriteRenderer));
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Projectile destroy effect error: {e.Message}");
            }
        }

        private System.Collections.IEnumerator AnimateExplosion(GameObject effectObj, SpriteRenderer renderer)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 endScale = Vector3.one * 2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                effectObj.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                renderer.color = new Color(1f, 1f, 0.5f, 1f - t);

                yield return null;
            }

            UnityEngine.Object.Destroy(effectObj);
        }

        private System.Collections.IEnumerator StartCoroutine(System.Collections.IEnumerator routine)
        {
            // Simple coroutine starter - in a real implementation you'd want proper coroutine handling
            return routine;
        }

        #endregion

        private void CleanupOldProjectiles()
        {
            try
            {
                var currentTime = Time.time;
                var localProjectilesToRemove = new List<int>();

                // Clean up local projectiles that no longer exist
                foreach (var kvp in localProjectiles)
                {
                    if (kvp.Value == null || !kvp.Value.isActiveAndEnabled)
                    {
                        localProjectilesToRemove.Add(kvp.Key);
                    }
                }

                foreach (var projectileId in localProjectilesToRemove)
                {
                    localProjectiles.Remove(projectileId);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Cleanup error: {e.Message}");
            }
        }

        #region Network Event Handlers

        /// <summary>
        /// Handle received projectile spawn data
        /// </summary>
        public void OnProjectileSpawnReceived(ProjectileData data)
        {
            try
            {
                // TODO: Fix ProjectileData property access
                /*
                var projectileData = new RemoteProjectileData
                {
                    ProjectileId = data.ProjectileId,
                    Position = data.Position,
                    Velocity = data.Velocity,
                    Damage = data.Damage,
                    ProjectileType = data.ProjectileType,
                    OwnerId = data.OwnerId,
                    IsPlayerProjectile = data.IsPlayerProjectile,
                    SpawnTime = Time.time,
                    LastUpdateTime = Time.time,
                    TargetPosition = data.Position
                };

                remoteProjectiles[data.ProjectileId] = projectileData;
                CreateRemoteProjectile(data.ProjectileId, projectileData);
                
                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Remote projectile spawned: {data.ProjectileId}");
                */
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Projectile spawn receive error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle received projectile update data
        /// </summary>
        public void OnProjectileUpdateReceived(ProjectileData data)
        {
            try
            {
                // TODO: Fix ProjectileData property access
                /*
                if (remoteProjectiles.ContainsKey(data.ProjectileId))
                {
                    var projectileData = remoteProjectiles[data.ProjectileId];
                    projectileData.Position = data.Position;
                    projectileData.Velocity = data.Velocity;
                    projectileData.LastUpdateTime = Time.time;
                    
                    remoteProjectiles[data.ProjectileId] = projectileData;
                }
                */
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Projectile update receive error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle received projectile destroy data
        /// </summary>
        public void OnProjectileDestroyReceived(int projectileId)
        {
            try
            {
                RemoveRemoteProjectile(projectileId);
                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Remote projectile destroyed: {projectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Projectile destroy receive error: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Get all remote projectile states
        /// </summary>
        public Dictionary<int, RemoteProjectileData> GetRemoteProjectiles()
        {
            return new Dictionary<int, RemoteProjectileData>(remoteProjectiles);
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Cleanup()
        {
            foreach (var kvp in remoteProjectileObjects)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }

            localProjectiles.Clear();
            remoteProjectiles.Clear();
            remoteProjectileObjects.Clear();

            GungeonTogether.Logging.Debug.Log("[ProjectileSync] Cleanup complete");
        }

        #region Static Methods

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
        /// Spawn remote projectile from network data
        /// </summary>
        public static void SpawnRemoteProjectile(ProjectileSpawnData data)
        {
            Instance.HandleProjectileSpawn(data);
        }

        #endregion

        private void HandleProjectileSpawn(ProjectileSpawnData data)
        {
            try
            {
                var remoteData = new RemoteProjectileData
                {
                    ProjectileId = data.ProjectileId,
                    Position = data.Position,
                    Velocity = data.Velocity,
                    OwnerId = (ulong)data.OwnerId,
                    IsPlayerProjectile = data.IsPlayerProjectile,
                    SpawnTime = Time.time,
                    LastUpdateTime = Time.time,
                    TargetPosition = data.Position
                };

                remoteProjectiles[data.ProjectileId] = remoteData;
                CreateRemoteProjectileVisual(data.ProjectileId, remoteData);

                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Spawned remote projectile {data.ProjectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Error spawning remote projectile: {e.Message}");
            }
        }

        private void CreateRemoteProjectileVisual(int projectileId, RemoteProjectileData data)
        {
            try
            {
                // TODO: Create visual representation of remote projectile
                GungeonTogether.Logging.Debug.Log($"[ProjectileSync] Creating visual for projectile {projectileId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileSync] Error creating projectile visual: {e.Message}");
            }
        }
    }
}
