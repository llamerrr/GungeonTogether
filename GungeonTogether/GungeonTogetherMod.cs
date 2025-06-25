using System;
using BepInEx;
using UnityEngine;
using GungeonTogether.Game;

namespace GungeonTogether
{
    /// <summary>
    /// GungeonTogether mod for Enter the Gungeon using BepInEx
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class GungeonTogetherMod : BaseUnityPlugin
    {
        // Mod metadata
        public const string GUID = "liamspc.etg.gungeontogether";
        public const string NAME = "GungeonTogether";
        public const string VERSION = "1.0.0";        public static GungeonTogetherMod Instance { get; private set; }
        private Game.MinimalGameManager _gameManager;
        
        public void Awake()
        {
            Instance = this;
            Logger.LogInfo("GungeonTogether mod loading...");
            
            try
            {
                // Register event hooks
                SetupEventHooks();
                  Logger.LogInfo("GungeonTogether mod loaded successfully!");
                Logger.LogInfo("Waiting for GameManager to be alive...");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to load GungeonTogether: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
            }
        }
          public void Start()
        {
            Logger.LogInfo("Start() called, waiting for GameManager...");
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }
        
        public void GMStart(GameManager gameManager)
        {
            Logger.LogInfo("GameManager is alive! Initializing multiplayer systems...");
            Logger.LogInfo($"ETG GameManager type: {gameManager.GetType().Name}");
              try
            {                Logger.LogInfo("Step 1: Skipping Steam checks for minimal test...");
                
                Logger.LogInfo("Step 2: Creating MinimalGameManager...");
                
                // Initialize our minimal multiplayer systems for testing
                _gameManager = new Game.MinimalGameManager();
                Logger.LogInfo("MinimalGameManager created successfully!");
                
                Logger.LogInfo("Step 3: Setting up debug controls...");
                // Setup debug controls
                SetupDebugControls();
                  Logger.LogInfo("GungeonTogether multiplayer systems initialized!");
                Logger.LogInfo("Debug controls: F3=Host, F4=Stop, F5=Status, F6=Join");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize GameManager: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
            }
        }
        
        private void SetupEventHooks()
        {
            Logger.LogDebug("Setting up ETGMod event hooks...");
              // Hook into BepInEx event system - will use Update() and scene detection instead
            
            Logger.LogDebug("Event hooks registered");
        }
          private void SetupDebugControls()
        {
            Logger.LogInfo("Debug controls enabled:");
            Logger.LogInfo("  F3 - Start hosting a multiplayer session");
            Logger.LogInfo("  F4 - Stop multiplayer session");
            Logger.LogInfo("  F5 - Show connection status");
            Logger.LogInfo("  F6 - Join last known Steam friend (for testing)");
        }
        
        void Update()
        {
            // Update the game manager each frame
            _gameManager?.Update();
            
            // Handle debug input
            HandleDebugInput();
        }
          private void HandleDebugInput()
        {
            if (_gameManager == null) return;
            
            try
            {
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    Logger.LogInfo("F3: Starting host session...");
                    StartHosting();
                }
                
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    Logger.LogInfo("F4: Stopping multiplayer session...");
                    StopMultiplayer();
                }
                  if (Input.GetKeyDown(KeyCode.F5))
                {
                    Logger.LogInfo("F5: Showing status...");
                    ShowStatus();
                }
                
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    Logger.LogInfo("F6: Attempting to join a Steam friend for testing...");
                    TryJoinSteamFriend();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in debug input: {e.Message}");
            }
        }
        
        // Event handlers
        private void OnGameStarted()
        {
            Logger.LogInfo("Game started event received");
            // Game has started, we can now safely interact with game systems
        }
        
        private void OnMainMenuLoaded(MainMenuFoyerController menu)
        {
            Logger.LogInfo("Main menu loaded");
            // Main menu is loaded, we could add UI elements here in the future
        }
          // Multiplayer API
        public void StartHosting()
        {
            try
            {
                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }
                  _gameManager.StartSession();
                Logger.LogInfo("Started hosting session in test mode!");
                Logger.LogInfo("Steam functionality temporarily disabled for testing");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start hosting: {e.Message}");
            }
        }
          public void JoinSession(string steamIdString)
        {
            try
            {
                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }                if (ulong.TryParse(steamIdString, out ulong steamId))
                {
                    Logger.LogInfo($"Join session called with ID: {steamIdString}");
                    Logger.LogInfo("Steam functionality temporarily disabled for testing");
                }
                else
                {
                    Logger.LogError($"Invalid Steam ID format: {steamIdString}");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to join session: {e.Message}");
            }
        }
          public void StopMultiplayer()
        {
            try
            {
                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }
                  _gameManager.StopSession();
                Logger.LogInfo("Stopped session in test mode!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to stop multiplayer: {e.Message}");
            }
        }        public void ShowStatus()
        {
            if (_gameManager == null)
            {
                Logger.LogInfo("GameManager not initialized");
                return;
            }
            
            Logger.LogInfo("=== GungeonTogether Status ===");
            Logger.LogInfo($"Session Active: {_gameManager.IsActive}");
            Logger.LogInfo($"Status: {_gameManager.Status}");
            Logger.LogInfo("Steam functionality temporarily disabled for testing");
        }        private void TryJoinSteamFriend()
        {
            Logger.LogInfo("Steam friend join functionality temporarily disabled for testing");
            Logger.LogInfo("Join session test completed");
        }
    }
}
