using System;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    public static class SteamNetworkingSocketsHelper
    {
        private static object _networkingSocketsInstance;
        private static Type _steamNetworkingSocketsType;
        private static MethodInfo _createListenSocketP2P;
        private static MethodInfo _connectP2P;
        private static MethodInfo _sendMessageToConnection;
        private static MethodInfo _receiveMessagesOnConnection;
        private static bool _initialized;

        public static bool Initialize()
        {
            if (_initialized) return true;
            try
            {
                var steamworksAssembly = typeof(SteamReflectionHelper).Assembly;
                _steamNetworkingSocketsType = steamworksAssembly.GetType("Steamworks.SteamNetworkingSockets");
                if (ReferenceEquals(_steamNetworkingSocketsType, null))
                {
                    Debug.LogError("[SteamNetworkingSocketsHelper] Could not find SteamNetworkingSockets type");
                    return false;
                }
                var getNetworkingSockets = _steamNetworkingSocketsType.GetProperty("Interface", BindingFlags.Static | BindingFlags.Public);
                if (ReferenceEquals(getNetworkingSockets, null))
                {
                    Debug.LogError("[SteamNetworkingSocketsHelper] Could not find Interface property");
                    return false;
                }
                // Fix: Pass null for both instance and index parameters
                _networkingSocketsInstance = getNetworkingSockets.GetValue(null, null);
                if (ReferenceEquals(_networkingSocketsInstance, null))
                {
                    Debug.LogError("[SteamNetworkingSocketsHelper] Could not get ISteamNetworkingSockets instance");
                    return false;
                }
                _createListenSocketP2P = _networkingSocketsInstance.GetType().GetMethod("CreateListenSocketP2P");
                _connectP2P = _networkingSocketsInstance.GetType().GetMethod("ConnectP2P");
                _sendMessageToConnection = _networkingSocketsInstance.GetType().GetMethod("SendMessageToConnection");
                _receiveMessagesOnConnection = _networkingSocketsInstance.GetType().GetMethod("ReceiveMessagesOnConnection");
                _initialized = true;
                Debug.Log("[SteamNetworkingSocketsHelper] Successfully initialized");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamNetworkingSocketsHelper] Initialization failed: {e.Message}");
                return false;
            }
        }

        public static object CreateListenSocketP2P(int virtualPort)
        {
            if (!Initialize()) return null;
            return _createListenSocketP2P.Invoke(_networkingSocketsInstance, new object[] { virtualPort, 0, 0 });
        }

        public static object ConnectP2P(ulong steamIdRemote, int virtualPort)
        {
            if (!Initialize()) return null;
            var csteamIdType = _networkingSocketsInstance.GetType().Assembly.GetType("Steamworks.CSteamID");
            var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
            return _connectP2P.Invoke(_networkingSocketsInstance, new object[] { csteamId, virtualPort, 0, IntPtr.Zero });
        }

        public static int SendMessageToConnection(object hConn, byte[] data, int flags)
        {
            if (!Initialize()) return -1;
            return (int)_sendMessageToConnection.Invoke(_networkingSocketsInstance, new object[] { hConn, data, data.Length, flags, IntPtr.Zero });
        }

        public static int ReceiveMessagesOnConnection(object hConn, IntPtr messages, int maxMessages)
        {
            if (!Initialize()) return -1;
            return (int)_receiveMessagesOnConnection.Invoke(_networkingSocketsInstance, new object[] { hConn, messages, maxMessages });
        }
    }
}
