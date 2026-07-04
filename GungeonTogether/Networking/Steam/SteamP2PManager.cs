using System;
using System.Reflection;
using UnityEngine;
using GungeonTogether.Core;
using GungeonTogether.Systems.Logging;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.Networking.Steam
{
    public class SteamP2PManager
    {
        private static SteamP2PManager _instance;
        
        public static SteamP2PManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    try
                    {
                        _instance = new SteamP2PManager();
                    }
                    catch (System.TypeLoadException tle)
                    {
                        Debug.LogError($"SteamP2PManager: TypeLoadException: {tle.Message}");
                        throw;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"SteamP2PManager: {ex.GetType().Name}: {ex.Message}");
                        throw;
                    }
                }
                return _instance;
            }
        }

        private object _p2pSessionConnectFailCallback;

        private const int CHANNEL_INDEX = 0;
        
        public bool IsInitialised { get; private set; }
        public ulong LocalSteamID { get; private set; }
        
        private bool _steamIdRetrieved = false;
        private object _p2pSessionRequestCallback;
        private Assembly _steamworksAssembly;

        // Events
        public event Action<ulong, byte[]> OnPacketReceived;
        public event Action<ulong> OnP2PSessionRequest;

        public SteamP2PManager()
        {
            IsInitialised = false;
            LocalSteamID = 0;
        }

        public void Initialise()
        {
            if (IsInitialised) return;

            try
            {

                Debug.Log("SteamP2PManager: Initializing Steam Reflection Helper...");
                SteamReflectionHelper.InitialiseSteamTypes();
                
                if (!SteamReflectionHelper.IsInitialised)
                {
                    Debug.LogError("Failed to initialise Steam Reflection Helper.");
                    return;
                }
                Debug.Log("SteamP2PManager: Steam Reflection Helper initialized.");

                _steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                TryHookP2PSessionRequestCallback();

                // Steam ID will be retrieved lazily in Update() once Steamworks is initialized
                LocalSteamID = 0;
                _steamIdRetrieved = false;
                Debug.Log("Steam P2P Manager Initialised. Local SteamID will be retrieved when Steamworks is ready.");
                IsInitialised = true;
            }
            catch (System.TypeLoadException tle)
            {
                Debug.LogError($"SteamP2PManager: TypeLoadException - Type: {tle.TypeName}, Message: {tle.Message}");
                Debug.LogError($"SteamP2PManager: Full exception: {tle}");
                IsInitialised = false;
                throw;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"SteamP2PManager: Exception - {ex.GetType().Name}: {ex.Message}");
                Debug.LogError($"SteamP2PManager: Stack: {ex.StackTrace}");
                IsInitialised = false;
                throw;
            }
        }

        public void Update()
        {
            if (!IsInitialised) return;
            
            // Try to retrieve Steam ID if we haven't yet
            if (!_steamIdRetrieved)
            {
                TryRetrieveSteamId();
            }
            
            ReadPackets();
        }

        private void TryRetrieveSteamId()
        {
            try
            {
                ulong steamId = SteamReflectionHelper.GetLocalSteamId();
                if (steamId != 0)
                {
                    LocalSteamID = steamId;
                    _steamIdRetrieved = true;
                    Debug.Log($"SteamP2PManager: Successfully retrieved Steam ID: {LocalSteamID}");
                }
            }
            catch (System.InvalidOperationException)
            {
                // Steamworks not initialized yet - will retry next frame
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SteamP2PManager: Unexpected error retrieving Steam ID: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TryHookP2PSessionRequestCallback()
        {
            try
            {
                if (_p2pSessionRequestCallback != null)
                {
                    return;
                }

                if (_steamworksAssembly == null)
                {
                    _steamworksAssembly = SteamReflectionHelper.GetSteamworksAssembly();
                }

                if (_steamworksAssembly == null)
                {
                    Debug.LogWarning("SteamP2PManager: Steamworks assembly not available; skipping P2P session callback hookup.");
                    return;
                }

                Type callbackType = ResolveP2PSessionRequestCallbackType();
                if (callbackType == null)
                {
                    Debug.LogWarning("SteamP2PManager: Could not resolve P2PSessionRequest callback type.");
                    return;
                }

                _p2pSessionRequestCallback = SteamCallbackRouter.CreateCallback(_steamworksAssembly, callbackType, HandleP2PSessionRequestCallback);

                if (_p2pSessionRequestCallback != null)
                {
                    Debug.Log("SteamP2PManager: Registered P2PSessionRequest callback.");
                }
                else
                {
                    Debug.LogWarning("SteamP2PManager: Failed to register P2PSessionRequest callback.");
                }

                if (SteamReflectionHelper.P2PSessionConnectFailType != null)
                {
                    _p2pSessionConnectFailCallback = SteamCallbackRouter.CreateCallback(
                        _steamworksAssembly,
                        SteamReflectionHelper.P2PSessionConnectFailType,
                        HandleP2PSessionConnectFail);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SteamP2PManager: Failed to hook P2PSessionRequest callback: {ex.Message}");
            }
        }

        private void HandleP2PSessionConnectFail(object callbackData)
        {
            ulong remoteSteamId = ExtractSteamIDFromCallback(callbackData);
            // Optionally read the error enum: m_eP2PSessionError
            if (remoteSteamId != 0)
            {
                Debug.Log($"P2P session failed with {remoteSteamId}");
                // Raise an event that the host can listen to
                OnP2PSessionFailed?.Invoke(remoteSteamId);
            }
        }
        public event Action<ulong> OnP2PSessionFailed;

        private Type ResolveP2PSessionRequestCallbackType()
        {
            if (_steamworksAssembly == null) return null;

            string[] candidateTypeNames =
            {
                "Steamworks.P2PSessionRequest_t",
                "Steamworks.P2PSessionRequest",
                "Steamworks.P2PSessionRequest_t"
            };

            foreach (string typeName in candidateTypeNames)
            {
                Type resolvedType = _steamworksAssembly.GetType(typeName, false);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            foreach (Type type in _steamworksAssembly.GetTypes())
            {
                if (type != null && type.Name.Contains("P2PSessionRequest"))
                {
                    return type;
                }
            }

            return null;
        }

        private void HandleP2PSessionRequestCallback(object callbackData)
        {
            ulong remoteSteamId = ExtractSteamIDFromCallback(callbackData);
            Debug.Log($"[SteamP2P] Received P2PSessionRequest from {remoteSteamId}");

            if (remoteSteamId == 0)
            {
                Debug.LogWarning("[SteamP2P] P2PSessionRequest with invalid Steam ID");
                return;
            }

            // Accept the session
            bool accepted = TryAcceptP2PSession(remoteSteamId);
            Debug.Log($"[SteamP2P] AcceptP2PSessionWithUser result: {accepted}");

            // Fire event regardless (host controller will handle join logic)
            OnP2PSessionRequest?.Invoke(remoteSteamId);
        }

    
        private bool TryAcceptP2PSession(ulong remoteSteamId)
        {
            if (remoteSteamId == 0) return false;

            try
            {
                if (SteamReflectionHelper.AcceptP2PSessionMethod == null)
                {
                    Debug.LogError("[SteamP2P] AcceptP2PSessionMethod is null!");
                    return false;
                }

                object steamIdObj = SteamReflectionHelper.CreateCSteamID(remoteSteamId);
                if (steamIdObj == null)
                {
                    Debug.LogError($"[SteamP2P] Failed to create CSteamID for {remoteSteamId}");
                    return false;
                }

                object result = SteamReflectionHelper.AcceptP2PSessionMethod.Invoke(null, new object[] { steamIdObj });
                bool accepted = result is bool b && b;
                Debug.Log($"[SteamP2P] AcceptP2PSessionWithUser returned {accepted}");
                return accepted;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamP2P] Exception in TryAcceptP2PSession: {e.Message}");
                return false;
            }
        }

        private ulong ExtractSteamIDFromCallback(object callbackData)
        {
            if (callbackData == null) return 0;

            Type callbackType = callbackData.GetType();
            string[] fieldNames = { "m_steamIDRemote", "m_SteamIDRemote", "m_ulSteamIDRemote", "m_SteamID", "m_ulSteamIDLobby" };

            foreach (string fieldName in fieldNames)
            {
                var field = callbackType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return ExtractSteamID(field.GetValue(callbackData));
                }
            }

            return 0;
        }

        private int _readPacketsCallCount = 0;
        private const int READ_PACKETS_LOG_INTERVAL = 60;

        private void ReadPackets()
        {
            _readPacketsCallCount++;
            if (_readPacketsCallCount % READ_PACKETS_LOG_INTERVAL == 0)
                Debug.Log("[SteamP2P] ReadPackets called, checking for packets...");
            try
            {
                var isAvailableMethod = SteamReflectionHelper.IsP2PPacketAvailableMethod;
                var readMethod = SteamReflectionHelper.ReadP2PPacketMethod;

                if (isAvailableMethod == null || readMethod == null) return;

                const int MAX_PACKETS_PER_FRAME = 100;
                int packetsRead = 0;

                while (packetsRead++ < MAX_PACKETS_PER_FRAME)
                {
                    
                    uint msgSize = 0;
                    object[] availArgs = new object[] { msgSize, CHANNEL_INDEX };
                    bool isAvailable = (bool)isAvailableMethod.Invoke(null, availArgs);
                    if (!isAvailable) break;
                    Debug.Log($"[SteamP2P] Packet available from some sender, size {msgSize}");
                    msgSize = (uint)availArgs[0];

                    byte[] data = new byte[msgSize];
                    ulong senderId = 0;
                    object senderCSteamID = Activator.CreateInstance(
                        SteamReflectionHelper.GetSteamworksAssembly().GetType("Steamworks.CSteamID"));

                    uint actualMsgSize = 0;
                    object[] readArgs = new object[] { data, msgSize, actualMsgSize, senderCSteamID, CHANNEL_INDEX };
                    bool readSuccess = (bool)readMethod.Invoke(null, readArgs);
                    actualMsgSize = (uint)readArgs[2];

                    if (readSuccess)
                    {
                        senderCSteamID = readArgs[3];
                        senderId = ExtractSteamID(senderCSteamID);
                        Debug.Log($"[SteamP2P] Received packet from {senderId}, size={actualMsgSize}");
                        if (actualMsgSize != data.Length)
                        {
                            byte[] trimmedData = new byte[actualMsgSize];
                            Array.Copy(data, trimmedData, actualMsgSize);
                            data = trimmedData;
                        }
                        OnPacketReceived?.Invoke(senderId, data);
                    }
                }

                // Optionally log if no packets every 60 frames (to confirm polling works)
                // We'll skip for brevity.
            }
            catch (Exception)
            {
                // suppress spam
            }
        }

        public void SendPacket(ulong targetSteamId, byte[] data, bool reliable = true)
        {
            if (!IsInitialised) return;

            try
            {
                Debug.Log($"[SteamP2P] SendPacket to {targetSteamId}, size={data.Length}");
                object steamIdObj = SteamReflectionHelper.CreateCSteamID(targetSteamId);
                bool success = SteamReflectionHelper.TryDifferentSendSignatures(steamIdObj, data, reliable);
                if (!success)
                    Debug.LogError($"[SteamP2P] Failed to send P2P packet to {targetSteamId}");
                else
                    Debug.Log($"[SteamP2P] SendPacket to {targetSteamId} succeeded");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to send packet to {targetSteamId}: {e.Message}");
            }
        }

        private ulong ExtractSteamID(object cSteamID)
        {
            if (cSteamID == null) return 0;

            if (cSteamID is ulong ul)
            {
                return ul;
            }

            if (cSteamID is uint ui)
            {
                return ui;
            }

            if (cSteamID is long l)
            {
                return (ulong)l;
            }

            if (cSteamID is string s && ulong.TryParse(s, out ulong parsed))
            {
                return parsed;
            }

            try
            {
                return Convert.ToUInt64(cSteamID);
            }
            catch
            {
            }

            try
            {
                string[] fieldNames = { "m_SteamID", "m_steamID", "SteamID", "steamID", "m_ulSteamID" };
                Type type = cSteamID.GetType();

                foreach (string fieldName in fieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        object fieldValue = field.GetValue(cSteamID);
                        if (fieldValue != null)
                        {
                            return ExtractSteamID(fieldValue);
                        }
                    }
                }
            }
            catch
            {
            }

            return 0;
        }
    }
}
