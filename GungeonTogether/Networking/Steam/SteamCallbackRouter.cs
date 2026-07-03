using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GungeonTogether.Networking.Steam
{
    internal static class SteamCallbackRouter
    {
        private static readonly List<Action<object>> _handlers = new List<Action<object>>();
        private static readonly List<object> _callbackObjects = new List<object>(); // Prevent GC

        public static void Clear()
        {
            GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Clearing " + _handlers.Count + " handler(s) and " + _callbackObjects.Count + " callback object(s)");
            _handlers.Clear();
            _callbackObjects.Clear();
        }

        public static void Invoke(int handlerIndex, object callbackData)
        {
            if (callbackData != null)
                GungeonTogether.Systems.Logging.Debug.Log($"[SteamCallback] Invoke called with handlerIndex={handlerIndex}, dataType={callbackData.GetType().Name}");
            if (handlerIndex < 0 || handlerIndex >= _handlers.Count)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning($"[SteamCallback] Invalid handler index {handlerIndex} (count={_handlers.Count})");
                return;
            }
            _handlers[handlerIndex]?.Invoke(callbackData);
        }

        public static object CreateCallback(Assembly steamworksAssembly, Type callbackStructType, Action<object> handler)
        {
            if (steamworksAssembly == null || callbackStructType == null || handler == null)
                return null;

            Type callbackGenericDef = steamworksAssembly.GetType("Steamworks.Callback`1", false);
            if (callbackGenericDef == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Callback`1 type not found");
                return null;
            }

            Type callbackClosedType = callbackGenericDef.MakeGenericType(callbackStructType);

            // Find the static Create method with a single parameter (the delegate)
            MethodInfo createMethod = null;
            foreach (var m in callbackClosedType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "Create") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType.IsSubclassOf(typeof(Delegate)))
                {
                    createMethod = m;
                    break;
                }
            }
            // If not found, try with two parameters (delegate, bool)
            if (createMethod == null)
            {
                foreach (var m in callbackClosedType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "Create") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[0].ParameterType.IsSubclassOf(typeof(Delegate)) && ps[1].ParameterType == typeof(bool))
                    {
                        createMethod = m;
                        break;
                    }
                }
            }

            if (createMethod == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning($"[SteamCallback] No Create method found for {callbackStructType.Name}");
                return null;
            }

            Type delegateType = createMethod.GetParameters()[0].ParameterType;
            int handlerIndex = _handlers.Count;
            _handlers.Add(handler);

            // Create a dynamic method that matches the delegate signature
            DynamicMethod dm = new DynamicMethod(
                "GT_Callback_" + callbackStructType.Name,
                typeof(void),
                new Type[] { callbackStructType },
                typeof(SteamCallbackRouter).Module,
                skipVisibility: true);

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, handlerIndex);
            il.Emit(OpCodes.Ldarg_0);
            if (callbackStructType.IsValueType)
                il.Emit(OpCodes.Box, callbackStructType);
            il.Emit(OpCodes.Call, typeof(SteamCallbackRouter).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Static));
            il.Emit(OpCodes.Ret);

            Delegate del = dm.CreateDelegate(delegateType);
            object callbackObj;
            if (createMethod.GetParameters().Length == 1)
                callbackObj = createMethod.Invoke(null, new object[] { del });
            else
                callbackObj = createMethod.Invoke(null, new object[] { del, true });

            if (callbackObj != null)
            {
                _callbackObjects.Add(callbackObj);
                GungeonTogether.Systems.Logging.Debug.Log($"[SteamCallback] Successfully created callback for {callbackStructType.Name} (stored, total {_callbackObjects.Count})");
            }
            else
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning($"[SteamCallback] Callback creation returned null for {callbackStructType.Name}");
            }

            return callbackObj;
        }
    }
}