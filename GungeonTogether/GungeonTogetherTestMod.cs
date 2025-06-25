using System;
using BepInEx;
using UnityEngine;

namespace GungeonTogether
{
    /// <summary>
    /// Super minimal test version to isolate TypeLoadException
    /// </summary>
    [BepInPlugin("liamspc.etg.gungeontogether.test", "GungeonTogetherTest", "0.0.1")]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class GungeonTogetherTestMod : BaseUnityPlugin
    {
        public static GungeonTogetherTestMod Instance { get; private set; }
        
        public void Awake()
        {
            Instance = this;
            Logger.LogInfo("TEST: GungeonTogether test mod loading...");
            
            try
            {
                Logger.LogInfo("TEST: Awake completed successfully!");
            }
            catch (Exception e)
            {
                Logger.LogError($"TEST: Failed to load: {e.Message}");
            }
        }
        
        public void Start()
        {
            Logger.LogInfo("TEST: Start() called, waiting for GameManager...");
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }
        
        public void GMStart(GameManager gameManager)
        {
            Logger.LogInfo("TEST: GameManager is alive!");
            
            try
            {
                Logger.LogInfo("TEST: About to test MinimalGameManager creation...");
                
                // Test direct instantiation
                var testManager = new Game.MinimalGameManager();
                Logger.LogInfo("TEST: MinimalGameManager created successfully!");
                
                Logger.LogInfo($"TEST: Manager status: {testManager.Status}");
                Logger.LogInfo($"TEST: Manager active: {testManager.IsActive}");
                
                Logger.LogInfo("TEST: All tests passed!");
            }
            catch (Exception e)
            {
                Logger.LogError($"TEST: Failed: {e.Message}");
                Logger.LogError($"TEST: Exception type: {e.GetType().Name}");
                Logger.LogError($"TEST: Stack trace: {e.StackTrace}");
            }
        }
        
        void Update()
        {
            // Minimal update
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Logger.LogInfo("TEST: F1 pressed - test successful!");
            }
        }
    }
}
