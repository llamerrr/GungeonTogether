using System;
using UnityEngine;
using Dungeonator;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Hooks into Enter the Gungeon's dungeon generation system to intercept and synchronize
    /// dungeon generation across multiplayer clients.
    /// </summary>
    public static class DungeonGenerationHook
    {
        // Events for dungeon generation interception
        public static event System.Action<Dungeon> OnDungeonGenerated;
        public static event System.Action<RoomHandler> OnRoomGenerated;
        public static event System.Action<int> OnSeedChanged;
        
        private static bool hooksInstalled = false;
        private static int lastSeed = 0;
        
        /// <summary>
        /// Install hooks into the dungeon generation system
        /// </summary>
        public static void InstallHooks()
        {
            if (hooksInstalled) return;
            
            try
            {
                UnityEngine.Debug.Log("[DungeonHook] Installing dungeon generation hooks...");
                
                // Hook into GameManager events
                if (GameManager.Instance != null)
                {
                    // We'll monitor the GameManager's Update cycle to detect seed changes
                    var gameManagerObject = GameManager.Instance.gameObject;
                    var hookComponent = gameManagerObject.GetComponent<DungeonGenerationHookComponent>();
                    if (hookComponent == null)
                    {
                        hookComponent = gameManagerObject.AddComponent<DungeonGenerationHookComponent>();
                    }
                }
                
                hooksInstalled = true;
                UnityEngine.Debug.Log("[DungeonHook] Dungeon generation hooks installed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Failed to install hooks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Remove hooks from the dungeon generation system
        /// </summary>
        public static void RemoveHooks()
        {
            if (!hooksInstalled) return;
            
            try
            {
                // Remove component if it exists
                if (GameManager.Instance != null)
                {
                    var hookComponent = GameManager.Instance.gameObject.GetComponent<DungeonGenerationHookComponent>();
                    if (hookComponent != null)
                    {
                        UnityEngine.Object.Destroy(hookComponent);
                    }
                }
                
                hooksInstalled = false;
                UnityEngine.Debug.Log("[DungeonHook] Dungeon generation hooks removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error removing hooks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Trigger dungeon generated event
        /// </summary>
        internal static void TriggerDungeonGenerated(Dungeon dungeon)
        {
            try
            {
                UnityEngine.Debug.Log("[DungeonHook] Dungeon generation detected");
                OnDungeonGenerated?.Invoke(dungeon);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error triggering dungeon generated event: {e.Message}");
            }
        }
        
        /// <summary>
        /// Trigger room generated event
        /// </summary>
        internal static void TriggerRoomGenerated(RoomHandler room)
        {
            try
            {
                OnRoomGenerated?.Invoke(room);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error triggering room generated event: {e.Message}");
            }
        }
        
        /// <summary>
        /// Trigger seed changed event
        /// </summary>
        internal static void TriggerSeedChanged(int newSeed)
        {
            try
            {
                if (newSeed != lastSeed)
                {
                    lastSeed = newSeed;
                    UnityEngine.Debug.Log($"[DungeonHook] Seed changed to: {newSeed}");
                    OnSeedChanged?.Invoke(newSeed);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error triggering seed changed event: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Component that monitors GameManager for dungeon generation events
    /// </summary>
    public class DungeonGenerationHookComponent : MonoBehaviour
    {
        private Dungeon lastDungeon;
        private int lastSeed;
        private float lastCheckTime;
        private const float CHECK_INTERVAL = 0.1f; // Check every 100ms
        
        void Start()
        {
            UnityEngine.Debug.Log("[DungeonHook] Hook component started");
            lastSeed = GameManager.Instance?.CurrentRunSeed ?? 0;
        }
        
        void Update()
        {
            try
            {
                // Don't check every frame to avoid performance issues
                if (Time.time - lastCheckTime < CHECK_INTERVAL) return;
                lastCheckTime = Time.time;
                
                CheckForSeedChange();
                CheckForDungeonChange();
            }
            catch (Exception e)
            {
                // Only log errors occasionally to avoid spam
                if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
                {
                    UnityEngine.Debug.LogError($"[DungeonHook] Error in Update: {e.Message}");
                }
            }
        }
        
        private void CheckForSeedChange()
        {
            try
            {
                if (GameManager.Instance != null)
                {
                    int currentSeed = GameManager.Instance.CurrentRunSeed;
                    if (currentSeed != lastSeed && currentSeed != 0)
                    {
                        lastSeed = currentSeed;
                        DungeonGenerationHook.TriggerSeedChanged(currentSeed);
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error checking seed change: {e.Message}");
            }
        }
        
        private void CheckForDungeonChange()
        {
            try
            {
                if (GameManager.Instance != null && GameManager.Instance.Dungeon != null)
                {
                    Dungeon currentDungeon = GameManager.Instance.Dungeon;
                    
                    // Check if we have a new dungeon
                    if (currentDungeon != lastDungeon && currentDungeon.data != null)
                    {
                        lastDungeon = currentDungeon;
                        UnityEngine.Debug.Log("[DungeonHook] New dungeon detected");
                        DungeonGenerationHook.TriggerDungeonGenerated(currentDungeon);
                        
                        // Also check for new rooms in this dungeon
                        CheckForNewRooms(currentDungeon);
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error checking dungeon change: {e.Message}");
            }
        }
        
        private void CheckForNewRooms(Dungeon dungeon)
        {
            try
            {
                if (dungeon.data != null && dungeon.data.rooms != null)
                {
                    foreach (var room in dungeon.data.rooms)
                    {
                        if (room != null)
                        {
                            // Trigger room generated event for each room
                            // In a more sophisticated implementation, we'd track which rooms are new
                            DungeonGenerationHook.TriggerRoomGenerated(room);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonHook] Error checking for new rooms: {e.Message}");
            }
        }
        
        void OnDestroy()
        {
            UnityEngine.Debug.Log("[DungeonHook] Hook component destroyed");
        }
    }
}
