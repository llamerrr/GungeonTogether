using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GungeonTogether.Game;
using GungeonTogether.Steam;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Modern, responsive multiplayer menu that replaces the buggy OnGUI-based Ctrl+P menu
    /// Uses Unity's Canvas system for proper layout management and responsive design
    /// </summary>
    public class ModernMultiplayerMenu : MonoBehaviour
    {
        [Header("UI Configuration")]
        public KeyCode toggleKey = KeyCode.P;
        public bool requireCtrl = true;
        
        [Header("Menu Positioning")]
        public Vector2 menuSize = new Vector2(420f, 550f); // Increased size for better layout
        public Vector2 menuPosition = new Vector2(50f, 50f);
        
        // UI Components
        private Canvas menuCanvas;
        private GameObject backgroundPanel;
        private GameObject menuPanel;
        private bool isMenuVisible = false;
        private bool isInitialized = false;
        
        // Content panels
        private GameObject mainPanel;
        private GameObject hostingPanel;
        private GameObject friendsPanel;
        private GameObject hostSelectionPanel;
        private GameObject settingsPanel;
        private GameObject pingTestPanel;
        
        // UI Elements
        private Text statusText;
        private Text steamIdText;
        private Button hostButton;
        private Button joinButton;
        private Button disconnectButton;
        private Button friendsButton;
        private Button playerListButton;
        private Button settingsButton;
        private Button closeButton;
        // References
        private SimpleSessionManager sessionManager;
        private ETGSteamP2PNetworking steamNetworking;
        
        // Host selection
        private Transform hostListContent;
        private SteamHostManager.HostInfo[] _availableHostsForSelection = new SteamHostManager.HostInfo[0];
        
        // Cached values to prevent log spam
        private ulong cachedSteamId = 0;
        private bool steamIdCached = false;
        private float lastSteamIdCheck = 0f;
        
        void Start()
        {
            // Use Invoke to delay initialization to ensure all systems are ready
            Invoke(nameof(DelayedInitialization), 0.5f);
        }
        
        private void DelayedInitialization()
        {
            try
            {
                InitializeModernMenu();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Failed to initialize: {e.Message}");
            }
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            // Handle toggle input
            if (Input.GetKeyDown(toggleKey))
            {
                bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (!requireCtrl || ctrlPressed)
                {
                    ToggleMenu();
                }
            }
            
            // Handle ESC to close
            if (isMenuVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                HideMenu();
            }
            
            // Update UI elements
            if (isMenuVisible)
            {
                UpdateMenuContent();
            }
        }
        

        
        /// <summary>
        /// Initialize the modern menu system
        /// </summary>
        private void InitializeModernMenu()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Initializing modern menu system...");
            
            // Get references to game systems
            sessionManager = GungeonTogetherMod.Instance?.SessionManager;
            steamNetworking = ETGSteamP2PNetworking.Instance;
            
            // Create canvas
            CreateMenuCanvas();
            
            // Create background and main menu structure
            CreateBackgroundPanel();
            CreateMenuPanel();
            CreateMainPanel();
            CreateHostingPanel();
            CreateFriendsPanel();
            CreateHostSelectionPanel();
            CreatePingTestPanel();
            CreateSettingsPanel();
            
            // Ensure menu is hidden initially
            if (!ReferenceEquals(backgroundPanel, null))
            {
                backgroundPanel.SetActive(false);
                isMenuVisible = false;
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Background panel created and hidden - size: {menuSize}");
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[ModernMultiplayerMenu] Background panel is null after creation!");
            }
            
            if (!ReferenceEquals(menuPanel, null))
            {
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Menu panel created - size: {menuPanel.GetComponent<RectTransform>().sizeDelta}");
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[ModernMultiplayerMenu] Menu panel is null after creation!");
            }
            
            isInitialized = true;
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Modern menu system initialized successfully");
        }
        
        /// <summary>
        /// Create the main canvas for the menu
        /// </summary>
        private void CreateMenuCanvas()
        {
            var canvasObj = new GameObject("ModernMultiplayerMenuCanvas");
            canvasObj.transform.SetParent(transform);
            
            menuCanvas = canvasObj.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 2000; // Higher value to ensure it's on top
            
            // Add GraphicRaycaster for UI interaction
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Add EventSystem if it doesn't exist (required for UI interaction)
            if (ReferenceEquals(EventSystem.current, null))
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
            }
            
            // Create CanvasScaler for responsive design
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        }
        
        /// <summary>
        /// Create a full-screen background panel for the menu
        /// </summary>
        private void CreateBackgroundPanel()
        {
            backgroundPanel = new GameObject("MenuBackground");
            backgroundPanel.transform.SetParent(menuCanvas.transform, false);
            
            // Add background image that covers the entire screen
            var image = backgroundPanel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.6f); // Semi-transparent black background
            
            // Make the background cover the entire screen
            var rectTransform = backgroundPanel.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add button component to catch clicks on background and close menu
            var button = backgroundPanel.AddComponent<Button>();
            button.onClick.AddListener(() => HideMenu());
            
            // Make background clickable but transparent
            button.transition = Selectable.Transition.None;
            
            // Initially hide the background panel
            backgroundPanel.SetActive(false);
        }
        
        /// <summary>
        /// Create the main menu panel with proper layout
        /// </summary>
        private void CreateMenuPanel()
        {
            menuPanel = new GameObject("MenuPanel");
            menuPanel.transform.SetParent(backgroundPanel.transform, false);
            
            // Add background image with border effect
            var image = menuPanel.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.98f); // Darker, more opaque background
            
            // Add Outline component for border effect
            var outline = menuPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.6f, 1f, 0.8f); // Light blue border
            outline.effectDistance = new Vector2(2f, 2f);
            
            // Configure RectTransform for CENTER positioning
            var rectTransform = menuPanel.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f); // Center anchor
            rectTransform.pivot = new Vector2(0.5f, 0.5f); // Center pivot
            rectTransform.anchoredPosition = Vector2.zero; // Center position
            rectTransform.sizeDelta = new Vector2(menuSize.x, menuSize.y);
            
            // Temporarily disable Content Size Fitter to debug visibility issues
            // var contentSizeFitter = menuPanel.AddComponent<ContentSizeFitter>();
            // contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Menu panel should be active - visibility controlled by background panel
            menuPanel.SetActive(true);
        }
        
        /// <summary>
        /// Create the main panel with basic controls
        /// </summary>
        private void CreateMainPanel()
        {
            mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(menuPanel.transform, false);
            
            // Add vertical layout group with better spacing
            var layoutGroup = mainPanel.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(15, 15, 15, 15); // Increased padding
            layoutGroup.spacing = 12f; // Increased spacing between elements
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            
            // Configure RectTransform
            var rectTransform = mainPanel.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add Content Size Fitter
            var contentSizeFitter = mainPanel.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Create title
            CreateTitle("GungeonTogether Multiplayer", mainPanel.transform);
            
            // Create status section
            CreateStatusSection(mainPanel.transform);
            
            // Create main buttons
            CreateMainButtons(mainPanel.transform);
            
            // Create action buttons
            CreateActionButtons(mainPanel.transform);
            
            // Create close button
            CreateCloseButton(mainPanel.transform);
        }
        
        /// <summary>
        /// Create a title text element
        /// </summary>
        private void CreateTitle(string titleText, Transform parent)
        {
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);
            
            var text = titleObj.AddComponent<Text>();
            text.text = titleText;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            // Add Layout Element
            var layoutElement = titleObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30f;
            
            // Add Content Size Fitter
            var contentSizeFitter = titleObj.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        
        /// <summary>
        /// Create status information section
        /// </summary>
        private void CreateStatusSection(Transform parent)
        {
            var statusObj = new GameObject("StatusSection");
            statusObj.transform.SetParent(parent, false);
            
            // Add vertical layout with better spacing
            var layoutGroup = statusObj.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 6f; // Increased spacing between status lines
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.padding = new RectOffset(5, 5, 5, 5); // Add some padding
            
            // Add Layout Element - increased height to prevent text clipping
            var layoutElement = statusObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 90f; // Further increased to accommodate padding and spacing
            
            // Status text
            var statusTextObj = new GameObject("StatusText");
            statusTextObj.transform.SetParent(statusObj.transform, false);
            statusText = statusTextObj.AddComponent<Text>();
            statusText.text = "Status: Initializing...";
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 12;
            statusText.color = Color.cyan;
            statusText.alignment = TextAnchor.MiddleLeft;
            
            // Add layout element for status text
            var statusLayoutElement = statusTextObj.AddComponent<LayoutElement>();
            statusLayoutElement.preferredHeight = 25f;
            
            // Steam ID text with better wrapping
            var steamIdObj = new GameObject("SteamIdText");
            steamIdObj.transform.SetParent(statusObj.transform, false);
            steamIdText = steamIdObj.AddComponent<Text>();
            steamIdText.text = "Steam: Not connected";
            steamIdText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            steamIdText.fontSize = 10; // Slightly smaller font to prevent clipping
            steamIdText.color = Color.gray;
            steamIdText.alignment = TextAnchor.MiddleLeft;
            steamIdText.horizontalOverflow = HorizontalWrapMode.Wrap; // Enable text wrapping
            steamIdText.verticalOverflow = VerticalWrapMode.Truncate; // Truncate if too tall
            
            // Add layout element for Steam ID text
            var steamIdLayoutElement = steamIdObj.AddComponent<LayoutElement>();
            steamIdLayoutElement.preferredHeight = 30f; // Increased height for potential wrapping
        }
        
        /// <summary>
        /// Create main action buttons
        /// </summary>
        private void CreateMainButtons(Transform parent)
        {
            var buttonContainer = new GameObject("MainButtons");
            buttonContainer.transform.SetParent(parent, false);
            
            // Add horizontal layout
            var layoutGroup = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 8f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            
            // Add Layout Element
            var layoutElement = buttonContainer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 35f;
            
            // Host button
            hostButton = CreateButton("Host Session", buttonContainer.transform, OnHostClicked);
            
            // Join Friend button
            joinButton = CreateButton("Join Friend", buttonContainer.transform, OnJoinFriendClicked);
            
            // Disconnect button
            disconnectButton = CreateButton("Disconnect", buttonContainer.transform, OnDisconnectClicked);
        }
        
        /// <summary>
        /// Create action/utility buttons
        /// </summary>
        private void CreateActionButtons(Transform parent)
        {
            var buttonContainer = new GameObject("ActionButtons");
            buttonContainer.transform.SetParent(parent, false);
            
            // Add horizontal layout
            var layoutGroup = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 8f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            
            // Add Layout Element
            var layoutElement = buttonContainer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 35f;
            
            // Friends button
            friendsButton = CreateButton("Friends", buttonContainer.transform, OnFriendsClicked);
            
            // Player List button
            playerListButton = CreateButton("Player List", buttonContainer.transform, OnPlayerListClicked);
            
            // Settings button
            settingsButton = CreateButton("Settings", buttonContainer.transform, OnSettingsClicked);
        }
        
        /// <summary>
        /// Create close button
        /// </summary>
        private void CreateCloseButton(Transform parent)
        {
            closeButton = CreateButton("Close Menu", parent, OnCloseClicked);
            
            // Make close button red
            var colors = closeButton.colors;
            colors.normalColor = new Color(0.6f, 0.2f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.4f, 0.1f, 0.1f, 1f);
            closeButton.colors = colors;
        }
        
        /// <summary>
        /// Create a styled button
        /// </summary>
        private Button CreateButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObj = new GameObject($"Button_{text.Replace(" ", "")}");
            buttonObj.transform.SetParent(parent, false);
            
            // Add button component
            var button = buttonObj.AddComponent<Button>();
            
            // Add image for background
            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            
            // Configure colors
            var colors = button.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            button.colors = colors;
            
            // Add text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            var textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 12;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleCenter;
            
            // Configure text RectTransform
            var textRect = textComponent.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            // Add Layout Element for consistent sizing
            var layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30f;
            layoutElement.flexibleWidth = 1f;
            
            // Set click handler
            if (!ReferenceEquals(onClick, null))
            {
                button.onClick.AddListener(onClick);
            }
            
            return button;
        }
        
        /// <summary>
        /// Create hosting panel (placeholder for now)
        /// </summary>
        private void CreateHostingPanel()
        {
            hostingPanel = new GameObject("HostingPanel");
            hostingPanel.transform.SetParent(menuPanel.transform, false);
            hostingPanel.SetActive(false);
        }
        
        /// <summary>
        /// Create friends panel for selecting friends to join
        /// </summary>
        private void CreateFriendsPanel()
        {
            friendsPanel = new GameObject("FriendsPanel");
            friendsPanel.transform.SetParent(menuPanel.transform, false);
            friendsPanel.SetActive(false);
            
            // Add vertical layout
            var verticalLayout = friendsPanel.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 10f;
            verticalLayout.padding = new RectOffset(15, 15, 15, 15);
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            
            // Set RectTransform to fill parent
            var friendsPanelRect = friendsPanel.GetComponent<RectTransform>();
            friendsPanelRect.anchorMin = Vector2.zero;
            friendsPanelRect.anchorMax = Vector2.one;
            friendsPanelRect.offsetMin = Vector2.zero;
            friendsPanelRect.offsetMax = Vector2.zero;
            
            // Add title
            var titleObj = new GameObject("FriendsTitle");
            titleObj.transform.SetParent(friendsPanel.transform, false);
            
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "Select Friend to Join";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            
            var titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 30f;
            
            // Create scroll area for friends list
            CreateFriendsScrollArea();
            
            // Add back button
            var backButton = CreateButton("Back", friendsPanel.transform, OnBackFromFriendsSelection);
            var backLayout = backButton.gameObject.AddComponent<LayoutElement>();
            backLayout.preferredHeight = 35f;
        }
        
        /// <summary>
        /// Create scrollable area for friends list
        /// </summary>
        private void CreateFriendsScrollArea()
        {
            // Create scroll area container
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(friendsPanel.transform, false);
            
            // Add Layout Element for the scroll area
            var scrollLayout = scrollArea.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.preferredHeight = 300f;
            
            // Add ScrollRect
            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            
            // Create viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
            
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0.1f);
            
            scrollRect.viewport = viewportRect;
            
            // Create content container
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            // Add vertical layout to content
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 5f;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            
            // Add ContentSizeFitter
            var contentSizeFitter = content.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scrollRect.content = contentRect;
        }
        
        /// <summary>
        /// Handle back button from friends selection
        /// </summary>
        private void OnBackFromFriendsSelection()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Back from friends selection clicked");
            SwitchToPanel(mainPanel);
        }
        
        /// <summary>
        /// Create ping test panel (placeholder for now)
        /// </summary>
        private void CreatePingTestPanel()
        {
            pingTestPanel = new GameObject("PingTestPanel");
            pingTestPanel.transform.SetParent(menuPanel.transform, false);
            pingTestPanel.SetActive(false);
        }
        
        /// <summary>
        /// Create settings panel (placeholder for now)
        /// </summary>
        private void CreateSettingsPanel()
        {
            settingsPanel = new GameObject("SettingsPanel");
            settingsPanel.transform.SetParent(menuPanel.transform, false);
            settingsPanel.SetActive(false);
        }
        
        /// <summary>
        /// Create host selection panel for choosing which host to join
        /// </summary>
        private void CreateHostSelectionPanel()
        {
            hostSelectionPanel = new GameObject("HostSelectionPanel");
            hostSelectionPanel.transform.SetParent(menuPanel.transform, false);
            hostSelectionPanel.SetActive(false);
            
            // Add vertical layout
            var layoutGroup = hostSelectionPanel.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 10f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.padding = new RectOffset(20, 20, 20, 20);
            
            // Configure RectTransform
            var rect = hostSelectionPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(hostSelectionPanel.transform, false);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "Select Host to Join";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 18;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            
            var titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 30f;
            
            // Instructions
            var instructionsObj = new GameObject("Instructions");
            instructionsObj.transform.SetParent(hostSelectionPanel.transform, false);
            var instructionsText = instructionsObj.AddComponent<Text>();
            instructionsText.text = "🎮 Click on any host below to join their session:";
            instructionsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            instructionsText.fontSize = 12;
            instructionsText.color = Color.gray;
            instructionsText.alignment = TextAnchor.MiddleCenter;
            
            var instructionsLayout = instructionsObj.AddComponent<LayoutElement>();
            instructionsLayout.preferredHeight = 20f;
            
            // Scroll area for hosts (in case there are many)
            CreateHostScrollArea();
            
            // Back button
            var backButton = CreateButton("Back to Main Menu", hostSelectionPanel.transform, OnBackFromHostSelection);
            backButton.GetComponent<LayoutElement>().preferredHeight = 35f;
            
            // Make back button stand out
            var backColors = backButton.colors;
            backColors.normalColor = new Color(0.4f, 0.4f, 0.6f, 0.8f);
            backColors.highlightedColor = new Color(0.5f, 0.5f, 0.7f, 0.9f);
            backButton.colors = backColors;
        }
        
        /// <summary>
        /// Create scrollable area for host list
        /// </summary>
        private void CreateHostScrollArea()
        {
            // Create scroll area container
            var scrollAreaObj = new GameObject("HostScrollArea");
            scrollAreaObj.transform.SetParent(hostSelectionPanel.transform, false);
            
            // Add layout element to control size
            var scrollLayout = scrollAreaObj.AddComponent<LayoutElement>();
            scrollLayout.preferredHeight = 300f;
            scrollLayout.flexibleHeight = 1f;
            
            // Configure RectTransform
            var scrollRect = scrollAreaObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            
            // Create content area that will hold the host buttons
            var contentObj = new GameObject("HostListContent");
            contentObj.transform.SetParent(scrollAreaObj.transform, false);
            
            // Add vertical layout to content
            var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 5f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            
            // Configure content RectTransform
            var contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            // Store reference for later use
            hostListContent = contentObj.transform;
        }
        
        /// <summary>
        /// Toggle menu visibility
        /// </summary>
        public void ToggleMenu()
        {
            if (isMenuVisible)
            {
                HideMenu();
            }
            else
            {
                ShowMenu();
            }
        }
        
        /// <summary>
        /// Show the menu
        /// </summary>
        public void ShowMenu()
        {
            if (!isInitialized || ReferenceEquals(backgroundPanel, null)) 
            {
                GungeonTogether.Logging.Debug.LogError("[ModernMultiplayerMenu] Cannot show menu - not initialized");
                return;
            }
            
            backgroundPanel.SetActive(true);
            
            // Ensure menu panel is also active
            if (!ReferenceEquals(menuPanel, null))
            {
                menuPanel.SetActive(true);
            }
            
            isMenuVisible = true;
            UpdateMenuContent();
        }
        
        /// <summary>
        /// Hide the menu
        /// </summary>
        public void HideMenu()
        {
            GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] HideMenu called - isInitialized: {isInitialized}, backgroundPanel null: {ReferenceEquals(backgroundPanel, null)}");
            
            if (!isInitialized || ReferenceEquals(backgroundPanel, null)) 
            {
                GungeonTogether.Logging.Debug.LogError("[ModernMultiplayerMenu] Cannot hide menu - not initialized or background panel is null");
                return;
            }
            
            backgroundPanel.SetActive(false);
            isMenuVisible = false;
            
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Menu hidden");
        }
        
        /// <summary>
        /// Update menu content based on current state
        /// </summary>
        private void UpdateMenuContent()
        {
            if (!isInitialized) return;
            
            try
            {
                // Update status text
                if (!ReferenceEquals(statusText, null))
                {
                    if (!ReferenceEquals(sessionManager, null))
                    {
                        if (sessionManager.IsActive)
                        {
                            statusText.text = sessionManager.IsHost ? "Status: Hosting session" : "Status: Connected to host";
                            statusText.color = Color.green;
                        }
                        else
                        {
                            statusText.text = "Status: Not connected";
                            statusText.color = Color.yellow;
                        }
                    }
                    else
                    {
                        statusText.text = "Status: Session manager not available";
                        statusText.color = Color.red;
                    }
                }
                
                // Update Steam ID text (cached to prevent log spam)
                if (!ReferenceEquals(steamIdText, null))
                {
                    // Only check Steam ID once per second to prevent spam
                    if (!steamIdCached || Time.time - lastSteamIdCheck > 1.0f)
                    {
                        try
                        {
                            // Try to get Steam networking instance if we don't have it
                            if (ReferenceEquals(steamNetworking, null))
                            {
                                steamNetworking = ETGSteamP2PNetworking.Instance;
                            }
                            
                            // Try to get Steam ID directly from reflection helper (more reliable)
                            ulong steamId = 0;
                            
                            if (!ReferenceEquals(steamNetworking, null))
                            {
                                steamId = steamNetworking.GetSteamID();
                            }
                            else
                            {
                                // Fallback: try direct Steam reflection helper
                                steamId = SteamReflectionHelper.GetSteamID();
                            }
                            
                            if (steamId > 0)
                            {
                                cachedSteamId = steamId;
                                steamIdCached = true;
                            }
                            else
                            {
                                // Only mark as unavailable if we actually get 0 back
                                cachedSteamId = 0;
                                steamIdCached = false;
                            }
                        }
                        catch (Exception steamEx)
                        {
                            GungeonTogether.Logging.Debug.LogWarning($"[ModernMultiplayerMenu] Failed to get Steam ID: {steamEx.Message}");
                            cachedSteamId = 0;
                            steamIdCached = false;
                        }
                        lastSteamIdCheck = Time.time;
                    }
                    
                    // Update UI with cached value
                    if (steamIdCached && cachedSteamId > 0)
                    {
                        steamIdText.text = $"Steam: {cachedSteamId}";
                        steamIdText.color = Color.cyan;
                    }
                    else
                    {
                        steamIdText.text = "Steam: Not available";
                        steamIdText.color = Color.red;
                    }
                }
                
                // Update button states
                if (!ReferenceEquals(sessionManager, null))
                {
                    bool isActive = sessionManager.IsActive;
                    
                    if (!ReferenceEquals(hostButton, null))
                        hostButton.interactable = !isActive;
                    
                    if (!ReferenceEquals(joinButton, null))
                        joinButton.interactable = !isActive;
                    
                    if (!ReferenceEquals(disconnectButton, null))
                        disconnectButton.interactable = isActive;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error updating menu content: {e.Message}");
            }
        }
        
        // Button click handlers
        private void OnHostClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Host button clicked");
            GungeonTogetherMod.Instance?.StartHosting();
        }
        
        private void OnJoinFriendClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Join Friend button clicked");
            
            try
            {
                // Get Steam networking instance
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    MultiplayerUIManager.ShowNotification("Steam networking not available", 3f);
                    return;
                }

                // Get friends information
                var allFriends = SteamFriendsHelper.GetSteamFriends();
                
                if (allFriends == null || allFriends.Length == 0)
                {
                    MultiplayerUIManager.ShowNotification("No Steam friends found\n\nAdd friends on Steam to play together!", 4f);
                    return;
                }

                // Filter ONLY for friends playing ETG with GungeonTogether
                var gungeonTogetherFriends = new List<SteamFriendsHelper.FriendInfo>();
                foreach (var friend in allFriends)
                {
                    if (friend.isPlayingETG && friend.hasGungeonTogether)
                    {
                        gungeonTogetherFriends.Add(friend);
                    }
                }

                if (gungeonTogetherFriends.Count == 0)
                {
                    int etgFriendsCount = 0;
                    foreach (var friend in allFriends)
                    {
                        if (friend.isPlayingETG) etgFriendsCount++;
                    }
                    
                    string message = "No friends with GungeonTogether found";
                    if (etgFriendsCount > 0)
                    {
                        message += $"\n\n{etgFriendsCount} friend(s) playing vanilla ETG found.\nAsk them to install GungeonTogether!";
                    }
                    else
                    {
                        message += "\n\nNo friends currently playing Enter the Gungeon";
                    }
                    
                    MultiplayerUIManager.ShowNotification(message, 5f);
                    return;
                }

                // Show friends selection panel with GungeonTogether players only
                ShowFriendsSelectionPanel(gungeonTogetherFriends.ToArray());
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error in OnJoinFriendClicked: {e.Message}");
                MultiplayerUIManager.ShowNotification($"Error getting friends: {e.Message}", 4f);
            }
        }
        
        /// <summary>
        /// Show the host selection panel with clickable host entries
        /// </summary>
        private void ShowHostSelectionPanel(SteamHostManager.HostInfo[] hosts)
        {
            try
            {
                // Store hosts for reference
                _availableHostsForSelection = hosts;
                
                // Clear any existing host buttons
                ClearHostList();
                
                // Create buttons for each host
                for (int i = 0; i < hosts.Length; i++)
                {
                    CreateHostButton(hosts[i], i + 1);
                }
                
                // Switch to host selection panel
                SwitchToPanel(hostSelectionPanel);
                
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Showing {hosts.Length} hosts in selection panel");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error showing host selection: {e.Message}");
                MultiplayerUIManager.ShowNotification($"Error showing hosts: {e.Message}", 4f);
            }
        }
        
        /// <summary>
        /// Show the friends selection panel with clickable friend entries
        /// </summary>
        private void ShowFriendsSelectionPanel(SteamFriendsHelper.FriendInfo[] friends)
        {
            try
            {
                // Clear any existing friend buttons
                ClearFriendsList();
                
                // Create buttons for each friend
                for (int i = 0; i < friends.Length; i++)
                {
                    CreateFriendButton(friends[i], i + 1);
                }
                
                // Switch to friends panel
                SwitchToPanel(friendsPanel);
                
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Showing {friends.Length} friends in selection panel");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error showing friends selection: {e.Message}");
                MultiplayerUIManager.ShowNotification($"Error showing friends: {e.Message}", 4f);
            }
        }
        
        /// <summary>
        /// Clear the friends list content
        /// </summary>
        private void ClearFriendsList()
        {
            var friendsContent = friendsPanel?.transform.Find("ScrollArea/Viewport/Content");
            if (!ReferenceEquals(friendsContent, null))
            {
                // Destroy all existing friend buttons
                for (int i = friendsContent.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(friendsContent.GetChild(i).gameObject);
                }
            }
        }
        
        /// <summary>
        /// Create a clickable button for a specific friend
        /// </summary>
        private void CreateFriendButton(SteamFriendsHelper.FriendInfo friend, int number)
        {
            var friendsContent = friendsPanel?.transform.Find("ScrollArea/Viewport/Content");
            if (ReferenceEquals(friendsContent, null))
            {
                GungeonTogether.Logging.Debug.LogError("[ModernMultiplayerMenu] Friends content container not found");
                return;
            }

            // Create friend button container
            var friendButton = new GameObject($"Friend_{number}");
            friendButton.transform.SetParent(friendsContent, false);
            
            // Add Button component
            var button = friendButton.AddComponent<Button>();
            
            // Add Image for button background
            var buttonImage = friendButton.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.25f, 0.3f, 0.8f);
            
            // Add Layout Element
            var layoutElement = friendButton.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50f;
            layoutElement.flexibleWidth = 1f;
            
            // Create friend info text
            var textObj = new GameObject("FriendText");
            textObj.transform.SetParent(friendButton.transform, false);
            
            var friendText = textObj.AddComponent<Text>();
            friendText.text = GetFriendButtonText(friend, number);
            friendText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            friendText.fontSize = 14;
            friendText.color = Color.white;
            friendText.alignment = TextAnchor.MiddleLeft;
            
            // Position text
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            
            // Set up button click handler
            button.onClick.AddListener(() => OnFriendButtonClicked(friend));
            
            // Style button colors
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.25f, 0.3f, 0.8f);
            colors.highlightedColor = new Color(0.3f, 0.35f, 0.4f, 0.9f);
            colors.pressedColor = new Color(0.15f, 0.2f, 0.25f, 1f);
            button.colors = colors;
        }
        
        /// <summary>
        /// Get display text for friend button
        /// </summary>
        private string GetFriendButtonText(SteamFriendsHelper.FriendInfo friend, int number)
        {
            string status = "";
            if (friend.hasGungeonTogether)
            {
                if (friend.gungeonTogetherStatus.Equals("hosting"))
                {
                    status = " [🌐 HOSTING]";
                }
                else if (friend.gungeonTogetherStatus.Equals("playing"))
                {
                    status = " [🤝 PLAYING GT]";
                }
                else if (!string.IsNullOrEmpty(friend.gungeonTogetherStatus))
                {
                    status = $" [GT: {friend.gungeonTogetherStatus}]";
                }
                else
                {
                    status = " [🔧 GT Ready]";
                }
                
                // Add version if available
                if (!string.IsNullOrEmpty(friend.gungeonTogetherVersion))
                {
                    status += $" v{friend.gungeonTogetherVersion}";
                }
            }
            else
            {
                // This shouldn't happen since we only show GT players now, but just in case
                status = " [ETG Only]";
            }
            
            return $"{number}. {friend.personaName}{status}";
        }
        
        /// <summary>
        /// Handle clicking on a specific friend button
        /// </summary>
        private void OnFriendButtonClicked(SteamFriendsHelper.FriendInfo friend)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Attempting to join friend: {friend.personaName} (ID: {friend.steamId})");
                
                // Check if we have a valid Steam ID
                if (friend.steamId != 0)
                {
                    // Try to join the friend's session
                    GungeonTogetherMod.Instance?.JoinSpecificHost(friend.steamId);
                    MultiplayerUIManager.ShowNotification($"Attempting to join {friend.personaName}...", 3f);
                    HideMenu();
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Invalid Steam ID: {friend.steamId}");
                    MultiplayerUIManager.ShowNotification("Invalid friend Steam ID", 3f);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error joining friend {friend.personaName}: {e.Message}");
                MultiplayerUIManager.ShowNotification($"Error joining {friend.personaName}: {e.Message}", 4f);
            }
        }

        /// <summary>
        /// Clear the host list content
        /// </summary>
        private void ClearHostList()
        {
            if (!ReferenceEquals(hostListContent, null))
            {
                // Destroy all existing host buttons
                for (int i = hostListContent.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(hostListContent.GetChild(i).gameObject);
                }
            }
        }
        
        /// <summary>
        /// Create a clickable button for a specific host
        /// </summary>
        private void CreateHostButton(SteamHostManager.HostInfo host, int number)
        {
            if (ReferenceEquals(hostListContent, null)) return;
            
            var buttonObj = new GameObject($"HostButton_{host.steamId}");
            buttonObj.transform.SetParent(hostListContent, false);
            
            // Add button component
            var button = buttonObj.AddComponent<Button>();
            
            // Add image for background
            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.4f, 0.8f);
            
            // Configure button colors
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.3f, 0.4f, 0.8f);
            colors.highlightedColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
            colors.pressedColor = new Color(0.1f, 0.2f, 0.3f, 0.9f);
            button.colors = colors;
            
            // Add layout element
            var layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 55f;
            
            // Create text content
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            var textComponent = textObj.AddComponent<Text>();
            textComponent.text = $"🎮 {host.sessionName}\n💻 Steam ID: {host.steamId}";
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 11;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;
            
            // Configure text RectTransform with padding
            var textRect = textComponent.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 5);  // Left and bottom padding
            textRect.offsetMax = new Vector2(-12, -5); // Right and top padding
            
            // Add click handler
            button.onClick.AddListener(() => OnHostButtonClicked(host));
            
            GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Created host button: {host.sessionName}");
        }
        
        /// <summary>
        /// Handle clicking on a specific host button
        /// </summary>
        private void OnHostButtonClicked(SteamHostManager.HostInfo host)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[ModernMultiplayerMenu] Host button clicked: {host.sessionName} ({host.steamId})");
                
                // Join the selected host
                GungeonTogetherMod.Instance?.JoinSpecificHost(host.steamId);
                MultiplayerUIManager.ShowNotification($"🔗 Connecting to {host.sessionName}...\n\nPlease wait while the connection is established.", 4f);
                
                // Close the menu
                HideMenu();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error joining host: {e.Message}");
                MultiplayerUIManager.ShowNotification($"❌ Failed to join {host.sessionName}\n\nError: {e.Message}", 5f);
            }
        }
        
        /// <summary>
        /// Switch to a specific panel, hiding others
        /// </summary>
        private void SwitchToPanel(GameObject targetPanel)
        {
            // Hide all panels
            if (!ReferenceEquals(mainPanel, null)) mainPanel.SetActive(false);
            if (!ReferenceEquals(hostingPanel, null)) hostingPanel.SetActive(false);
            if (!ReferenceEquals(friendsPanel, null)) friendsPanel.SetActive(false);
            if (!ReferenceEquals(hostSelectionPanel, null)) hostSelectionPanel.SetActive(false);
            if (!ReferenceEquals(settingsPanel, null)) settingsPanel.SetActive(false);
            if (!ReferenceEquals(pingTestPanel, null)) pingTestPanel.SetActive(false);
            
            // Show target panel
            if (!ReferenceEquals(targetPanel, null))
            {
                targetPanel.SetActive(true);
            }
        }
        
        /// <summary>
        /// Handle back button from host selection
        /// </summary>
        private void OnBackFromHostSelection()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Back from host selection clicked");
            SwitchToPanel(mainPanel);
        }
        
        private void OnDisconnectClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Disconnect button clicked");
            GungeonTogetherMod.Instance?.StopMultiplayer();
        }
        
        private void OnFriendsClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Friends button clicked");
            
            try
            {
                // Get Steam networking instance
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (ReferenceEquals(steamNet, null) || !steamNet.IsAvailable())
                {
                    MultiplayerUIManager.ShowNotification("Steam networking not available", 3f);
                    return;
                }

                // Get friends information (this now uses caching)
                var allFriends = SteamFriendsHelper.GetSteamFriends();
                
                // Build friends list message
                string message = "";
                int totalOnlineFriends = 0;
                int etgPlayingFriends = 0;
                int gungeonTogetherFriends = 0;
                
                // Count different types of friends
                foreach (var friend in allFriends)
                {
                    if (friend.isOnline) totalOnlineFriends++;
                    if (friend.isPlayingETG) etgPlayingFriends++;
                    if (friend.hasGungeonTogether) gungeonTogetherFriends++;
                }
                
                // Header with general info
                message += $"Steam Friends Status:\n";
                message += $"• Total friends online: {totalOnlineFriends}\n";
                message += $"• Playing Enter the Gungeon: {etgPlayingFriends}\n";
                message += $"• Using GungeonTogether: {gungeonTogetherFriends}\n\n";
                
                // Show friends with GungeonTogether
                if (gungeonTogetherFriends > 0)
                {
                    message += $"Friends with GungeonTogether ({gungeonTogetherFriends}):\n";
                    int shownGTFriends = 0;
                    foreach (var friend in allFriends)
                    {
                        if (friend.hasGungeonTogether && shownGTFriends < 8)
                        {
                            string gtStatus = "";
                            if (friend.gungeonTogetherStatus.Equals("hosting"))
                            {
                                gtStatus = " [🌐 HOSTING]";
                            }
                            else if (friend.gungeonTogetherStatus.Equals("playing"))
                            {
                                gtStatus = " [🤝 PLAYING]";
                            }
                            else if (!string.IsNullOrEmpty(friend.gungeonTogetherStatus))
                            {
                                gtStatus = $" [{friend.gungeonTogetherStatus}]";
                            }
                            else
                            {
                                gtStatus = " [Ready]";
                            }
                            
                            message += $"• {friend.personaName}{gtStatus}\n";
                            shownGTFriends++;
                        }
                    }
                    if (gungeonTogetherFriends > 8)
                    {
                        message += $"• ...and {gungeonTogetherFriends - 8} more\n";
                    }
                    message += "\nClick 'Join Friend' to connect to them!";
                }
                else if (etgPlayingFriends > 0)
                {
                    message += $"Friends playing vanilla ETG ({etgPlayingFriends - gungeonTogetherFriends}):\n";
                    int shownVanillaFriends = 0;
                    foreach (var friend in allFriends)
                    {
                        if (friend.isPlayingETG && !friend.hasGungeonTogether && shownVanillaFriends < 5)
                        {
                            message += $"• {friend.personaName} (vanilla ETG)\n";
                            shownVanillaFriends++;
                        }
                    }
                    message += "\nAsk them to install GungeonTogether for multiplayer!";
                }
                else
                {
                    message += "No friends currently playing Enter the Gungeon\n";
                    message += "Ask friends to play ETG with GungeonTogether!";
                }
                
                // Show all online friends if there are some
                if (totalOnlineFriends > etgPlayingFriends && allFriends.Length > 0)
                {
                    message += $"\n\nOther online friends ({totalOnlineFriends - etgPlayingFriends}):\n";
                    int shown = 0;
                    foreach (var friend in allFriends)
                    {
                        if (friend.isOnline && !friend.isPlayingETG && shown < 5)
                        {
                            message += $"• {friend.personaName}\n";
                            shown++;
                        }
                    }
                    if (totalOnlineFriends - etgPlayingFriends > 5)
                    {
                        message += $"• ...and {totalOnlineFriends - etgPlayingFriends - 5} more\n";
                    }
                }
                
                // Show the friends list
                MultiplayerUIManager.ShowNotification(message, 8f);
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[ModernMultiplayerMenu] Error getting friends list: {ex.Message}");
                MultiplayerUIManager.ShowNotification($"Error getting friends: {ex.Message}", 5f);
            }
        }
        
        private void OnPlayerListClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Player list button clicked");
            // Toggle player list UI
            var playerListUI = PlayerListUI.Instance;
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.TogglePlayerList();
            }
            else
            {
                GungeonTogether.Logging.Debug.LogWarning("[ModernMultiplayerMenu] PlayerListUI instance not found");
            }
        }
        
        
        private void OnSettingsClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Settings button clicked");
            // Show settings (placeholder)
        }
        
        private void OnCloseClicked()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Close button clicked");
            HideMenu();
        }
        
        void OnDestroy()
        {
            GungeonTogether.Logging.Debug.Log("[ModernMultiplayerMenu] Menu destroyed");
        }
    }
}
