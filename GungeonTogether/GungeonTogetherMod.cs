using System;
using BepInEx;
using UnityEngine;
using GungeonTogether.Game;
using GungeonTogether.Steam;
using GungeonTogether.UI;

namespace GungeonTogether
{
    /// <summary>
    /// GungeonTogether mod for Enter the Gungeon using BepInEx
    /// Now includes beautiful modern UI for multiplayer functionality
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class GungeonTogetherMod : BaseUnityPlugin
    {
        // Mod metadata
        public const string GUID = "liamspc.etg.gungeontogether";
        public const string NAME = "GungeonTogether";
        public const string VERSION = "0.0.2"; // Updated version for UI release
        public static GungeonTogetherMod Instance { get; private set; }
        public SimpleSessionManager _sessionManager; // Made public for UI access
        
        // Public property for UI access
        public SimpleSessionManager SessionManager => _sessionManager;
        
        // UI System
        private bool uiInitialized = false;
        
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
            {
                // Check for Steam command line arguments for join requests
                CheckSteamCommandLineArgs();
                
                Logger.LogInfo("Initializing SimpleSessionManager (bypassing BasicGameManager)...");
                _sessionManager = new SimpleSessionManager();
                Logger.LogInfo("SimpleSessionManager created successfully!");
                
                // Initialize UI System
                InitializeUISystem();
                
                Logger.LogInfo("Setting up Steam integration...");
                try
                {
                    SteamSessionHelper.Initialize(_sessionManager);
                    
                    // Subscribe to Steam overlay join events
                    ETGSteamP2PNetworking.OnOverlayJoinRequested += OnSteamOverlayJoinRequested;
                    Logger.LogInfo("Subscribed to Steam overlay 'Join Game' events");
                    
                    Logger.LogInfo("Steam integration initialized!");
                }
                catch (Exception steamEx)
                {
                    Logger.LogWarning($"Steam integration failed: {steamEx.Message}");
                    Logger.LogInfo("Continuing without Steam features...");
                }
                
                Logger.LogInfo("Setting up debug controls...");
                SetupDebugControls();
                
            Logger.LogInfo("GungeonTogether initialized successfully!!!!!!! YAY!!!!!!!!!!!!!!!!!!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize GungeonTogether: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
            }        }

        /// <summary>
        /// Initialize the modern UI system
        /// </summary>
        private void InitializeUISystem()
        {
            try
            {
                Logger.LogInfo("Initializing modern UI system...");
                
                // Initialize the UI manager
                MultiplayerUIManager.Initialize();
                
                // Set the session manager reference for the UI
                if (_sessionManager is not null)
                {
                    MultiplayerUIManager.SetSessionManager(_sessionManager);
                }
                
                uiInitialized = true;
                Logger.LogInfo("Modern UI system initialized successfully!");
                Logger.LogInfo("Press Ctrl+M to open the multiplayer menu");
                
                // Initialize Steam P2P test script for debugging
                InitializeTestScript();
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize UI system: {e.Message}");
                uiInitialized = false;
            }
        }
        
        private void SetupEventHooks()
        {
            Logger.LogDebug("Setting up ETGMod event hooks...");
              // Hook into BepInEx event system - will use Update() and scene detection instead
            
            Logger.LogDebug("Event hooks registered");
        }          void Update()
        {
            // Update the session manager each frame (includes P2P networking and player sync)
            _sessionManager?.Update();
            
            // CRITICAL: Prevent game pausing when hosting a multiplayer session
            if (_sessionManager is not null && _sessionManager.IsActive && _sessionManager.IsHost)
            {
                PreventGamePauseWhenHosting();
            }
            
            // Handle debug input
            HandleDebugInput();
            
            // Handle UI input
            HandleUIInput();
        }
        
        /// <summary>
        /// Prevent the game from pausing when hosting a multiplayer session
        /// This ensures the server continues running even when ESC is pressed or menus are opened
        /// </summary>
        private void PreventGamePauseWhenHosting()
        {
            try
            {
                // Ensure Time.timeScale stays at 1.0 when hosting (this is the most reliable method)
                if (Time.timeScale != 1.0f)
                {
                    Time.timeScale = 1.0f;
                    
                    // Log once per pause attempt to inform user
                    if (Time.frameCount % 60 == 0) // Log once per second at 60fps
                    {
                        Logger.LogInfo("[Multiplayer] Game pause prevented - keeping server running (timeScale forced to 1.0)");
                    }
                }
                
                // Try a safer approach using method invocation instead of property access
                try
                {
                    var gameManagerType = System.Type.GetType("GameManager, Assembly-CSharp");
                    if (gameManagerType is not null)
                    {
                        // Get the static Instance field instead of property to avoid reflection issues
                        var instanceField = gameManagerType.GetField("Instance", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        
                        if (instanceField is not null)
                        {
                            var gameManager = instanceField.GetValue(null);
                            if (gameManager is not null)
                            {
                                // Try to find and manipulate pause-related fields only (avoiding properties)
                                var isPausedField = gameManagerType.GetField("m_isPaused", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                
                                if (isPausedField is not null)
                                {
                                    var pausedValue = isPausedField.GetValue(gameManager);
                                    if (pausedValue is bool isPaused && isPaused)
                                    {
                                        isPausedField.SetValue(gameManager, false);
                                        Logger.LogInfo("[Multiplayer] Overrode GameManager pause state - server continues running");
                                    }
                                }
                                
                                // Try alternative field names
                                var pausedField = gameManagerType.GetField("IsPaused", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                
                                if (pausedField is not null)
                                {
                                    var pausedValue = pausedField.GetValue(gameManager);
                                    if (pausedValue is bool isPaused && isPaused)
                                    {
                                        pausedField.SetValue(gameManager, false);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // Only log reflection errors once every 5 seconds to avoid spam
                    if (Time.frameCount % 300 == 0)
                    {
                        Logger.LogWarning($"[Multiplayer] GameManager pause override failed (using timeScale fallback): {ex.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {
                // Fallback error handling
                if (Time.frameCount % 300 == 0) // Log every ~5 seconds at 60fps
                {
                    Logger.LogWarning($"[Multiplayer] Pause prevention error (timeScale fallback active): {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Handle UI-specific input
        /// </summary>
        private void HandleUIInput()
        {
            try
            {
                // Ctrl+M to toggle main multiplayer UI
                if (Input.GetKeyDown(KeyCode.M) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    if (uiInitialized)
                    {
                        Logger.LogInfo("multiplayer ui toggled");
                        MultiplayerUIManager.ToggleUI();
                        
                        // Only show hosting notification if we just opened the UI and are hosting
                        // Don't spam it every time the menu is toggled
                        if (_sessionManager is not null && _sessionManager.IsActive && _sessionManager.IsHost)
                        {
                            // Only show the notification when opening the UI, not closing it
                            // Check if UI was just opened (you might need to track this state)
                            // For now, we'll remove the spam by not showing this notification here
                            // The user already gets feedback when they start hosting
                        }
                    }
                    else
                    {
                        Logger.LogWarning("UI broken idk");
                    }
                }
                
                // ESC key handling - intercept when hosting to inform user
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    // Check if we're hosting a multiplayer session
                    if (_sessionManager is not null && _sessionManager.IsActive && _sessionManager.IsHost)
                    {
                        // When hosting: close UI if open, show notification, but don't pause game
                        if (uiInitialized)
                        {
                            MultiplayerUIManager.HideUI();
                            MultiplayerUIManager.ShowNotification("Game cannot pause while hosting multiplayer server", 3f);
                        }
                        Logger.LogInfo("[Multiplayer] ESC pressed while hosting - UI closed, pause prevented to keep server running");
                    }
                    else
                    {
                        // Not hosting, allow normal ESC behavior (close our UI if open)
                        if (uiInitialized)
                        {
                            MultiplayerUIManager.HideUI();
                        }
                    }
                }
                
                // Ctrl+N to show notification test
                if (Input.GetKeyDown(KeyCode.N) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.ShowNotification("UI test notification - System working perfectly!", 3f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in UI input handling: {e.Message}");
            }
        }
        
        private void HandleDebugInput()
        {
            if (_sessionManager is null) return;
            
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
        
        // Multiplayer API with UI integration
        public void StartHosting()
        {
            try
            {
                if (_sessionManager is not null)
                {
                    _sessionManager.StartSession();
                    Logger.LogInfo("StartSession called on SimpleSessionManager");
                    
                    // Check if session actually started (could be blocked by location validation)
                    if (_sessionManager.IsActive)
                    {
                        Logger.LogInfo("Started hosting session with SimpleSessionManager!");
                        Logger.LogInfo($"Manager Active: {_sessionManager.IsActive}");
                        
                        // Notify UI and user about hosting status and pause prevention
                        if (uiInitialized)
                        {
                            MultiplayerUIManager.OnSessionStateChanged(true, true);
                            MultiplayerUIManager.ShowNotification("Hosting multiplayer server - game will not pause!", 5f);
                        }
                        
                        Logger.LogInfo("[Multiplayer] Game pause prevention is now active - server will continue running even when menus are opened");
                    }
                    else
                    {
                        // Session didn't start - likely due to location restriction
                        string status = _sessionManager.Status;
                        Logger.LogWarning($"Failed to start session: {status}");
                        
                        if (uiInitialized)
                        {
                            if (status.Contains("Cannot start session from"))
                            {
                                MultiplayerUIManager.ShowNotification("Multiplayer only available in Main Menu or Gungeon Foyer", 4f);
                            }
                            else
                            {
                                MultiplayerUIManager.ShowNotification($"Failed to start: {status}", 4f);
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogError("No session manager available");
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.ShowNotification("Session manager not available", 3f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start hosting: {e.Message}");
                if (uiInitialized)
                {
                    MultiplayerUIManager.ShowNotification($"Failed to start hosting: {e.Message}", 4f);
                }
            }
        }
        
        public void JoinSession(string steamIdString)
        {
            try
            {
                if (_sessionManager is null)
                {
                    Logger.LogError("SessionManager not initialized");
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.ShowNotification("Session manager not initialized", 3f);
                    }
                    return;
                }

                if (ulong.TryParse(steamIdString, out ulong steamId))
                {
                    Logger.LogInfo($"Join session called with Steam ID: {steamIdString}");
                    
                    // Convert Steam ID to session format
                    string sessionId = $"steam_{steamIdString}";
                    
                    Logger.LogInfo($"Attempting to join session: {sessionId}");
                    _sessionManager.JoinSession(sessionId);
                    
                    // Check if join actually started (could be blocked by location validation)
                    if (_sessionManager.IsActive)
                    {
                        // Notify UI
                        if (uiInitialized)
                        {
                            MultiplayerUIManager.ShowNotification($"Connecting to host: {steamIdString}", 3f);
                        }
                    }
                    else
                    {
                        // Join didn't start - likely due to location restriction
                        string status = _sessionManager.Status;
                        Logger.LogWarning($"Failed to join session: {status}");
                        
                        if (uiInitialized)
                        {
                            if (status.Contains("Cannot join session from"))
                            {
                                MultiplayerUIManager.ShowNotification("Multiplayer only available in Main Menu or Gungeon Foyer", 4f);
                            }
                            else
                            {
                                MultiplayerUIManager.ShowNotification($"Failed to join: {status}", 4f);
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogError($"Invalid Steam ID format: {steamIdString}");
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.ShowNotification($"Invalid Steam ID: {steamIdString}", 3f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to join session: {e.Message}");
                if (uiInitialized)
                {
                    MultiplayerUIManager.ShowNotification($"Failed to join: {e.Message}", 4f);
                }
            }
        }
        
        public void StopMultiplayer()
        {
            try
            {
                if (_sessionManager is not null)
                {
                    _sessionManager.StopSession();
                    Logger.LogInfo("Stopped session with SimpleSessionManager!");
                    
                    // Notify UI and user that hosting has stopped
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.OnSessionStateChanged(false, false);
                        MultiplayerUIManager.ShowNotification("Multiplayer session ended - normal pause behavior restored", 3f);
                    }
                    
                    Logger.LogInfo("[Multiplayer] Game pause prevention deactivated - normal pause behavior restored");
                }
                else
                {
                    Logger.LogError("No session manager available to stop");
                    if (uiInitialized)
                    {
                        MultiplayerUIManager.ShowNotification("No session to stop", 2f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to stop multiplayer: {e.Message}");
                if (uiInitialized)
                {
                    MultiplayerUIManager.ShowNotification($"Failed to stop: {e.Message}", 3f);
                }
            }
        }        public void ShowStatus()
        {
            Logger.LogInfo("=== GungeonTogether Status ===");
            
            if (_sessionManager is not null)
            {
                Logger.LogInfo("Using: SimpleSessionManager with AUTOMATIC Steam Integration");
                Logger.LogInfo($"Session Active: {_sessionManager.IsActive}");
                Logger.LogInfo($"Status: {_sessionManager.Status}");
                
                // Show Steam ID and hosting info
                try
                {
                    var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                    if (steamNet is not null && steamNet.IsAvailable())
                    {
                        ulong mySteamId = steamNet.GetSteamID();
                        Logger.LogInfo($"Your Steam ID: {mySteamId}");
                        
                        if (_sessionManager.IsActive && _sessionManager.IsHost)
                        {
                            Logger.LogInfo($"Server hosting!");
                            Logger.LogInfo("Friends can join you by:");
                            Logger.LogInfo("  • Using Steam overlay 'Join Game'");
                            Logger.LogInfo("  • Pressing F4 to auto-find your session");
                        }
                        
                        // Show available hosts
                        ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                        if (availableHosts.Length > 0)
                        {
                            Logger.LogInfo($"🔍 Available hosts ({availableHosts.Length}):");
                            for (int i = 0; i < availableHosts.Length; i++)
                            {
                                Logger.LogInfo($"{availableHosts[i]}");
                            }
                            Logger.LogInfo("Press F4 to auto-join the best available host!");
                        }
                        else
                        {
                            Logger.LogInfo("No available hosts found");
                        }
                        
                        // Show last invite info if available
                        ulong lastInvite = ETGSteamP2PNetworking.GetLastInviterSteamId();
                        if (lastInvite > 0)
                        {
                            Logger.LogInfo($"Priority invite from: {lastInvite}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Could not get Steam info: {e.Message}");
                }
            }
            else
            {
                Logger.LogInfo("Status: No manager initialized");
                Logger.LogInfo("Error: Mod failed to initialize properly");
            }
        }        private void TryJoinSteamFriend()
        {
            Logger.LogInfo("F7: Scanning for available hosts...");
            
            try
            {
                // Get available hosts automatically
                ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                
                if (availableHosts.Length == 0)
                {
                    Logger.LogInfo("No available hosts found");
                }
                else
                {
                    Logger.LogInfo($"Found {availableHosts.Length} available host(s):");
                    for (int i = 0; i < availableHosts.Length; i++)
                    {
                        Logger.LogInfo($"Host {i + 1}: Steam ID {availableHosts[i]}");
                    }
                    
                    // If there's exactly one host, we could auto-select it
                    if (availableHosts.Length == 1)
                    {
                        ulong selectedHost = availableHosts[0];
                        Logger.LogInfo($"Auto-selected host: {selectedHost}");
                        Logger.LogInfo("Press F4 to join this host!");
                    }
                }
                
                // Show current invite status
                ulong lastInvite = ETGSteamP2PNetworking.GetLastInviterSteamId();
                if (lastInvite > 0)
                {
                    Logger.LogInfo($"Active invite from: {lastInvite} (priority join target)");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scanning for hosts: {e.Message}");
            }
        }
          private void ShowSteamInviteDialog()
        {
            try
            {
                if (_sessionManager is null || !_sessionManager.IsActive)
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
        /// Event handler for Steam overlay "Join Game" requests
        /// This gets called automatically when someone clicks "Join Game" in the Steam overlay
        /// </summary>
        private void OnSteamOverlayJoinRequested(string hostSteamId)
        {
            try
            {
                Logger.LogInfo($"'Join Game' event received for host: {hostSteamId}");
                
                // Notify UI
                if (uiInitialized)
                {
                    MultiplayerUIManager.OnSteamJoinRequested(hostSteamId);
                }
                
                // Use the Steam session helper to handle the join
                SteamSessionHelper.HandleJoinGameRequest($"steam_lobby_{hostSteamId}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to handle join event: {e.Message}");
                if (uiInitialized)
                {
                    MultiplayerUIManager.OnSteamConnectionFailed(e.Message);
                }
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
                Logger.LogInfo($"F9: Simulating Steam overlay 'Join Game' click...");
                
                // Test direct overlay join event firing
                var steamNetwork = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNetwork is not null && steamNetwork.IsAvailable())
                {
                    ulong mySteamId = steamNetwork.GetSteamID();
                    
                    // Check for available hosts first
                    ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                    
                    if (availableHosts.Length > 0)
                    {
                        // Use the first available host for simulation
                        ulong simulatedHostSteamId = availableHosts[0];
                        Logger.LogInfo($"Found real host, simulating overlay invite from: {simulatedHostSteamId}");
                        
                        // Set up the invite as if it came from Steam overlay
                        ETGSteamP2PNetworking.SetInviteInfo(simulatedHostSteamId, steamLobbyId);
                        
                        // Fire the overlay join event directly using public method
                        Logger.LogInfo($"Firing OnOverlayJoinRequested event for Steam ID: {simulatedHostSteamId}");
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(simulatedHostSteamId.ToString());
                        
                        Logger.LogInfo("Steam overlay join simulation complete - check for join activity!");
                    }
                    else
                    {

                        // Create a fake host for testing the overlay join system
                        ulong fakeHostId = mySteamId + 1;
                        Logger.LogInfo($"Testing with fake host: {fakeHostId}");
                        Logger.LogInfo("(This tests the overlay join flow, but P2P connection will fail as expected)");
                        
                        ETGSteamP2PNetworking.SetInviteInfo(fakeHostId, steamLobbyId);
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(fakeHostId.ToString());
                    }
                }
                else
                {
                    Logger.LogError("Steam networking not available for overlay join simulation");
                }
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
                // Unsubscribe from Steam events
                ETGSteamP2PNetworking.OnOverlayJoinRequested -= OnSteamOverlayJoinRequested;
                Logger.LogInfo("Unsubscribed from Steam events");
                
                // Clean up session manager
                _sessionManager?.StopSession();
                
                // Clean up UI system
                if (uiInitialized)
                {
                    MultiplayerUIManager.Cleanup();
                }
                
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
                if (_sessionManager is null)
                {
                    Logger.LogError("No session manager available for joining");
                    return;
                }
                
                // AUTOMATIC: Find the best available host
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNet is not null && steamNet.IsAvailable())
                {
                    // Get the best available host automatically
                    ulong hostSteamId = ETGSteamP2PNetworking.GetBestAvailableHost();
                    
                    if (hostSteamId > 0)
                    {
                        ulong mySteamId = steamNet.GetSteamID();
                        if (mySteamId == hostSteamId)
                        {
                            Logger.LogInfo("Cannot join yourself!");
                            return;
                        }
                        
                        Logger.LogInfo($"F4: Auto-joining host Steam ID: {hostSteamId}");
                        
                        // Join using the automatically selected host
                        SteamSessionHelper.HandleJoinGameRequest($"auto_join_{hostSteamId}");
                        Logger.LogInfo($"Automatically joined session: {hostSteamId}");
                    }
                    else
                    {
                        // Check available hosts
                        ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                        
                        if (availableHosts.Length == 0)
                        {
                            Logger.LogInfo("F4: No available hosts found");
                            Logger.LogInfo("How to connect:");
                            Logger.LogInfo("   1. Have someone host a session (F3)");
                            Logger.LogInfo("   2. They will automatically appear as available");
                            Logger.LogInfo("   3. Press F4 again to auto-join them");
                            Logger.LogInfo("   4. Or use Steam overlay 'Join Game' for instant connection");
                        }
                        else
                        {
                            Logger.LogInfo($" Found {availableHosts.Length} hosts but none are suitable for joining");
                            Logger.LogInfo("Available hosts:");
                            for (int i = 0; i < availableHosts.Length; i++)
                            {
                                Logger.LogInfo($"Host {i + 1}: Steam ID {availableHosts[i]}");
                            }
                        }
                    }
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
        
        /// <summary>
        /// Check for Steam command line arguments that indicate a join request
        /// Steam passes arguments like "+connect_lobby [lobbyid]" or "+connect [steamid]"
        /// </summary>
        private void CheckSteamCommandLineArgs()
        {
            try
            {
                string[] args = System.Environment.GetCommandLineArgs();
                Logger.LogInfo($"[Steam Args] Checking {args.Length} command line arguments...");
                
                for (int i = 0; i < args.Length; i++)
                {
                    Logger.LogInfo($"[Steam Args] Arg {i}: {args[i]}");
                    
                    // Check for Steam connect commands
                    if (args[i].StartsWith("+connect") && i + 1 < args.Length)
                    {
                        string connectTarget = args[i + 1];
                        Logger.LogInfo($"[Steam Args] Found Steam connect command: {args[i]} {connectTarget}");
                        
                        // Try to parse as Steam ID
                        if (ulong.TryParse(connectTarget, out ulong steamId) && steamId > 0)
                        {
                            Logger.LogInfo($"[Steam Args] Detected Steam overlay join request for Steam ID: {steamId}");
                            
                            // Set this as a pending join request
                            ETGSteamP2PNetworking.SetInviteInfo(steamId);
                            
                            // Schedule automatic join after initialization
                            ScheduleAutoJoin(steamId);
                        }
                    }
                    
                    // Check for lobby connect commands
                    if (args[i].StartsWith("+connect_lobby") && i + 1 < args.Length)
                    {
                        string lobbyId = args[i + 1];
                        Logger.LogInfo($"[Steam Args] Found Steam lobby connect command: {args[i]} {lobbyId}");
                        
                        // Try to parse lobby ID and extract host Steam ID
                        if (ulong.TryParse(lobbyId, out ulong parsedLobbyId) && parsedLobbyId > 0)
                        {
                            Logger.LogInfo($"[Steam Args] Detected Steam lobby join request for lobby: {parsedLobbyId}");
                            
                            // For now, treat lobby ID as potential Steam ID
                            ETGSteamP2PNetworking.SetInviteInfo(parsedLobbyId, lobbyId);
                            ScheduleAutoJoin(parsedLobbyId);
                        }
                    }
                }
                
                Logger.LogInfo("[Steam Args] Command line argument check complete");
            }
            catch (Exception e)
            {
                Logger.LogError($"[Steam Args] Error checking command line arguments: {e.Message}");
            }
        }
        
        /// <summary>
        /// Schedule an automatic join after the session manager is initialized
        /// </summary>
        private void ScheduleAutoJoin(ulong hostSteamId)
        {
            try
            {
                Logger.LogInfo($"[Steam Args] Scheduling auto-join for Steam ID: {hostSteamId}");
                
                // Use a coroutine-like approach with Unity's Invoke
                Invoke(nameof(ExecuteScheduledJoin), 2.0f); // Wait 2 seconds for initialization
                
                // Store the target for the delayed join
                scheduledJoinTarget = hostSteamId;
            }
            catch (Exception e)
            {
                Logger.LogError($"[Steam Args] Error scheduling auto-join: {e.Message}");
            }
        }
        
        private ulong scheduledJoinTarget = 0;
        
        /// <summary>
        /// Execute the scheduled join operation
        /// </summary>
        private void ExecuteScheduledJoin()
        {
            try
            {
                if (scheduledJoinTarget > 0 && _sessionManager is not null)
                {
                    Logger.LogInfo($"[Steam Args] Executing scheduled join for Steam ID: {scheduledJoinTarget}");
                    
                    // Join the specified session
                    string sessionId = $"steam_{scheduledJoinTarget}";
                    _sessionManager.JoinSession(sessionId);
                    
                    scheduledJoinTarget = 0; // Clear after use
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[Steam Args] Error executing scheduled join: {e.Message}");
            }
        }
        
        /// <summary>
        /// Setup debug controls and display help information
        /// </summary>
        private void SetupDebugControls()
        {
        }
        
        /// <summary>
        /// Initialize Steam P2P test script for debugging and packet testing
        /// </summary>
        private void InitializeTestScript()
        {
            try
            {
                Logger.LogInfo("Initializing Steam P2P test script...");
                
                // Create a GameObject to hold the test script
                var testObject = new GameObject("SteamP2PTestScript");
                
                // Don't destroy on load so it persists across scenes
                DontDestroyOnLoad(testObject);
                
                // Add the test script component
                var testScript = testObject.AddComponent<SteamP2PTestScript>();
                
                Logger.LogInfo("Steam P2P test script initialized successfully!");
                Logger.LogInfo("Use F8-F12 keys for Steam P2P testing (see console for controls)");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize Steam P2P test script: {e.Message}");
            }
        }
    }
}
