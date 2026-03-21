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

        private const int CHANNEL_INDEX = 0;
        
        public bool IsInitialised { get; private set; }
        public ulong LocalSteamID { get; private set; }
        
        private bool _steamIdRetrieved = false;

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
            if (!IsInitialised) return;

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
