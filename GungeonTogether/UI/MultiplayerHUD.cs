using System;
using UnityEngine;
using GungeonTogether.Game;
using GungeonTogether.Steam;

namespace GungeonTogether.UI
{
    /// <summary>
    /// In-game HUD overlay for GungeonTogether
    /// Shows connection status and provides quick access to multiplayer functions
    /// </summary>
    public class MultiplayerHUD : MonoBehaviour
    {
        [Header("HUD Settings")]
        public bool showHUD = true;
        public float updateInterval = 1f;
        
        [Header("HUD Position")]
        public Vector2 hudPosition = new Vector2(20, 20);
        public Vector2 hudSize = new Vector2(250, 80);
        
        private SimpleSessionManager sessionManager;
        private ISteamNetworking steamNetworking;
        private float lastUpdateTime;
        private Rect hudRect;
        private GUIStyle hudStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;
        
        // HUD state
        private string statusText = "Disconnected";
        private Color statusColor = Color.red;
        private bool isMinimized = false;
        
        void Start()
        {
            // Get session manager from the mod instance instead of FindObjectOfType
            if (GungeonTogetherMod.Instance != null)
            {
                sessionManager = GungeonTogetherMod.Instance._sessionManager;
            }
            steamNetworking = SteamNetworkingFactory.TryCreateSteamNetworking();
            
            hudRect = new Rect(hudPosition.x, hudPosition.y, hudSize.x, hudSize.y);
            
            Debug.Log("[MultiplayerHUD] In-game HUD initialized");
        }
        
        void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateHUDStatus();
                lastUpdateTime = Time.time;
            }
            
            // Toggle HUD with Ctrl+H
            if (Input.GetKeyDown(KeyCode.H) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                showHUD = !showHUD;
            }
        }
        
        void OnGUI()
        {
            if (!showHUD) return;
            
            InitializeStyles();
            DrawHUD();
        }
        
        /// <summary>
        /// Initialize GUI styles
        /// </summary>
        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            // HUD background style
            hudStyle = new GUIStyle(GUI.skin.box);
            hudStyle.normal.background = CreateSolidTexture(new Color(0.1f, 0.1f, 0.1f, 0.9f));
            hudStyle.border = new RectOffset(4, 4, 4, 4);
            hudStyle.padding = new RectOffset(8, 8, 8, 8);
            
            // Button style
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 10;
            buttonStyle.normal.background = CreateSolidTexture(new Color(0.3f, 0.3f, 0.3f, 0.8f));
            buttonStyle.hover.background = CreateSolidTexture(new Color(0.4f, 0.4f, 0.4f, 0.9f));
            buttonStyle.active.background = CreateSolidTexture(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            
            // Label style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 11;
            labelStyle.normal.textColor = Color.white;
            labelStyle.wordWrap = true;
            
            stylesInitialized = true;
        }
        
        /// <summary>
        /// Create a solid color texture
        /// </summary>
        private Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// Draw the HUD
        /// </summary>
        private void DrawHUD()
        {
            // Main HUD window
            hudRect = GUI.Window(12345, hudRect, DrawHUDWindow, "GungeonTogether", hudStyle);
        }
        
        /// <summary>
        /// Draw the HUD window content
        /// </summary>
        private void DrawHUDWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            if (!isMinimized)
            {
                // Status display
                var oldColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label($"Status: {statusText}", labelStyle);
                GUI.color = oldColor;
                
                // Steam ID if available
                if (steamNetworking != null && steamNetworking.IsAvailable())
                {
                    ulong steamId = steamNetworking.GetSteamID();
                    GUILayout.Label($"Steam: {steamId}", labelStyle);
                }
                
                // Control buttons
                GUILayout.BeginHorizontal();
                
                if (sessionManager != null)
                {
                    if (!sessionManager.IsActive)
                    {
                        if (GUILayout.Button("Host", buttonStyle, GUILayout.Width(60)))
                        {
                            GungeonTogetherMod.Instance?.StartHosting();
                        }
                        
                        if (GUILayout.Button("Join", buttonStyle, GUILayout.Width(60)))
                        {
                            GungeonTogetherMod.Instance?.TryJoinHost();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Stop", buttonStyle, GUILayout.Width(60)))
                        {
                            GungeonTogetherMod.Instance?.StopMultiplayer();
                        }
                        
                        if (GUILayout.Button("Menu", buttonStyle, GUILayout.Width(60)))
                        {
                            MultiplayerUIManager.ShowUI();
                        }
                    }
                }
                
                GUILayout.EndHorizontal();
                
                // Quick actions
                GUILayout.BeginHorizontal();
                
                if (GUILayout.Button("UI", buttonStyle, GUILayout.Width(40)))
                {
                    MultiplayerUIManager.ToggleUI();
                }
                
                if (GUILayout.Button("?", buttonStyle, GUILayout.Width(25)))
                {
                    ShowHelp();
                }
                
                if (GUILayout.Button("_", buttonStyle, GUILayout.Width(25)))
                {
                    isMinimized = true;
                    hudRect.height = 30;
                }
                
                GUILayout.EndHorizontal();
            }
            else
            {
                // Minimized view
                GUILayout.BeginHorizontal();
                GUILayout.Label($"GT: {statusText}", labelStyle);
                
                if (GUILayout.Button("^", buttonStyle, GUILayout.Width(25)))
                {
                    isMinimized = false;
                    hudRect.height = hudSize.y;
                }
                
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
            
            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
        
        /// <summary>
        /// Update HUD status
        /// </summary>
        private void UpdateHUDStatus()
        {
            if (sessionManager != null)
            {
                if (sessionManager.IsActive)
                {
                    if (sessionManager.IsHost)
                    {
                        statusText = "Hosting";
                        statusColor = new Color(0.2f, 0.6f, 1f, 1f); // Blue
                    }
                    else
                    {
                        statusText = "Connected";
                        statusColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
                    }
                }
                else
                {
                    statusText = "Ready";
                    statusColor = Color.white;
                }
            }
            else
            {
                statusText = "Error";
                statusColor = Color.red;
            }
        }
        
        /// <summary>
        /// Show help information
        /// </summary>
        private void ShowHelp()
        {
            MultiplayerUIManager.ShowNotification("GungeonTogether Help:\n• Ctrl+M: Main menu\n• Ctrl+H: Toggle HUD\n• F3-F10: Debug keys", 5f);
        }
        
        /// <summary>
        /// Set HUD visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            showHUD = visible;
        }
        
        /// <summary>
        /// Toggle HUD visibility
        /// </summary>
        public void ToggleVisibility()
        {
            showHUD = !showHUD;
        }
        
        /// <summary>
        /// Update session manager reference
        /// </summary>
        public void SetSessionManager(SimpleSessionManager manager)
        {
            sessionManager = manager;
        }
    }
}
