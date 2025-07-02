using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GungeonTogether.Steam
{
    public static class SteamNetworkingSocketsHelper
    {
        private static Type _steamNetworkingType;
        private static MethodInfo _sendP2PPacketMethod;
        private static MethodInfo _readP2PPacketMethod;
        private static MethodInfo _isP2PPacketAvailableMethod;
        private static MethodInfo _acceptP2PSessionMethod;
        private static MethodInfo _closeP2PSessionMethod;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        public static bool Initialize()
        {
            if (_initialized) return true;
            try
            {
                // Find Assembly-CSharp-firstpass which contains ETG's Steamworks.NET
                Assembly steamworksAssembly = null;
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (string.Equals(assemblies[i].GetName().Name, "Assembly-CSharp-firstpass"))
                    {
                        steamworksAssembly = assemblies[i];
                        break;
                    }
                }
                
                if (ReferenceEquals(steamworksAssembly, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] Assembly-CSharp-firstpass not found");
                    return false;
                }
                
                // Use the existing working SteamNetworking P2P API instead of trying to find SteamNetworkingSockets
                _steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                if (ReferenceEquals(_steamNetworkingType, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] Could not find SteamNetworking type");
                    return false;
                }
                
                GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Found SteamNetworking type, setting up P2P methods");
                
                // Get P2P networking methods that are known to work in ETG
                var allMethods = _steamNetworkingType.GetMethods(BindingFlags.Static | BindingFlags.Public);
                
                foreach (var method in allMethods)
                {
                    if (string.Equals(method.Name, "SendP2PPacket"))
                    {
                        _sendP2PPacketMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found SendP2PPacket method with {method.GetParameters().Length} parameters");
                    }
                    else if (string.Equals(method.Name, "ReadP2PPacket"))
                    {
                        _readP2PPacketMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found ReadP2PPacket method with {method.GetParameters().Length} parameters");
                    }
                    else if (string.Equals(method.Name, "IsP2PPacketAvailable"))
                    {
                        _isP2PPacketAvailableMethod = method;
                        var paramNames = new string[method.GetParameters().Length];
                        for (int i = 0; i < method.GetParameters().Length; i++)
                        {
                            paramNames[i] = method.GetParameters()[i].ParameterType.Name;
                        }
                        var paramTypes = string.Join(", ", paramNames);
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found IsP2PPacketAvailable method with {method.GetParameters().Length} parameters: ({paramTypes})");
                    }
                    else if (string.Equals(method.Name, "AcceptP2PSessionWithUser"))
                    {
                        _acceptP2PSessionMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found AcceptP2PSessionWithUser method with {method.GetParameters().Length} parameters");
                    }
                    else if (string.Equals(method.Name, "CloseP2PSessionWithUser"))
                    {
                        _closeP2PSessionMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found CloseP2PSessionWithUser method with {method.GetParameters().Length} parameters");
                    }
                }
                
                if (ReferenceEquals(_sendP2PPacketMethod, null) || ReferenceEquals(_readP2PPacketMethod, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] Could not find required P2P methods");
                    return false;
                }
                
                _initialized = true;
                GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Successfully initialized with Steam P2P networking");
                return true;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] Initialization failed: {e.Message}");
                return false;
            }
        }

        public static bool SendP2PPacket(ulong steamIdRemote, byte[] data, int channel = 0)
        {
            if (!Initialize()) return false;
            
            try
            {
                // Create CSteamID object
                var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                
                // Try different SendP2PPacket signatures
                var parameters = _sendP2PPacketMethod.GetParameters();
                
                if (parameters.Length >= 4)
                {
                    // Standard signature: SendP2PPacket(CSteamID steamIDRemote, byte[] data, uint cubData, EP2PSend eP2PSendType, int nChannel = 0)
                    var ep2pSendType = _steamNetworkingType.Assembly.GetType("Steamworks.EP2PSend");
                    var reliableType = Enum.Parse(ep2pSendType, "k_EP2PSendReliable"); // Use reliable sending
                    
                    object result = _sendP2PPacketMethod.Invoke(null, new object[] { csteamId, data, (uint)data.Length, reliableType, channel });
                    return (bool)result;
                }
                
                return false;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] SendP2PPacket failed: {e.Message}");
                return false;
            }
        }

        public static bool IsP2PPacketAvailable(int channel = 0)
        {
            if (!Initialize() || ReferenceEquals(_isP2PPacketAvailableMethod, null)) return false;
            
            try
            {
                // Check the method signature to call it correctly
                var parameters = _isP2PPacketAvailableMethod.GetParameters();
                
                if (parameters.Length == 0)
                {
                    // No parameters version: IsP2PPacketAvailable()
                    object result = _isP2PPacketAvailableMethod.Invoke(null, new object[0]);
                    return (bool)result;
                }
                else if (parameters.Length == 1)
                {
                    // Single parameter version: IsP2PPacketAvailable(int nChannel) OR IsP2PPacketAvailable(out uint pcubMsgSize)
                    if (parameters[0].ParameterType.Equals(typeof(int)))
                    {
                        // Channel parameter
                        object result = _isP2PPacketAvailableMethod.Invoke(null, new object[] { channel });
                        return (bool)result;
                    }
                    else
                    {
                        // Out parameter for size - try with default values
                        object[] args = new object[1];
                        object result = _isP2PPacketAvailableMethod.Invoke(null, args);
                        return (bool)result;
                    }
                }
                else if (parameters.Length == 2)
                {
                    // Two parameters: IsP2PPacketAvailable(out uint pcubMsgSize, int nChannel)
                    object[] args = new object[] { null, channel };
                    object result = _isP2PPacketAvailableMethod.Invoke(null, args);
                    return (bool)result;
                }
                
                return false;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] IsP2PPacketAvailable failed: {e.Message}");
                return false;
            }
        }

        public static byte[] ReadP2PPacket(int channel = 0)
        {
            if (!Initialize() || ReferenceEquals(_readP2PPacketMethod, null)) return null;
            
            try
            {
                var parameters = _readP2PPacketMethod.GetParameters();
                
                // Common signatures:
                // ReadP2PPacket(byte[] pubDest, uint cubDest, out uint pcubMsgSize, out CSteamID psteamIDRemote, int nChannel = 0)
                // ReadP2PPacket(byte[] pubDest, uint cubDest, out uint pcubMsgSize, out CSteamID psteamIDRemote)
                
                if (parameters.Length >= 4)
                {
                    byte[] buffer = new byte[1024]; // Max packet size
                    object[] args;
                    
                    if (parameters.Length == 4)
                    {
                        // No channel parameter
                        args = new object[] { buffer, (uint)buffer.Length, null, null };
                    }
                    else
                    {
                        // With channel parameter
                        args = new object[] { buffer, (uint)buffer.Length, null, null, channel };
                    }
                    
                    object result = _readP2PPacketMethod.Invoke(null, args);
                    if ((bool)result)
                    {
                        uint actualSize = (uint)args[2];
                        if (actualSize > 0 && actualSize <= buffer.Length)
                        {
                            byte[] actualData = new byte[actualSize];
                            Array.Copy(buffer, actualData, actualSize);
                            return actualData;
                        }
                    }
                }
                
                return null;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] ReadP2PPacket failed: {e.Message}");
                return null;
            }
        }

        public static bool AcceptP2PSession(ulong steamIdRemote)
        {
            if (!Initialize() || ReferenceEquals(_acceptP2PSessionMethod, null)) return false;
            
            try
            {
                var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                
                object result = _acceptP2PSessionMethod.Invoke(null, new object[] { csteamId });
                return (bool)result;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] AcceptP2PSession failed: {e.Message}");
                return false;
            }
        }

        public static bool CloseP2PSession(ulong steamIdRemote)
        {
            if (!Initialize() || ReferenceEquals(_closeP2PSessionMethod, null)) return false;
            
            try
            {
                var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                
                object result = _closeP2PSessionMethod.Invoke(null, new object[] { csteamId });
                return (bool)result;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] CloseP2PSession failed: {e.Message}");
                return false;
            }
        }

        public static bool GetP2PSessionState(ulong steamIdRemote)
        {
            if (!Initialize()) return false;
            
            try
            {
                // Try to find GetP2PSessionState method
                var getP2PSessionStateMethod = _steamNetworkingType.GetMethod("GetP2PSessionState", BindingFlags.Static | BindingFlags.Public);
                if (!ReferenceEquals(getP2PSessionStateMethod, null))
                {
                    var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                    var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                    
                    // This would return a P2PSessionState_t struct, but for simplicity just return bool
                    object result = getP2PSessionStateMethod.Invoke(null, new object[] { csteamId, null });
                    return (bool)result;
                }
                return false;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] GetP2PSessionState failed: {e.Message}");
                return false;
            }
        }
    }
}
