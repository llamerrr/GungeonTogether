using UnityEngine;
using System.Collections.Generic;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Spawns invisible dummy enemies on clients to prevent rooms from auto-clearing.
    /// The dummies are destroyed when the host sends a room clear signal.
    /// </summary>
    public class ClientRoomStateManager
    {
        private static ClientRoomStateManager _instance;
        public static ClientRoomStateManager Instance => _instance ??= new ClientRoomStateManager();

        private readonly Dictionary<Vector2, List<GameObject>> _roomDummies = new Dictionary<Vector2, List<GameObject>>();
        private bool _isClient;
        private Vector2 _lastPlayerRoom = Vector2.zero;
        private float _lastRoomCheckTime;

        public void Initialize(bool isClient)
        {
            _isClient = isClient;
            if (_isClient)
            {
                GungeonTogether.Logging.Debug.Log("[ClientRoomStateManager] Initialized for client - will spawn dummy enemies");
            }
        }

        public void Update()
        {
            if (!_isClient) return;
            
            // Check for room changes every 0.5 seconds
            if (Time.time - _lastRoomCheckTime < 0.5f) return;
            _lastRoomCheckTime = Time.time;
            
            CheckForRoomChange();
        }

        private void CheckForRoomChange()
        {
            try
            {
                var player = GameManager.Instance?.PrimaryPlayer;
                if (player == null) return;

                var currentRoom = player.CurrentRoom;
                if (currentRoom == null) return;

                // Get room position
                var roomPos = new Vector2(currentRoom.area.basePosition.x, currentRoom.area.basePosition.y);
                
                if (roomPos != _lastPlayerRoom)
                {
                    _lastPlayerRoom = roomPos;
                    OnRoomEntered(roomPos);
                }
            }
            catch (System.Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ClientRoomStateManager] Error checking room change: {e.Message}");
            }
        }

        public void OnRoomEntered(Vector2 roomPosition)
        {
            if (!_isClient) return;

            // Only spawn dummies if this room doesn't already have them
            if (_roomDummies.ContainsKey(roomPosition)) return;

            // Spawn invisible dummy enemies to prevent room from being "cleared"
            SpawnDummyEnemies(roomPosition);
        }

        public void OnHostRoomCleared(Vector2 roomPosition)
        {
            if (!_isClient) return;

            // Remove dummy enemies for this room
            if (_roomDummies.TryGetValue(roomPosition, out var dummies))
            {
                foreach (var dummy in dummies)
                {
                    if (dummy != null)
                    {
                        Object.Destroy(dummy);
                    }
                }
                _roomDummies.Remove(roomPosition);
                GungeonTogether.Logging.Debug.Log($"[ClientRoomStateManager] Cleared room {roomPosition} - removed {dummies.Count} dummy enemies");
            }
        }

        private void SpawnDummyEnemies(Vector2 roomPosition)
        {
            try
            {
                var dummies = new List<GameObject>();

                // Create 1-2 invisible dummy enemies
                for (int i = 0; i < 2; i++)
                {
                    var dummy = new GameObject($"DummyEnemy_{roomPosition.x}_{roomPosition.y}_{i}");
                    
                    // Add AIActor component to make it count as an enemy
                    var aiActor = dummy.AddComponent<AIActor>();
                    
                    // Make it invisible and non-interactive
                    var renderer = dummy.AddComponent<SpriteRenderer>();
                    renderer.color = Color.clear; // Invisible
                    
                    // Position it off-screen or in a corner
                    dummy.transform.position = new Vector3(roomPosition.x * 20 + i, roomPosition.y * 20, 0);
                    
                    // Make sure it has health so it counts as "alive"
                    var healthHaver = dummy.AddComponent<HealthHaver>();
                    healthHaver.SetHealthMaximum(1f);
                    
                    // Disable AI behavior and movement
                    if (aiActor.behaviorSpeculator != null)
                    {
                        aiActor.behaviorSpeculator.enabled = false;
                    }
                    
                    dummies.Add(dummy);
                }

                _roomDummies[roomPosition] = dummies;
                GungeonTogether.Logging.Debug.Log($"[ClientRoomStateManager] Spawned {dummies.Count} dummy enemies for room {roomPosition}");
            }
            catch (System.Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ClientRoomStateManager] Failed to spawn dummy enemies: {e.Message}");
            }
        }

        public void Reset()
        {
            // Clean up all dummy enemies
            foreach (var roomDummies in _roomDummies.Values)
            {
                foreach (var dummy in roomDummies)
                {
                    if (dummy != null)
                    {
                        Object.Destroy(dummy);
                    }
                }
            }
            _roomDummies.Clear();
            GungeonTogether.Logging.Debug.Log("[ClientRoomStateManager] Reset - cleared all dummy enemies");
        }
    }
}
