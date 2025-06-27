using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using GungeonTogether.Game;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Player list UI component for GungeonTogether
    /// Shows connected players with their status, ping, and Steam information
    /// </summary>
    public class PlayerListUI : MonoBehaviour
    {
        private static PlayerListUI _instance;
        public static PlayerListUI Instance => _instance;

        [Header("UI References")]
        public Canvas playerListCanvas;
        public GameObject playerListBackground;
        public GameObject playerListPanel;
        public ScrollRect playerScrollRect;
        public Transform playerListContainer;
        public GameObject playerEntryPrefab;
        
        [Header("Player List Settings")]
        public Vector2 panelSize = new Vector2(350, 500);
        public Vector2 playerEntrySize = new Vector2(330, 60);
        public float updateInterval = 1f;
        
        [Header("Animation Settings")]
        public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // Player data
        [System.Serializable]
        public class PlayerData
        {
            public ulong steamId;
            public string playerName;
            public bool isHost;
            public bool isConnected;
            public float ping;
            public PlayerStatus status;
            public DateTime lastUpdate;
            
            public PlayerData(ulong id, string name)
            {
                steamId = id;
                playerName = name;
                isHost = false;
                isConnected = true;
                ping = 0f;
                status = PlayerStatus.Connected;
                lastUpdate = DateTime.Now;
            }
        }
        
        public enum PlayerStatus
        {
            Connected,
            Connecting,
            Disconnected,
            Away,
            InGame,
            InMenu
        }
        
        // State management
        private bool isPlayerListVisible = false;
        private bool isInitialized = false;
        private float lastUpdateTime = 0f;
        
        // Player tracking
        private Dictionary<ulong, PlayerData> connectedPlayers = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, PlayerEntryUI> playerEntryUIs = new Dictionary<ulong, PlayerEntryUI>();
        private SimpleSessionManager sessionManager;
        
        // UI Colors for player status
        private static readonly Dictionary<PlayerStatus, Color> StatusColors = new Dictionary<PlayerStatus, Color>
        {
            { PlayerStatus.Connected, new Color(0.2f, 0.8f, 0.2f, 1f) },     // Green
            { PlayerStatus.Connecting, new Color(1f, 0.8f, 0.2f, 1f) },      // Orange
            { PlayerStatus.Disconnected, new Color(0.8f, 0.2f, 0.2f, 1f) },  // Red
            { PlayerStatus.Away, new Color(0.6f, 0.6f, 0.6f, 1f) },          // Gray
            { PlayerStatus.InGame, new Color(0.2f, 0.6f, 1f, 1f) },          // Blue
            { PlayerStatus.InMenu, new Color(0.8f, 0.6f, 1f, 1f) }           // Purple
        };
        
        void Awake()
        {
            if (ReferenceEquals(_instance, null))
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[PlayerListUI] Player List UI initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            // Use Invoke to defer initialization instead of coroutines
            Invoke(nameof(DelayedInitialization), 0.1f);
        }
        
        private void DelayedInitialization()
        {
            try 
            {
                InitializePlayerListUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerListUI] Error in delayed initialization: {ex.Message}");
                Debug.LogError($"[PlayerListUI] Stack trace: {ex.StackTrace}");
            }
        }
        
        void Update()
        {
            // Update player list periodically
            if (isPlayerListVisible && Time.time - lastUpdateTime > updateInterval)
            {
                UpdatePlayerList();
                lastUpdateTime = Time.time;
            }
            
            // ESC to close
            if (isPlayerListVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                HidePlayerList();
            }
        }
        
        /// <summary>
        /// Initialize the player list UI system
        /// </summary>
        public void InitializePlayerListUI()
        {
            try
            {
                Debug.Log("[PlayerListUI] Initializing player list UI...");
                
                // Safety check for Unity context
                if (ReferenceEquals(gameObject, null) || ReferenceEquals(transform, null))
                {
                    Debug.LogError("[PlayerListUI] GameObject or transform is null during initialization");
                    return;
                }
                
                CreatePlayerListCanvas();
                CreatePlayerListBackground();
                CreatePlayerListPanel();
                SetupPlayerEntryPrefab();
                
                // Get session manager reference
                var mod = GungeonTogetherMod.Instance;
                if (!ReferenceEquals(mod, null))
                {
                    sessionManager = mod.SessionManager;
                }
                
                isInitialized = true;
                Debug.Log("[PlayerListUI] Player list UI initialization complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerListUI] Failed to initialize player list UI: {ex.Message}");
                Debug.LogError($"[PlayerListUI] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Create the player list canvas
        /// </summary>
        private void CreatePlayerListCanvas()
        {
            var canvasObj = new GameObject("PlayerListCanvas");
            canvasObj.transform.SetParent(transform);
            
            playerListCanvas = canvasObj.AddComponent<Canvas>();
            playerListCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            playerListCanvas.sortingOrder = 500;
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Initially hidden
            playerListCanvas.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Create the player list background panel
        /// </summary>
        private void CreatePlayerListBackground()
        {
            playerListBackground = new GameObject("PlayerListBackground");
            playerListBackground.transform.SetParent(playerListCanvas.transform, false);
            
            // Add background image that covers the entire screen
            var image = playerListBackground.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.4f); // Semi-transparent black background (lighter than main menu)
            
            // Make the background cover the entire screen
            var rectTransform = playerListBackground.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add button component to catch clicks on background and close player list
            var button = playerListBackground.AddComponent<Button>();
            button.onClick.AddListener(() => HidePlayerList());
            
            // Make background clickable but transparent
            button.transition = Selectable.Transition.None;
            
            // Initially hide the background panel
            playerListBackground.SetActive(false);
        }
        
        /// <summary>
        /// Create the main player list panel
        /// </summary>
        private void CreatePlayerListPanel()
        {
            playerListPanel = CreateUIPanel(playerListBackground.transform, "PlayerListMainPanel", panelSize);
            
            var panelRect = playerListPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-panelSize.x / 2 - 20, 0);
            
            var panelImage = playerListPanel.GetComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.98f); // Match modern menu background
            
            // Add border to match modern menu
            var outline = playerListPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.6f, 1f, 0.8f); // Light blue border to match modern menu
            outline.effectDistance = new Vector2(2, 2);
            
            // Create title bar
            CreateTitleBar();
            
            // Create scroll view for players
            CreatePlayerScrollView();
            
            // Create footer with player count
            CreatePlayerListFooter();
        }
        
        /// <summary>
        /// Create the title bar
        /// </summary>
        private void CreateTitleBar()
        {
            var titleBar = CreateUIPanel(playerListPanel.transform, "TitleBar", new Vector2(panelSize.x, 40));
            var titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -20);
            
            var titleImage = titleBar.GetComponent<Image>();
            titleImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // Title text
            CreateUIText(titleBar.transform, "PlayerListTitle", "Connected Players", 
                       new Vector2(-10, 0), new Vector2(panelSize.x - 80, 40), 16, TextAnchor.MiddleLeft);
            
            // Close button
            var closeButton = CreateUIButton(titleBar.transform, "CloseButton", "✕", 
                                           new Vector2(panelSize.x / 2 - 25, 0), new Vector2(30, 30));
            closeButton.onClick.AddListener(HidePlayerList);
            
            var closeButtonImage = closeButton.GetComponent<Image>();
            closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            
            // Refresh button
            var refreshButton = CreateUIButton(titleBar.transform, "RefreshButton", "🔄", 
                                             new Vector2(panelSize.x / 2 - 60, 0), new Vector2(30, 30));
            refreshButton.onClick.AddListener(RefreshPlayerList);
            
            var refreshButtonImage = refreshButton.GetComponent<Image>();
            refreshButtonImage.color = new Color(0.2f, 0.6f, 1f, 1f);
        }
        
        /// <summary>
        /// Create the scroll view for players
        /// </summary>
        private void CreatePlayerScrollView()
        {
            var scrollViewObj = new GameObject("PlayerScrollView");
            scrollViewObj.transform.SetParent(playerListPanel.transform);
            
            var scrollRect = scrollViewObj.transform as RectTransform;
            if (ReferenceEquals(scrollRect, null))
            {
                scrollRect = scrollViewObj.AddComponent<RectTransform>();
            }
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10, 40); // Leave space for footer
            scrollRect.offsetMax = new Vector2(-10, -50); // Leave space for title bar
            
            playerScrollRect = scrollViewObj.AddComponent<ScrollRect>();
            playerScrollRect.horizontal = false;
            playerScrollRect.vertical = true;
            
            // Viewport
            var viewport = CreateUIPanel(scrollViewObj.transform, "Viewport", Vector2.zero);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            playerScrollRect.viewport = viewportRect;
            
            // Content container
            var content = CreateUIPanel(viewport.transform, "Content", Vector2.zero);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.sizeDelta = new Vector2(0, 0);
            contentRect.anchoredPosition = Vector2.zero;
            
            var layoutGroup = content.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 5f;
            layoutGroup.padding = new RectOffset(5, 5, 5, 5);
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlHeight = false;
            
            var contentSizeFitter = content.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            playerScrollRect.content = contentRect;
            playerListContainer = content.transform;
        }
        
        /// <summary>
        /// Create the footer with player count
        /// </summary>
        private void CreatePlayerListFooter()
        {
            var footer = CreateUIPanel(playerListPanel.transform, "Footer", new Vector2(panelSize.x, 30));
            var footerRect = footer.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.anchoredPosition = new Vector2(0, 15);
            
            var footerImage = footer.GetComponent<Image>();
            footerImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // Player count text
            CreateUIText(footer.transform, "PlayerCount", "0 players connected", 
                       Vector2.zero, new Vector2(panelSize.x - 20, 30), 12, TextAnchor.MiddleCenter);
        }
        
        /// <summary>
        /// Setup the player entry prefab
        /// </summary>
        private void SetupPlayerEntryPrefab()
        {
            playerEntryPrefab = CreatePlayerEntryPrefab();
        }
        
        /// <summary>
        /// Create a player entry prefab
        /// </summary>
        private GameObject CreatePlayerEntryPrefab()
        {
            var entryObj = CreateUIPanel(null, "PlayerEntryPrefab", playerEntrySize);
            var entryImage = entryObj.GetComponent<Image>();
            entryImage.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
            
            // Player avatar placeholder
            var avatar = CreateUIPanel(entryObj.transform, "Avatar", new Vector2(40, 40));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(30, 0);
            
            var avatarImage = avatar.GetComponent<Image>();
            avatarImage.color = new Color(0.3f, 0.6f, 1f, 1f);
            
            // Player name
            CreateUIText(entryObj.transform, "PlayerName", "Player Name", 
                       new Vector2(25, 10), new Vector2(180, 20), 14, TextAnchor.MiddleLeft);
            
            // Player status
            CreateUIText(entryObj.transform, "PlayerStatus", "Connected", 
                       new Vector2(25, -10), new Vector2(120, 15), 11, TextAnchor.MiddleLeft);
            
            // Ping display
            CreateUIText(entryObj.transform, "PlayerPing", "0ms", 
                       new Vector2(120, -10), new Vector2(50, 15), 11, TextAnchor.MiddleRight);
            
            // Host indicator
            var hostIcon = CreateUIText(entryObj.transform, "HostIcon", "👑", 
                                      new Vector2(-30, 0), new Vector2(20, 20), 16, TextAnchor.MiddleCenter);
            hostIcon.gameObject.SetActive(false);
            
            // Status indicator dot
            var statusDot = CreateUIPanel(entryObj.transform, "StatusDot", new Vector2(12, 12));
            var statusDotRect = statusDot.GetComponent<RectTransform>();
            statusDotRect.anchorMin = new Vector2(1f, 0.5f);
            statusDotRect.anchorMax = new Vector2(1f, 0.5f);
            statusDotRect.anchoredPosition = new Vector2(-15, 0);
            
            var statusDotImage = statusDot.GetComponent<Image>();
            statusDotImage.color = StatusColors[PlayerStatus.Connected];
            
            // Add hover effect
            var button = entryObj.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            button.colors = colors;
            
            return entryObj;
        }
        
        /// <summary>
        /// Toggle player list visibility
        /// </summary>
        public void TogglePlayerList()
        {
            if (isPlayerListVisible)
                HidePlayerList();
            else
                ShowPlayerList();
        }
        
        /// <summary>
        /// Show player list
        /// </summary>
        public void ShowPlayerList()
        {
            if (!isInitialized) return;
            
            playerListCanvas.gameObject.SetActive(true);
            playerListBackground.SetActive(true);
            isPlayerListVisible = true;
            
            MultiplayerUIManager.PlayUISound("ui_open");
            
            // Animate in
            AnimatePanel(true);
            
            // Refresh player list immediately
            RefreshPlayerList();
            
            Debug.Log("[PlayerListUI] Player list opened");
        }
        
        /// <summary>
        /// Hide player list
        /// </summary>
        public void HidePlayerList()
        {
            if (!isPlayerListVisible) return;
            
            isPlayerListVisible = false;
            MultiplayerUIManager.PlayUISound("ui_close");
            
            // Animate out and then hide
            AnimatePanel(false);
            
            Debug.Log("[PlayerListUI] Player list closed");
        }
        
        /// <summary>
        /// Add a player to the list
        /// </summary>
        public void AddPlayer(ulong steamId, string playerName, bool isHost = false)
        {
            if (connectedPlayers.ContainsKey(steamId))
            {
                UpdatePlayer(steamId, playerName, isHost);
                return;
            }
            
            var playerData = new PlayerData(steamId, playerName)
            {
                isHost = isHost,
                status = PlayerStatus.Connected
            };
            
            connectedPlayers[steamId] = playerData;
            
            if (isPlayerListVisible)
            {
                CreatePlayerEntryUI(playerData);
            }
            
            UpdatePlayerCount();
            MultiplayerUIManager.PlayUISound("mp_player_joined");
            
            Debug.Log($"[PlayerListUI] Added player: {playerName} (Host: {isHost})");
        }
        
        /// <summary>
        /// Remove a player from the list
        /// </summary>
        public void RemovePlayer(ulong steamId)
        {
            if (!connectedPlayers.ContainsKey(steamId)) return;
            
            var playerData = connectedPlayers[steamId];
            connectedPlayers.Remove(steamId);
            
            if (playerEntryUIs.ContainsKey(steamId))
            {
                Destroy(playerEntryUIs[steamId].gameObject);
                playerEntryUIs.Remove(steamId);
            }
            
            UpdatePlayerCount();
            MultiplayerUIManager.PlayUISound("mp_player_left");
            
            Debug.Log($"[PlayerListUI] Removed player: {playerData.playerName}");
        }
        
        /// <summary>
        /// Update a player's information
        /// </summary>
        public void UpdatePlayer(ulong steamId, string playerName = null, bool? isHost = null, PlayerStatus? status = null, float? ping = null)
        {
            if (!connectedPlayers.ContainsKey(steamId)) return;
            
            var playerData = connectedPlayers[steamId];
            
            if (!ReferenceEquals(playerName, null)) playerData.playerName = playerName;
            if (isHost.HasValue) playerData.isHost = isHost.Value;
            if (status.HasValue) playerData.status = status.Value;
            if (ping.HasValue) playerData.ping = ping.Value;
            
            playerData.lastUpdate = DateTime.Now;
            
            if (playerEntryUIs.ContainsKey(steamId))
            {
                UpdatePlayerEntryUI(playerEntryUIs[steamId], playerData);
            }
        }
        
        /// <summary>
        /// Refresh the entire player list
        /// </summary>
        public void RefreshPlayerList()
        {
            if (ReferenceEquals(sessionManager, null)) return;
            
            // Clear existing UI
            foreach (var entry in playerEntryUIs.Values)
            {
                if (!ReferenceEquals(entry, null)) Destroy(entry.gameObject);
            }
            playerEntryUIs.Clear();
            
            // Recreate UI for all players
            foreach (var playerData in connectedPlayers.Values)
            {
                CreatePlayerEntryUI(playerData);
            }
            
            UpdatePlayerCount();
            MultiplayerUIManager.PlayUISound("ui_refresh");
        }
        
        /// <summary>
        /// Update player list
        /// </summary>
        private void UpdatePlayerList()
        {
            // Update ping and status for all players
            foreach (var kvp in connectedPlayers.ToList())
            {
                var steamId = kvp.Key;
                var playerData = kvp.Value;
                
                // Simulate ping updates (in real implementation, get from network)
                playerData.ping = UnityEngine.Random.Range(20f, 150f);
                
                // Check for disconnected players (haven't updated in a while)
                if ((DateTime.Now - playerData.lastUpdate).TotalSeconds > 30)
                {
                    playerData.status = PlayerStatus.Disconnected;
                }
                
                if (playerEntryUIs.ContainsKey(steamId))
                {
                    UpdatePlayerEntryUI(playerEntryUIs[steamId], playerData);
                }
            }
        }
        
        /// <summary>
        /// Create UI for a player entry
        /// </summary>
        private void CreatePlayerEntryUI(PlayerData playerData)
        {
            var entryObj = Instantiate(playerEntryPrefab, playerListContainer);
            entryObj.SetActive(true);
            
            var entryUI = entryObj.AddComponent<PlayerEntryUI>();
            entryUI.Initialize(playerData);
            
            playerEntryUIs[playerData.steamId] = entryUI;
            
            // Set initial data
            UpdatePlayerEntryUI(entryUI, playerData);
        }
        
        /// <summary>
        /// Update a player entry UI
        /// </summary>
        private void UpdatePlayerEntryUI(PlayerEntryUI entryUI, PlayerData playerData)
        {
            if (ReferenceEquals(entryUI, null)) return;
            
            entryUI.UpdateDisplay(playerData);
        }
        
        /// <summary>
        /// Update player count display
        /// </summary>
        private void UpdatePlayerCount()
        {
            var footerText = playerListPanel.transform.Find("Footer/PlayerCount")?.GetComponent<Text>();
            if (!ReferenceEquals(footerText, null))
            {
                int playerCount = connectedPlayers.Count;
                footerText.text = $"{playerCount} player{(!playerCount.Equals(1) ? "s" : "")} connected";
            }
        }
        
        // Animation and helper methods
        private void AnimatePanel(bool show)
        {
            // Simple instant animation to avoid coroutine issues
            if (!ReferenceEquals(playerListPanel, null))
            {
                var panelRect = playerListPanel.GetComponent<RectTransform>();
                if (!ReferenceEquals(panelRect, null))
                {
                    var targetPos = show ? new Vector2(-panelSize.x / 2 - 20, 0) : new Vector2(0, 0);
                    panelRect.anchoredPosition = targetPos;
                }
            }
            
            if (!show)
            {
                if (!ReferenceEquals(playerListBackground, null))
                {
                    playerListBackground.SetActive(false);
                }
                if (!ReferenceEquals(playerListCanvas, null))
                {
                    playerListCanvas.gameObject.SetActive(false);
                }
            }
        }
        
        // UI Helper methods - full implementations
        private GameObject CreateUIPanel(Transform parent, string name, Vector2 size)
        {
            var panelObj = new GameObject(name);
            if (!ReferenceEquals(parent, null))
                panelObj.transform.SetParent(parent);
            
            var rectTransform = panelObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            
            var image = panelObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            return panelObj;
        }
        
        private UnityEngine.UI.Text CreateUIText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            
            var rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            
            var textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = fontSize;
            textComponent.alignment = anchor;
            textComponent.color = Color.white;
            
            return textComponent;
        }
        
        private UnityEngine.UI.Button CreateUIButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);
            
            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            
            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);
            
            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            
            // Add text to button
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            var textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 12;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            
            return button;
        }
    }
    
    /// <summary>
    /// UI component for individual player entries
    /// </summary>
    public class PlayerEntryUI : MonoBehaviour
    {
        private Text playerNameText;
        private Text playerStatusText;
        private Text playerPingText;
        private Text hostIconText;
        private Image statusDotImage;
        private Image avatarImage;
        
        private PlayerListUI.PlayerData playerData;
        
        public void Initialize(PlayerListUI.PlayerData data)
        {
            playerData = data;
            
            // Get UI components
            playerNameText = transform.Find("PlayerName")?.GetComponent<Text>();
            playerStatusText = transform.Find("PlayerStatus")?.GetComponent<Text>();
            playerPingText = transform.Find("PlayerPing")?.GetComponent<Text>();
            hostIconText = transform.Find("HostIcon")?.GetComponent<Text>();
            statusDotImage = transform.Find("StatusDot")?.GetComponent<Image>();
            avatarImage = transform.Find("Avatar")?.GetComponent<Image>();
        }
        
        public void UpdateDisplay(PlayerListUI.PlayerData data)
        {
            playerData = data;
            
            if (!ReferenceEquals(playerNameText, null))
                playerNameText.text = data.playerName;
                
            if (!ReferenceEquals(playerStatusText, null))
                playerStatusText.text = data.status.ToString();
                
            if (!ReferenceEquals(playerPingText, null))
                playerPingText.text = $"{Mathf.RoundToInt(data.ping)}ms";
                
            if (!ReferenceEquals(hostIconText, null))
                hostIconText.gameObject.SetActive(data.isHost);
                
            if (!ReferenceEquals(statusDotImage, null) && !ReferenceEquals(PlayerListUI.Instance, null))
            {
                var statusColors = new Dictionary<PlayerListUI.PlayerStatus, Color>
                {
                    { PlayerListUI.PlayerStatus.Connected, new Color(0.2f, 0.8f, 0.2f, 1f) },
                    { PlayerListUI.PlayerStatus.Connecting, new Color(1f, 0.8f, 0.2f, 1f) },
                    { PlayerListUI.PlayerStatus.Disconnected, new Color(0.8f, 0.2f, 0.2f, 1f) },
                    { PlayerListUI.PlayerStatus.Away, new Color(0.6f, 0.6f, 0.6f, 1f) },
                    { PlayerListUI.PlayerStatus.InGame, new Color(0.2f, 0.6f, 1f, 1f) },
                    { PlayerListUI.PlayerStatus.InMenu, new Color(0.8f, 0.6f, 1f, 1f) }
                };
                
                if (statusColors.ContainsKey(data.status))
                {
                    statusDotImage.color = statusColors[data.status];
                }
            }
            
            // Update status text color
            if (!ReferenceEquals(playerStatusText, null) && object.Equals(data.status, PlayerListUI.PlayerStatus.Disconnected))
            {
                playerStatusText.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            }
            else if (!ReferenceEquals(playerStatusText, null))
            {
                playerStatusText.color = Color.white;
            }
        }
    }
}
