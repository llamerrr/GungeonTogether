using BepInEx;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Systems.Logging;

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
            
            // Initialize Logging
            GungeonTogether.Systems.Logging.Logger.Initialize(base.Logger);

            // Initialize Networking
            NetworkManager.Instance.Initialize();
        }

        private void Update()
        {
            NetworkManager.Instance.Update();
        }
    }
}
