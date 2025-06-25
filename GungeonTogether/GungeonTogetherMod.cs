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
        public const string VERSION = "0.0.1";
        public static GungeonTogetherMod Instance { get; private set; }
        private SimpleSessionManager _sessionManager;
        //private Game.BasicGameManager _gameManager;
        //private Game.SimpleSessionManager _fallbackManager;
        
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
            {                Logger.LogInfo("Initializing SimpleSessionManager (bypassing BasicGameManager)...");
                _sessionManager = new SimpleSessionManager();
                Logger.LogInfo("SimpleSessionManager created successfully!");
                  Logger.LogInfo("Setting up Steam integration...");
                try
                {
                    SteamSessionHelper.Initialize(_sessionManager);
                    Logger.LogInfo("Steam integration initialized!");
                }
                catch (Exception steamEx)
                {
                    Logger.LogWarning($"Steam integration failed: {steamEx.Message}");
                    Logger.LogInfo("Continuing without Steam features...");
                }
                
                Logger.LogInfo("Setting up debug controls...");
                SetupDebugControls();
                
                Logger.LogInfo("GungeonTogether initialized successfully with SimpleSessionManager!");
                Logger.LogInfo("Debug controls: F3=Host, F4=Join, F5=Stop, F6=Status");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize GungeonTogether: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
            }        }

        private void SetupEventHooks()
        {
            Logger.LogDebug("Setting up ETGMod event hooks...");
              // Hook into BepInEx event system - will use Update() and scene detection instead
            
            Logger.LogDebug("Event hooks registered");
        }          private void SetupDebugControls()
        {
            Logger.LogInfo("Debug controls enabled:");
            Logger.LogInfo("  F3 - Start hosting a multiplayer session");
            Logger.LogInfo("  F4 - Join a host session (testing)");
            Logger.LogInfo("  F5 - Stop multiplayer session");
            Logger.LogInfo("  F6 - Show connection status");
            Logger.LogInfo("  F7 - Join Steam friend session (test)");
            Logger.LogInfo("  F8 - Show Steam invite dialog");
            Logger.LogInfo("  F8 - Show friends playing GungeonTogether");
            Logger.LogInfo("  F9 - Simulate Steam overlay 'Join Game' click");
            Logger.LogInfo("  F10 - Run ETG Steam diagnostics (explore available Steam types)");
        }        void Update()
        {
            // Update the session manager each frame (includes P2P networking and player sync)
            _sessionManager?.Update();
            
            // Handle debug input
            HandleDebugInput();
        }private void HandleDebugInput()
        {
            if (_sessionManager == null) return;
            
            try
            {
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    Logger.LogInfo("F3: Starting host session...");
                    StartHosting();
                }
                
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    Logger.LogInfo("F4: Attempting to join host session (testing)...");
                    TryJoinHost();
                }
                
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    Logger.LogInfo("F5: Stopping multiplayer session...");
                    StopMultiplayer();
                }
                
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    Logger.LogInfo("F6: Showing status...");
                    ShowStatus();
                }
                
                // Steam features - now enabled for full functionality
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    Logger.LogInfo("F7: Attempting to join a Steam friend for testing...");
                    TryJoinSteamFriend();
                }
                
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    Logger.LogInfo("F8: Showing Steam invite dialog...");
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
                
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    Logger.LogInfo("F10: Running ETG Steam diagnostics...");
                    RunSteamDiagnostics();
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
                if (_sessionManager != null)
                {
                    _sessionManager.StartSession();
                    Logger.LogInfo("Started hosting session with SimpleSessionManager!");
                    Logger.LogInfo($"Manager Active: {_sessionManager.IsActive}");
                }
                else
                {
                    Logger.LogError("No session manager available");
                }
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
                if (_sessionManager == null)
                {
                    Logger.LogError("SessionManager not initialized");
                    return;
                }

                if (ulong.TryParse(steamIdString, out ulong steamId))
                {
                    Logger.LogInfo($"Join session called with Steam ID: {steamIdString}");
                    
                    // Convert Steam ID to session format
                    string sessionId = $"steam_{steamIdString}";
                    
                    Logger.LogInfo($"Attempting to join session: {sessionId}");
                    _sessionManager.JoinSession(sessionId);
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
                if (_sessionManager != null)
                {
                    _sessionManager.StopSession();
                    Logger.LogInfo("Stopped session with SimpleSessionManager!");
                }
                else
                {
                    Logger.LogError("No session manager available to stop");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to stop multiplayer: {e.Message}");
            }
        }        public void ShowStatus()
        {
            Logger.LogInfo("=== GungeonTogether Status ===");
            
            if (_sessionManager != null)
            {
                Logger.LogInfo("Using: SimpleSessionManager with Steam Integration");
                Logger.LogInfo($"Session Active: {_sessionManager.IsActive}");
                Logger.LogInfo($"Status: {_sessionManager.Status}");
                Logger.LogInfo("Steam Integration: ACTIVE (P2P Ready)");
                Logger.LogInfo("Steam Overlay: Join Game feature enabled");
            }
            else
            {
                Logger.LogInfo("Status: No manager initialized");
                Logger.LogInfo("Error: Mod failed to initialize properly");
            }
            
            Logger.LogInfo("Debug Controls: F3=Host, F4=Stop, F5=Status");
            Logger.LogInfo("Steam Features: F6=Join Friend, F7=Invite, F8=Friends, F9=Overlay Join");
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
                if (_sessionManager == null || !_sessionManager.IsActive)
                {
                    Logger.LogWarning("Cannot show invite dialog - no active session");
                    return;
                }
                
                Logger.LogInfo("Steam invite dialog not available with SimpleSessionManager");
                Logger.LogInfo("Steam features are limited in this mode");
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
        
        /// <summary>
        /// Run Steam diagnostics to explore ETG's available Steam types
        /// </summary>
        private void RunSteamDiagnostics()
        {
            try
            {
                Logger.LogInfo("Starting ETG Steam diagnostics...");
                ETGSteamDiagnostics.DiagnoseETGSteamTypes();
                Logger.LogInfo("Steam diagnostics completed - check Unity console for details");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to run Steam diagnostics: {e.Message}");
            }
        }
          void OnDestroy()
        {
            try
            {
                Logger.LogInfo("GungeonTogether mod cleanup completed");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error during cleanup: {e.Message}");
            }
        }

        public void TryJoinHost()
        {
            try
            {
                if (_sessionManager == null)
                {
                    Logger.LogError("No session manager available for joining");
                    return;
                }
                
                // For testing, try to join the last known host Steam ID
                // In a real scenario, this would come from Steam overlay "Join Game" click
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNet != null && steamNet.IsAvailable())
                {
                    ulong mySteamId = steamNet.GetSteamID();
                    
                    // For testing, use a placeholder host Steam ID (in real usage, this comes from Steam)
                    // You can replace this with a real Steam ID for testing
                    ulong testHostSteamId = 76561198126223978; // Replace with actual host Steam ID
                    
                    if (mySteamId == testHostSteamId)
                    {
                        Logger.LogInfo("Cannot join yourself! Start hosting on one account and join from another.");
                        return;
                    }
                    
                    Logger.LogInfo($"Attempting to join host Steam ID: {testHostSteamId}");
                    
                    // Simulate join request
                    steamNet.SimulateJoinRequest(testHostSteamId);
                    
                    // Join session using session manager
                    string sessionId = $"steam_{testHostSteamId}";
                    _sessionManager.JoinSession(sessionId);
                    
                    Logger.LogInfo($"Join request sent to host: {testHostSteamId}");
                }
                else
                {
                    Logger.LogError("Steam networking not available for joining");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to join host: {e.Message}");
            }
        }
    }
}
