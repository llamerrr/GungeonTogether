using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Dungeonator;

namespace GungeonTogether.Debug
{
    /// <summary>
    /// Save and load system for dungeon data to enable testing and debugging
    /// Allows saving current dungeon state and loading it back for consistency testing
    /// F6: Save current dungeon to JSON file
    /// F5: Load saved dungeon and verify consistency  
    /// F7: Compare current vs saved dungeon for determinism testing
    /// </summary>
    [Serializable]
    public class DungeonSaveData
    {
        public int seed;
        public string tilesetId;
        public string floorName;
        public int width;
        public int height;
        public CellDataSave[][] cellData;
        public List<RoomSaveData> rooms;
        public Vector2 playerStartPosition;
        public string dungeonShortName;
        public DateTime saveTime;
        public string gameVersion;
        
        [Serializable]
        public class CellDataSave
        {
            public CellType type;
            public bool breakable;
            public bool isOccupied;
            public DiagonalWallType diagonalWallType;
            public float distanceFromNearestRoom;
            public bool hasBeenGenerated;
            
            public static CellDataSave FromCellData(CellData cell)
            {
                if (cell == null) return null;
                
                return new CellDataSave
                {
                    type = cell.type,
                    breakable = cell.breakable,
                    isOccupied = cell.isOccupied,
                    diagonalWallType = cell.diagonalWallType,
                    distanceFromNearestRoom = cell.distanceFromNearestRoom,
                    hasBeenGenerated = true
                };
            }
            
            public bool IsEquivalentTo(CellData cell)
            {
                if (cell == null) return false;
                
                return type == cell.type &&
                       breakable == cell.breakable &&
                       isOccupied == cell.isOccupied &&
                       diagonalWallType == cell.diagonalWallType &&
                       Math.Abs(distanceFromNearestRoom - cell.distanceFromNearestRoom) < 0.01f;
            }
        }
        
        [Serializable]
        public class RoomSaveData
        {
            public string roomName;
            public Vector2 position;
            public Vector2 dimensions;
            public string category;
            public bool isShop;
            public bool isBossRoom;
            public bool isSecretRoom;
            public bool hasEnemies;
            public int enemyCount;
            public List<EnemySaveData> enemies;
            
            [Serializable]
            public class EnemySaveData
            {
                public string enemyGuid;
                public Vector3 position;
                public float health;
                public float maxHealth;
                public string actorName;
                public bool isAlive;
                
                public static EnemySaveData FromAIActor(AIActor actor)
                {
                    if (actor == null) return null;
                    
                    return new EnemySaveData
                    {
                        enemyGuid = actor.EnemyGuid,
                        position = actor.transform.position,
                        health = actor.healthHaver?.GetCurrentHealth() ?? 0f,
                        maxHealth = actor.healthHaver?.GetMaxHealth() ?? 0f,
                        actorName = actor.GetActorName() ?? actor.name,
                        isAlive = actor.healthHaver?.IsAlive ?? false
                    };
                }
            }
            
