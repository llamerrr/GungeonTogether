using System;
using BepInEx;
using UnityEngine;
using GungeonTogether.Game;
using Steamworks;

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
        public const string VERSION = "1.0.0";
          public static GungeonTogetherMod Instance { get; private set; }
        private Game.GameManager _gameManager;
        
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
            {
                // Check if Steam is available
                if (!SteamManager.Initialized)
                {
                    Logger.LogError("Steam not initialized! Steam is required for GungeonTogether.");
                    Logger.LogError("Make sure you're running the Steam version of Enter the Gungeon.");
                    return;
                }
                  // Initialize your multiplayer systems
                _gameManager = new Game.GameManager();
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
            try            {
                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }
                
                _gameManager.StartHosting();
                Logger.LogInfo("Started hosting multiplayer session!");
                Logger.LogInfo($"Your Steam ID: {SteamUser.GetSteamID()}");
                Logger.LogInfo("Friends can join using your Steam ID");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start hosting: {e.Message}");
            }
        }
        
        public void JoinSession(string steamIdString)
        {
            try
            {                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }
                
                if (ulong.TryParse(steamIdString, out ulong steamId))
                {
                    var steamUserId = new CSteamID(steamId);
                    _gameManager.JoinSession(steamUserId);
                    Logger.LogInfo($"Attempting to join session: {steamUserId}");
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
        {            try
            {
                if (_gameManager == null)
                {
                    Logger.LogError("GameManager not initialized");
                    return;
                }
                
                _gameManager.StopMultiplayer();
                Logger.LogInfo("Stopped multiplayer session!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to stop multiplayer: {e.Message}");
            }
        }
        
        public void ShowStatus()
        {
            if (_gameManager == null)
            {
                Logger.LogInfo("GameManager not initialized");
                return;
            }
            
            Logger.LogInfo("=== GungeonTogether Status ===");
            Logger.LogInfo($"Multiplayer Active: {_gameManager.IsMultiplayerActive}");
            Logger.LogInfo($"Is Host: {_gameManager.IsHost}");
            
            if (SteamManager.Initialized)
            {
                Logger.LogInfo($"Steam ID: {SteamUser.GetSteamID()}");
                Logger.LogInfo($"Steam Name: {SteamFriends.GetPersonaName()}");
            }
            else
            {
                Logger.LogInfo("Steam not initialized");
            }
        }
        
        private void TryJoinSteamFriend()
        {
            if (!SteamManager.Initialized)
            {
                Logger.LogError("Steam not initialized");
                return;
            }
            
            // Get the first Steam friend for testing
            int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);            if (friendCount > 0)
            {
                var friendId = SteamFriends.GetFriendByIndex(0, EFriendFlags.k_EFriendFlagImmediate);
                var friendName = SteamFriends.GetFriendPersonaName(friendId);
                
                Logger.LogInfo($"Attempting to join friend: {friendName} ({friendId})");
                JoinSession(friendId.ToString());
            }
            else
            {
                Logger.LogInfo("No Steam friends found for testing");
            }
        }
    }
}
