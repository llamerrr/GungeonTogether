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
            {
                // Check for Steam command line arguments for join requests
                CheckSteamCommandLineArgs();
                
                Logger.LogInfo("Initializing SimpleSessionManager (bypassing BasicGameManager)...");
                _sessionManager = new SimpleSessionManager();
                Logger.LogInfo("SimpleSessionManager created successfully!");
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
            Logger.LogInfo("🎮 AUTOMATIC Multiplayer System Enabled!");
            Logger.LogInfo("No manual Steam ID setup required - everything is automatic!");
            Logger.LogInfo("");
            Logger.LogInfo("Debug controls:");
            Logger.LogInfo("  F3 - Start hosting (auto-registers as discoverable host)");
            Logger.LogInfo("  F4 - Auto-join best available host or pending invite");
            Logger.LogInfo("  F5 - Stop multiplayer session");
            Logger.LogInfo("  F6 - Show status, your Steam ID, and available hosts");
            Logger.LogInfo("  F7 - Scan for available hosts");
            Logger.LogInfo("  F8 - Show Steam invite dialog");
            Logger.LogInfo("  F9 - Simulate Steam overlay 'Join Game' click");
            Logger.LogInfo("  F10 - Run ETG Steam diagnostics");
            Logger.LogInfo("");
            Logger.LogInfo("🚀 How to use (SUPER SIMPLE):");
            Logger.LogInfo("1. Player 1: Press F3 to host (auto-discoverable)");
            Logger.LogInfo("2. Player 2: Press F4 to auto-join Player 1");
            Logger.LogInfo("3. That's it! No Steam IDs, no manual setup!");
            Logger.LogInfo("");
            Logger.LogInfo("Or use Steam overlay 'Join Game' for instant connection!");
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
                Logger.LogInfo("Using: SimpleSessionManager with AUTOMATIC Steam Integration");
                Logger.LogInfo($"Session Active: {_sessionManager.IsActive}");
                Logger.LogInfo($"Status: {_sessionManager.Status}");
                
                // Show Steam ID and hosting info
                try
                {
                    var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                    if (steamNet != null && steamNet.IsAvailable())
                    {
                        ulong mySteamId = steamNet.GetSteamID();
                        Logger.LogInfo($"Your Steam ID: {mySteamId}");
                        
                        if (_sessionManager.IsActive && _sessionManager.IsHost)
                        {
                            Logger.LogInfo($"� HOSTING: You are automatically discoverable!");
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
                                Logger.LogInfo($"   🏠 {availableHosts[i]}");
                            }
                            Logger.LogInfo("Press F4 to auto-join the best available host!");
                        }
                        else
                        {
                            Logger.LogInfo("🔍 No available hosts found");
                        }
                        
                        // Show last invite info if available
                        ulong lastInvite = ETGSteamP2PNetworking.GetLastInviterSteamId();
                        if (lastInvite != 0)
                        {
                            Logger.LogInfo($"📨 Priority invite from: {lastInvite}");
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
            
            Logger.LogInfo("Debug Controls: F3=Host, F4=Auto-Join, F5=Stop, F6=Status");
            Logger.LogInfo("Steam Features: F7=Scan Hosts, F8=Friends, F9=Overlay Join, F10=Diagnostics");
        }        private void TryJoinSteamFriend()
        {
            Logger.LogInfo("🔍 F7: Scanning for available hosts...");
            
            try
            {
                // Get available hosts automatically
                ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                
                if (availableHosts.Length == 0)
                {
                    Logger.LogInfo("❌ No available hosts found");
                    Logger.LogInfo("💡 To find hosts:");
                    Logger.LogInfo("   • Wait for friends to start hosting (F3)");
                    Logger.LogInfo("   • Hosts automatically broadcast their availability");
                    Logger.LogInfo("   • Use Steam overlay 'Join Game' for instant connection");
                }
                else
                {
                    Logger.LogInfo($"✅ Found {availableHosts.Length} available host(s):");
                    for (int i = 0; i < availableHosts.Length; i++)
                    {
                        Logger.LogInfo($"   🏠 Host {i + 1}: Steam ID {availableHosts[i]}");
                    }
                    
                    Logger.LogInfo("🎮 Press F4 to automatically join the best available host!");
                    
                    // If there's exactly one host, we could auto-select it
                    if (availableHosts.Length == 1)
                    {
                        ulong selectedHost = availableHosts[0];
                        Logger.LogInfo($"🎯 Auto-selected host: {selectedHost}");
                        Logger.LogInfo("Press F4 to join this host!");
                    }
                }
                
                // Show current invite status
                ulong lastInvite = ETGSteamP2PNetworking.GetLastInviterSteamId();
                if (lastInvite != 0)
                {
                    Logger.LogInfo($"📨 Active invite from: {lastInvite} (priority join target)");
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
        /// Event handler for Steam overlay "Join Game" requests
        /// This gets called automatically when someone clicks "Join Game" in the Steam overlay
        /// </summary>
        private void OnSteamOverlayJoinRequested(string hostSteamId)
        {
            try
            {
                Logger.LogInfo($"🚀 REAL Steam overlay 'Join Game' event received for host: {hostSteamId}");
                Logger.LogInfo("This is a REAL overlay join event, not a simulation!");
                
                // Use the Steam session helper to handle the join
                SteamSessionHelper.HandleJoinGameRequest($"steam_lobby_{hostSteamId}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to handle real Steam overlay join event: {e.Message}");
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
                Logger.LogInfo($"🎮 F9: Simulating Steam overlay 'Join Game' click...");
                
                // Test direct overlay join event firing
                var steamNetwork = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNetwork != null && steamNetwork.IsAvailable())
                {
                    ulong mySteamId = steamNetwork.GetSteamID();
                    
                    // Check for available hosts first
                    ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                    
                    if (availableHosts.Length > 0)
                    {
                        // Use the first available host for simulation
                        ulong simulatedHostSteamId = availableHosts[0];
                        Logger.LogInfo($"📡 Found real host, simulating overlay invite from: {simulatedHostSteamId}");
                        
                        // Set up the invite as if it came from Steam overlay
                        ETGSteamP2PNetworking.SetInviteInfo(simulatedHostSteamId, steamLobbyId);
                        
                        // Fire the overlay join event directly using public method
                        Logger.LogInfo($"🔥 Firing OnOverlayJoinRequested event for Steam ID: {simulatedHostSteamId}");
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(simulatedHostSteamId.ToString());
                        
                        Logger.LogInfo("✅ Steam overlay join simulation complete - check for join activity!");
                    }
                    else
                    {
                        Logger.LogWarning("❌ No available hosts found for overlay join simulation");
                        Logger.LogInfo("💡 To test overlay join properly:");
                        Logger.LogInfo("   1. First press F3 to start hosting");
                        Logger.LogInfo("   2. Then press F9 to simulate someone joining your session");
                        Logger.LogInfo("   3. Or test with a friend hosting on another computer");
                        Logger.LogInfo("");
                        Logger.LogInfo("🔧 Creating fake host for testing overlay join system...");
                        
                        // Create a fake host for testing the overlay join system
                        ulong fakeHostId = mySteamId + 1;
                        Logger.LogInfo($"🎭 Testing with fake host: {fakeHostId}");
                        Logger.LogInfo("(This tests the overlay join flow, but P2P connection will fail as expected)");
                        
                        ETGSteamP2PNetworking.SetInviteInfo(fakeHostId, steamLobbyId);
                        ETGSteamP2PNetworking.TriggerOverlayJoinEvent(fakeHostId.ToString());
                    }
                }
                else
                {
                    Logger.LogError("❌ Steam networking not available for overlay join simulation");
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
                
                // AUTOMATIC: Find the best available host
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNet != null && steamNet.IsAvailable())
                {
                    // Get the best available host automatically
                    ulong hostSteamId = ETGSteamP2PNetworking.GetBestAvailableHost();
                    
                    if (hostSteamId != 0)
                    {
                        ulong mySteamId = steamNet.GetSteamID();
                        if (mySteamId == hostSteamId)
                        {
                            Logger.LogInfo("🚫 Cannot join yourself!");
                            return;
                        }
                        
                        Logger.LogInfo($"🎯 F4: Auto-joining host Steam ID: {hostSteamId}");
                        
                        // Join using the automatically selected host
                        SteamSessionHelper.HandleJoinGameRequest($"auto_join_{hostSteamId}");
                        Logger.LogInfo($"✅ Automatically joined session: {hostSteamId}");
                    }
                    else
                    {
                        // Check available hosts
                        ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                        
                        if (availableHosts.Length == 0)
                        {
                            Logger.LogInfo("🔍 F4: No available hosts found");
                            Logger.LogInfo("💡 How to connect:");
                            Logger.LogInfo("   1. Have someone host a session (F3)");
                            Logger.LogInfo("   2. They will automatically appear as available");
                            Logger.LogInfo("   3. Press F4 again to auto-join them");
                            Logger.LogInfo("   4. Or use Steam overlay 'Join Game' for instant connection");
                        }
                        else
                        {
                            Logger.LogInfo($"🤔 Found {availableHosts.Length} hosts but none are suitable for joining");
                            Logger.LogInfo("Available hosts:");
                            for (int i = 0; i < availableHosts.Length; i++)
                            {
                                Logger.LogInfo($"   🏠 Host {i + 1}: Steam ID {availableHosts[i]}");
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
                        if (ulong.TryParse(connectTarget, out ulong steamId) && steamId != 0)
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
                        if (ulong.TryParse(lobbyId, out ulong parsedLobbyId) && parsedLobbyId != 0)
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
                if (scheduledJoinTarget != 0 && _sessionManager != null)
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
    }
}
