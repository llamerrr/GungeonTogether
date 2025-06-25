using System;
using BepInEx;
using UnityEngine;
using GungeonTogether.Game;
using GungeonTogether.Steam;

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
        public const string VERSION = "0.0.1";        public static GungeonTogetherMod Instance { get; private set; }
        private Game.BasicGameManager _gameManager;
        private Game.SimpleSessionManager _fallbackManager;
        
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
        }        public void GMStart(GameManager gameManager)
        {
            Logger.LogInfo("GameManager is alive! Initializing multiplayer systems...");
            Logger.LogInfo($"ETG GameManager type: {gameManager.GetType().Name}");
              try
            {                Logger.LogInfo("Step 1: Creating ultra-minimal GameManager to isolate TypeLoadException...");
                
                // Try to create the most basic instance possible                Logger.LogInfo("Step 1a: About to instantiate BasicGameManager...");
                _gameManager = new Game.BasicGameManager();
                Logger.LogInfo("Step 1b: BasicGameManager created successfully!");
                
                Logger.LogInfo("Step 2: Checking manager state...");
                Logger.LogInfo($"Step 2a: Manager Active: {_gameManager.IsActive}");
                Logger.LogInfo($"Step 2b: Manager Status: {_gameManager.Status}");
                
                Logger.LogInfo("Step 3: Attempting Steam session helper initialization...");
                try
                {
                    SteamSessionHelper.Initialize(_gameManager);
                    Logger.LogInfo("Step 3a: Steam session helper initialized!");
                }
                catch (Exception steamEx)
                {
                    Logger.LogWarning($"Steam helper initialization failed: {steamEx.Message}");
                    Logger.LogWarning("Continuing without Steam integration...");
                }
                
                Logger.LogInfo("Step 4: Setting up event handlers...");
                try
                {
                    SetupSessionEventHandlers();
                    Logger.LogInfo("Step 4a: Event handlers configured!");
                }
                catch (Exception eventEx)
                {
                    Logger.LogWarning($"Event handler setup failed: {eventEx.Message}");
                    Logger.LogWarning("Continuing without event handlers...");
                }
                
                Logger.LogInfo("Step 5: Setting up debug controls...");
                SetupDebugControls();
                  Logger.LogInfo("GungeonTogether multiplayer systems initialized!");
                Logger.LogInfo("Debug controls: F3=Host, F4=Stop, F5=Status");
            }            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize BasicGameManager: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
                Logger.LogError($"Exception type: {e.GetType().Name}");
                
                // Try fallback implementation
                Logger.LogInfo("Attempting fallback SimpleSessionManager...");
                try
                {
                    _fallbackManager = new Game.SimpleSessionManager();
                    Logger.LogInfo("Fallback SimpleSessionManager created successfully!");
                    Logger.LogInfo("Limited functionality available: F3=Start, F4=Stop, F5=Status");
                }
                catch (Exception fallbackEx)
                {
                    Logger.LogError($"Fallback manager also failed: {fallbackEx.Message}");
                    Logger.LogError("Mod initialization completely failed!");
                }
            }
        }
            private void SetupSessionEventHandlers()
        {
            Logger.LogInfo("Setting up session event handlers...");
            
            if (_gameManager == null)
            {
                Logger.LogWarning("Cannot setup event handlers - GameManager is null");
                return;
            }
            
            try
            {
                // Subscribe to session events with null checks
                _gameManager.OnSessionStarted += OnSessionStarted;
                Logger.LogInfo("OnSessionStarted event subscribed");
                
                _gameManager.OnSessionStopped += OnSessionStopped;
                Logger.LogInfo("OnSessionStopped event subscribed");
                
                _gameManager.OnSessionJoined += OnSessionJoined;
                Logger.LogInfo("OnSessionJoined event subscribed");
                
                _gameManager.OnSessionJoinFailed += OnSessionJoinFailed;
                Logger.LogInfo("OnSessionJoinFailed event subscribed");
                
                Logger.LogInfo("Session event handlers configured successfully");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error setting up event handlers: {e.Message}");
                throw;
            }
        }
          private void OnSessionStarted()
        {
            try
            {
                Logger.LogInfo("Session started - updating Steam rich presence");
                if (_gameManager?.CurrentSessionId != null)
                {
                    SteamSessionHelper.UpdateRichPresence(true, _gameManager.CurrentSessionId);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in OnSessionStarted: {e.Message}");
            }
        }
        
        private void OnSessionStopped()
        {
            try
            {
                Logger.LogInfo("Session stopped - clearing Steam rich presence");
                SteamSessionHelper.UpdateRichPresence(false, null);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in OnSessionStopped: {e.Message}");
            }
        }
        
        private void OnSessionJoined(string sessionId)
        {
            try
            {
                Logger.LogInfo($"Successfully joined session: {sessionId}");
                SteamSessionHelper.UpdateRichPresence(false, sessionId);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in OnSessionJoined: {e.Message}");
            }
        }
        
        private void OnSessionJoinFailed(string error)
        {
            try
            {
                Logger.LogError($"Failed to join session: {error}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in OnSessionJoinFailed: {e.Message}");
            }
        }

        private void SetupEventHooks()
        {
            Logger.LogDebug("Setting up ETGMod event hooks...");
              // Hook into BepInEx event system - will use Update() and scene detection instead
            
            Logger.LogDebug("Event hooks registered");
        }          private void SetupDebugControls()
        {
            Logger.LogInfo("Debug controls enabled:");
            Logger.LogInfo("  F3 - Start hosting a multiplayer session");
            Logger.LogInfo("  F4 - Stop multiplayer session");
            Logger.LogInfo("  F5 - Show connection status");
            Logger.LogInfo("  F6 - Join Steam friend session (test)");
            Logger.LogInfo("  F7 - Show Steam invite dialog");
            Logger.LogInfo("  F8 - Show friends playing GungeonTogether");
            Logger.LogInfo("  F9 - Simulate Steam overlay 'Join Game' click");
        }
        
        void Update()
        {
            // Update the game manager each frame
            _gameManager?.Update();
            
            // Handle debug input
            HandleDebugInput();
        }        private void HandleDebugInput()
        {
            if (_gameManager == null && _fallbackManager == null) return;
            
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
                
                // Steam features only available with full BasicGameManager
                if (_gameManager != null)
                {
                    if (Input.GetKeyDown(KeyCode.F6))
                    {
                        Logger.LogInfo("F6: Attempting to join a Steam friend for testing...");
                        TryJoinSteamFriend();
                    }
                    
                    if (Input.GetKeyDown(KeyCode.F7))
                    {
                        Logger.LogInfo("F7: Showing Steam invite dialog...");
                        ShowSteamInviteDialog();
                    }
                      if (Input.GetKeyDown(KeyCode.F8))
                    {
                        Logger.LogInfo("F8: Showing friends playing GungeonTogether...");
                        ShowFriendsPlayingGame();
                    }
                    
                    if (Input.GetKeyDown(KeyCode.F9))
                    {
                        Logger.LogInfo("F9: Simulating Steam overlay 'Join Game' click...");
                        SimulateSteamOverlayJoin();
                    }
                }
                else if (_fallbackManager != null)
                {
                    if (Input.GetKeyDown(KeyCode.F6))
                    {
                        Logger.LogInfo("F6: Steam features not available with fallback manager");
                    }
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
                if (_gameManager != null)
                {
                    _gameManager.StartSession();
                    Logger.LogInfo("Started hosting session with BasicGameManager!");
                    Logger.LogInfo($"Manager Active: {_gameManager.IsActive}");
                }
                else if (_fallbackManager != null)
                {
                    _fallbackManager.StartSession();
                    Logger.LogInfo("Started hosting session with fallback SimpleSessionManager!");
                    Logger.LogInfo($"Manager Active: {_fallbackManager.IsActive}");
                }
                else
                {
                    Logger.LogError("No game manager available (neither BasicGameManager nor fallback)");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start hosting: {e.Message}");            }
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
                    Logger.LogInfo($"Join session called with Steam ID: {steamIdString}");
                    
                    // Convert Steam ID to session format
                    string sessionId = $"steam_{steamIdString}";
                    
                    Logger.LogInfo($"Attempting to join session: {sessionId}");
                    _gameManager.JoinSession(sessionId);
                }
                else
                {
                    Logger.LogError($"Invalid Steam ID format: {steamIdString}");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to join session: {e.Message}");            }
        }
        
        public void StopMultiplayer()
        {
            try
            {
                if (_gameManager != null)
                {
                    _gameManager.StopSession();
                    Logger.LogInfo("Stopped session with BasicGameManager!");
                }
                else if (_fallbackManager != null)
                {
                    _fallbackManager.StopSession();
                    Logger.LogInfo("Stopped session with fallback SimpleSessionManager!");
                }
                else
                {
                    Logger.LogError("No game manager available to stop");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to stop multiplayer: {e.Message}");
            }
        }        public void ShowStatus()
        {
            Logger.LogInfo("=== GungeonTogether Status ===");
            
            if (_gameManager != null)
            {
                Logger.LogInfo("Using: BasicGameManager");
                Logger.LogInfo($"Session Active: {_gameManager.IsActive}");
                Logger.LogInfo($"Session ID: {_gameManager.CurrentSessionId ?? "None"}");
                Logger.LogInfo($"Is Host: {_gameManager.IsHost}");
                Logger.LogInfo($"Status: {_gameManager.Status}");
                Logger.LogInfo("Steam Integration: Ready");
            }
            else if (_fallbackManager != null)
            {
                Logger.LogInfo("Using: SimpleSessionManager (Fallback)");
                Logger.LogInfo($"Session Active: {_fallbackManager.IsActive}");
                Logger.LogInfo($"Status: {_fallbackManager.Status}");
                Logger.LogInfo("Steam Integration: Not Available");
            }
            else
            {
                Logger.LogInfo("Status: No manager initialized");
                Logger.LogInfo("Error: Mod failed to initialize properly");
            }
            
            Logger.LogInfo("Debug Controls: F3-F9 available");
        }private void TryJoinSteamFriend()
        {
            Logger.LogInfo("Testing Steam friend join functionality...");
            
            // Test with a fake Steam ID for development
            string testSteamId = "76561198000000001";
            SteamSessionHelper.JoinFriendSession(testSteamId);
            
            Logger.LogInfo("Steam friend join test completed");
        }
        
        private void ShowSteamInviteDialog()
        {
            try
            {
                if (!_gameManager.IsActive)
                {
                    Logger.LogWarning("Cannot show invite dialog - no active session");
                    return;
                }
                
                Logger.LogInfo("Showing Steam invite dialog...");
                SteamSessionHelper.ShowInviteDialog();
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to show invite dialog: {e.Message}");
            }
        }
        
        private void ShowFriendsPlayingGame()
        {
            try
            {
                Logger.LogInfo("Getting friends playing GungeonTogether...");
                string[] friends = SteamSessionHelper.GetFriendsPlayingGame();
                
                if (friends.Length == 0)
                {
                    Logger.LogInfo("No friends currently playing GungeonTogether");
                }
                else
                {
                    Logger.LogInfo($"Friends playing GungeonTogether ({friends.Length}):");
                    for (int i = 0; i < friends.Length; i++)
                    {
                        Logger.LogInfo($"  {i + 1}. Steam ID: {friends[i]}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to get friends list: {e.Message}");
            }
        }
        
        /// <summary>
        /// Handle Steam overlay "Join Game" requests
        /// </summary>
        public void HandleSteamJoinRequest(string steamLobbyId)
        {
            try
            {
                Logger.LogInfo($"Received Steam join request for lobby: {steamLobbyId}");
                SteamSessionHelper.HandleJoinGameRequest(steamLobbyId);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to handle Steam join request: {e.Message}");
            }
        }
        
        /// <summary>
        /// Simulate Steam overlay "Join Game" functionality for testing
        /// In real implementation, this would be called by Steam callbacks
        /// </summary>
        public void SimulateSteamOverlayJoin(string steamLobbyId = "test_lobby_12345")
        {
            try
            {
                Logger.LogInfo($"[SIMULATION] Steam overlay 'Join Game' clicked for lobby: {steamLobbyId}");
                HandleSteamJoinRequest(steamLobbyId);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to simulate Steam overlay join: {e.Message}");
            }
        }
        
        void OnDestroy()
        {
            try
            {
                // Cleanup session event handlers
                if (_gameManager != null)
                {
                    _gameManager.OnSessionStarted -= OnSessionStarted;
                    _gameManager.OnSessionStopped -= OnSessionStopped;
                    _gameManager.OnSessionJoined -= OnSessionJoined;
                    _gameManager.OnSessionJoinFailed -= OnSessionJoinFailed;
                }
                
                Logger.LogInfo("GungeonTogether mod cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error during cleanup: {e.Message}");
            }
        }
    }
}
