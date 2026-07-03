using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GungeonTogether.Networking.Steam
{
    internal static class SteamCallbackRouter
    {
        private static readonly List<Action<object>> _handlers = new List<Action<object>>();

        public static void Clear()
        {
            GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Clearing " + _handlers.Count + " registered handler(s)");
            _handlers.Clear();
        }

        public static void Invoke(int handlerIndex, object callbackData)
        {
            GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Invoke called with handlerIndex=" + handlerIndex);
            if (handlerIndex < 0 || handlerIndex >= _handlers.Count)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Invalid handler index: " + handlerIndex + " (count=" + _handlers.Count + ")");
                return;
            }
            var handler = _handlers[handlerIndex];
            if (handler == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Handler at index " + handlerIndex + " is NULL");
                return;
            }
            GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Executing handler " + handlerIndex);
            handler(callbackData);
        }

        public static object CreateCallback(Assembly steamworksAssembly, Type callbackStructType, Action<object> handler)
        {
            if (steamworksAssembly == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] CreateCallback: Assembly is null");
                return null;
            }
            if (callbackStructType == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] CreateCallback: Type is null");
                return null;
            }
            if (handler == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] CreateCallback: Handler is null");
                return null;
            }

            Type callbackGenericDef = steamworksAssembly.GetType("Steamworks.Callback`1", false);
            if (callbackGenericDef == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Callback`1 type not found");
                return null;
            }

            Type callbackClosedType = callbackGenericDef.MakeGenericType(callbackStructType);

            // Steamworks.NET typically exposes: public static Callback<T> Create(DispatchDelegate func)
            // Where DispatchDelegate is: delegate void DispatchDelegate(T param);
            MethodInfo createMethod = null;
            MethodInfo[] methods = callbackClosedType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, "Create")) continue;
                var ps = methods[i].GetParameters();
                if (ps != null && ps.Length == 1)
                {
                    createMethod = methods[i];
                    break;
                }
            }

            if (createMethod == null)
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Create method not found for " + callbackStructType.Name);
                return null;
            }

            Type delegateType = createMethod.GetParameters()[0].ParameterType;

            int handlerIndex = _handlers.Count;
            _handlers.Add(handler);
            GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Registered handler index " + handlerIndex + " for " + callbackStructType.Name);

            var dm = new DynamicMethod(
                "GT_Callback_" + callbackStructType.Name,
                typeof(void),
                new Type[] { callbackStructType },
                typeof(SteamCallbackRouter).Module,
                skipVisibility: true);

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, handlerIndex);
            il.Emit(OpCodes.Ldarg_0);
            if (callbackStructType.IsValueType)
            {
                il.Emit(OpCodes.Box, callbackStructType);
            }
            il.Emit(OpCodes.Call, typeof(SteamCallbackRouter).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Static));
            il.Emit(OpCodes.Ret);

            Delegate del = dm.CreateDelegate(delegateType);
            object callbackObj = createMethod.Invoke(null, new object[] { del });
            
            if (callbackObj != null)
            {
                GungeonTogether.Systems.Logging.Debug.Log("[SteamCallback] Successfully created callback for " + callbackStructType.Name);
            }
            else
            {
                GungeonTogether.Systems.Logging.Debug.LogWarning("[SteamCallback] Callback creation returned null for " + callbackStructType.Name);
            }
            
            return callbackObj;
        }
    }
}