            public static RoomSaveData FromRoomHandler(RoomHandler room)
            {
                if (room == null) return null;
                
                var saveData = new RoomSaveData
                {
                    roomName = "Room_" + room.GetHashCode(),
                    position = Vector2.zero, // We'll try to determine this from cells
                    dimensions = Vector2.zero,
                    category = "Unknown",
                    isShop = false,
                    isBossRoom = false,
                    isSecretRoom = false,
                    enemies = new List<EnemySaveData>()
                };
                
                // Try to find enemies in this room
                try
                {
                    var enemiesInRoom = room.GetActiveEnemies(RoomHandler.ActiveEnemyType.All);
                    
                    saveData.hasEnemies = enemiesInRoom != null && enemiesInRoom.Count > 0;
                    saveData.enemyCount = enemiesInRoom?.Count ?? 0;
                    
                    if (enemiesInRoom != null)
                    {
                        foreach (var enemy in enemiesInRoom.Take(10)) // Limit to prevent excessive data
                        {
                            var enemyData = EnemySaveData.FromAIActor(enemy);
                            if (enemyData != null)
                            {
                                saveData.enemies.Add(enemyData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                     UnityEngine.Debug.LogWarning($"[DungeonSaveLoad] Failed to get enemies for room {saveData.roomName}: {ex.Message}");
                }
                
                return saveData;
            }
        }
    }
    
    public static class DungeonSaveLoad
    {
        private static readonly string SAVE_DIRECTORY = Path.Combine(Application.persistentDataPath, "GungeonTogetherDebug");
        private static readonly string SAVE_FILENAME = "dungeon_save.json";
        private static readonly string BACKUP_FILENAME = "dungeon_save_backup.json";
        private static readonly string COMPARISON_LOG = "dungeon_comparison.txt";
        
        private static string SavePath => Path.Combine(SAVE_DIRECTORY, SAVE_FILENAME);
        private static string BackupPath => Path.Combine(SAVE_DIRECTORY, BACKUP_FILENAME);
        private static string ComparisonLogPath => Path.Combine(SAVE_DIRECTORY, COMPARISON_LOG);
        
        private static DungeonSaveData lastSavedData;
        
        /// <summary>
        /// Initialize the save/load system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(SAVE_DIRECTORY))
                {
                    Directory.CreateDirectory(SAVE_DIRECTORY);
                }
                
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Initialized. Save directory: {SAVE_DIRECTORY}");
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Failed to initialize: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save the current dungeon state to a JSON file
        /// </summary>
        public static bool SaveCurrentDungeon()
        {
            try
            {
                if (GameManager.Instance?.Dungeon == null)
                {
                     UnityEngine.Debug.LogError("[DungeonSaveLoad] No dungeon to save!");
                    return false;
                }
                
                 UnityEngine.Debug.Log("[DungeonSaveLoad] Starting dungeon save...");
                
                var dungeon = GameManager.Instance.Dungeon;
                var saveData = CreateSaveDataFromDungeon(dungeon);
                
                // Create backup of existing save
                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, true);
                     UnityEngine.Debug.Log("[DungeonSaveLoad] Created backup of existing save");
                }
                
                // Save to JSON using Unity's JsonUtility
                var json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(SavePath, json);
                
                // Store reference for comparison
                lastSavedData = saveData;
                
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] ✅ Dungeon saved successfully!");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Seed: {saveData.seed}, Size: {saveData.width}x{saveData.height}");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Rooms: {saveData.rooms?.Count ?? 0}, Save location: {SavePath}");
                
                // Show notification in UI if available
                try
                {
                    GungeonTogether.UI.MultiplayerUIManager.ShowNotification($"Dungeon saved! Seed: {saveData.seed}", 3f);
                }
                catch { /* Ignore if UI not available */ }
                
                return true;
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] ❌ Failed to save dungeon: {ex.Message}");
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Stack trace: {ex.StackTrace}");
                
                try
                {
                    GungeonTogether.UI.MultiplayerUIManager.ShowNotification($"Save failed: {ex.Message}", 4f);
                }
                catch { /* Ignore if UI not available */ }
                
                return false;
            }
        }
        
        /// <summary>
        /// Load dungeon from saved JSON file and verify consistency
        /// </summary>
        public static bool LoadDungeon()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                     UnityEngine.Debug.LogError("[DungeonSaveLoad] No saved dungeon found!");
                    
                    try
                    {
                        GungeonTogether.UI.MultiplayerUIManager.ShowNotification("No saved dungeon found", 3f);
                    }
                    catch { /* Ignore if UI not available */ }
                    
