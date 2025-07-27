using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    // TO:DO
    // Client and server sync for level created projectiles are stuffed
    // For some reason the client is the only one that can see all projectiles
    // Hosts can create projectiles but only the client can see them
    // Client and server "destroying" of projectiles is also doesn't work (using a "blank" to destroy all projectiles sends the packets but doesn't seem to actually destroy them)
    /*
        [Info   : Unity Log] [GungeonTogether] [ProjectileSync] Removed remote projectile -545048
        [Info   : Unity Log] [GungeonTogether] [ProjectileSync] Removed remote projectile -545068
        [Info   : Unity Log] [GungeonTogether] [ProjectileSync] Removed remote projectile -545136
    */

    public static class ProjectileBlockingHooks
    {
        private static bool _hooksInitialized = false;
        private static readonly HashSet<int> _authorizedProjectileIds = new HashSet<int>();

        public static void InitializeHooks()
        {
            if (_hooksInitialized) return;

            try
            {
                var harmony = new Harmony("GungeonTogether.ProjectileBlocking");

                // Hook Gun.ShootSingleProjectile for player projectiles
                var gunShootMethod = typeof(Gun).GetMethod("ShootSingleProjectile",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (gunShootMethod != null)
                {
                    harmony.Patch(gunShootMethod,
                        prefix: new HarmonyMethod(typeof(ProjectileBlockingHooks),
                            nameof(ShootSingleProjectile_Prefix)));
                    GungeonTogether.Logging.Debug.Log("[ProjectileBlocking] Hooked Gun.ShootSingleProjectile");
                }

                // Hook SpawnManager.SpawnProjectile for all projectile spawning
                var spawnManagerType = typeof(SpawnManager);

                // Hook the main SpawnProjectile method with GameObject prefab
                var spawnProjectileMethod1 = spawnManagerType.GetMethod("SpawnProjectile",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion) },
                    null);

                if (spawnProjectileMethod1 != null)
                {
                    harmony.Patch(spawnProjectileMethod1,
                        prefix: new HarmonyMethod(typeof(ProjectileBlockingHooks),
                            nameof(SpawnProjectile_GameObject_Prefix)));
                    GungeonTogether.Logging.Debug.Log($"[ProjectileBlocking] Hooked SpawnManager.SpawnProjectile(GameObject, Vector3, Quaternion)");
                }

                // Hook the SpawnProjectile method with bool ignoresPools parameter
                var spawnProjectileMethod2 = spawnManagerType.GetMethod("SpawnProjectile",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(bool) },
                    null);

                if (spawnProjectileMethod2 != null)
                {
                    harmony.Patch(spawnProjectileMethod2,
                        prefix: new HarmonyMethod(typeof(ProjectileBlockingHooks),
                            nameof(SpawnProjectile_GameObject_Bool_Prefix)));
                    GungeonTogether.Logging.Debug.Log($"[ProjectileBlocking] Hooked SpawnManager.SpawnProjectile(GameObject, Vector3, Quaternion, bool)");
                }

                // Hook the SpawnProjectile method with string prefab
                var spawnProjectileMethod3 = spawnManagerType.GetMethod("SpawnProjectile",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(Vector3), typeof(Quaternion) },
                    null);

                if (spawnProjectileMethod3 != null)
                {
                    harmony.Patch(spawnProjectileMethod3,
                        prefix: new HarmonyMethod(typeof(ProjectileBlockingHooks),
                            nameof(SpawnProjectile_String_Prefix)));
                    GungeonTogether.Logging.Debug.Log($"[ProjectileBlocking] Hooked SpawnManager.SpawnProjectile(string, Vector3, Quaternion)");
                }

                _hooksInitialized = true;
                GungeonTogether.Logging.Debug.Log("[ProjectileBlocking] All hooks initialized successfully");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Failed to initialize hooks: {e.Message}");
            }
        }

        public static void AuthorizeProjectile(int projectileId)
        {
            lock (_authorizedProjectileIds)
            {
                _authorizedProjectileIds.Add(projectileId);
            }
        }

        public static void DeauthorizeProjectile(int projectileId)
        {
            lock (_authorizedProjectileIds)
            {
                _authorizedProjectileIds.Remove(projectileId);
            }
        }

        [HarmonyPrefix]
        private static bool ShootSingleProjectile_Prefix(Gun __instance, ProjectileModule mod,
            ProjectileData overrideProjectileData = null, GameObject overrideBulletObject = null)
        {
            try
            {
                // Check if this is a player gun and we're not the host
                if (!ShouldAllowProjectileCreation(__instance))
                {
                    // This is a client-side player shooting attempt - send request to server instead
                    HandleClientShootingRequest(__instance, mod, overrideProjectileData, overrideBulletObject);
                    return false; // Block the original method
                }

                return true; // Allow original method to proceed
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error in ShootSingleProjectile_Prefix: {e.Message}");
                return true; // Allow original on error to prevent breaking the game
            }
        }

        [HarmonyPrefix]
        private static bool SpawnProjectile_GameObject_Prefix(ref GameObject __result, GameObject prefab, Vector3 position, Quaternion rotation)
        {
            try
            {
                return ShouldAllowProjectileSpawn(prefab?.name);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error in SpawnProjectile_GameObject_Prefix: {e.Message}");
                return true; // Allow original on error
            }
        }

        [HarmonyPrefix]
        private static bool SpawnProjectile_GameObject_Bool_Prefix(ref GameObject __result, GameObject prefab, Vector3 position, Quaternion rotation, bool ignoresPools)
        {
            try
            {
                return ShouldAllowProjectileSpawn(prefab?.name);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error in SpawnProjectile_GameObject_Bool_Prefix: {e.Message}");
                return true; // Allow original on error
            }
        }

        [HarmonyPrefix]
        private static bool SpawnProjectile_String_Prefix(ref GameObject __result, string resourcePath, Vector3 position, Quaternion rotation)
        {
            try
            {
                return ShouldAllowProjectileSpawn(resourcePath);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error in SpawnProjectile_String_Prefix: {e.Message}");
                return true; // Allow original on error
            }
        }

        private static bool ShouldAllowProjectileSpawn(string prefabName)
        {
            // Only block if we're a client (not host) and in multiplayer
            if (NetworkManager.Instance == null || NetworkManager.Instance.IsHost())
                return true;

            // Check if this projectile spawn is from our authorized synchronizer
            var stackTrace = new System.Diagnostics.StackTrace();
            bool isFromSynchronizer = false;
            bool isFromAuthorizedSource = false;

            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame.GetMethod();

                if (method.DeclaringType == typeof(ProjectileSynchronizer))
                {
                    isFromSynchronizer = true;
                    break;
                }

                // Allow spawning from specific authorized methods/classes
                if (IsAuthorizedSpawnSource(method as MethodInfo))
                {
                    isFromAuthorizedSource = true;
                    break;
                }
            }

            // Allow non-projectile spawns (VFX, debris, etc.)
            if (IsNonProjectileSpawn(prefabName))
            {
                return true;
            }

            // Block if not from synchronizer or other authorized sources
            if (!isFromSynchronizer && !isFromAuthorizedSource)
            {
                GungeonTogether.Logging.Debug.Log($"[ProjectileBlocking] Blocked unauthorized projectile spawn: {prefabName}");
                return false;
            }

            return true; // Allow spawn
        }

        private static bool IsNonProjectileSpawn(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;

            var prefabLower = prefabName.ToLower();

            // Allow VFX, effects, debris, and other non-projectile spawns
            var allowedPrefabs = new[]
            {
                "vfx", "effect", "debris", "particle", "spark", "smoke", "flash",
                "explosion", "impact", "trail", "beam", "laser", "shell", "casing",
                "pickup", "item", "chest", "enemy", "npc", "decoration", "tile"
            };

            return allowedPrefabs.Any(allowed => prefabLower.Contains(allowed));
        }

        private static bool ShouldAllowProjectileCreation(Gun gun)
        {
            // Always allow if NetworkManager not available or not connected
            if (NetworkManager.Instance == null)
                return true;

            // Always allow if we're the host
            if (NetworkManager.Instance.IsHost())
                return true;

            // Check if this is a player gun
            var owner = gun.CurrentOwner;
            if (owner is PlayerController playerController)
            {
                // Only allow local player shooting on clients if they're requesting through server
                return false; // Block all client-side player shooting - will be handled by server request
            }

            // Allow AI/enemy guns (they should be server-controlled but may need special handling)
            return true;
        }

        private static void HandleClientShootingRequest(Gun gun, ProjectileModule mod,
            ProjectileData overrideProjectileData, GameObject overrideBulletObject)
        {
            try
            {
                var owner = gun.CurrentOwner as PlayerController;
                if (owner == null) return;

                // Calculate shooting data that would have been used
                Vector3 position = gun.barrelOffset.position;
                position = new Vector3(position.x, position.y, -1f);

                float num = owner.stats.GetStatValue(PlayerStats.StatType.Accuracy);
                float angleForShot = mod.GetAngleForShot(gun.m_moduleData[mod].alternateAngleSign, num);
                Vector2 direction = BraveMathCollege.DegreesToVector(gun.gunAngle + angleForShot);

                // Get weapon ID
                int weaponId = GetWeaponId(gun);

                // Send to server using the existing method signature
                NetworkManager.Instance.SendShootRequest(position, direction, weaponId);

                // Create visual/audio effects locally for responsiveness
                CreateShootingEffects(gun, mod, position, gun.gunAngle + angleForShot);

                GungeonTogether.Logging.Debug.Log($"[ProjectileBlocking] Sent shoot request for player {owner.PlayerIDX}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error handling client shoot request: {e.Message}");
            }
        }

        private static void CreateShootingEffects(Gun gun, ProjectileModule mod, Vector3 position, float angle)
        {
            // Not working currently!
            try
            {
                // Muzzle flash
                if (gun.muzzleFlashEffects != null)
                {
                    gun.muzzleFlashEffects.SpawnAtPosition(position, angle, gun.transform);
                }

                // Screen shake
                if (gun.doesScreenShake)
                {
                    GameManager.Instance.MainCameraController.DoScreenShake(gun.gunScreenShake, null);
                }

                // Basic audio - simplified to avoid missing properties
                try
                {
                    if (!string.IsNullOrEmpty(gun.OverrideNormalFireAudioEvent))
                    {
                        AkSoundEngine.PostEvent(gun.OverrideNormalFireAudioEvent, gun.gameObject);
                    }
                    else
                    {
                        // Use default gun firing sound
                        AkSoundEngine.PostEvent("Play_WPN_gun_shot_01", gun.gameObject);
                    }
                }
                catch
                {
                    // Audio failed, continue without error
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ProjectileBlocking] Error creating shooting effects: {e.Message}");
            }
        }

        private static int GetWeaponId(Gun gun)
        {
            // This would need to be implemented based on how weapons are identified
            // For now, return a hash of the gun name
            return gun.name?.GetHashCode() ?? 0;
        }

        private static bool IsAuthorizedSpawnSource(MethodInfo method)
        {
            if (method?.DeclaringType == null) return false;

            var typeName = method.DeclaringType.Name;
            var methodName = method.Name;

            // Allow VFX, debris, and other non-projectile spawns
            if (methodName.Contains("VFX") || methodName.Contains("Debris") ||
                methodName.Contains("Effect") || methodName.Contains("Particle"))
            {
                return true;
            }

            // Allow specific game systems that should continue working
            var allowedTypes = new[]
            {
                "TrapSystem", "HazardSystem", "EnvironmentSystem", "TutorialSystem"
            };

            return allowedTypes.Any(allowed => typeName.Contains(allowed));
        }
    }
}
