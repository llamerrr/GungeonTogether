using HarmonyLib;
using UnityEngine;
using GungeonTogether.Game;
using System;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Patches the PickupObject.Pickup method to broadcast pickup events.
    /// </summary>
    // Manual patch applied from GungeonTogetherMod after PatchAll to avoid PatchAll IL issues
    internal static class ItemPickupPatch
    {
    public static System.Reflection.MethodBase FindTargetMethod()
        {
            try
            {
                var pickupType = Type.GetType("PickupObject, Assembly-CSharp");
                if (pickupType == null) return null;
                var methods = pickupType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m.Name == "Pickup")
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 1) return m; // typical signature: bool Pickup(PlayerController player)
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[ItemPickupPatch] TargetMethod failed: " + e.Message);
            }
            return null;
        }

    public static void Postfix(object __instance)
        {
            try
            {
                if (__instance is UnityEngine.Object uObj)
                {
                    var go = (uObj as Component)?.gameObject;
                    if (go != null)
                        ItemSynchronizer.Instance.NotifyLocalPickup(go);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ItemPickupPatch] Error in Postfix: {e.Message}");
            }
        }
    }
}
