using BepInEx;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Systems.Logging;
using GungeonTogether.UI;
using GungeonTogether.Networking.Sync;

namespace GungeonTogether.Core
{
    [BepInPlugin("com.llamerrr.gungeontogether", "Gungeon Together", "1.0.0")]
    [BepInDependency("etgmodding.etg.mtgapi")]
    public class GungeonTogetherMod : BaseUnityPlugin
    {
        public static GungeonTogetherMod Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            
            try
            {
                // Initialise Logging
                GungeonTogether.Systems.Logging.Logger.Initialise(base.Logger);
                Logger.LogInfo("Gungeon Together starting...");

                // Initialise Networking
                NetworkManager.Instance.Initialise();

                // Initialise UI
                UIManager.Initialise();
                
                //initialise room sync
                RoomSyncManager.Instance.gameObject.SetActive(true);
                WorldSyncManager.Instance.gameObject.SetActive(true);
                PlayerSyncManager.Instance.gameObject.SetActive(true);
                LoadingSyncManager.Instance.gameObject.SetActive(true);           

                Logger.LogInfo("Gungeon Together ready.");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Exception during initialization: {ex.GetType().Name}: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void Update()
        {
            try
            {
                NetworkManager.Instance.Update();
                UIManager.Update();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in Update: {ex.Message}");
            }
        }
    }
}
