using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Last-resort client side enemy suppression in case spawn patches miss some creation paths.
    /// Host is authoritative; any locally created AIActor on a joiner client is destroyed.
    /// </summary>
    internal static class ClientEnemySuppressor
    {
        private static float _lastScan;
        private const float SCAN_INTERVAL = 0.4f; // seconds
        private static Type _aiActorType;
        private static System.Reflection.PropertyInfo _isNormalEnemyProp;
        private static bool _resolvedTypes;
        private static int _destroyedThisRoom;
        private static float _lastLogTime;

        private static void ResolveTypes()
        {
            if (_resolvedTypes) return;
            _aiActorType = typeof(AIActor); // direct reference available via ETG assembly refs
            if (_aiActorType != null)
            {
                _isNormalEnemyProp = _aiActorType.GetProperty("IsNormalEnemy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            }
            _resolvedTypes = true;
        }

        public static void Update(bool isClientActive)
        {
            if (!isClientActive) { _destroyedThisRoom = 0; return; }
            if (Time.time - _lastScan < SCAN_INTERVAL) return;
            _lastScan = Time.time;
            try
            {
                ResolveTypes();
                // Find all AIActor components
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                int destroyed = 0;
                for (int i = 0; i < allObjects.Length; i++)
                {
                    var go = allObjects[i];
                    if (go == null || !go.activeInHierarchy) continue;
                    var ai = go.GetComponent<AIActor>();
                    if (ai == null) continue;
                    bool isNormal = true;
                    try
                    {
                        if (_isNormalEnemyProp != null)
                        {
                            var val = _isNormalEnemyProp.GetValue(ai, null);
                            if (val is bool) isNormal = (bool)val;
                        }
                    }
                    catch { }
                    if (!isNormal) continue; // leave NPCs / special actors
                    try { UnityEngine.Object.Destroy(ai.gameObject); destroyed++; } catch { }
                }
                _destroyedThisRoom += destroyed;
                if (destroyed > 0 && Time.time - _lastLogTime > 2f)
                {
                    _lastLogTime = Time.time;
                    GungeonTogether.Logging.Debug.Log("[ClientEnemySuppressor] Destroyed " + destroyed + " stray enemies (total this room: " + _destroyedThisRoom + ")");
                }
            }
            catch (Exception e)
            {
                if (Time.time - _lastLogTime > 5f)
                {
                    _lastLogTime = Time.time;
                    GungeonTogether.Logging.Debug.LogError("[ClientEnemySuppressor] Error: " + e.Message);
                }
            }
        }
    }
}
