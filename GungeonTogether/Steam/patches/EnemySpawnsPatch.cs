using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using GungeonTogether.Steam;

namespace GungeonTogether.Game
{
    internal static class EnemySpawnBlocker
    {
        private static bool _loggedInit;
        private static bool IsClient => NetworkManager.Instance != null && !NetworkManager.Instance.IsHost();

        private static void LogInitOnce()
        {
            if (_loggedInit) return;
            _loggedInit = true;
            UnityEngine.Debug.Log("[EnemySpawnBlocker] Active: enemy creation will be blocked on JOINER client");
        }

        private static void LogBlocked(string where)
        {
            UnityEngine.Debug.Log("[EnemySpawnBlocker] Blocked spawn at " + where);
        }

        private static IEnumerator EmptyEnumerator() { yield break; }

        [HarmonyPatch]
        private static class Patch_AIActor_Spawn_Static
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                var list = new List<MethodBase>();
                try
                {
                    var t = typeof(AIActor);
                    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        var m = methods[i];
                        if (m.Name == "Spawn" && m.ReturnType == typeof(AIActor)) list.Add(m);
                    }
                }
                catch { }
                return list;
            }
            static void Postfix(AIActor __result)
            {
                try
                {
                    if (__result == null) return;
                    // Host: broadcast minimal spawn describing this enemy
                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost())
                    {
                        int enemyId = __result.GetInstanceID();
                        int typeHash = __result.GetType().FullName.GetHashCode();
                        float maxHealth = 0f;
                        try { if (__result.healthHaver != null) maxHealth = __result.healthHaver.GetMaxHealth(); } catch { }
                        // Use existing EnemySpawn packet (no separate minimal variant needed)
                        NetworkManager.Instance.SendEnemySpawn(enemyId, typeHash, (__result.transform.position), __result.transform.eulerAngles.z, maxHealth);
                    }
                }
                catch { }
            }
            static bool Prefix(ref AIActor __result, MethodBase __originalMethod)
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("AIActor." + __originalMethod.Name);
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyFactory), "SpawnWaveCR")]
        private static class Patch_EnemyFactory_SpawnWaveCR
        {
            static bool Prefix(ref IEnumerator __result)
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("EnemyFactory.SpawnWaveCR");
                __result = EmptyEnumerator();
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyFactory), "SpawnWave")]
        private static class Patch_EnemyFactory_SpawnWave
        {
            static bool Prefix()
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("EnemyFactory.SpawnWave");
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyFactory), "OnWaveCleared")]
        private static class Patch_EnemyFactory_OnWaveCleared
        {
            static bool Prefix()
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("EnemyFactory.OnWaveCleared");
                return false;
            }
        }

        [HarmonyPatch(typeof(AIActor), "Start")]
        private static class Patch_AIActor_Start
        {
            static bool Prefix(MonoBehaviour __instance)
            {
                if (!IsClient) return true;
                var ai = __instance as AIActor;
                if (ai != null)
                {
                    LogInitOnce();
                    LogBlocked("AIActor.Start pre-existing -> destroy");
                    try { UnityEngine.Object.Destroy(ai.gameObject); } catch { }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(EnemyFactory), "ProvideReward")]
        private static class Patch_EnemyFactory_ProvideReward
        {
            static bool Prefix()
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("EnemyFactory.ProvideReward");
                return false;
            }
        }

        [HarmonyPatch]
        private static class Patch_LootEngine_SpawnAny
        {
            static Type LootEngineType => Type.GetType("LootEngine, Assembly-CSharp");
            static IEnumerable<MethodBase> TargetMethods()
            {
                var list = new List<MethodBase>();
                try
                {
                    if (LootEngineType == null) return list;
                    var methods = LootEngineType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        var m = methods[i];
                        if (!m.Name.StartsWith("Spawn", StringComparison.Ordinal)) continue;
                        if (m.GetParameters().Length == 0) continue;
                        if (m.ReturnType == typeof(void) || m.ReturnType == typeof(GameObject)) list.Add(m);
                    }
                }
                catch { }
                return list;
            }
            static bool Prefix(MethodBase __originalMethod)
            {
                if (!IsClient) return true;
                LogInitOnce();
                LogBlocked("LootEngine." + __originalMethod.Name);
                return false;
            }
        }

    }
}
