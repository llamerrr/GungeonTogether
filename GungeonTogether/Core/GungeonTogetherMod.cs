using BepInEx;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Systems.Logging;
using GungeonTogether.UI;

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
            Logger.LogInfo("Gungeon Together started!");
            
            // Initialise Logging
            GungeonTogether.Systems.Logging.Logger.Initialise(base.Logger);

            // Initialise Networking
            NetworkManager.Instance.Initialise();

            // Initialise UI
            UIManager.Initialise();
        }

        private void Update()
        {
            NetworkManager.Instance.Update();
            UIManager.Update();
        }
    }
}
