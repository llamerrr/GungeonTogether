using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using GungeonTogether.Systems.Logging;

namespace GungeonTogether.Networking
{
    public static class ETGReflectionHelper
    {
        private static Assembly _gameAssembly;
        private static Type _gameManagerType;
        private static Type _roomHandlerType;
        private static Type _enemyControllerType;

        // GameManager
        private static PropertyInfo _gameManagerInstanceProperty;
        private static PropertyInfo _currentFloorProperty;
        private static PropertyInfo _currentRoomHandlerProperty;
        private static PropertyInfo _isFoyerProperty;
        private static MethodInfo _goToFoyerMethod;
        private static MethodInfo _loadFloorMethod;
        private static PropertyInfo _primaryPlayerProperty;

        // RoomHandler
        private static FieldInfo _activeEnemiesField;

        // EnemyController
        private static PropertyInfo _healthProperty;
        private static FieldInfo _aiStateField;
        private static FieldInfo _isDeadField;

        private static bool _initialised;

        public static void Initialise()
        {
            if (_initialised) return;

            try
            {
                // ETG's game assembly is "Assembly-CSharp"
                _gameAssembly = Assembly.Load("Assembly-CSharp");
                if (_gameAssembly == null)
                {
                    UnityEngine.Debug.LogError("[ETGReflection] Could not load Assembly-CSharp");
                    return;
                }

                _gameManagerType = _gameAssembly.GetType("GameManager");
                _roomHandlerType = _gameAssembly.GetType("RoomHandler");
                _enemyControllerType = _gameAssembly.GetType("EnemyController");

                if (_gameManagerType != null)
                {
                    // Singleton instance
                    _gameManagerInstanceProperty = _gameManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (_gameManagerInstanceProperty == null)
                    {
                        var method = _gameManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                        if (method != null)
                            _gameManagerInstanceProperty = _gameManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static); // not needed
                        // We'll handle fallback in GetGameManager()
                    }

                    _currentFloorProperty = _gameManagerType.GetProperty("CurrentFloor", BindingFlags.Public | BindingFlags.Instance);
                    _currentRoomHandlerProperty = _gameManagerType.GetProperty("CurrentRoomHandler", BindingFlags.Public | BindingFlags.Instance);
                    _isFoyerProperty = _gameManagerType.GetProperty("IsFoyer", BindingFlags.Public | BindingFlags.Instance);
                    _goToFoyerMethod = _gameManagerType.GetMethod("GoToFoyer", BindingFlags.Public | BindingFlags.Instance);
                    _loadFloorMethod = _gameManagerType.GetMethod("LoadFloor", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    _primaryPlayerProperty = _gameManagerType.GetProperty("PrimaryPlayer", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_roomHandlerType != null)
                {
                    _activeEnemiesField = _roomHandlerType.GetField("activeEnemies", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_enemyControllerType != null)
                {
                    _healthProperty = _enemyControllerType.GetProperty("Health", BindingFlags.Public | BindingFlags.Instance);
                    _aiStateField = _enemyControllerType.GetField("_aiState", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_aiStateField == null)
                        _aiStateField = _enemyControllerType.GetField("m_aiState", BindingFlags.NonPublic | BindingFlags.Instance);
                    _isDeadField = _enemyControllerType.GetField("IsDead", BindingFlags.Public | BindingFlags.Instance);
                    if (_isDeadField == null)
                        _isDeadField = _enemyControllerType.GetField("_dead", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                _initialised = true;
                UnityEngine.Debug.Log("[ETGReflection] Initialized successfully.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ETGReflection] Initialization failed: {e.Message}");
                UnityEngine.Debug.LogError(e.StackTrace);
            }
        }

        // ---- GameManager helpers ----

        public static object GetGameManager()
        {
            if (!_initialised || _gameManagerType == null) return null;
            try
            {
                if (_gameManagerInstanceProperty != null)
                    return _gameManagerInstanceProperty.GetValue(null, null);
                // Fallback: FindObjectOfType
                return UnityEngine.Object.FindObjectOfType(_gameManagerType);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsInFoyer()
        {
            var gm = GetGameManager();
            if (gm == null) return false;
            if (_isFoyerProperty != null)
                return (bool)_isFoyerProperty.GetValue(gm, null);
            // Fallback: check scene name
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Foyer";
        }

        public static int GetCurrentFloorIndex()
        {
            var gm = GetGameManager();
            if (gm == null) return -1;
            if (_currentFloorProperty != null)
            {
                var floor = _currentFloorProperty.GetValue(gm, null);
                if (floor != null)
                {
                    var idxProp = floor.GetType().GetProperty("Index", BindingFlags.Public | BindingFlags.Instance);
                    if (idxProp != null) return (int)idxProp.GetValue(floor, null);
                    // Try name
                    var nameProp = floor.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp != null) return nameProp.GetValue(floor, null).GetHashCode();
                }
            }
            return -1;
        }

        public static string GetCurrentRoomIdentifier()
        {
            var gm = GetGameManager();
            if (gm == null) return "";
            if (_currentRoomHandlerProperty != null)
            {
                var room = _currentRoomHandlerProperty.GetValue(gm, null);
                if (room != null)
                {
                    // RoomHandler might have a name or unique ID
                    var nameProp = room.GetType().GetProperty("RoomName", BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp != null)
                    {
                        var val = nameProp.GetValue(room, null);
                        if (val != null) return val.ToString();
                    }
                    // Fallback: use GetHashCode
                    return room.GetHashCode().ToString();
                }
            }
            return "";
        }

        public static Vector3 GetPlayerPosition()
        {
            var gm = GetGameManager();
            if (gm == null) return Vector3.zero;
            if (_primaryPlayerProperty != null)
            {
                var player = _primaryPlayerProperty.GetValue(gm, null);
                if (player != null)
                {
                    var transformProp = player.GetType().GetProperty("transform");
                    if (transformProp != null)
                    {
                        var t = transformProp.GetValue(player, null) as Transform;
                        if (t != null) return t.position;
                    }
                }
            }
            return Vector3.zero;
        }

        public static float GetPlayerRotation()
        {
            var gm = GetGameManager();
            if (gm == null) return 0f;
            if (_primaryPlayerProperty != null)
            {
                var player = _primaryPlayerProperty.GetValue(gm, null);
                if (player != null)
                {
                    var transformProp = player.GetType().GetProperty("transform");
                    if (transformProp != null)
                    {
                        var t = transformProp.GetValue(player, null) as Transform;
                        if (t != null) return t.eulerAngles.z;
                    }
                }
            }
            return 0f;
        }

        public static void TeleportToFoyer()
        {
            var gm = GetGameManager();
            if (gm == null || _goToFoyerMethod == null) return;
            _goToFoyerMethod.Invoke(gm, null);
        }

        public static void TeleportToFloor(int floorIndex)
        {
            var gm = GetGameManager();
            if (gm == null || _loadFloorMethod == null) return;
            _loadFloorMethod.Invoke(gm, new object[] { floorIndex });
        }

        public static void TeleportToPosition(Vector2 position, float rotation)
        {
            var gm = GetGameManager();
            if (gm == null) return;
            if (_primaryPlayerProperty != null)
            {
                var player = _primaryPlayerProperty.GetValue(gm, null);
                if (player != null)
                {
                    var transformProp = player.GetType().GetProperty("transform");
                    if (transformProp != null)
                    {
                        var t = transformProp.GetValue(player, null) as Transform;
                        if (t != null)
                        {
                            t.position = new Vector3(position.x, position.y, 0f);
                            t.rotation = Quaternion.Euler(0f, 0f, rotation);
                        }
                    }
                }
            }
        }

        // ---- Enemies ----

        public static object GetCurrentRoomHandler()
        {
            var gm = GetGameManager();
            if (gm == null || _currentRoomHandlerProperty == null) return null;
            return _currentRoomHandlerProperty.GetValue(gm, null);
        }

        public static List<object> GetActiveEnemies()
        {
            if (!_initialised || _activeEnemiesField == null) return new List<object>();
            var room = GetCurrentRoomHandler();
            if (room == null) return new List<object>();
            return _activeEnemiesField.GetValue(room) as List<object> ?? new List<object>();
        }

        public static int GetEnemyHealth(object enemy)
        {
            if (!_initialised || _healthProperty == null || enemy == null) return 0;
            return (int)_healthProperty.GetValue(enemy, null);
        }

        public static int GetEnemyAIState(object enemy)
        {
            if (!_initialised || _aiStateField == null || enemy == null) return 0;
            object val = _aiStateField.GetValue(enemy);
            return val != null ? (int)val : 0;
        }

        public static bool IsEnemyDead(object enemy)
        {
            if (!_initialised || _isDeadField == null || enemy == null) return false;
            return (bool)_isDeadField.GetValue(enemy);
        }

        public static Vector3 GetEnemyPosition(object enemy)
        {
            if (enemy == null) return Vector3.zero;
            var transformProp = enemy.GetType().GetProperty("transform");
            if (transformProp == null) return Vector3.zero;
            Transform t = transformProp.GetValue(enemy, null) as Transform;
            return t != null ? t.position : Vector3.zero;
        }

        public static float GetEnemyRotation(object enemy)
        {
            if (enemy == null) return 0f;
            var transformProp = enemy.GetType().GetProperty("transform");
            if (transformProp == null) return 0f;
            Transform t = transformProp.GetValue(enemy, null) as Transform;
            return t != null ? t.eulerAngles.z : 0f;
        }
    }
}