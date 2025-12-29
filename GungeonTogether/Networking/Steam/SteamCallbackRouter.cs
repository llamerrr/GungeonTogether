using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GungeonTogether.Networking.Steam
{
    internal static class SteamCallbackRouter
    {
        private static readonly List<Action<object>> _handlers = new List<Action<object>>();

        public static void Invoke(int handlerIndex, object callbackData)
        {
            if (handlerIndex < 0 || handlerIndex >= _handlers.Count) return;
            var handler = _handlers[handlerIndex];
            if (handler == null) return;
            handler(callbackData);
        }

        public static object CreateCallback(Assembly steamworksAssembly, Type callbackStructType, Action<object> handler)
        {
            if (steamworksAssembly == null) return null;
            if (callbackStructType == null) return null;
            if (handler == null) return null;

            Type callbackGenericDef = steamworksAssembly.GetType("Steamworks.Callback`1", false);
            if (callbackGenericDef == null) return null;

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

            if (createMethod == null) return null;

            Type delegateType = createMethod.GetParameters()[0].ParameterType;

            int handlerIndex = _handlers.Count;
            _handlers.Add(handler);

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
            return callbackObj;
        }
    }
}