                    return false;
                }
                
                 UnityEngine.Debug.Log("[DungeonSaveLoad] Loading dungeon from save...");
                
                var json = File.ReadAllText(SavePath);
                var saveData = JsonUtility.FromJson<DungeonSaveData>(json);
                
                if (saveData == null)
                {
                     UnityEngine.Debug.LogError("[DungeonSaveLoad] Failed to deserialize save data");
                    return false;
                }
                
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] ✅ Loaded dungeon save:");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Seed: {saveData.seed}, Size: {saveData.width}x{saveData.height}");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Floor: {saveData.floorName}, Rooms: {saveData.rooms?.Count ?? 0}");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Saved: {saveData.saveTime}");
                
                // Store for comparison
                lastSavedData = saveData;
                
                // Attempt to verify current dungeon matches saved data
                VerifyDungeonConsistency(saveData);
                
                try
                {
                    GungeonTogether.UI.MultiplayerUIManager.ShowNotification($"Dungeon loaded! Seed: {saveData.seed}", 3f);
                }
                catch { /* Ignore if UI not available */ }
                
                return true;
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] ❌ Failed to load dungeon: {ex.Message}");
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Stack trace: {ex.StackTrace}");
                
                try
                {
                    GungeonTogether.UI.MultiplayerUIManager.ShowNotification($"Load failed: {ex.Message}", 4f);
                }
                catch { /* Ignore if UI not available */ }
                
                return false;
            }
        }
        
        /// <summary>
        /// Compare current dungeon vs saved dungeon for determinism testing
        /// </summary>
        public static void CompareDungeonWithSaved()
        {
            try
            {
                if (GameManager.Instance?.Dungeon == null)
                {
                     UnityEngine.Debug.LogError("[DungeonSaveLoad] No current dungeon to compare!");
                    return;
                }
                
                if (lastSavedData == null && File.Exists(SavePath))
                {
                    // Try to load saved data for comparison
                    var json = File.ReadAllText(SavePath);
                    lastSavedData = JsonUtility.FromJson<DungeonSaveData>(json);
                }
                
                if (lastSavedData == null)
                {
                     UnityEngine.Debug.LogError("[DungeonSaveLoad] No saved dungeon data to compare against!");
                    
                    try
                    {
                        GungeonTogether.UI.MultiplayerUIManager.ShowNotification("No saved data to compare", 3f);
                    }
                    catch { /* Ignore if UI not available */ }
                    
                    return;
                }
                
                 UnityEngine.Debug.Log("[DungeonSaveLoad] 🔍 Starting dungeon comparison...");
                
                var currentDungeon = GameManager.Instance.Dungeon;
                var currentData = CreateSaveDataFromDungeon(currentDungeon);
                
                var comparisonResult = CompareDungeonData(currentData, lastSavedData);
                
                // Write detailed comparison to log file
                WriteComparisonLog(comparisonResult, currentData, lastSavedData);
                
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] 📊 Comparison complete: {comparisonResult.OverallMatch}");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Matches: Seed={comparisonResult.SeedMatch}, Size={comparisonResult.SizeMatch}, Rooms={comparisonResult.RoomsMatch}");
                 UnityEngine.Debug.Log($"[DungeonSaveLoad] Detailed log saved to: {ComparisonLogPath}");
                
                try
                {
                    string status = comparisonResult.OverallMatch ? "✅ Dungeons match!" : "❌ Dungeons differ";
                    GungeonTogether.UI.MultiplayerUIManager.ShowNotification(status, 4f);
                }
                catch { /* Ignore if UI not available */ }
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] ❌ Failed to compare dungeons: {ex.Message}");
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Stack trace: {ex.StackTrace}");
            }
        }
        
        #region Private Helper Methods
        
        private static DungeonSaveData CreateSaveDataFromDungeon(Dungeon dungeon)
        {
            var saveData = new DungeonSaveData
            {
                seed = dungeon.GetDungeonSeed(),
                tilesetId = dungeon.tileIndices?.tilesetId.ToString() ?? "Unknown",
                floorName = dungeon.DungeonFloorName ?? "Unknown",
                width = dungeon.Width,
                height = dungeon.Height,
                dungeonShortName = dungeon.DungeonShortName ?? "Unknown",
                saveTime = DateTime.Now,
                gameVersion = Application.version,
                rooms = new List<DungeonSaveData.RoomSaveData>()
            };
            
            // Save cell data
            if (dungeon.data?.cellData != null)
            {
                saveData.cellData = new DungeonSaveData.CellDataSave[dungeon.Width][];
                for (int x = 0; x < dungeon.Width; x++)
                {
                    saveData.cellData[x] = new DungeonSaveData.CellDataSave[dungeon.Height];
                    for (int y = 0; y < dungeon.Height; y++)
                    {
                        saveData.cellData[x][y] = DungeonSaveData.CellDataSave.FromCellData(dungeon.data.cellData[x][y]);
                    }
                }
            }
            
            // Save room data
            if (dungeon.data != null && dungeon.data.rooms != null)
            {
                // Get all rooms from the dungeon data
                foreach (var room in dungeon.data.rooms)
                {
                    if (room != null)
                    {
                        var roomData = DungeonSaveData.RoomSaveData.FromRoomHandler(room);
                        if (roomData != null)
                        {
                            saveData.rooms.Add(roomData);
                        }
                    }
                }
            }
            
            // Try to get player start position
            if (GameManager.Instance.PrimaryPlayer != null)
            {
                saveData.playerStartPosition = GameManager.Instance.PrimaryPlayer.transform.position;
            }
            
            return saveData;
        }
        
        private static void VerifyDungeonConsistency(DungeonSaveData saveData)
        {
            try
            {
                if (GameManager.Instance?.Dungeon == null)
                {
                     UnityEngine.Debug.LogWarning("[DungeonSaveLoad] Cannot verify - no current dungeon");
                    return;
                }
                
                var currentDungeon = GameManager.Instance.Dungeon;
                bool consistent = true;
                
                // Check basic properties
                if (currentDungeon.GetDungeonSeed() != saveData.seed)
                {
                     UnityEngine.Debug.LogWarning($"[DungeonSaveLoad] ⚠️ Seed mismatch: Current={currentDungeon.GetDungeonSeed()}, Saved={saveData.seed}");
                    consistent = false;
                }
                
                if (currentDungeon.Width != saveData.width || currentDungeon.Height != saveData.height)
                {
                     UnityEngine.Debug.LogWarning($"[DungeonSaveLoad] ⚠️ Size mismatch: Current={currentDungeon.Width}x{currentDungeon.Height}, Saved={saveData.width}x{saveData.height}");
                    consistent = false;
                }
                
                if (consistent)
                {
                     UnityEngine.Debug.Log("[DungeonSaveLoad] ✅ Basic dungeon properties are consistent");
                }
                else
                {
                     UnityEngine.Debug.LogWarning("[DungeonSaveLoad] ⚠️ Dungeon inconsistencies detected - this may indicate determinism issues");
                }
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Error during verification: {ex.Message}");
            }
        }
        
        private static ComparisonResult CompareDungeonData(DungeonSaveData current, DungeonSaveData saved)
        {
            var result = new ComparisonResult();
            
            // Compare basic properties
            result.SeedMatch = current.seed == saved.seed;
            result.SizeMatch = current.width == saved.width && current.height == saved.height;
            result.FloorMatch = current.floorName == saved.floorName;
            result.TilesetMatch = current.tilesetId == saved.tilesetId;
            
            // Compare rooms
            result.RoomsMatch = current.rooms?.Count == saved.rooms?.Count;
            if (result.RoomsMatch && current.rooms != null && saved.rooms != null)
            {
                for (int i = 0; i < current.rooms.Count && result.RoomsMatch; i++)
                {
                    var currentRoom = current.rooms[i];
                    var savedRoom = saved.rooms[i];
                    
                    result.RoomsMatch = currentRoom.roomName == savedRoom.roomName &&
                                       currentRoom.position == savedRoom.position &&
                                       currentRoom.dimensions == savedRoom.dimensions;
                }
            }
            
            // Compare cell data (sample only to avoid performance issues)
            result.CellDataMatch = CompareCellDataSample(current, saved);
            
            result.OverallMatch = result.SeedMatch && result.SizeMatch && result.FloorMatch && 
                                 result.TilesetMatch && result.RoomsMatch && result.CellDataMatch;
            
            return result;
        }
        
        private static bool CompareCellDataSample(DungeonSaveData current, DungeonSaveData saved)
        {
            if (current.cellData == null || saved.cellData == null)
                return current.cellData == saved.cellData;
            
            if (current.width != saved.width || current.height != saved.height)
                return false;
            
            // Sample 100 random cells for comparison (to avoid performance issues)
            var random = new System.Random(12345); // Fixed seed for consistent sampling
            int sampleCount = Math.Min(100, current.width * current.height);
            
            for (int i = 0; i < sampleCount; i++)
            {
                int x = random.Next(current.width);
                int y = random.Next(current.height);
                
                var currentCell = current.cellData[x][y];
                var savedCell = saved.cellData[x][y];
                
                if (currentCell == null && savedCell == null) continue;
                if (currentCell == null || savedCell == null) return false;
                
                if (!savedCell.IsEquivalentTo(null)) // We need to create a CellData equivalent for comparison
                {
                    // For now, just compare basic properties
                    if (currentCell.type != savedCell.type ||
                        currentCell.breakable != savedCell.breakable ||
                        currentCell.isOccupied != savedCell.isOccupied)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        private static void WriteComparisonLog(ComparisonResult result, DungeonSaveData current, DungeonSaveData saved)
        {
            try
            {
                var log = new System.Text.StringBuilder();
                log.AppendLine("=== GUNGEON TOGETHER DUNGEON COMPARISON LOG ===");
                log.AppendLine($"Comparison Time: {DateTime.Now}");
                log.AppendLine($"Overall Match: {result.OverallMatch}");
                log.AppendLine();
                
                log.AppendLine("BASIC PROPERTIES:");
                log.AppendLine($"  Seed Match: {result.SeedMatch} (Current: {current.seed}, Saved: {saved.seed})");
                log.AppendLine($"  Size Match: {result.SizeMatch} (Current: {current.width}x{current.height}, Saved: {saved.width}x{saved.height})");
                log.AppendLine($"  Floor Match: {result.FloorMatch} (Current: {current.floorName}, Saved: {saved.floorName})");
                log.AppendLine($"  Tileset Match: {result.TilesetMatch} (Current: {current.tilesetId}, Saved: {saved.tilesetId})");
                log.AppendLine();
                
                log.AppendLine("ROOMS:");
                log.AppendLine($"  Rooms Match: {result.RoomsMatch}");
                log.AppendLine($"  Current Room Count: {current.rooms?.Count ?? 0}");
                log.AppendLine($"  Saved Room Count: {saved.rooms?.Count ?? 0}");
                log.AppendLine();
                
                log.AppendLine("CELL DATA:");
                log.AppendLine($"  Cell Data Match (sample): {result.CellDataMatch}");
                log.AppendLine();
                
                if (!result.OverallMatch)
                {
                    log.AppendLine("DETERMINISM ANALYSIS:");
                    log.AppendLine("If dungeons don't match, this could indicate:");
                    log.AppendLine("  • Random number generation inconsistencies");
                    log.AppendLine("  • Network sync issues in multiplayer");
                    log.AppendLine("  • Mod interference with dungeon generation");
                    log.AppendLine("  • Timing-dependent generation code");
                }
                
                File.WriteAllText(ComparisonLogPath, log.ToString());
            }
            catch (Exception ex)
            {
                 UnityEngine.Debug.LogError($"[DungeonSaveLoad] Failed to write comparison log: {ex.Message}");
            }
        }
        
        private class ComparisonResult
        {
            public bool SeedMatch { get; set; }
            public bool SizeMatch { get; set; }
            public bool FloorMatch { get; set; }
            public bool TilesetMatch { get; set; }
            public bool RoomsMatch { get; set; }
            public bool CellDataMatch { get; set; }
            public bool OverallMatch { get; set; }
        }
        
        #endregion
    }
}
