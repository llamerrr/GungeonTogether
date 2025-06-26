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
        private GameObject settingsPanel;
        private GameObject pingTestPanel;
        
        // UI Elements
        private Text statusText;
        private Text steamIdText;
        private Button hostButton;
        private Button joinButton;
        private Button disconnectButton;
        private Button pingTestButton;
        private Button friendsButton;
        private Button playerListButton;
        private Button settingsButton;
        private Button closeButton;
        
        // Friends list
        private Transform friendsContainer;
        private GameObject friendEntryPrefab;
        
        // Ping test
        private Text pingStatusText;
        private Button startPingButton;
        private Button stopPingButton;
        
        // References
        private SimpleSessionManager sessionManager;
        private ETGSteamP2PNetworking steamNetworking;
        private SteamP2PTestScript testScript;
        
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
                Debug.LogError($"[ModernMultiplayerMenu] Failed to initialize: {e.Message}");
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
                    Debug.Log("[ModernMultiplayerMenu] Ctrl+P detected - toggling menu");
                    ToggleMenu();
                }
            }
            
            // Handle ESC to close
            if (isMenuVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[ModernMultiplayerMenu] ESC pressed - hiding menu");
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
            Debug.Log("[ModernMultiplayerMenu] Initializing modern menu system...");
            
            // Get references to game systems
            sessionManager = GungeonTogetherMod.Instance?.SessionManager;
            steamNetworking = ETGSteamP2PNetworking.Instance;
            testScript = FindObjectOfType<SteamP2PTestScript>();
            
            // Create canvas
            CreateMenuCanvas();
            
            // Create background and main menu structure
            CreateBackgroundPanel();
            CreateMenuPanel();
            CreateMainPanel();
            CreateHostingPanel();
            CreateFriendsPanel();
            CreatePingTestPanel();
            CreateSettingsPanel();
            
            // Ensure menu is hidden initially
            if (!ReferenceEquals(backgroundPanel, null))
            {
                backgroundPanel.SetActive(false);
                isMenuVisible = false;
                Debug.Log($"[ModernMultiplayerMenu] Background panel created and hidden - size: {menuSize}");
            }
            else
            {
                Debug.LogError("[ModernMultiplayerMenu] Background panel is null after creation!");
            }
            
            if (!ReferenceEquals(menuPanel, null))
            {
                Debug.Log($"[ModernMultiplayerMenu] Menu panel created - size: {menuPanel.GetComponent<RectTransform>().sizeDelta}");
            }
            else
            {
                Debug.LogError("[ModernMultiplayerMenu] Menu panel is null after creation!");
            }
            
            isInitialized = true;
            Debug.Log("[ModernMultiplayerMenu] Modern menu system initialized successfully");
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
            if (EventSystem.current == null)
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
            
            // Join button
            joinButton = CreateButton("Join Session", buttonContainer.transform, OnJoinClicked);
            
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
            
            // Ping Test button
            pingTestButton = CreateButton("Ping Test", buttonContainer.transform, OnPingTestClicked);
            
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
            if (onClick != null)
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
        /// Create friends panel (placeholder for now)
        /// </summary>
        private void CreateFriendsPanel()
        {
            friendsPanel = new GameObject("FriendsPanel");
            friendsPanel.transform.SetParent(menuPanel.transform, false);
            friendsPanel.SetActive(false);
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
        /// Toggle menu visibility
        /// </summary>
        public void ToggleMenu()
        {
            Debug.Log($"[ModernMultiplayerMenu] ToggleMenu called - isMenuVisible: {isMenuVisible}");
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
            Debug.Log($"[ModernMultiplayerMenu] ShowMenu called - isInitialized: {isInitialized}, backgroundPanel null: {ReferenceEquals(backgroundPanel, null)}");
            
            if (!isInitialized || ReferenceEquals(backgroundPanel, null)) 
            {
                Debug.LogError("[ModernMultiplayerMenu] Cannot show menu - not initialized or background panel is null");
                return;
            }
            
            backgroundPanel.SetActive(true);
            
            // Ensure menu panel is also active
            if (!ReferenceEquals(menuPanel, null))
            {
                menuPanel.SetActive(true);
                Debug.Log($"[ModernMultiplayerMenu] Menu panel activated - active: {menuPanel.activeSelf}");
            }
            else
            {
                Debug.LogError("[ModernMultiplayerMenu] Menu panel is null!");
            }
            
            isMenuVisible = true;
            UpdateMenuContent();
            
            Debug.Log("[ModernMultiplayerMenu] Menu shown");
        }
        
        /// <summary>
        /// Hide the menu
        /// </summary>
        public void HideMenu()
        {
            Debug.Log($"[ModernMultiplayerMenu] HideMenu called - isInitialized: {isInitialized}, backgroundPanel null: {ReferenceEquals(backgroundPanel, null)}");
            
            if (!isInitialized || ReferenceEquals(backgroundPanel, null)) 
            {
                Debug.LogError("[ModernMultiplayerMenu] Cannot hide menu - not initialized or background panel is null");
                return;
            }
            
            backgroundPanel.SetActive(false);
            isMenuVisible = false;
            
            Debug.Log("[ModernMultiplayerMenu] Menu hidden");
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
                
                // Update Steam ID text
                if (!ReferenceEquals(steamIdText, null) && !ReferenceEquals(steamNetworking, null))
                {
                    if (steamNetworking.IsAvailable())
                    {
                        var steamId = steamNetworking.GetSteamID();
                        steamIdText.text = $"Steam: {steamId}";
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
                Debug.LogError($"[ModernMultiplayerMenu] Error updating menu content: {e.Message}");
            }
        }
        
        // Button click handlers
        private void OnHostClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Host button clicked");
            GungeonTogetherMod.Instance?.StartHosting();
        }
        
        private void OnJoinClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Join button clicked");
            GungeonTogetherMod.Instance?.TryJoinHost();
        }
        
        private void OnDisconnectClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Disconnect button clicked");
            GungeonTogetherMod.Instance?.StopMultiplayer();
        }
        
        private void OnFriendsClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Friends button clicked - Enhanced detection");
            
            try
            {
                // Get Steam networking instance for direct access
                var steamNet = SteamNetworkingFactory.TryCreateSteamNetworking();
                if (steamNet == null || !steamNet.IsAvailable())
                {
                    MultiplayerUIManager.ShowNotification("Steam networking not available", 3f);
                    return;
                }

                // Get all friends playing Enter the Gungeon (base game)
                var allFriends = ETGSteamP2PNetworking.Instance?.GetSteamFriends(false) ?? new System.Collections.Generic.List<ETGSteamP2PNetworking.FriendInfo>();
                var etgFriends = ETGSteamP2PNetworking.Instance?.GetETGFriends() ?? new System.Collections.Generic.List<ETGSteamP2PNetworking.FriendInfo>();
                
                // Also check for available hosts (people actually running GungeonTogether and hosting)
                ulong[] availableHosts = ETGSteamP2PNetworking.GetAvailableHosts();
                
                // Build comprehensive message
                string message = "";
                int totalOnlineFriends = 0;
                int etgPlayingFriends = 0;
                
                // Count online friends
                foreach (var friend in allFriends)
                {
                    if (friend.isOnline) totalOnlineFriends++;
                    if (friend.isPlayingETG) etgPlayingFriends++;
                }
                
                // Header with general info
                message += $"🔍 Steam Friends Analysis:\n";
                message += $"• Total friends online: {totalOnlineFriends}\n";
                message += $"• Playing Enter the Gungeon: {etgPlayingFriends}\n\n";
                
                // Show friends playing ETG (potential GungeonTogether players)
                if (etgFriends.Count > 0)
                {
                    message += $"🎮 Friends in Enter the Gungeon ({etgFriends.Count}):\n";
                    for (int i = 0; i < Math.Min(etgFriends.Count, 4); i++)
                    {
                        var friend = etgFriends[i];
                        string status = friend.isOnline ? "Online" : "Offline";
                        message += $"• {friend.personaName} ({status})\n";
                        message += $"  Steam ID: {friend.steamId}\n";
                    }
                    if (etgFriends.Count > 4)
                    {
                        message += $"• ...and {etgFriends.Count - 4} more\n";
                    }
                    message += "\n💡 These friends might have GungeonTogether!\n";
                }
                else
                {
                    message += "🎮 No friends currently in Enter the Gungeon\n\n";
                }
                
                // Show confirmed GungeonTogether hosts
                if (availableHosts.Length > 0)
                {
                    message += $"🌐 Confirmed GungeonTogether hosts ({availableHosts.Length}):\n";
                    for (int i = 0; i < Math.Min(availableHosts.Length, 3); i++)
                    {
                        message += $"• Host: {availableHosts[i]}\n";
                    }
                    if (availableHosts.Length > 3)
                    {
                        message += $"• ...and {availableHosts.Length - 3} more\n";
                    }
                    message += "\n✅ Press 'Join Session' to connect!";
                }
                else
                {
                    message += "🌐 No confirmed GungeonTogether hosts found\n";
                    if (etgPlayingFriends > 0)
                    {
                        message += "💬 Try asking friends above to install GungeonTogether!";
                    }
                    else
                    {
                        message += "💡 Ask friends to play Enter the Gungeon with GungeonTogether!";
                    }
                }
                
                // Show the information as a notification with longer duration
                MultiplayerUIManager.ShowNotification(message, 10f);
                
                // Enhanced debugging
                Debug.Log($"[ModernMultiplayerMenu] Enhanced Friends Analysis:");
                Debug.Log($"  Total friends: {allFriends.Count}");
                Debug.Log($"  Online friends: {totalOnlineFriends}");
                Debug.Log($"  ETG players: {etgPlayingFriends}");
                Debug.Log($"  Available hosts: {availableHosts.Length}");
                Debug.Log($"  Steam networking available: {steamNet.IsAvailable()}");
                Debug.Log($"  Your Steam ID: {steamNet.GetSteamID()}");
                
                // List all friends for debugging
                foreach (var friend in allFriends)
                {
                    Debug.Log($"  Friend: {friend.personaName} (ID: {friend.steamId}) - Online: {friend.isOnline}, ETG: {friend.isPlayingETG}, Game: {friend.currentGameName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModernMultiplayerMenu] Error in enhanced friends detection: {ex.Message}");
                MultiplayerUIManager.ShowNotification($"Error getting friends info: {ex.Message}", 5f);
            }
        }
        
        private void OnPlayerListClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Player list button clicked");
            // Toggle player list UI
            var playerListUI = PlayerListUI.Instance;
            if (playerListUI != null)
            {
                playerListUI.TogglePlayerList();
            }
            else
            {
                Debug.LogWarning("[ModernMultiplayerMenu] PlayerListUI instance not found");
            }
        }
        
        private void OnPingTestClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Ping test button clicked");
            // Start ping test using F8 key functionality
            if (!ReferenceEquals(testScript, null))
            {
                testScript.StartPingSession();
            }
            else
            {
                Debug.LogWarning("[ModernMultiplayerMenu] Test script not available for ping test");
            }
        }
        
        private void OnSettingsClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Settings button clicked");
            // Show settings (placeholder)
        }
        
        private void OnCloseClicked()
        {
            Debug.Log("[ModernMultiplayerMenu] Close button clicked");
            HideMenu();
        }
        
        void OnDestroy()
        {
            Debug.Log("[ModernMultiplayerMenu] Menu destroyed");
        }
    }
}
