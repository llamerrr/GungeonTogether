using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Steamworks;

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
        private static MethodInfo _allowP2PPacketRelayMethod;
        private static MethodInfo _getP2PSessionStateMethod;
        private static bool _initialized;
        
        // Cache for P2P session state - critical for proper session management
        private static readonly System.Collections.Generic.HashSet<ulong> _acceptedSessions = new System.Collections.Generic.HashSet<ulong>();
        
        // Steam callback registration
        private static Callback<P2PSessionRequest_t> _p2pSessionRequest;
        private static Callback<P2PSessionConnectFail_t> _p2pSessionConnectFail;

        public static bool IsInitialized => _initialized;
        
        // Event for when a P2P session request is received
        public static event System.Action<ulong> OnP2PSessionRequested;

        static SteamNetworkingSocketsHelper()
        {
            GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Static constructor called. Steam P2P helper ready, but will not force early initialization.");
            // Register Steamworks.NET callbacks
            _p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            _p2pSessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
        }

        public static bool Initialize()
        {
            if (_initialized) return true;
            
            GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Starting P2P initialization...");
            
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
                
                // Use the existing working SteamNetworking P2P API
                _steamNetworkingType = steamworksAssembly.GetType("Steamworks.SteamNetworking", false);
                if (ReferenceEquals(_steamNetworkingType, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] Could not find SteamNetworking type");
                    return false;
                }
                
                GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Found SteamNetworking type, setting up P2P methods");
                
                // Get P2P networking methods
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
                    else if (string.Equals(method.Name, "AllowP2PPacketRelay"))
                    {
                        _allowP2PPacketRelayMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found AllowP2PPacketRelay method");
                    }
                    else if (string.Equals(method.Name, "GetP2PSessionState"))
                    {
                        _getP2PSessionStateMethod = method;
                        GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Found GetP2PSessionState method");
                    }
                }
                
                // Log all static public methods on SteamNetworking for debugging
                GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Listing all static public methods on SteamNetworking:");
                foreach (var method in allMethods)
                {
                    var paramList = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray());
                    GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Method: {method.Name}({paramList})");
                }
                
                if (ReferenceEquals(_sendP2PPacketMethod, null) || ReferenceEquals(_readP2PPacketMethod, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] Could not find required P2P methods");
                    return false;
                }
                
                GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Found all required P2P methods");
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

        /// <summary>
        /// Enables the Steam P2P relay if Steamworks is initialized. Should be called after Steamworks is ready.
        /// </summary>
        public static void EnableRelayIfReady()
        {
            bool relayEnabled = false;
            try
            {
                // Only attempt if Steamworks is initialized
                if (!SteamManager.Initialized)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamNetworkingSocketsHelper] Steamworks not initialized, cannot enable relay yet.");
                    return;
                }
                // Try direct call first
                if (SteamNetworking.AllowP2PPacketRelay(true))
                {
                    relayEnabled = true;
                    GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Enabled P2P packet relay (direct call success)");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning("[SteamNetworkingSocketsHelper] Direct call to AllowP2PPacketRelay returned false");
                }
            }
            catch (Exception directEx)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] Direct call to AllowP2PPacketRelay failed: {directEx.Message}");
            }

            // Also try reflection-based call as backup
            if (!ReferenceEquals(_allowP2PPacketRelayMethod, null))
            {
                try
                {
                    object result = _allowP2PPacketRelayMethod.Invoke(null, new object[] { true });
                    if (result is bool && (bool)result)
                    {
                        relayEnabled = true;
                        GungeonTogether.Logging.Debug.Log("[SteamNetworkingSocketsHelper] Enabled P2P packet relay via reflection (success)");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] Reflection call to AllowP2PPacketRelay returned: {result}");
                    }
                }
                catch (Exception relayEx)
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] Exception while enabling packet relay via reflection: {relayEx}");
                }
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning("[SteamNetworkingSocketsHelper] AllowP2PPacketRelay method not found via reflection");
            }

            if (!relayEnabled)
            {
                GungeonTogether.Logging.Debug.LogError("[SteamNetworkingSocketsHelper] CRITICAL: Failed to enable P2P packet relay! P2P connections may fail.");
            }
        }

        private static void OnP2PSessionRequest(P2PSessionRequest_t param)
        {
            // Accept the session if appropriate (e.g., if the user is in our lobby)
            GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] OnP2PSessionRequest from {param.m_steamIDRemote}");
            // Accept all for now (you may want to add checks here)
            SteamNetworking.AcceptP2PSessionWithUser(param.m_steamIDRemote);
            // Notify listeners if needed
            OnP2PSessionRequested?.Invoke((ulong)param.m_steamIDRemote);
        }
        private static void OnP2PSessionConnectFail(P2PSessionConnectFail_t param)
        {
            GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] OnP2PSessionConnectFail from {param.m_steamIDRemote}, error: {param.m_eP2PSessionError}");
        }
        
        public static bool AcceptP2PSession(ulong steamIdRemote)
        {
            if (!Initialize() || ReferenceEquals(_acceptP2PSessionMethod, null)) return false;
            
            // Check if we already accepted this session
            if (_acceptedSessions.Contains(steamIdRemote))
            {
                return true; // Already accepted
            }
            
            try
            {
                // Create CSteamID object
                var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                
                object result = _acceptP2PSessionMethod.Invoke(null, new object[] { csteamId });
                bool accepted = (bool)result;
                
                if (accepted)
                {
                    _acceptedSessions.Add(steamIdRemote);
                    GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Accepted P2P session with {steamIdRemote}");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] Failed to accept P2P session with {steamIdRemote}");
                }
                
                return accepted;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] AcceptP2PSession failed: {e.Message}");
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
                    var reliableType = Enum.Parse(ep2pSendType, "k_EP2PSendReliable"); // Use reliable sending for critical data
                    
                    object result = _sendP2PPacketMethod.Invoke(null, new object[] { csteamId, data, (uint)data.Length, reliableType, channel });
                    bool sent = (bool)result;
                    
                    if (!sent)
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[SteamNetworkingSocketsHelper] Failed to send P2P packet to {steamIdRemote}");
                    }
                    
                    return sent;
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

        public static byte[] ReadP2PPacket(out ulong senderSteamId, int channel = 0)
        {
            senderSteamId = 0UL;
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
                        var csteamIdRemote = args[3];
                        
                        // Extract sender Steam ID
                        if (!ReferenceEquals(csteamIdRemote, null))
                        {
                            senderSteamId = (ulong)csteamIdRemote.GetType().GetField("m_SteamID").GetValue(csteamIdRemote);
                        }
                        
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
        
        // Backward compatibility method
        public static byte[] ReadP2PPacket(int channel = 0)
        {
            ulong senderSteamId;
            return ReadP2PPacket(out senderSteamId, channel);
        }

        public static bool CloseP2PSession(ulong steamIdRemote)
        {
            if (!Initialize() || ReferenceEquals(_closeP2PSessionMethod, null)) return false;
            
            try
            {
                var csteamIdType = _steamNetworkingType.Assembly.GetType("Steamworks.CSteamID");
                var csteamId = Activator.CreateInstance(csteamIdType, steamIdRemote);
                
                object result = _closeP2PSessionMethod.Invoke(null, new object[] { csteamId });
                bool closed = (bool)result;
                
                if (closed)
                {
                    _acceptedSessions.Remove(steamIdRemote);
                    GungeonTogether.Logging.Debug.Log($"[SteamNetworkingSocketsHelper] Closed P2P session with {steamIdRemote}");
                }
                
                return closed;
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
        
        /// <summary>
        /// Data structure for incoming packet with sender info
        /// </summary>
        public struct IncomingPacket
        {
            public byte[] data;
            public ulong senderSteamId;
            
            public IncomingPacket(byte[] data, ulong senderSteamId)
            {
                this.data = data;
                this.senderSteamId = senderSteamId;
            }
        }
        
        /// <summary>
        /// Continuously poll for incoming P2P packets and return them with sender info
        /// This should be called from the main update loop
        /// </summary>
        public static System.Collections.Generic.List<IncomingPacket> PollIncomingPackets(int channel = 0)
        {
            var packets = new System.Collections.Generic.List<IncomingPacket>();
            
            if (!Initialize()) return packets;
            
            try
            {
                // Read all available packets
                while (IsP2PPacketAvailable(channel))
                {
                    ulong senderSteamId;
                    byte[] data = ReadP2PPacket(out senderSteamId, channel);
                    if (data != null)
                    {
                        packets.Add(new IncomingPacket(data, senderSteamId));
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamNetworkingSocketsHelper] PollIncomingPackets failed: {e.Message}");
            }
            
            return packets;
        }
    }
}
