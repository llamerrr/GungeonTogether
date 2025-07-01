using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeonator;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages dungeon generation and synchronization across multiplayer sessions.
    /// The server generates the dungeon and sends the data to all clients to ensure
    /// everyone has the same map layout.
    /// </summary>
    public class NetworkedDungeonManager
    {
        private static NetworkedDungeonManager instance;
        public static NetworkedDungeonManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new NetworkedDungeonManager();
                }
                return instance;
            }
        }
        
        // Dungeon synchronization state
        private bool isServer;
        private Dictionary<string, DungeonData> cachedDungeonData;
        private int currentDungeonSeed;
        private string currentFloorName;
        private bool dungeonSyncInProgress;
        
        // Networking callback for dungeon data
        public System.Action<DungeonData> OnDungeonDataReceived;
        
        public struct DungeonData
        {
            public int seed;
            public string floorName;
            public Vector2[] roomPositions;
            public string[] roomTypes;
            public bool[] roomVisited;
            public Vector2 playerSpawnPosition;
            public Dictionary<string, object> roomExtraData;
        }
        
        private NetworkedDungeonManager()
        {
            cachedDungeonData = new Dictionary<string, DungeonData>();
            isServer = false;
            dungeonSyncInProgress = false;
            UnityEngine.Debug.Log("[DungeonSync] NetworkedDungeonManager initialized");
        }
        
        /// <summary>
        /// Initialize as server or client
        /// </summary>
        public void Initialize(bool asServer)
        {
            isServer = asServer;
            UnityEngine.Debug.Log($"[DungeonSync] Initialized as {(isServer ? "SERVER" : "CLIENT")}");
            
            if (isServer)
            {
                // Hook into dungeon generation to capture and send data
                HookDungeonGeneration();
            }
        }
        
        /// <summary>
        /// Hook into ETG's dungeon generation system
        /// </summary>
        private void HookDungeonGeneration()
        {
            try
            {
                // We need to hook into the GameManager's dungeon generation
                UnityEngine.Debug.Log("[DungeonSync] Hooking into dungeon generation system...");
                
                // For now, we'll use the DungeonGenerationHook system
                DungeonGenerationHook.OnDungeonGenerated += OnServerDungeonGenerated;
                DungeonGenerationHook.OnRoomGenerated += OnServerRoomGenerated;
                
                UnityEngine.Debug.Log("[DungeonSync] Dungeon generation hooks registered");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] Failed to hook dungeon generation: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called when the server generates a new dungeon
        /// </summary>
        private void OnServerDungeonGenerated(Dungeon dungeon)
        {
            if (!isServer) return;
            
            try
            {
                UnityEngine.Debug.Log("[DungeonSync] SERVER: Capturing dungeon generation data...");
                
                var dungeonData = CaptureDungeonData(dungeon);
                
                // Cache the data
                string dungeonKey = $"{dungeonData.floorName}_{dungeonData.seed}";
                cachedDungeonData[dungeonKey] = dungeonData;
                
                // Send to all clients
                BroadcastDungeonData(dungeonData);
                
                UnityEngine.Debug.Log($"[DungeonSync] SERVER: Dungeon data captured and sent to clients (seed: {dungeonData.seed})");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] SERVER: Error capturing dungeon data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called when the server generates a new room
        /// </summary>
        private void OnServerRoomGenerated(RoomHandler room)
        {
            if (!isServer) return;
            
            try
            {
                // Send room-specific data to clients for progressive loading
                BroadcastRoomData(room);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] SERVER: Error broadcasting room data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Capture dungeon data from the generated dungeon
        /// </summary>
        private DungeonData CaptureDungeonData(Dungeon dungeon)
        {
            var data = new DungeonData();
            
            try
            {
                // Get basic dungeon info
                data.seed = GameManager.Instance.CurrentRunSeed;
                data.floorName = "Unknown"; // Initialize with default value
                
                // Try to get the current floor name safely
                try
                {
                    // Try multiple approaches to get floor name
                    if (GameManager.Instance.Dungeon != null)
                    {
                        // Method 1: Try DungeonFloorName property
                        try
                        {
                            var floorNameProperty = GameManager.Instance.Dungeon.GetType().GetProperty("DungeonFloorName");
                            if (floorNameProperty != null)
                            {
                                var floorName = floorNameProperty.GetValue(GameManager.Instance.Dungeon, null) as string;
                                if (!string.IsNullOrEmpty(floorName))
                                {
                                    data.floorName = floorName;
                                }
                            }
                        }
                        catch (Exception) { /* Ignore and try next method */ }
                        
                        // Method 2: Try to get from reflection on dungeon type
                        if (string.IsNullOrEmpty(data.floorName) || data.floorName == "Unknown")
                        {
                            try
                            {
                                // Try to find any flow-related property
                                var dungeonType = GameManager.Instance.Dungeon.GetType();
                                var flowProperty = dungeonType.GetProperty("tileIndices") ?? 
                                                  dungeonType.GetProperty("dungeonMaterial") ??
                                                  dungeonType.GetProperty("data");
                                
                                if (flowProperty != null)
                                {
                                    var flowValue = flowProperty.GetValue(GameManager.Instance.Dungeon, null);
                                    if (flowValue != null)
                                    {
                                        data.floorName = $"Dungeon_{flowValue.GetHashCode()}";
                                    }
                                }
                            }
                            catch (Exception) { /* Ignore and try next method */ }
                        }
                    }
                    
                    // Method 3: Use current floor number as fallback
                    if (string.IsNullOrEmpty(data.floorName) || data.floorName == "Unknown")
                    {
                        try
                        {
                            int currentFloor = GameManager.Instance.CurrentFloor;
                            data.floorName = $"Floor_{currentFloor}";
                        }
                        catch (Exception)
                        {
                            data.floorName = "Unknown";
                        }
                    }
                }
                catch (Exception e)
                {
                    data.floorName = "Unknown";
                    UnityEngine.Debug.LogWarning($"[DungeonSync] Could not get floor name: {e.Message}");
                }
                
                // Get room data
                var roomPositions = new List<Vector2>();
                var roomTypes = new List<string>();
                var roomVisited = new List<bool>();
                
                if (dungeon.data != null && dungeon.data.rooms != null)
                {
                    foreach (var room in dungeon.data.rooms)
                    {
                        if (room != null)
                        {
                            roomPositions.Add(new Vector2(room.area.basePosition.x, room.area.basePosition.y));
                            
                            // Try to get room type safely
                            string roomType = "Unknown";
                            try
                            {
                                // Method 1: Try PrototypeRoomCategory from area
                                if (room.area != null && room.area.PrototypeRoomCategory != null)
                                {
                                    roomType = room.area.PrototypeRoomCategory.ToString();
                                }
                                // Method 2: Try category from prototype room
                                else if (room.area != null && room.area.prototypeRoom != null)
                                {
                                    var categoryProperty = room.area.prototypeRoom.GetType().GetProperty("category");
                                    if (categoryProperty != null)
                                    {
                                        var categoryValue = categoryProperty.GetValue(room.area.prototypeRoom, null);
                                        if (categoryValue != null)
                                        {
                                            roomType = categoryValue.ToString();
                                        }
                                    }
                                }
                                // Method 3: Fallback determination
                                else
                                {
                                    roomType = DetermineRoomType(room);
                                }
                            }
                            catch (Exception e)
                            {
                                roomType = DetermineRoomType(room); // Final fallback
                                UnityEngine.Debug.LogWarning($"[DungeonSync] Could not get room type, using fallback: {e.Message}");
                            }
                            
                            roomTypes.Add(roomType);
                            roomVisited.Add(room.visibility == RoomHandler.VisibilityStatus.VISITED);
                        }
                    }
                }
                
                data.roomPositions = roomPositions.ToArray();
                data.roomTypes = roomTypes.ToArray();
                data.roomVisited = roomVisited.ToArray();
                
                // Get player spawn position
                if (GameManager.Instance.PrimaryPlayer != null)
                {
                    data.playerSpawnPosition = GameManager.Instance.PrimaryPlayer.transform.position;
                }
                else
                {
                    data.playerSpawnPosition = Vector2.zero;
                }
                
                // Extra data for specific room types, power-ups, etc.
                data.roomExtraData = new Dictionary<string, object>();
                
                UnityEngine.Debug.Log($"[DungeonSync] Captured dungeon data: {data.roomPositions.Length} rooms, seed {data.seed}");
                
                return data;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] Error capturing dungeon data: {e.Message}");
                return data; // Return partial data
            }
        }
        
        /// <summary>
        /// Send dungeon data to all connected clients
        /// </summary>
        private void BroadcastDungeonData(DungeonData data)
        {
            try
            {
                UnityEngine.Debug.Log($"[DungeonSync] SERVER: Broadcasting dungeon data to clients...");
                
                // TODO: Implement actual networking to send data
                // For now, we'll serialize the data and log it
                string serializedData = SerializeDungeonData(data);
                UnityEngine.Debug.Log($"[DungeonSync] SERVER: Serialized dungeon data ({serializedData.Length} chars)");
                
                // In a real implementation, this would send over Steam P2P:
                // steamNetworking.SendToAll("DUNGEON_DATA", serializedData);
                
                // For testing purposes, simulate receiving the data
                if (UnityEngine.Input.GetKeyDown(KeyCode.F11))
                {
                    OnDungeonDataReceived?.Invoke(data);
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] SERVER: Error broadcasting dungeon data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send room data to all connected clients
        /// </summary>
        private void BroadcastRoomData(RoomHandler room)
        {
            try
            {
                // TODO: Send room-specific data for progressive loading
                UnityEngine.Debug.Log($"[DungeonSync] SERVER: Broadcasting room data for room at {room.area.basePosition}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] SERVER: Error broadcasting room data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle dungeon data received from server (client-side)
        /// </summary>
        public void OnReceiveDungeonData(DungeonData data)
        {
            if (isServer) return; // Servers don't receive their own data
            
            try
            {
                UnityEngine.Debug.Log($"[DungeonSync] CLIENT: Received dungeon data from server (seed: {data.seed})");
                
                // Cache the received data
                string dungeonKey = $"{data.floorName}_{data.seed}";
                cachedDungeonData[dungeonKey] = data;
                
                // Override local dungeon generation with server data
                ApplyServerDungeonData(data);
                
                UnityEngine.Debug.Log("[DungeonSync] CLIENT: Applied server dungeon data successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] CLIENT: Error applying server dungeon data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Apply server dungeon data to local game state
        /// </summary>
        private void ApplyServerDungeonData(DungeonData data)
        {
            try
            {
                dungeonSyncInProgress = true;
                
                // Force the game to use the server's seed
                UnityEngine.Debug.Log($"[DungeonSync] CLIENT: Overriding local seed with server seed: {data.seed}");
                
                // This is the critical part - we need to set the seed BEFORE dungeon generation
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.CurrentRunSeed = data.seed;
                    UnityEngine.Random.InitState(data.seed);
                    
                    // Also ensure BraveRandom uses the same seed
                    try
                    {
                        BraveRandom.InitializeWithSeed(data.seed);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[DungeonSync] Could not initialize BraveRandom: {e.Message}");
                    }
                }
                
                currentDungeonSeed = data.seed;
                currentFloorName = data.floorName;
                
                UnityEngine.Debug.Log($"[DungeonSync] CLIENT: Seed synchronized. Local generation should now match server.");
                
                dungeonSyncInProgress = false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] CLIENT: Error applying server dungeon data: {e.Message}");
                dungeonSyncInProgress = false;
            }
        }
        
        /// <summary>
        /// Check if we should override dungeon generation (client-side)
        /// </summary>
        public bool ShouldOverrideGeneration()
        {
            return !isServer && dungeonSyncInProgress;
        }
        
        /// <summary>
        /// Get the synchronized seed for dungeon generation
        /// </summary>
        public int GetSynchronizedSeed()
        {
            return currentDungeonSeed;
        }
        
        /// <summary>
        /// Serialize dungeon data for network transmission
        /// </summary>
        private string SerializeDungeonData(DungeonData data)
        {
            try
            {
                // Simple JSON-like serialization (in a real implementation, use proper serialization)
                var serialized = $"{{\"seed\":{data.seed},\"floor\":\"{data.floorName}\",\"rooms\":{data.roomPositions.Length}}}";
                return serialized;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] Error serializing dungeon data: {e.Message}");
                return "{}";
            }
        }
        
        /// <summary>
        /// Check if current dungeon generation should be intercepted
        /// This is called during dungeon generation to ensure sync
        /// </summary>
        public bool InterceptDungeonGeneration()
        {
            try
            {
                if (isServer)
                {
                    // Server generates normally but captures data
                    return false;
                }
                else
                {
                    // Client should wait for server data or use synchronized seed
                    if (currentDungeonSeed != 0)
                    {
                        UnityEngine.Debug.Log($"[DungeonSync] CLIENT: Using synchronized seed {currentDungeonSeed} for generation");
                        GameManager.Instance.CurrentRunSeed = currentDungeonSeed;
                        return false; // Allow generation with correct seed
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[DungeonSync] CLIENT: Waiting for server dungeon data...");
                        return true; // Block generation until we have server data
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] Error in InterceptDungeonGeneration: {e.Message}");
                return false; // Allow generation to continue on error
            }
        }
        
        /// <summary>
        /// Cleanup when session ends
        /// </summary>
        public void Cleanup()
        {
            try
            {
                cachedDungeonData.Clear();
                currentDungeonSeed = 0;
                currentFloorName = null;
                dungeonSyncInProgress = false;
                
                // Unhook events
                DungeonGenerationHook.OnDungeonGenerated -= OnServerDungeonGenerated;
                DungeonGenerationHook.OnRoomGenerated -= OnServerRoomGenerated;
                
                UnityEngine.Debug.Log("[DungeonSync] NetworkedDungeonManager cleaned up");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[DungeonSync] Error during cleanup: {e.Message}");
            }
        }
        
        /// <summary>
        /// Helper method to determine room type when category is not directly accessible
        /// </summary>
        private string DetermineRoomType(RoomHandler room)
        {
            try
            {
                // Try to determine room type by examining room properties
                if (room.area != null)
                {
                    // Check if it's a boss room
                    if (room.area.IsProceduralRoom == false && room.area.runtimePrototypeData != null)
                    {
                        var prototypeData = room.area.runtimePrototypeData;
                        if (prototypeData.roomEvents != null)
                        {
                            // Check for boss room indicators
                            foreach (var roomEvent in prototypeData.roomEvents)
                            {
                                if (roomEvent != null && roomEvent.action != null)
                                {
                                    string actionType = roomEvent.action.ToString().ToLower();
                                    if (actionType.Contains("boss"))
                                    {
                                        return "BOSS";
                                    }
                                }
                            }
                        }
                    }
                    
                    // Check room dimensions to guess type
                    int roomWidth = room.area.dimensions.x;
                    int roomHeight = room.area.dimensions.y;
                    
                    // Large rooms are likely boss rooms
                    if (roomWidth > 20 || roomHeight > 20)
                    {
                        return "BOSS";
                    }
                    // Very small rooms might be secret or special
                    else if (roomWidth < 8 && roomHeight < 8)
                    {
                        return "SECRET";
                    }
                    // Medium rooms are likely normal
                    else
                    {
                        return "NORMAL";
                    }
                }
                
                return "UNKNOWN";
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[DungeonSync] Error determining room type: {e.Message}");
                return "ERROR";
            }
        }
    }
}
