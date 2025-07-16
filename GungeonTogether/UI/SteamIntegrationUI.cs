using GungeonTogether.Game;
using GungeonTogether.Steam;
using System;
using UnityEngine;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Beautiful Steam integration UI overlay
    /// Shows Steam status, friends list, and overlay integration features
    /// </summary>
    public class SteamIntegrationUI : MonoBehaviour
    {
        [Header("Steam UI Elements")]
        public GameObject steamPanel;
        public UnityEngine.UI.Text steamStatusText;
        public UnityEngine.UI.Text steamIdText;
        public UnityEngine.UI.Button inviteFriendsButton;
        public UnityEngine.UI.Button overlayButton;
        public Transform friendsListContainer;

        [Header("Rich Presence Display")]
        public UnityEngine.UI.Text richPresenceText;
        public UnityEngine.UI.Image richPresenceIcon;

        private ISteamNetworking steamNetworking;
        private bool isVisible = false;

        void Start()
        {
            steamNetworking = SteamNetworkingFactory.TryCreateSteamNetworking();
            InitializeSteamUI();
        }

        void Update()
        {
            if (isVisible)
            {
                UpdateSteamStatus();
            }
        }

        /// <summary>
        /// Initialize the Steam integration UI
        /// </summary>
        private void InitializeSteamUI()
        {
            CreateSteamPanel();
            SetupSteamEventHandlers();
        }

        /// <summary>
        /// Create the main Steam panel
        /// </summary>
        private void CreateSteamPanel()
        {
            if (ReferenceEquals(steamPanel, null))
            {
                steamPanel = CreateUIPanel(transform, "SteamPanel", new Vector2(350, 250));

                var panelRect = steamPanel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0, 1);
                panelRect.anchorMax = new Vector2(0, 1);
                panelRect.anchoredPosition = new Vector2(20, -20);

                // Panel styling with Steam colors
                var panelImage = steamPanel.GetComponent<UnityEngine.UI.Image>();
                if (panelImage)
                {
                    panelImage.color = new Color(0.12f, 0.15f, 0.18f, 0.95f); // Steam dark theme
                }

                CreateSteamUI();

                // Initially hide the panel
                steamPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Create Steam UI elements
        /// </summary>
        private void CreateSteamUI()
        {
            // Steam title with icon
            var titleObj = CreateUIText(steamPanel.transform, "SteamTitle", "🎮 Steam Integration",
                                      new Vector2(0, 100), new Vector2(330, 30), 18, TextAnchor.MiddleCenter);
            titleObj.GetComponent<UnityEngine.UI.Text>().color = new Color(0.4f, 0.7f, 1f, 1f); // Steam blue

            // Steam status
            steamStatusText = CreateUIText(steamPanel.transform, "SteamStatus", "Checking Steam...",
                                         new Vector2(0, 70), new Vector2(330, 20), 14, TextAnchor.MiddleCenter).GetComponent<UnityEngine.UI.Text>();

            // Steam ID display
            steamIdText = CreateUIText(steamPanel.transform, "SteamId", "Steam ID: Loading...",
                                     new Vector2(0, 50), new Vector2(330, 18), 12, TextAnchor.MiddleCenter).GetComponent<UnityEngine.UI.Text>();

            // Rich Presence display
            CreateRichPresenceDisplay();

            // Steam action buttons
            CreateSteamButtons();

            // Friends list area
            CreateFriendsListArea();
        }

        /// <summary>
        /// Create Rich Presence display
        /// </summary>
        private void CreateRichPresenceDisplay()
        {
            var richPresencePanel = CreateUIPanel(steamPanel.transform, "RichPresencePanel", new Vector2(320, 40));
            var richPresenceRect = richPresencePanel.GetComponent<RectTransform>();
            richPresenceRect.anchoredPosition = new Vector2(0, 15);

            var richPresenceImage = richPresencePanel.GetComponent<UnityEngine.UI.Image>();
            richPresenceImage.color = new Color(0.2f, 0.3f, 0.4f, 0.6f);

            // Rich Presence icon
            var iconObj = new GameObject("RichPresenceIcon");
            iconObj.transform.SetParent(richPresencePanel.transform);
            richPresenceIcon = iconObj.AddComponent<UnityEngine.UI.Image>();

            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(25, 25);
            iconRect.anchoredPosition = new Vector2(20, 0);

            richPresenceIcon.color = new Color(0.6f, 0.8f, 1f, 1f);

            // Rich Presence text
            richPresenceText = CreateUIText(richPresencePanel.transform, "RichPresenceText", "Rich Presence: Not set",
                                          new Vector2(30, 0), new Vector2(270, 35), 11, TextAnchor.MiddleLeft).GetComponent<UnityEngine.UI.Text>();
        }

        /// <summary>
        /// Create Steam action buttons
        /// </summary>
        private void CreateSteamButtons()
        {
            // Invite Friends button
            inviteFriendsButton = CreateUIButton(steamPanel.transform, "InviteFriendsButton", "📧 Invite Friends",
                                               new Vector2(-80, -20), new Vector2(140, 30), OnInviteFriendsClicked);

            // Steam Overlay button
            overlayButton = CreateUIButton(steamPanel.transform, "OverlayButton", "🖥️ Steam Overlay",
                                         new Vector2(80, -20), new Vector2(140, 30), OnOverlayClicked);

            // Style buttons with Steam theme
            StyleSteamButton(inviteFriendsButton);
            StyleSteamButton(overlayButton);
        }

        /// <summary>
        /// Create friends list area
        /// </summary>
        private void CreateFriendsListArea()
        {
            var friendsPanel = CreateUIPanel(steamPanel.transform, "FriendsPanel", new Vector2(320, 80));
            var friendsRect = friendsPanel.GetComponent<RectTransform>();
            friendsRect.anchoredPosition = new Vector2(0, -80);

            var friendsImage = friendsPanel.GetComponent<UnityEngine.UI.Image>();
            friendsImage.color = new Color(0.1f, 0.12f, 0.15f, 0.8f);

            // Friends title
            CreateUIText(friendsPanel.transform, "FriendsTitle", "Friends Playing GungeonTogether:",
                       new Vector2(0, 25), new Vector2(310, 20), 12, TextAnchor.MiddleCenter);

            // Friends container (scrollable)
            var friendsContentObj = new GameObject("FriendsContent");
            friendsContentObj.transform.SetParent(friendsPanel.transform);
            friendsListContainer = friendsContentObj.transform;

            var contentRect = friendsContentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.7f);
            contentRect.offsetMin = new Vector2(5, 5);
            contentRect.offsetMax = new Vector2(-5, -5);

            var contentLayout = friendsContentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            contentLayout.spacing = 2f;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
        }

        /// <summary>
        /// Style a button with Steam theme
        /// </summary>
        private void StyleSteamButton(UnityEngine.UI.Button button)
        {
            var buttonImage = button.GetComponent<UnityEngine.UI.Image>();
            if (buttonImage)
            {
                buttonImage.color = new Color(0.3f, 0.45f, 0.6f, 0.9f); // Steam button blue
            }

            var colors = button.colors;
            colors.normalColor = new Color(0.3f, 0.45f, 0.6f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.55f, 0.7f, 1f);
            colors.pressedColor = new Color(0.2f, 0.35f, 0.5f, 1f);
            button.colors = colors;
        }

        /// <summary>
        /// Update Steam status display
        /// </summary>
        private void UpdateSteamStatus()
        {
            if (!ReferenceEquals(steamNetworking, null) && steamNetworking.IsAvailable())
            {
                steamStatusText.text = "✅ Steam Connected";
                steamStatusText.color = new Color(0.4f, 0.8f, 0.4f, 1f); // Green

                ulong steamId = steamNetworking.GetSteamID();
                steamIdText.text = $"Steam ID: {steamId}";
                steamIdText.color = Color.white;

                UpdateRichPresence();
                UpdateFriendsList();

                // Enable buttons
                if (inviteFriendsButton) inviteFriendsButton.interactable = true;
                if (overlayButton) overlayButton.interactable = true;
            }
            else
            {
                steamStatusText.text = "❌ Steam Unavailable";
                steamStatusText.color = new Color(0.8f, 0.4f, 0.4f, 1f); // Red

                steamIdText.text = "Steam ID: Not available";
                steamIdText.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Gray

                richPresenceText.text = "Rich Presence: Steam not available";
                richPresenceText.color = new Color(0.6f, 0.6f, 0.6f, 1f);

                // Disable buttons
                if (inviteFriendsButton) inviteFriendsButton.interactable = false;
                if (overlayButton) overlayButton.interactable = false;
            }
        }

        /// <summary>
        /// Update Rich Presence display
        /// </summary>
        private void UpdateRichPresence()
        {
            // This would query the current Rich Presence state
            // For now, we'll show a placeholder based on session state
            SimpleSessionManager sessionManager = null;
            if (!ReferenceEquals(GungeonTogetherMod.Instance, null))
            {
                sessionManager = GungeonTogetherMod.Instance._sessionManager;
            }

            if (!ReferenceEquals(sessionManager, null) && sessionManager.IsActive)
            {
                if (sessionManager.IsHost)
                {
                    richPresenceText.text = "Rich Presence: Hosting GungeonTogether";
                    richPresenceIcon.color = new Color(0.4f, 0.7f, 1f, 1f); // Blue for hosting
                }
                else
                {
                    richPresenceText.text = "Rich Presence: Playing GungeonTogether";
                    richPresenceIcon.color = new Color(0.4f, 0.8f, 0.4f, 1f); // Green for playing
                }
            }
            else
            {
                richPresenceText.text = "Rich Presence: In Enter the Gungeon";
                richPresenceIcon.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Gray for not in session
            }

            richPresenceText.color = Color.white;
        }

        /// <summary>
        /// Update friends list
        /// </summary>
        private void UpdateFriendsList()
        {
            // Clear existing friends list
            foreach (Transform child in friendsListContainer)
            {
                Destroy(child.gameObject);
            }

            try
            {
                // Get friends playing GungeonTogether
                string[] friends = SteamSessionHelper.GetFriendsPlayingGame();

                if (friends.Length > 0)
                {
                    foreach (string friendId in friends)
                    {
                        CreateFriendEntry(friendId);
                    }
                }
                else
                {
                    CreateNoFriendsEntry();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamIntegrationUI] Error updating friends list: {e.Message}");
                CreateErrorEntry();
            }
        }

        /// <summary>
        /// Create a friend entry in the list
        /// </summary>
        private void CreateFriendEntry(string friendId)
        {
            var friendEntry = CreateUIPanel(friendsListContainer, $"FriendEntry_{friendId}", new Vector2(300, 20));

            var entryImage = friendEntry.GetComponent<UnityEngine.UI.Image>();
            entryImage.color = new Color(0.2f, 0.25f, 0.3f, 0.6f);

            // Friend text
            var friendText = CreateUIText(friendEntry.transform, "FriendText", $"👤 {friendId}",
                                        new Vector2(-80, 0), new Vector2(160, 18), 10, TextAnchor.MiddleLeft);

            // Join button
            var joinButton = CreateUIButton(friendEntry.transform, "JoinFriendButton", "Join",
                                          new Vector2(100, 0), new Vector2(60, 18), () => OnJoinFriendClicked(friendId));

            StyleSteamButton(joinButton);
        }

        /// <summary>
        /// Create "No friends" entry
        /// </summary>
        private void CreateNoFriendsEntry()
        {
            var noFriendsEntry = CreateUIPanel(friendsListContainer, "NoFriendsEntry", new Vector2(300, 20));

            CreateUIText(noFriendsEntry.transform, "NoFriendsText", "No friends currently playing",
                       new Vector2(0, 0), new Vector2(290, 18), 10, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// Create error entry
        /// </summary>
        private void CreateErrorEntry()
        {
            var errorEntry = CreateUIPanel(friendsListContainer, "ErrorEntry", new Vector2(300, 20));

            var errorText = CreateUIText(errorEntry.transform, "ErrorText", "Error loading friends list",
                                       new Vector2(0, 0), new Vector2(290, 18), 10, TextAnchor.MiddleCenter);
            errorText.GetComponent<UnityEngine.UI.Text>().color = new Color(0.8f, 0.4f, 0.4f, 1f);
        }

        // Event Handlers

        private void OnInviteFriendsClicked()
        {
            GungeonTogether.Logging.Debug.Log("[SteamIntegrationUI] Invite friends button clicked");

            try
            {
                SteamSessionHelper.ShowInviteDialog();
                MultiplayerUIManager.ShowNotification("🎮 Steam invite dialog opened!", 2f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamIntegrationUI] Error showing invite dialog: {e.Message}");
                MultiplayerUIManager.ShowNotification("❌ Failed to open invite dialog", 2f);
            }
        }

        private void OnOverlayClicked()
        {
            GungeonTogether.Logging.Debug.Log("[SteamIntegrationUI] Steam overlay button clicked");
            MultiplayerUIManager.ShowNotification("💬 Steam overlay features not implemented yet", 2f);
        }

        private void OnJoinFriendClicked(string friendId)
        {
            GungeonTogether.Logging.Debug.Log($"[SteamIntegrationUI] Join friend clicked: {friendId}");

            try
            {
                SteamSessionHelper.JoinFriendSession(friendId);
                MultiplayerUIManager.ShowNotification($"🔗 Joining friend: {friendId}", 2f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[SteamIntegrationUI] Error joining friend: {e.Message}");
                MultiplayerUIManager.ShowNotification($"❌ Failed to join friend: {friendId}", 3f);
            }
        }

        /// <summary>
        /// Toggle Steam panel visibility
        /// </summary>
        public void ToggleVisibility()
        {
            isVisible = !isVisible;

            if (steamPanel)
            {
                steamPanel.SetActive(isVisible);

                if (isVisible)
                {
                    UpdateSteamStatus();
                }
            }
        }

        /// <summary>
        /// Show the Steam panel
        /// </summary>
        public void Show()
        {
            if (!isVisible)
            {
                ToggleVisibility();
            }
        }

        /// <summary>
        /// Hide the Steam panel
        /// </summary>
        public void Hide()
        {
            if (isVisible)
            {
                ToggleVisibility();
            }
        }

        // UI Helper methods (reusing from MultiplayerUIController)

        private GameObject CreateUIPanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent);

            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.1f);

            return panel;
        }

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
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return textObj;
        }

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
            textComponent.fontSize = 12;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return button;
        }

        /// <summary>
        /// Setup Steam event handlers
        /// </summary>
        private void SetupSteamEventHandlers()
        {
            // Subscribe to Steam events if available
            try
            {
                ETGSteamP2PNetworking.OnOverlayJoinRequested += OnSteamOverlayJoinRequested;
                GungeonTogether.Logging.Debug.Log("[SteamIntegrationUI] Subscribed to Steam events");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamIntegrationUI] Could not subscribe to Steam events: {e.Message}");
            }
        }

        private void OnSteamOverlayJoinRequested(string hostSteamId)
        {
            MultiplayerUIManager.ShowNotification($"\ud83c\udfae Steam join request: {hostSteamId}", 3f);
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            try
            {
                ETGSteamP2PNetworking.OnOverlayJoinRequested -= OnSteamOverlayJoinRequested;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[SteamIntegrationUI] Error unsubscribing from Steam events: {e.Message}");
            }
        }
    }
}
