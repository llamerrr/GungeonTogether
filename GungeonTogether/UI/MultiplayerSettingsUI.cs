using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Settings panel for GungeonTogether UI customization
    /// Allows players to configure UI behavior, keybinds, and preferences
    /// </summary>
    public class MultiplayerSettingsUI : MonoBehaviour
    {
        private static MultiplayerSettingsUI _instance;
        public static MultiplayerSettingsUI Instance => _instance;

        [Header("UI References")]
        public Canvas settingsCanvas;
        public GameObject settingsPanel;
        public ScrollRect settingsScrollRect;
        
        [Header("Settings Sections")]
        public Transform generalSettingsContainer;
        public Transform uiSettingsContainer;
        public Transform audioSettingsContainer;
        public Transform keybindSettingsContainer;
        
        [Header("Animation Settings")]
        public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // Settings data
        public class UISettings
        {
            public bool enableSoundEffects = true;
            public bool enableNotifications = true;
            public bool enableHUDDragging = true;
            public bool minimizeHUDByDefault = false;
            public float notificationDuration = 3f;
            public float uiScale = 1f;
            public bool enableAnimations = true;
            public bool showAdvancedControls = false;
            
            // Keybinds
            public KeyCode toggleMainUI = KeyCode.M;
            public KeyCode toggleHUD = KeyCode.H;
            public KeyCode testNotification = KeyCode.N;
            public bool requireCtrlModifier = true;
        }
        
        private UISettings currentSettings;
        private bool isSettingsVisible = false;
        private bool isInitialized = false;
        
        // UI Elements
        private Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
        private Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
        private Dictionary<string, Dropdown> dropdowns = new Dictionary<string, Dropdown>();
        private Dictionary<string, Button> keybindButtons = new Dictionary<string, Button>();
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
                Debug.Log("[MultiplayerSettings] Settings UI initialized");
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
                InitializeSettingsUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerSettings] Error in delayed initialization: {ex.Message}");
                Debug.LogError($"[MultiplayerSettings] Stack trace: {ex.StackTrace}");
            }
        }
        
        void Update()
        {
            // Handle input for toggling settings
            if (Input.GetKeyDown(KeyCode.P) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                ToggleSettings();
            }
            
            // ESC to close settings
            if (isSettingsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                HideSettings();
            }
        }
        
        /// <summary>
        /// Initialize the settings UI system
        /// </summary>
        public void InitializeSettingsUI()
        {
            try
            {
                Debug.Log("[MultiplayerSettings] Initializing settings UI...");
                
                // Safety check for Unity context
                if (gameObject == null || transform == null)
                {
                    Debug.LogError("[MultiplayerSettings] GameObject or transform is null during initialization");
                    return;
                }
                
                CreateSettingsCanvas();
                CreateSettingsPanel();
                CreateSettingsSections();
                PopulateSettings();
                ApplySettings();
                
                isInitialized = true;
                Debug.Log("[MultiplayerSettings] Settings UI initialization complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerSettings] Failed to initialize settings UI: {ex.Message}");
                Debug.LogError($"[MultiplayerSettings] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Create the settings canvas
        /// </summary>
        private void CreateSettingsCanvas()
        {
            var canvasObj = new GameObject("MultiplayerSettingsCanvas");
            canvasObj.transform.SetParent(transform);
            
            settingsCanvas = canvasObj.AddComponent<Canvas>();
            settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            settingsCanvas.sortingOrder = 1000; // High priority
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Initially hidden
            settingsCanvas.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Create the main settings panel
        /// </summary>
        private void CreateSettingsPanel()
        {
            settingsPanel = CreateUIPanel(settingsCanvas.transform, "SettingsMainPanel", new Vector2(800, 600));
            
            var panelRect = settingsPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            
            var panelImage = settingsPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // Add border
            var outline = settingsPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.6f, 1f, 1f);
            outline.effectDistance = new Vector2(2, 2);
            
            // Title bar
            CreateTitleBar();
            
            // Create scroll view for settings content
            CreateScrollView();
        }
        
        /// <summary>
        /// Create the settings title bar
        /// </summary>
        private void CreateTitleBar()
        {
            var titleBar = CreateUIPanel(settingsPanel.transform, "TitleBar", new Vector2(800, 50));
            var titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -25);
            
            var titleImage = titleBar.GetComponent<Image>();
            titleImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // Title text
            CreateUIText(titleBar.transform, "SettingsTitle", "GungeonTogether Settings", 
                       new Vector2(-30, 0), new Vector2(700, 50), 20, TextAnchor.MiddleLeft);
            
            // Close button
            var closeButton = CreateUIButton(titleBar.transform, "CloseButton", "✕", 
                                           new Vector2(375, 0), new Vector2(50, 40));
            closeButton.onClick.AddListener(HideSettings);
            
            var closeButtonImage = closeButton.GetComponent<Image>();
            closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            
            // Hover effects
            var buttonColors = closeButton.colors;
            buttonColors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
            buttonColors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
            closeButton.colors = buttonColors;
        }
        
        /// <summary>
        /// Create scroll view for settings content
        /// </summary>
        private void CreateScrollView()
        {
            var scrollViewObj = new GameObject("SettingsScrollView");
            scrollViewObj.transform.SetParent(settingsPanel.transform);
            
            var scrollRect = scrollViewObj.transform as RectTransform;
            if (scrollRect == null)
            {
                scrollRect = scrollViewObj.AddComponent<RectTransform>();
            }
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -60); // Leave space for title bar
            
            settingsScrollRect = scrollViewObj.AddComponent<ScrollRect>();
            settingsScrollRect.horizontal = false;
            settingsScrollRect.vertical = true;
            
            // Viewport
            var viewport = CreateUIPanel(scrollViewObj.transform, "Viewport", Vector2.zero);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            settingsScrollRect.viewport = viewportRect;
            
            // Content
            var content = CreateUIPanel(viewport.transform, "Content", Vector2.zero);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.sizeDelta = new Vector2(0, 1000); // Will be adjusted based on content
            contentRect.anchoredPosition = Vector2.zero;
            
            var layoutGroup = content.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 20f;
            layoutGroup.padding = new RectOffset(20, 20, 20, 20);
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlHeight = false;
            
            var contentSizeFitter = content.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            settingsScrollRect.content = contentRect;
            
            // Store section containers
            generalSettingsContainer = content.transform;
        }
        
        /// <summary>
        /// Create settings sections
        /// </summary>
        private void CreateSettingsSections()
        {
            // General Settings Section
            CreateSettingsSection("General Settings", generalSettingsContainer, new Dictionary<string, SettingDefinition>
            {
                { "enableNotifications", new SettingDefinition("Enable Notifications", SettingType.Toggle, "Show toast notifications for multiplayer events") },
                { "enableAnimations", new SettingDefinition("Enable Animations", SettingType.Toggle, "Enable smooth UI animations") },
                { "showAdvancedControls", new SettingDefinition("Show Advanced Controls", SettingType.Toggle, "Display advanced debug controls") },
                { "notificationDuration", new SettingDefinition("Notification Duration", SettingType.Slider, "How long notifications stay visible", 1f, 10f) },
                { "uiScale", new SettingDefinition("UI Scale", SettingType.Slider, "Scale factor for UI elements", 0.5f, 2f) }
            });
            
            // Audio Settings Section
            CreateSettingsSection("Audio Settings", generalSettingsContainer, new Dictionary<string, SettingDefinition>
            {
                { "enableSoundEffects", new SettingDefinition("Enable Sound Effects", SettingType.Toggle, "Play UI sound effects") }
            });
            
            // HUD Settings Section
            CreateSettingsSection("HUD Settings", generalSettingsContainer, new Dictionary<string, SettingDefinition>
            {
                { "enableHUDDragging", new SettingDefinition("Enable HUD Dragging", SettingType.Toggle, "Allow dragging the multiplayer HUD") },
                { "minimizeHUDByDefault", new SettingDefinition("Minimize HUD by Default", SettingType.Toggle, "Start with HUD minimized") }
            });
            
            // Keybind Settings Section
            CreateKeybindSection();
        }
        
        /// <summary>
        /// Create a settings section
        /// </summary>
        private void CreateSettingsSection(string sectionName, Transform parent, Dictionary<string, SettingDefinition> settings)
        {
            // Section header
            var headerObj = CreateUIText(parent, $"{sectionName}Header", sectionName, Vector2.zero, new Vector2(760, 30), 18, TextAnchor.MiddleLeft);
            var headerText = headerObj.GetComponent<Text>();
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.3f, 0.6f, 1f, 1f);
            
            // Section separator
            var separator = CreateUIPanel(parent, $"{sectionName}Separator", new Vector2(760, 2));
            var separatorImage = separator.GetComponent<Image>();
            separatorImage.color = new Color(0.3f, 0.6f, 1f, 0.5f);
            
            // Settings items
            foreach (var kvp in settings)
            {
                CreateSettingItem(parent, kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// Create a setting item
        /// </summary>
        private void CreateSettingItem(Transform parent, string settingKey, SettingDefinition definition)
        {
            var itemObj = CreateUIPanel(parent, $"Setting_{settingKey}", new Vector2(760, 40));
            var itemImage = itemObj.GetComponent<Image>();
            itemImage.color = new Color(0.05f, 0.05f, 0.05f, 0.3f);
            
            // Label
            CreateUIText(itemObj.transform, "Label", definition.displayName, 
                       new Vector2(-280, 0), new Vector2(300, 40), 14, TextAnchor.MiddleLeft);
            
            // Tooltip (if provided)
            if (!string.IsNullOrEmpty(definition.tooltip))
            {
                var tooltipButton = CreateUIButton(itemObj.transform, "TooltipButton", "?", 
                                                 new Vector2(-150, 0), new Vector2(20, 20));
                // Add tooltip functionality here
            }
            
            // Control based on type
            switch (definition.type)
            {
                case SettingType.Toggle:
                    CreateToggleControl(itemObj.transform, settingKey);
                    break;
                case SettingType.Slider:
                    CreateSliderControl(itemObj.transform, settingKey, definition.minValue, definition.maxValue);
                    break;
                case SettingType.Dropdown:
                    CreateDropdownControl(itemObj.transform, settingKey, definition.options);
                    break;
            }
        }
        
        /// <summary>
        /// Create keybind settings section
        /// </summary>
        private void CreateKeybindSection()
        {
            var keybinds = new Dictionary<string, string>
            {
                { "toggleMainUI", "Toggle Main UI" },
                { "toggleHUD", "Toggle HUD" },
                { "testNotification", "Test Notification" }
            };
            
            // Section header
            var headerObj = CreateUIText(generalSettingsContainer, "KeybindHeader", "Keybinds", Vector2.zero, new Vector2(760, 30), 18, TextAnchor.MiddleLeft);
            var headerText = headerObj.GetComponent<Text>();
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.3f, 0.6f, 1f, 1f);
            
            // Ctrl modifier setting
            CreateSettingItem(generalSettingsContainer, "requireCtrlModifier", 
                new SettingDefinition("Require Ctrl Modifier", SettingType.Toggle, "Require holding Ctrl for keybinds"));
            
            // Individual keybinds
            foreach (var kvp in keybinds)
            {
                CreateKeybindItem(generalSettingsContainer, kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// Create a keybind item
        /// </summary>
        private void CreateKeybindItem(Transform parent, string keybindKey, string displayName)
        {
            var itemObj = CreateUIPanel(parent, $"Keybind_{keybindKey}", new Vector2(760, 40));
            var itemImage = itemObj.GetComponent<Image>();
            itemImage.color = new Color(0.05f, 0.05f, 0.05f, 0.3f);
            
            // Label
            CreateUIText(itemObj.transform, "Label", displayName, 
                       new Vector2(-280, 0), new Vector2(300, 40), 14, TextAnchor.MiddleLeft);
            
            // Keybind button
            var keybindButton = CreateUIButton(itemObj.transform, "KeybindButton", GetCurrentKeybind(keybindKey), 
                                             new Vector2(200, 0), new Vector2(120, 30));
            
            keybindButtons[keybindKey] = keybindButton;
            
            // Add click listener for keybind capture
            keybindButton.onClick.AddListener(() => StartKeybindCapture(keybindKey));
            
            // Reset button
            var resetButton = CreateUIButton(itemObj.transform, "ResetButton", "Reset", 
                                           new Vector2(330, 0), new Vector2(60, 30));
            resetButton.onClick.AddListener(() => ResetKeybind(keybindKey));
        }
        
        /// <summary>
        /// Toggle settings visibility
        /// </summary>
        public void ToggleSettings()
        {
            if (isSettingsVisible)
                HideSettings();
            else
                ShowSettings();
        }
        
        /// <summary>
        /// Show settings panel
        /// </summary>
        public void ShowSettings()
        {
            if (!isInitialized) return;
            
            settingsCanvas.gameObject.SetActive(true);
            isSettingsVisible = true;
            
            MultiplayerUIManager.PlayUISound("ui_open");
            
            // Animate in
            AnimatePanel(true);
            
            Debug.Log("[MultiplayerSettings] Settings panel opened");
        }
        
        /// <summary>
        /// Hide settings panel
        /// </summary>
        public void HideSettings()
        {
            if (!isSettingsVisible) return;
            
            isSettingsVisible = false;
            MultiplayerUIManager.PlayUISound("ui_close");
            
            // Animate out and then hide
            AnimatePanel(false);
            
            Debug.Log("[MultiplayerSettings] Settings panel closed");
        }
        
        // Helper classes and methods continue...
        
        public enum SettingType
        {
            Toggle,
            Slider,
            Dropdown,
            Keybind
        }
        
        public class SettingDefinition
        {
            public string displayName;
            public SettingType type;
            public string tooltip;
            public float minValue;
            public float maxValue;
            public string[] options;
            
            public SettingDefinition(string name, SettingType settingType, string tooltipText = "", float min = 0f, float max = 1f, string[] dropdownOptions = null)
            {
                displayName = name;
                type = settingType;
                tooltip = tooltipText;
                minValue = min;
                maxValue = max;
                options = dropdownOptions;
            }
        }
        
        // UI Helper methods - full implementations
        private GameObject CreateUIPanel(Transform parent, string name, Vector2 size)
        {
            var panelObj = new GameObject(name);
            if (parent != null)
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
        
        private void CreateToggleControl(Transform parent, string settingKey)
        {
            var toggleObj = new GameObject($"Toggle_{settingKey}");
            toggleObj.transform.SetParent(parent);
            
            var rectTransform = toggleObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(300, 0);
            rectTransform.sizeDelta = new Vector2(50, 30);
            
            var toggle = toggleObj.AddComponent<UnityEngine.UI.Toggle>();
            toggle.isOn = true; // Default value
            
            toggles[settingKey] = toggle;
        }
        
        private void CreateSliderControl(Transform parent, string settingKey, float min, float max)
        {
            var sliderObj = new GameObject($"Slider_{settingKey}");
            sliderObj.transform.SetParent(parent);
            
            var rectTransform = sliderObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(250, 0);
            rectTransform.sizeDelta = new Vector2(150, 20);
            
            var slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = (min + max) / 2f; // Default to middle
            
            sliders[settingKey] = slider;
        }
        
        private void CreateDropdownControl(Transform parent, string settingKey, string[] options)
        {
            var dropdownObj = new GameObject($"Dropdown_{settingKey}");
            dropdownObj.transform.SetParent(parent);
            
            var rectTransform = dropdownObj.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(250, 0);
            rectTransform.sizeDelta = new Vector2(150, 30);
            
            var dropdown = dropdownObj.AddComponent<UnityEngine.UI.Dropdown>();
            if (options != null)
            {
                dropdown.options.Clear();
                foreach (string option in options)
                {
                    dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData(option));
                }
            }
            
            dropdowns[settingKey] = dropdown;
        }
        
        private string GetCurrentKeybind(string keybindKey)
        {
            return keybindKey switch
            {
                "toggleMainUI" => currentSettings.toggleMainUI.ToString(),
                "toggleHUD" => currentSettings.toggleHUD.ToString(),
                "testNotification" => currentSettings.testNotification.ToString(),
                _ => "None"
            };
        }
        
        private void StartKeybindCapture(string keybindKey)
        {
            Debug.Log($"[MultiplayerSettings] Starting keybind capture for: {keybindKey}");
            // Implementation for keybind capture would go here
        }
        
        private void ResetKeybind(string keybindKey)
        {
            Debug.Log($"[MultiplayerSettings] Resetting keybind: {keybindKey}");
            // Reset to default values
        }
        
        private void PopulateSettings()
        {
            // Load current settings into UI controls
            if (toggles.ContainsKey("enableNotifications"))
                toggles["enableNotifications"].isOn = currentSettings.enableNotifications;
            
            if (sliders.ContainsKey("notificationDuration"))
                sliders["notificationDuration"].value = currentSettings.notificationDuration;
            
            // Add more setting population as needed
        }
        
        private void ApplySettings()
        {
            // Apply settings from UI controls to the system
            if (toggles.ContainsKey("enableNotifications"))
                currentSettings.enableNotifications = toggles["enableNotifications"].isOn;
            
            if (sliders.ContainsKey("notificationDuration"))
                currentSettings.notificationDuration = sliders["notificationDuration"].value;
            
            // Apply settings to other systems
            MultiplayerUIManager.SetUIAudioEnabled(currentSettings.enableSoundEffects);
        }
        
        private void LoadSettings()
        {
            currentSettings = new UISettings();
            // Load from PlayerPrefs or file in future
        }
        
        private void SaveSettings()
        {
            // Save to PlayerPrefs or file
            Debug.Log("[MultiplayerSettings] Settings saved");
        }
        
        private void AnimatePanel(bool showPanel)
        {
            // Simple instant animation to avoid coroutine issues
            if (settingsPanel != null)
            {
                var panelRect = settingsPanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    panelRect.localScale = showPanel ? Vector3.one : Vector3.zero;
                }
            }
            
            if (!showPanel && settingsCanvas != null)
            {
                settingsCanvas.gameObject.SetActive(false);
            }
        }
    }
}
