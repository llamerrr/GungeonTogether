using BepInEx;
using GungeonTogether.Game;
using GungeonTogether.Steam;
using GungeonTogether.UI;
using HarmonyLib;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GungeonTogether
{
    /// <summary>
    /// GungeonTogether mod for Enter the Gungeon using BepInEx
    /// Full multiplayer mod with Steam P2P networking
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class GungeonTogetherMod : BaseUnityPlugin
    {
        // Mod metadata
        public const string GUID = "liamspc.etg.gungeontogether";
        public const string NAME = "GungeonTogether";
        public const string VERSION = "0.1.0"; // Updated version for networking release
        public static GungeonTogetherMod Instance { get; private set; }
        public SimpleSessionManager _sessionManager; // Made public for UI access

        // Public property for UI access
        public SimpleSessionManager SessionManager => _sessionManager;

        // UI System
        private bool uiInitialized = false;

        // Networking and synchronization systems
        private bool networkingInitialized = false;

    // Config entries
    internal static BepInEx.Configuration.ConfigEntry<string> ConfigSyncedItemCategories;
    internal static BepInEx.Configuration.ConfigEntry<string> ConfigDuplicatedItemCategories;

        public void Awake()
        {
            Instance = this;
            Logger.LogInfo("GungeonTogether mod loading...");

            // Bind configuration
            try
            {
                ConfigSyncedItemCategories = Config.Bind("Items", "SyncedCategories", "Gun,Passive,Active", "Item categories that should have a single shared instance (pickup removes for everyone). Use comma-separated values of: Gun,Passive,Active,Consumable,Currency,Key,Heart,Armor,Other");
                ConfigDuplicatedItemCategories = Config.Bind("Items", "DuplicatedCategories", "Consumable,Currency,Key,Heart,Armor", "Item categories that should duplicate (each player can pick separately). If a category appears in both this and SyncedCategories, SyncedCategories wins.");
            }
            catch (Exception cfgEx)
            {
                Logger.LogWarning($"Failed to bind config entries: {cfgEx.Message}");
            }

            // Prepare Harmony instance; actual PatchAll deferred until GameManager alive
            try { _harmony = new Harmony(GUID); } catch (Exception e) { Logger.LogError("Failed to create Harmony instance: " + e.Message); }

            try
            {
                // CRITICAL: Initialize Steam callbacks IMMEDIATELY to catch early join requests
                Logger.LogInfo("Initializing Steam callbacks early to catch join requests...");
                try
                {
                    // Initialize Steam reflection helper first
                    SteamReflectionHelper.InitializeSteamTypes();

                    // Initialize Steam callbacks as early as possible
                    SteamCallbackManager.InitializeSteamCallbacks();

                    // Initialize networking sockets helper
                    if (SteamNetworkingSocketsHelper.Initialize())
                    {
                        Logger.LogInfo("Steam Networking Sockets initialized successfully");
                        // Enable relay after Steamworks is initialized
                        SteamNetworkingSocketsHelper.EnableRelayIfReady();
                    }
                    else
                    {
                        Logger.LogWarning("Failed to initialize Steam Networking Sockets");
                    }

                    // Check command line arguments for Steam join requests
                    CheckSteamCommandLineArgs();

                    Logger.LogInfo("Early Steam initialization complete!");
                }
                catch (Exception steamEx)
                {
                    Logger.LogWarning($"Early Steam initialization failed: {steamEx.Message}");
                    Logger.LogInfo("Will retry Steam initialization later...");
                }

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
                // Apply all Harmony patches now that game types should be loaded
                try
                {
                    if (_harmony != null)
                    {
                        Logger.LogInfo("Applying Harmony patches (deferred)...");
                        _harmony.PatchAll();
                        Logger.LogInfo("Harmony PatchAll base complete. (Item pickup Harmony patch disabled; using fallback detection)");
                        // PatchItemPickupManually(); // Disabled due to IL compile error; relying on ItemSynchronizer fallback detection
                    }
                }
                catch (Exception hpEx)
                {
                    Logger.LogError("Harmony PatchAll failed: " + hpEx.Message + "\n" + hpEx.StackTrace);
                }
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

                Logger.LogInfo("Skipping legacy debug control setup (stubs)");

                // Initialize networking and synchronization systems
                InitializeNetworking();

                Logger.LogInfo("GungeonTogether initialized successfully!!!!!!! YAY!!!!!!!!!!!!!!!!!!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize GungeonTogether: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Initialize the modern UI system
        /// </summary>
        private void InitializeUISystem()
        {
            try
            {
                Logger.LogInfo("Initializing modern UI system...");

                // Initialize the MultiplayerUIManager first
                MultiplayerUIManager.Initialize();

                // Create a GameObject for the modern menu
                var modernMenuObject = new GameObject("ModernMultiplayerMenu");
                UnityEngine.Object.DontDestroyOnLoad(modernMenuObject);

                // Add the ModernMultiplayerMenu component
                var modernMenu = modernMenuObject.AddComponent<ModernMultiplayerMenu>();

                uiInitialized = true;
                Logger.LogInfo("Modern UI system initialized successfully!");
                Logger.LogInfo("====================================");
                Logger.LogInfo(" GungeonTogether Ready!");
                Logger.LogInfo("Press Ctrl+P to open multiplayer menu");
                Logger.LogInfo("All multiplayer actions are in the UI");
                Logger.LogInfo("====================================");
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
        }

        void Update()
        {

            // Update the session manager each frame (includes P2P networking and player sync)
            _sessionManager?.Update();

            // Update networking systems
            if (networkingInitialized)
            {
                NetworkManager.Instance.Update();

                if (_sessionManager is not null && _sessionManager.IsActive)
                {
                    // Removed noisy log: PlayerSynchroniser.StaticUpdate() on HOST/JOINER
                    PlayerSynchroniser.StaticUpdate();
                    EnemySynchronizer.StaticUpdate();
                    ProjectileSynchronizer.StaticUpdate();
                    ItemSynchronizer.Instance.Update();
                }
            }
            else
            {
                if (Time.frameCount % 300 == 0)
                    Logger.LogInfo($"[GT Update] networkingInitialized is false");
            }


            // FINAL FALLBACK: If networking is initialized and not host, always run synchronizers for joiner
            if (networkingInitialized && (_sessionManager == null || (_sessionManager != null && !_sessionManager.IsHost)))
            {
                PlayerSynchroniser.StaticUpdate();
                EnemySynchronizer.StaticUpdate();
                ProjectileSynchronizer.StaticUpdate();
                ItemSynchronizer.Instance.Update();
            }

            // CATCH-ALL: If this process is not the host, always run synchronizer update
            if (_sessionManager == null || (_sessionManager != null && !_sessionManager.IsHost))
            {
                PlayerSynchroniser.StaticUpdate();
                EnemySynchronizer.StaticUpdate();
                ProjectileSynchronizer.StaticUpdate();
                ItemSynchronizer.Instance.Update();
            }

            // CRITICAL: Process Steam callbacks every frame to catch join requests
            try
            {
                SteamCallbackManager.ProcessSteamCallbacks();
            }
            catch (Exception e)
            {
                // Only log errors occasionally to avoid spam
                if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
                {
                    Logger.LogWarning($"Error processing Steam callbacks: {e.Message}");
                }
            }


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
                if (!Time.timeScale.Equals(1.0f))
                {
                    Time.timeScale = 1.0f;
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
                {                // Only log reflection errors once every 5 seconds to avoid spam
                    if (Time.frameCount % 300 == 0)
                    {
                        Logger.LogWarning($"[Multiplayer] GameManager pause override failed (using timeScale fallback): {ex.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {            // Fallback error handling
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
                // All UI input is now handled by ModernMultiplayerMenu (Ctrl+P)
                // Legacy Ctrl+M support removed for unified experience

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

        /// <summary>
        /// Handle debug input - only developer debugging keys remain
        /// All user functionality is now in the UI (Ctrl+P)
        /// </summary>
        private void HandleDebugInput()
        {
            if (_sessionManager is null) return;

            try
            {
                // F1: Toggle comprehensive debug UI
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    Logger.LogInfo("F1: Toggling comprehensive debug UI...");
                    DebugUIManager.ToggleVisibility();
                }

                // Dungeon debugging controls
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    Logger.LogInfo("F5: Loading saved dungeon...");
                    GungeonTogether.Debug.DungeonSaveLoad.LoadDungeon();
                }

                if (Input.GetKeyDown(KeyCode.F6))
                {
                    Logger.LogInfo("F6: Saving current dungeon...");
                    GungeonTogether.Debug.DungeonSaveLoad.SaveCurrentDungeon();
                }

                if (Input.GetKeyDown(KeyCode.F7))
                {
                    Logger.LogInfo("F7: Comparing current vs saved dungeon...");
                    GungeonTogether.Debug.DungeonSaveLoad.CompareDungeonWithSaved();
                }

                // Keep F8 for developer debugging only
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    Logger.LogInfo("F8: Developer debug - Showing friends playing GungeonTogether...");
                    ShowFriendsPlayingGame();
                }

                // Keep F10 for Steam diagnostics (developer only)
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    Logger.LogInfo("F10: Developer debug - Steam diagnostics stub");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in debug input: {e.Message}");
            }
        }

        // ===== Stub members for backward compatibility with UI / debug references =====
        public string MultiplayerRole
        {
            get
            {
                if (_sessionManager == null) return "None";
                if (_sessionManager.IsActive)
                    return _sessionManager.IsHost ? "Host" : "Client";
                return "Singleplayer";
            }
        }

        // Previously existed debug setup method - now a no-op to satisfy callers
        private void SetupDebugControls() { }
        private void InitializeDebuggingSystem() { }
        private void RunSteamDiagnostics() { }

    private Harmony _harmony;

        private void PatchItemPickupManually()
        {
            try
            {
                var target = GungeonTogether.Steam.ItemPickupPatch.FindTargetMethod();
                if (target == null)
                {
                    Logger.LogWarning("ItemPickupPatch target method not found (PickupObject.Pickup)");
                    return;
                }
                var postfix = new HarmonyMethod(typeof(GungeonTogether.Steam.ItemPickupPatch).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static));
                if (postfix == null)
                {
                    Logger.LogWarning("ItemPickupPatch Postfix not found");
                    return;
                }
                _harmony.Patch(target, null, postfix, null);
                Logger.LogInfo("Manually patched PickupObject.Pickup (item pickup sync)");
            }
            catch (Exception e)
            {
                Logger.LogError("Manual item pickup patch failed: " + e.Message);
            }
        }

        // Expose available hosts to UI (wrapper over SteamHostManager)
        public List<Steam.SteamHostManager.HostInfo> GetAvailableHosts()
        {
            try
            {
                return Steam.SteamHostManager.Instance.GetAvailableHostsList();
            }
            catch { return new List<Steam.SteamHostManager.HostInfo>(); }
        }

        public void JoinSpecificHost(ulong steamId)
        {
            try
            {
                // Bridge to existing join flow
                JoinSession(steamId.ToString());
            }
            catch (Exception e)
            {
                Logger.LogError($"JoinSpecificHost failed: {e.Message}");
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

                        // Initialize NetworkManager as host
                        if (networkingInitialized)
                        {
                            var hostSteamId = SteamReflectionHelper.GetLocalSteamId();
                            NetworkManager.Instance.InitializeAsHost(hostSteamId);
                            NetworkedDungeonManager.Instance.Initialize(true);
                            Logger.LogInfo("NetworkManager initialized as HOST");
                        }

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
                        // Initialize NetworkManager as client
                        if (networkingInitialized)
                        {
                            NetworkManager.Instance.InitializeAsClient(steamId);
                            NetworkedDungeonManager.Instance.Initialize(false);
                            Logger.LogInfo($"NetworkManager initialized as CLIENT connecting to {steamId}");
                        }
                        // CRITICAL: Ensure PlayerSynchroniser is initialized for joiner!
                        Logger.LogInfo("[JOINER] Calling PlayerSynchroniser.StaticInitialize() after join");
                        PlayerSynchroniser.StaticInitialize();
                        EnemySynchronizer.StaticInitialize();
                        ProjectileSynchronizer.StaticInitialize();
                        
                        // Initialize player persistence manager for joiners
                        PlayerPersistenceManager.Instance.Initialize();
                        Logger.LogInfo("[JOINER] Player persistence manager initialized");
                        
                        DungeonGenerationHook.InstallHooks();
                        NetworkedDungeonManager.Instance.Initialize(false);
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

                    // Shutdown networking systems
                    if (networkingInitialized)
                    {
                        NetworkManager.Instance.Shutdown();
                        Logger.LogInfo("NetworkManager shutdown complete");
                        
                        // Shutdown persistence manager
                        PlayerPersistenceManager.Instance.Shutdown();
                        Logger.LogInfo("PlayerPersistenceManager shutdown complete");
                    }

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
        }

        /// <summary>
        /// Get the session manager instance for Steam callback integration
        /// </summary>
        public SimpleSessionManager GetSessionManager()
        {
            return _sessionManager;
        }

        public void ShowStatus()
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
                            Logger.LogInfo("  • Opening the multiplayer menu (Ctrl+P) and joining your session");
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
                            Logger.LogInfo("Open multiplayer menu (Ctrl+P) to join a host!");
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
        }
        private void ShowFriendsPlayingGame()
        {
            try
            {
                Logger.LogInfo("Getting friends playing GungeonTogether...");

                // Use the improved friends list functionality
                var steamNet = ETGSteamP2PNetworking.Instance;
                if (!ReferenceEquals(steamNet, null) && steamNet.IsAvailable())
                {
                    Logger.LogInfo("Running comprehensive Steam friends debug...");
                    steamNet.PrintFriendsList(); // This now calls the improved method
                }
                else
                {
                    Logger.LogWarning("Steam networking not available for friends detection");
                }

                // Also get the specific GungeonTogether friends info
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
                        Logger.LogInfo($"  {i + 1}. {friends[i]}");
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
                        Logger.LogWarning("No available hosts found for simulation");
                    }
                }
                else
                {
                    Logger.LogWarning("Steam networking not available for simulation");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to simulate Steam overlay join: {e.Message}");
            }
        }

        /// <summary>
        /// Initialize networking and synchronization systems
        /// </summary>
        private void InitializeNetworking()
        {
            try
            {
                Logger.LogInfo("Initializing networking systems...");

                // Initialize packet serializer
                PacketSerializer.Initialize();

                // Initialize game synchronizers
                PlayerSynchroniser.StaticInitialize();
                EnemySynchronizer.StaticInitialize();
                ProjectileSynchronizer.StaticInitialize();
                // Initialize item synchronizer (default categories; TODO: load from config)
                string syncCats = ConfigSyncedItemCategories?.Value ?? "Gun,Passive,Active";
                string dupCats = ConfigDuplicatedItemCategories?.Value ?? "Consumable,Currency,Key,Heart,Armor";
                ItemSynchronizer.Instance.Initialize(_sessionManager != null && _sessionManager.IsHost, syncCats, dupCats);

                // Initialize player persistence manager
                PlayerPersistenceManager.Instance.Initialize();

                // Initialize dungeon hooks
                DungeonGenerationHook.InstallHooks();

                // Initialize networked dungeon manager
                NetworkedDungeonManager.Instance.Initialize(false); // Will be set to true when hosting

                networkingInitialized = true;
                Logger.LogInfo("Networking systems initialized successfully!");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to initialize networking: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
                networkingInitialized = false;
            }
        }

        /// <summary>
        /// Check Steam command line arguments for join requests
        /// </summary>
        private void CheckSteamCommandLineArgs()
        {
            try
            {
                // Check for Steam join commands in command line arguments
                string[] args = System.Environment.GetCommandLineArgs();
                foreach (string arg in args)
                {
                    if (arg.StartsWith("+connect_lobby"))
                    {
                        Logger.LogInfo($"Steam lobby join request detected: {arg}");
                        // TODO: Parse and handle lobby join request
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error checking Steam command line args: {e.Message}");
            }
        }

        // (Rest of file unchanged - existing methods)
    }
}
