using GungeonTogether.Game;
using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Modern UI controller for GungeonTogether multiplayer functionality
    /// Provides a beautiful, intuitive interface for hosting, joining, and managing multiplayer sessions
    /// </summary>
    public class MultiplayerUIController : MonoBehaviour
    {
        private static MultiplayerUIController _instance;
        public static MultiplayerUIController Instance => _instance;

        [Header("UI References")]
        public Canvas uiCanvas;
        public GameObject mainPanel;
        public GameObject statusIndicator;
        public GameObject hostListPanel;

        [Header("Main Panel Elements")]
        public UnityEngine.UI.Button hostButton;
        public UnityEngine.UI.Button joinButton;
        public UnityEngine.UI.Button disconnectButton;
        public UnityEngine.UI.Button refreshButton;
        public UnityEngine.UI.Text statusText;
        public UnityEngine.UI.Text steamIdText;

        [Header("Host List Elements")]
        public Transform hostListContainer;
        public GameObject hostEntryPrefab;

        [Header("Status Indicator Elements")]
        public UnityEngine.UI.Image statusIcon;
        public UnityEngine.UI.Text statusLabel;

        [Header("Animation Settings")]
        public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // State management
        private bool isUIVisible = false;
        private bool isInitialized = false;
        private SimpleSessionManager sessionManager;
        private ISteamNetworking steamNetworking;
        private List<HostEntryUI> hostEntries = new List<HostEntryUI>();
        private ModernMultiplayerMenu modernMenu;

        // UI Colors
        private static readonly Color ConnectedColor = new Color(0.2f, 0.8f, 0.2f, 1f);      // Green
        private static readonly Color DisconnectedColor = new Color(0.8f, 0.2f, 0.2f, 1f);   // Red
        private static readonly Color HostingColor = new Color(0.2f, 0.6f, 1f, 1f);          // Blue
        private static readonly Color ConnectingColor = new Color(1f, 0.8f, 0.2f, 1f);       // Orange

        void Awake()
        {
            if (ReferenceEquals(_instance, null))
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] UI Controller initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            InitializeUI();

            // Delay finding the modern menu to ensure it's created
            Invoke(nameof(FindModernMenu), 1f);
        }

        private void FindModernMenu()
        {
            // Find the modern menu component
            modernMenu = FindObjectOfType<ModernMultiplayerMenu>();
            if (!ReferenceEquals(modernMenu, null))
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Found ModernMultiplayerMenu component");
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] ModernMultiplayerMenu component not found");
                // Try again in 1 second
                Invoke(nameof(FindModernMenu), 1f);
            }
        }

        void Update()
        {
            // Handle ESC key to close UI
            if (isUIVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                HideUI();
            }

            // CRITICAL: Prevent game pausing when hosting and UI is open
            if (isUIVisible && IsHostingSession())
            {
                PreventGamePause();
            }

            // Remove duplicate input handling - this is handled by the main mod
            // Only update UI elements if visible
            if (isUIVisible && isInitialized)
            {
                UpdateUIElements();
            }
        }

        /// <summary>
        /// Check if we are currently hosting a multiplayer session
        /// </summary>
        private bool IsHostingSession()
        {
            try
            {
                var sessionManager = GungeonTogetherMod.Instance?.SessionManager;
                return (!ReferenceEquals(sessionManager, null)) && sessionManager.IsActive && sessionManager.IsHost;
            }
            catch (System.Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUI] Error checking hosting status: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prevent the game from pausing when hosting a multiplayer session
        /// This ensures the server continues running even when the UI is open
        /// </summary>
        private void PreventGamePause()
        {
            try
            {
                // Ensure Time.timeScale stays at 1.0 when hosting
                if (!ReferenceEquals(Time.timeScale, 1.0f))
                {
                    Time.timeScale = 1.0f;
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Prevented game pause - keeping server running");
                }

                // Try to access ETG's GameManager if available to prevent pause
                var gameManagerType = System.Type.GetType("GameManager, Assembly-CSharp");
                if (!ReferenceEquals(gameManagerType, null))
                {
                    var instanceProperty = gameManagerType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (!ReferenceEquals(instanceProperty, null))
                    {
                        var gameManager = instanceProperty.GetValue(null, null);
                        if (!ReferenceEquals(gameManager, null))
                        {
                            // Try to find and manipulate pause-related fields/properties
                            var pausedField = gameManagerType.GetField("IsPaused",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                            if (!ReferenceEquals(pausedField, null) && ReferenceEquals((bool)pausedField.GetValue(gameManager), true))
                            {
                                pausedField.SetValue(gameManager, false);
                                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Overrode GameManager pause state to keep server running");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                // Silently handle reflection errors - not all ETG versions may have the same structure
                GungeonTogether.Logging.Debug.LogWarning($"[MultiplayerUI] Could not access pause system (game version difference): {e.Message}");
            }
        }

        /// <summary>
        /// Initialize the multiplayer UI system
        /// </summary>
        public void InitializeUI()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Initializing UI system...");

                CreateUICanvas();
                CreateMainPanel();
                SetupEventHandlers();

                // Initially hide the UI
                if (mainPanel) mainPanel.SetActive(false);

                isInitialized = true;
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] UI system initialized successfully");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUI] Failed to initialize UI: {e.Message}");
            }
        }

        /// <summary>
        /// Create the main UI canvas
        /// </summary>
        private void CreateUICanvas()
        {
            if (ReferenceEquals(uiCanvas, null))
            {
                GameObject canvasObject = new GameObject("GungeonTogether_UICanvas");
                DontDestroyOnLoad(canvasObject);

                uiCanvas = canvasObject.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 32767; // Maximum sorting order to ensure it's on top
                uiCanvas.pixelPerfect = false;

                var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920, 1080);
                canvasScaler.matchWidthOrHeight = 0.5f;
                canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

                var graphicRaycaster = canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                graphicRaycaster.ignoreReversedGraphics = true;
                graphicRaycaster.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;

                // CRITICAL: Create EventSystem for UI interactions to work
                CreateEventSystemIfMissing();

                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] UI Canvas created with sortingOrder: {uiCanvas.sortingOrder}");
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas renderMode: {uiCanvas.renderMode}");
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas enabled: {uiCanvas.enabled}");
            }
        }

        /// <summary>
        /// Create EventSystem if it doesn't exist - ESSENTIAL for UI interactions
        /// </summary>
        private void CreateEventSystemIfMissing()
        {
            // Check if EventSystem already exists
            EventSystem existingEventSystem = FindObjectOfType<EventSystem>();
            if (ReferenceEquals(existingEventSystem, null))
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Creating EventSystem for UI interactions...");

                GameObject eventSystemObject = new GameObject("GungeonTogether_EventSystem");
                DontDestroyOnLoad(eventSystemObject);

                var eventSystem = eventSystemObject.AddComponent<EventSystem>();
                var inputModule = eventSystemObject.AddComponent<StandaloneInputModule>();

                // Configure input module for better UI responsiveness
                inputModule.horizontalAxis = "Horizontal";
                inputModule.verticalAxis = "Vertical";
                inputModule.submitButton = "Submit";
                inputModule.cancelButton = "Cancel";
                inputModule.inputActionsPerSecond = 10f;
                inputModule.repeatDelay = 0.5f;

                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] EventSystem created with enhanced settings - UI should now be fully clickable!");
            }
            else
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] EventSystem already exists - verifying UI clickability");

                // Verify the existing EventSystem is properly configured
                var inputModule = existingEventSystem.GetComponent<StandaloneInputModule>();
                if (ReferenceEquals(inputModule, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] EventSystem exists but lacks StandaloneInputModule - adding it");
                    inputModule = existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
                }

                // Ensure EventSystem is enabled
                if (!existingEventSystem.enabled)
                {
                    existingEventSystem.enabled = true;
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Re-enabled EventSystem for UI interactions");
                }

                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] EventSystem verified - UI should be clickable");
            }
        }

        /// <summary>
        /// Create the main multiplayer panel
        /// </summary>
        private void CreateMainPanel()
        {
            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] CreateMainPanel called");
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] mainPanel is null: {ReferenceEquals(mainPanel, null)}");
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] uiCanvas is null: {ReferenceEquals(uiCanvas, null)}");

            if (ReferenceEquals(mainPanel, null) && !ReferenceEquals(uiCanvas, null))
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Creating main panel...");

                // Create main panel background
                mainPanel = CreateUIPanel(uiCanvas.transform, "MainPanel", new Vector2(600, 400));
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Main panel created: {!ReferenceEquals(mainPanel, null)}");

                if (ReferenceEquals(mainPanel, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[MultiplayerUI] Failed to create main panel!");
                    return;
                }

                // Ensure proper positioning (centered on screen)
                var rectTransform = mainPanel.GetComponent<RectTransform>();
                if (rectTransform)
                {
                    rectTransform.anchorMin = Vector2.one * 0.5f; // Center anchor
                    rectTransform.anchorMax = Vector2.one * 0.5f; // Center anchor
                    rectTransform.anchoredPosition = Vector2.zero; // Center position
                    rectTransform.sizeDelta = new Vector2(600, 400);
                }

                // Panel styling - make it more visible
                var panelImage = mainPanel.GetComponent<UnityEngine.UI.Image>();
                if (panelImage)
                {
                    panelImage.color = new Color(0.05f, 0.05f, 0.15f, 0.98f); // Dark blue, almost opaque

                    // Add a border effect to make it more obvious
                    var outline = mainPanel.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = new Color(0.3f, 0.6f, 1f, 1f); // Blue outline
                    outline.effectDistance = new Vector2(2, 2);
                }

                // Create title
                CreateUIText(mainPanel.transform, "TitleText", "🎮 GungeonTogether Multiplayer 🎮",
                           new Vector2(0, 150), new Vector2(580, 50), 24, TextAnchor.MiddleCenter);

                // Create status text
                statusText = CreateUIText(mainPanel.transform, "StatusText", "Ready to connect",
                                        new Vector2(0, 100), new Vector2(580, 30), 16, TextAnchor.MiddleCenter).GetComponent<UnityEngine.UI.Text>();

                // Create Steam ID text
                steamIdText = CreateUIText(mainPanel.transform, "SteamIdText", "Steam ID: Loading...",
                                         new Vector2(0, 70), new Vector2(580, 25), 14, TextAnchor.MiddleCenter).GetComponent<UnityEngine.UI.Text>();

                // Create buttons
                hostButton = CreateUIButton(mainPanel.transform, "HostButton", "🏠 Host Session",
                                           new Vector2(-150, 20), new Vector2(140, 40), OnHostClicked);

                joinButton = CreateUIButton(mainPanel.transform, "JoinButton", "🔗 Auto Join",
                                           new Vector2(0, 20), new Vector2(140, 40), OnJoinClicked);

                disconnectButton = CreateUIButton(mainPanel.transform, "DisconnectButton", "🚪 Disconnect",
                                                new Vector2(150, 20), new Vector2(140, 40), OnDisconnectClicked);

                refreshButton = CreateUIButton(mainPanel.transform, "RefreshButton", "🔄 Refresh Hosts",
                                             new Vector2(0, -30), new Vector2(200, 35), OnRefreshClicked);

                // Create host list panel
                CreateHostListPanel();

                // Create close button
                var closeButton = CreateUIButton(mainPanel.transform, "CloseButton", "✕",
                                                new Vector2(280, 180), new Vector2(30, 30), () => HideUI());

                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Main panel created");
            }
        }

        /// <summary>
        /// Create the host list panel
        /// </summary>
        private void CreateHostListPanel()
        {
            hostListPanel = CreateUIPanel(mainPanel.transform, "HostListPanel", new Vector2(560, 120));

            var hostListRect = hostListPanel.GetComponent<RectTransform>();
            hostListRect.anchoredPosition = new Vector2(0, -80);

            // Host list background
            var hostListImage = hostListPanel.GetComponent<UnityEngine.UI.Image>();
            if (hostListImage)
            {
                hostListImage.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
            }

            // Host list title
            CreateUIText(hostListPanel.transform, "HostListTitle", "Available Hosts:",
                       new Vector2(0, 45), new Vector2(540, 25), 16, TextAnchor.MiddleLeft);

            // Create scrollable content area
            var scrollRect = hostListPanel.AddComponent<UnityEngine.UI.ScrollRect>();

            // Content container
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(hostListPanel.transform);
            hostListContainer = contentObj.transform;

            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            var contentLayout = contentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            contentLayout.spacing = 5f;
            contentLayout.padding = new RectOffset(10, 10, 5, 5);

            var contentSizeFitter = contentObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            contentSizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Host list panel created");
        }

        /// <summary>
        /// Setup event handlers for UI interaction
        /// </summary>
        private void SetupEventHandlers()
        {
            // Handle escape key to close UI
            // This will be handled in Update()
        }

        /// <summary>
        /// Toggle the main UI panel visibility
        /// </summary>
        public void ToggleUI()
        {
            if (!ReferenceEquals(modernMenu, null))
            {
                modernMenu.ToggleMenu();
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Delegated toggle to ModernMultiplayerMenu");
                return;
            }

            // Fallback to old UI system if modern menu not available
            if (!isInitialized) return;

            isUIVisible = !isUIVisible;

            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] ToggleUI called - isUIVisible: {isUIVisible}");
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] mainPanel is null: {ReferenceEquals(mainPanel, null)}");
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] uiCanvas is null: {ReferenceEquals(uiCanvas, null)}");

            if (!ReferenceEquals(uiCanvas, null))
            {
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas enabled: {uiCanvas.enabled}");
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas sortingOrder: {uiCanvas.sortingOrder}");
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas renderMode: {uiCanvas.renderMode}");
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas children count: {uiCanvas.transform.childCount}");
            }

            if (mainPanel)
            {
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Main panel exists, setting active to: {isUIVisible}");
                mainPanel.SetActive(isUIVisible);

                if (isUIVisible)
                {
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] UI panel activated - should be visible now!");

                    // Remove test panel for production - the main panel should be visible
                    // CreateTestPanel();

                    UpdateUIElements();
                    RefreshHostList();

                    // Make sure the panel is properly positioned and visible
                    var rectTransform = mainPanel.GetComponent<RectTransform>();
                    if (rectTransform)
                    {
                        rectTransform.anchoredPosition = Vector2.zero; // Center on screen
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel positioned at: {rectTransform.anchoredPosition}");
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel size: {rectTransform.sizeDelta}");
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel anchors: min={rectTransform.anchorMin}, max={rectTransform.anchorMax}");
                    }

                    // Check if panel has Image component
                    var image = mainPanel.GetComponent<UnityEngine.UI.Image>();
                    if (image)
                    {
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel image color: {image.color}");
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel image enabled: {image.enabled}");
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] Main panel has no Image component!");
                    }

                    // Ensure canvas is active and rendering
                    if (uiCanvas)
                    {
                        uiCanvas.enabled = true;
                        GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Canvas enabled, sortingOrder: {uiCanvas.sortingOrder}");
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] UI panel deactivated - should be hidden now!");
                }
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[MultiplayerUI] Main panel is null - UI not properly initialized!");

                // Try to recreate the panel if it's missing
                if (!ReferenceEquals(uiCanvas, null))
                {
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Attempting to recreate main panel...");
                    CreateMainPanel();

                    if (!ReferenceEquals(mainPanel, null))
                    {
                        mainPanel.SetActive(isUIVisible);
                        GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Main panel recreated successfully");
                    }
                }
            }

            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] UI toggled: {(isUIVisible ? "Visible" : "Hidden")}");
        }

        /// <summary>
        /// Show the main UI panel
        /// </summary>
        public void ShowUI()
        {
            if (!ReferenceEquals(modernMenu, null))
            {
                modernMenu.ShowMenu();
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Delegated show to ModernMultiplayerMenu");
                return;
            }

            // Fallback to old UI system
            if (!isUIVisible)
            {
                ToggleUI();
            }

            // Immediately handle pause prevention if hosting
            if (IsHostingSession())
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Hosting session detected - preventing game pause");
                PreventGamePause();
            }
        }

        /// <summary>
        /// Hide the UI (public method for external access)
        /// </summary>
        public void HideUI()
        {
            if (!ReferenceEquals(modernMenu, null))
            {
                modernMenu.HideMenu();
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Delegated hide to ModernMultiplayerMenu");
                return;
            }

            // Fallback to old UI system
            if (isInitialized && !ReferenceEquals(mainPanel, null))
            {
                isUIVisible = false;
                mainPanel.SetActive(false);
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] UI hidden via HideUI()");
            }
        }

        /// <summary>
        /// Update UI elements with current state
        /// </summary>
        private void UpdateUIElements()
        {
            if (ReferenceEquals(sessionManager, null) && !ReferenceEquals(GungeonTogetherMod.Instance, null))
            {
                sessionManager = GungeonTogetherMod.Instance._sessionManager;
            }

            if (ReferenceEquals(steamNetworking, null))
            {
                steamNetworking = SteamNetworkingFactory.TryCreateSteamNetworking();
            }

            // Periodically verify UI clickability (every few seconds)
            if (Time.time % 3.0f < 0.1f) // Check roughly every 3 seconds
            {
                VerifyUIClickability();
            }

            UpdateStatusText();
            UpdateSteamIdText();
            UpdateButtonStates();
            UpdateStatusIndicator();
        }

        /// <summary>
        /// Verify that the UI remains clickable during runtime
        /// </summary>
        private void VerifyUIClickability()
        {
            try
            {
                var eventSystem = FindObjectOfType<EventSystem>();
                if (ReferenceEquals(eventSystem, null) || !eventSystem.enabled)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] EventSystem missing or disabled - recreating for UI clickability");
                    CreateEventSystemIfMissing();
                }

                // Verify GraphicRaycaster is working
                if (!ReferenceEquals(uiCanvas, null))
                {
                    var raycaster = uiCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                    if (ReferenceEquals(raycaster, null) || !raycaster.enabled)
                    {
                        GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] GraphicRaycaster missing or disabled - fixing for UI clickability");
                        if (ReferenceEquals(raycaster, null))
                        {
                            raycaster = uiCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                        }
                        raycaster.enabled = true;
                        raycaster.ignoreReversedGraphics = true;
                        raycaster.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
                    }
                }
            }
            catch (System.Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUI] Error verifying UI clickability: {e.Message}");
            }
        }

        /// <summary>
        /// Update the main status text
        /// </summary>
        private void UpdateStatusText()
        {
            if (ReferenceEquals(statusText, null)) return;

            string status = "Unknown";
            Color statusColor = Color.white;

            if (!ReferenceEquals(sessionManager, null))
            {
                if (sessionManager.IsActive)
                {
                    if (sessionManager.IsHost)
                    {
                        status = "🏠 Hosting session - Friends can join!";
                        statusColor = HostingColor;
                    }
                    else
                    {
                        status = "🔗 Connected to multiplayer session";
                        statusColor = ConnectedColor;
                    }
                }
                else
                {
                    status = "Ready to connect";
                    statusColor = Color.white;
                }
            }
            else
            {
                status = "Session manager not available";
                statusColor = DisconnectedColor;
            }

            statusText.text = status;
            statusText.color = statusColor;
        }

        /// <summary>
        /// Update the Steam ID text
        /// </summary>
        private void UpdateSteamIdText()
        {
            if (ReferenceEquals(steamIdText, null)) return;

            if (!ReferenceEquals(steamNetworking, null) && steamNetworking.IsAvailable())
            {
                ulong steamId = steamNetworking.GetSteamID();
                steamIdText.text = $"Steam ID: {steamId}";
                steamIdText.color = Color.white;
            }
            else
            {
                steamIdText.text = "Steam ID: Not available";
                steamIdText.color = DisconnectedColor;
            }
        }

        /// <summary>
        /// Update button states based on current session state
        /// </summary>
        private void UpdateButtonStates()
        {
            bool canHost = !ReferenceEquals(sessionManager, null) && !sessionManager.IsActive;
            bool canJoin = !ReferenceEquals(sessionManager, null) && !sessionManager.IsActive;
            bool canDisconnect = !ReferenceEquals(sessionManager, null) && sessionManager.IsActive;

            if (hostButton) hostButton.interactable = canHost;
            if (joinButton) joinButton.interactable = canJoin;
            if (disconnectButton) disconnectButton.interactable = canDisconnect;
            if (refreshButton) refreshButton.interactable = true;
        }

        /// <summary>
        /// Update the status indicator
        /// </summary>
        private void UpdateStatusIndicator()
        {
            if (ReferenceEquals(statusIcon, null) || ReferenceEquals(statusLabel, null)) return;

            if (!ReferenceEquals(sessionManager, null) && sessionManager.IsActive)
            {
                if (sessionManager.IsHost)
                {
                    statusIcon.color = HostingColor;
                    statusLabel.text = "Hosting";
                }
                else
                {
                    statusIcon.color = ConnectedColor;
                    statusLabel.text = "Connected";
                }
            }
            else
            {
                statusIcon.color = DisconnectedColor;
                statusLabel.text = "Disconnected";
            }
        }

        /// <summary>
        /// Refresh the host list
        /// </summary>
        public void RefreshHostList()
        {
            if (ReferenceEquals(hostListContainer, null)) return;

            // Clear existing entries
            foreach (var entry in hostEntries)
            {
                if (entry.gameObject) Destroy(entry.gameObject);
            }
            hostEntries.Clear();

            // Get available hosts
            if (!ReferenceEquals(steamNetworking, null) && steamNetworking.IsAvailable())
            {
                ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                ulong mySteamId = steamNetworking.GetSteamID();

                foreach (ulong hostId in availableHosts)
                {
                    // Don't show ourselves as a joinable host
                    if (!ReferenceEquals(hostId, mySteamId))
                    {
                        CreateHostEntry(hostId);
                    }
                }

                if (ReferenceEquals(availableHosts.Length, 0))
                {
                    CreateNoHostsEntry();
                }
            }
            else
            {
                CreateSteamUnavailableEntry();
            }
        }

        /// <summary>
        /// Create a host entry in the list
        /// </summary>
        private void CreateHostEntry(ulong hostSteamId)
        {
            var hostEntry = CreateUIPanel(hostListContainer, $"HostEntry_{hostSteamId}", new Vector2(540, 30));

            var entryImage = hostEntry.GetComponent<UnityEngine.UI.Image>();
            if (entryImage)
            {
                entryImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            // Host ID text
            var hostText = CreateUIText(hostEntry.transform, "HostText", $"Host: {hostSteamId}",
                                      new Vector2(-150, 0), new Vector2(250, 25), 12, TextAnchor.MiddleLeft).GetComponent<UnityEngine.UI.Text>();

            // Join button
            var joinHostButton = CreateUIButton(hostEntry.transform, "JoinHostButton", "Join",
                                              new Vector2(200, 0), new Vector2(80, 25), () => OnJoinHostClicked(hostSteamId));

            var hostEntryUI = hostEntry.AddComponent<HostEntryUI>();
            hostEntryUI.Initialize(hostSteamId, hostText, joinHostButton);
            hostEntries.Add(hostEntryUI);
        }

        /// <summary>
        /// Create "No hosts available" entry
        /// </summary>
        private void CreateNoHostsEntry()
        {
            var noHostsEntry = CreateUIPanel(hostListContainer, "NoHostsEntry", new Vector2(540, 30));

            CreateUIText(noHostsEntry.transform, "NoHostsText", "No hosts available - Have a friend start hosting!",
                       new Vector2(0, 0), new Vector2(530, 25), 12, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// Create "Steam unavailable" entry
        /// </summary>
        private void CreateSteamUnavailableEntry()
        {
            var steamUnavailableEntry = CreateUIPanel(hostListContainer, "SteamUnavailableEntry", new Vector2(540, 30));

            CreateUIText(steamUnavailableEntry.transform, "SteamUnavailableText", "Steam networking not available",
                       new Vector2(0, 0), new Vector2(530, 25), 12, TextAnchor.MiddleCenter);
        }

        // Event Handlers
        private void OnHostClicked()
        {
            GungeonTogether.Logging.Debug.Log("!!! OnHostClicked CALLED !!!");
            SteamCallbackManager.Instance.HostLobby();
            GungeonTogether.Logging.Debug.Log("!!! After HostLobby call !!!");
            UpdateUIElements();
        }

        private void OnJoinClicked()
        {
            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Join button clicked");

            if (!ReferenceEquals(sessionManager, null))
            {
                // Get available hosts and join the first one, or show selection
                var hosts = GungeonTogetherMod.Instance?.GetAvailableHosts() ?? new List<SteamHostManager.HostInfo>();
                if (hosts.Count > 0)
                {
                    // Join the first available host
                    GungeonTogetherMod.Instance?.JoinSpecificHost(hosts[0].steamId);
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUI] No hosts available to join");
                }
                UpdateUIElements();
            }
        }

        private void OnDisconnectClicked()
        {
            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Disconnect button clicked");

            if (!ReferenceEquals(sessionManager, null))
            {
                GungeonTogetherMod.Instance?.StopMultiplayer();
                sessionManager?.StopSession();
                UpdateUIElements();
                RefreshHostList();
            }
        }

        private void OnRefreshClicked()
        {
            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Refresh button clicked");
            RefreshHostList();
        }

        private void OnJoinHostClicked(ulong hostSteamId)
        {
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Join host clicked: {hostSteamId}");

            if (!ReferenceEquals(sessionManager, null))
            {
                string sessionId = $"steam_{hostSteamId}";
                GungeonTogetherMod.Instance?.JoinSession(hostSteamId.ToString());
                UpdateUIElements();
                HideUI(); // Hide UI after joining
            }
        }

        /// <summary>
        /// Create a simple test panel to verify UI rendering
        /// </summary>
        private void CreateTestPanel()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Creating test panel for debugging...");

                if (ReferenceEquals(uiCanvas, null))
                {
                    GungeonTogether.Logging.Debug.LogError("[MultiplayerUI] Canvas is null - cannot create test panel");
                    return;
                }

                // Create a very obvious test panel
                var testPanel = new GameObject("TestPanel");
                testPanel.transform.SetParent(uiCanvas.transform);

                var rectTransform = testPanel.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                var image = testPanel.AddComponent<UnityEngine.UI.Image>();
                image.color = Color.red; // Bright red - impossible to miss

                // Add text
                var textObj = new GameObject("TestText");
                textObj.transform.SetParent(testPanel.transform);

                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textObj.AddComponent<UnityEngine.UI.Text>();
                text.text = "TEST PANEL - UI SYSTEM WORKING";
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 48;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;

                GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Test panel created successfully");

                // Auto-destroy after 3 seconds
                Destroy(testPanel, 3f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUI] Failed to create test panel: {e.Message}");
            }
        }

        // UI Creation Helpers

        /// <summary>
        /// Create a UI panel
        /// </summary>
        private GameObject CreateUIPanel(Transform parent, string name, Vector2 size)
        {
            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Creating UI panel: {name} with size: {size}");

            var panel = new GameObject(name);
            panel.transform.SetParent(parent);

            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.1f, 0.1f, 0.2f, 0.95f); // Dark blue, mostly opaque

            GungeonTogether.Logging.Debug.Log($"[MultiplayerUI] Panel created: {name}, parent: {parent?.name}, color: {image.color}");

            return panel;
        }

        /// <summary>
        /// Create a UI text element
        /// </summary>
        private GameObject CreateUIText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;

            // Try to use ETG's font if available, otherwise use default
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return textObj;
        }

        /// <summary>
        /// Create a UI button
        /// </summary>
        private UnityEngine.UI.Button CreateUIButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            if (!ReferenceEquals(onClick, null))
            {
                button.onClick.AddListener(() => onClick.Invoke());
            }

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.fontSize = 14;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return button;
        }

        /// <summary>
        /// Set the session manager reference
        /// </summary>
        public void SetSessionManager(SimpleSessionManager manager)
        {
            sessionManager = manager;
            GungeonTogether.Logging.Debug.Log("[MultiplayerUI] Session manager set");
        }

        /// <summary>
        /// Show a notification message
        /// </summary>
        public void ShowNotification(string message, float duration = 3f)
        {
            ShowNotificationSimple(message, duration);
        }

        private void ShowNotificationSimple(string message, float duration)
        {
            try
            {
                // Safety check
                if (ReferenceEquals(uiCanvas, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] Cannot show notification - canvas not initialized");
                    return;
                }

                // Create temporary notification UI
                var notification = CreateUIPanel(uiCanvas.transform, "Notification", new Vector2(400, 60));

                if (ReferenceEquals(notification, null))
                {
                    GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUI] Failed to create notification panel");
                    return;
                }

                var notificationRect = notification.GetComponent<RectTransform>();
                notificationRect.anchorMin = new Vector2(0.5f, 1f);
                notificationRect.anchorMax = new Vector2(0.5f, 1f);
                notificationRect.anchoredPosition = new Vector2(0, -80);

                var notificationImage = notification.GetComponent<UnityEngine.UI.Image>();
                notificationImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

                CreateUIText(notification.transform, "NotificationText", message,
                           Vector2.zero, new Vector2(380, 50), 16, TextAnchor.MiddleCenter);

                // Store notification reference and use Invoke to destroy it
                var destroyComponent = notification.AddComponent<NotificationDestroyer>();
                destroyComponent.DestroyAfter(duration);
            }
            catch (System.Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUI] Error showing notification: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// UI component for individual host entries
    /// </summary>
    public class HostEntryUI : MonoBehaviour
    {
        public ulong HostSteamId { get; private set; }
        public UnityEngine.UI.Text HostText { get; private set; }
        public UnityEngine.UI.Button JoinButton { get; private set; }

        public void Initialize(ulong hostSteamId, UnityEngine.UI.Text hostText, UnityEngine.UI.Button joinButton)
        {
            HostSteamId = hostSteamId;
            HostText = hostText;
            JoinButton = joinButton;
        }
    }

    /// <summary>
    /// Simple helper component to destroy notifications after a delay without coroutines
    /// </summary>
    public class NotificationDestroyer : MonoBehaviour
    {
        public void DestroyAfter(float delay)
        {
            Invoke(nameof(DestroyNotification), delay);
        }

        private void DestroyNotification()
        {
            if (!ReferenceEquals(gameObject, null))
            {
                Destroy(gameObject);
            }
        }
    }
}
