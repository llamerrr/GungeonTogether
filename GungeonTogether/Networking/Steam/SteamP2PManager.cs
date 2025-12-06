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
        public static SteamP2PManager Instance => _instance ?? (_instance = new SteamP2PManager());

        private const int CHANNEL_INDEX = 0;
        
        public bool IsInitialized { get; private set; }
        public ulong LocalSteamID { get; private set; }

        // Events
        public event Action<ulong, byte[]> OnPacketReceived;
        public event Action<ulong> OnP2PSessionRequest;

        public void Initialize()
        {
            if (IsInitialized) return;

            SteamReflectionHelper.InitializeSteamTypes();
            if (!SteamReflectionHelper.IsInitialized)
            {
                Debug.LogError("Failed to initialize Steam Reflection Helper.");
                return;
            }

            LocalSteamID = SteamReflectionHelper.GetLocalSteamId();
            Debug.Log($"Steam P2P Manager Initialized. Local SteamID: {LocalSteamID}");
            IsInitialized = true;
        }

        public void Update()
        {
            if (!IsInitialized) return;
            
            ReadPackets();
        }

        private void ReadPackets()
        {
            try
            {
                var isAvailableMethod = SteamReflectionHelper.IsP2PPacketAvailableMethod;
                var readMethod = SteamReflectionHelper.ReadP2PPacketMethod;

                if (isAvailableMethod == null || readMethod == null) return;

                // Loop while packets are available
                while (true)
                {
                    uint msgSize = 0;
                    object[] args = new object[] { msgSize, CHANNEL_INDEX };
                    
                    bool isAvailable = (bool)isAvailableMethod.Invoke(null, args);
                    if (!isAvailable) break;

                    msgSize = (uint)args[0]; // Get the size back

                    byte[] data = new byte[msgSize];
                    ulong senderId = 0;
                    
                    // Create a CSteamID instance to hold the sender
                    object senderCSteamID = Activator.CreateInstance(SteamReflectionHelper.GetSteamworksAssembly().GetType("Steamworks.CSteamID"));
                    object[] readArgs = new object[] { data, msgSize, 0u, senderCSteamID, CHANNEL_INDEX };
                    
                    bool readSuccess = (bool)readMethod.Invoke(null, readArgs);
                    
                    if (readSuccess)
                    {
                        // Extract Sender ID
                        senderCSteamID = readArgs[3]; // Get the updated CSteamID
                        senderId = ExtractSteamID(senderCSteamID);
                        
                        OnPacketReceived?.Invoke(senderId, data);
                    }
                }
            }
            catch (Exception e)
            {
                // Suppress spam
            }
        }

        public void SendPacket(ulong targetSteamId, byte[] data, bool reliable = true)
        {
            if (!IsInitialized) return;

            try
            {
                object steamIdObj = SteamReflectionHelper.CreateCSteamID(targetSteamId);
                // SendP2PPacket(CSteamID steamIDRemote, byte[] pubData, uint cubData, EP2PSend eP2PSendType, int nChannel)
                // EP2PSend: 0 = Unreliable, 2 = Reliable
                
                // Use the helper to try different signatures as discovered
                SteamReflectionHelper.TryDifferentSendSignatures(steamIdObj, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to send packet to {targetSteamId}: {e.Message}");
            }
        }

        private ulong ExtractSteamID(object cSteamID)
        {
            // Helper to get ulong from CSteamID object
            try
            {
                var field = cSteamID.GetType().GetField("m_SteamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return (ulong)field.GetValue(cSteamID);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
