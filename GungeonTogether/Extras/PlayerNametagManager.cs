using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages nametags for remote players, displaying Steam usernames above their characters
    /// </summary>
    public class PlayerNametagManager : MonoBehaviour
    {
        private static PlayerNametagManager _instance;
        public static PlayerNametagManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Create instance if it doesn't exist
                    GameObject managerObject = new GameObject("PlayerNametagManager");
                    DontDestroyOnLoad(managerObject);
                    _instance = managerObject.AddComponent<PlayerNametagManager>();
                }
                return _instance;
            }
        }

        [Header("Nametag Settings")]
        public float nametagOffsetY = 1.7f; // How far above the player to show the nametag
        public float nametagScale = 0.8f; // Scale of the nametag text
        public Color nametagColor = Color.white; // Default nametag color
        
        public Color nametagBackgroundColor = new Color(0, 0, 0, 0.7f); // Semi-transparent black background

        // Nametag tracking
        private readonly Dictionary<ulong, GameObject> playerNametags = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, string> cachedPlayerNames = new Dictionary<ulong, string>();

        // Canvas for nametags (screen space overlay)
        private Canvas nametagCanvas;

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNametagSystem();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            CreateNametagCanvas();
        }

        private Sprite CreateRoundedRectSprite()
        {
            // Create a small texture for the rounded rectangle
            int width = 24;
            int height = 24;
            int cornerRadius = 15;
            
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            
            // Fill with transparent
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            
            // Draw rounded rectangle
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inCorner = false;
                    
                    // Check if we're in a corner region
                    if ((x < cornerRadius && y < cornerRadius) || // Top-left
                        (x >= width - cornerRadius && y < cornerRadius) || // Top-right
                        (x < cornerRadius && y >= height - cornerRadius) || // Bottom-left
                        (x >= width - cornerRadius && y >= height - cornerRadius)) // Bottom-right
                    {
                        // Calculate distance from corner center
                        int cornerCenterX, cornerCenterY;
                        
                        if (x < cornerRadius && y < cornerRadius) // Top-left
                        {
                            cornerCenterX = cornerRadius - 1;
                            cornerCenterY = cornerRadius - 1;
                        }
                        else if (x >= width - cornerRadius && y < cornerRadius) // Top-right
                        {
                            cornerCenterX = width - cornerRadius;
                            cornerCenterY = cornerRadius - 1;
                        }
                        else if (x < cornerRadius && y >= height - cornerRadius) // Bottom-left
                        {
                            cornerCenterX = cornerRadius - 1;
                            cornerCenterY = height - cornerRadius;
                        }
                        else // Bottom-right
                        {
                            cornerCenterX = width - cornerRadius;
                            cornerCenterY = height - cornerRadius;
                        }
                        
                        float distance = Mathf.Sqrt((x - cornerCenterX) * (x - cornerCenterX) + (y - cornerCenterY) * (y - cornerCenterY));
                        inCorner = distance <= cornerRadius;
                    }
                    else
                    {
                        inCorner = true; // Not in corner region, so it's part of the rectangle
                    }
                    
                    if (inCorner)
                    {
                        pixels[y * width + x] = Color.white;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            // Create sprite with 9-slice borders for proper stretching
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            return sprite;
        }


        /// <summary>
        /// Initialize the nametag system
        /// </summary>
        private void InitializeNametagSystem()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerNametags] Initializing nametag system");
        }

        /// <summary>
        /// Create the canvas for nametags
        /// </summary>
        private void CreateNametagCanvas()
        {
            if (nametagCanvas != null) return;

            // Create a screen space overlay canvas for nametags
            GameObject canvasObject = new GameObject("PlayerNametagCanvas");
            canvasObject.transform.SetParent(transform);

            nametagCanvas = canvasObject.AddComponent<Canvas>();
            nametagCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            nametagCanvas.sortingOrder = 100; // High sorting order to appear above game elements

            // Add CanvasScaler for proper scaling
            var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;

            // Add GraphicRaycaster for UI interaction (if needed)
            var graphicRaycaster = canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = true;

            GungeonTogether.Logging.Debug.Log("[PlayerNametags] Created nametag canvas (Screen Space Overlay)");
        }

        /// <summary>
        /// Add or update a nametag for a remote player
        /// </summary>
        /// <param name="steamId">Steam ID of the player</param>
        /// <param name="playerObject">The player's GameObject</param>
        public void AddNametagToPlayer(ulong steamId, GameObject playerObject)
        {
            if (playerObject == null)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerNametags] Cannot add nametag - player object is null for {steamId}");
                return;
            }

            try
            {
                // Determine if we're host or joiner for debugging
                var sessionManager = GungeonTogetherMod.Instance?.SessionManager;
                string roleInfo = "Unknown";
                if (sessionManager != null)
                {
                    roleInfo = sessionManager.IsHost ? "HOST" : "JOINER";
                }

                // Remove existing nametag if it exists
                RemoveNametagFromPlayer(steamId);

                // Get player name (try cache first, then fetch)
                string playerName = GetPlayerName(steamId);

                // Create nametag
                GameObject nametag = CreateNametag(steamId, playerName, playerObject);
                if (nametag != null)
                {
                    playerNametags[steamId] = nametag;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerNametags] Error adding nametag for player {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Remove nametag from a player
        /// </summary>
        /// <param name="steamId">Steam ID of the player</param>
        public void RemoveNametagFromPlayer(ulong steamId)
        {
            try
            {
                if (playerNametags.ContainsKey(steamId))
                {
                    var nametag = playerNametags[steamId];
                    if (nametag != null)
                    {
                        Destroy(nametag);
                    }
                    playerNametags.Remove(steamId);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerNametags] Error removing nametag for player {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Create a nametag GameObject for a player
        /// </summary>
        /// <param name="steamId">Steam ID of the player</param>
        /// <param name="playerName">Display name for the player</param>
        /// <param name="playerObject">The player's GameObject to follow</param>
        /// <returns>The created nametag GameObject</returns>
        private GameObject CreateNametag(ulong steamId, string playerName, GameObject playerObject)
        {
            try
            {
                
                // Ensure canvas exists
                if (nametagCanvas == null)
                {
                    CreateNametagCanvas();
                }

                if (nametagCanvas == null)
                {
                    return null;
                }

                // Create main nametag object
                GameObject nametagObject = new GameObject($"Nametag_{steamId}");

                // Set canvas as parent (screen space overlay)
                nametagObject.transform.SetParent(nametagCanvas.transform, false);

                // Add RectTransform for UI positioning (smaller size)
                var nametagRect = nametagObject.AddComponent<RectTransform>();
                nametagRect.sizeDelta = new Vector2(playerName.Length * 6 + 16, 24); // Smaller dimensions

                // Create background panel with rounded edges
                GameObject backgroundPanel = new GameObject("Background");
                backgroundPanel.transform.SetParent(nametagObject.transform, false);

                var backgroundImage = backgroundPanel.AddComponent<UnityEngine.UI.Image>();
                backgroundImage.color = new Color(0, 0, 0, 0.8f); // More opaque background
                
                // Create a rounded rectangle sprite for the background
                backgroundImage.sprite = CreateRoundedRectSprite();
                backgroundImage.type = UnityEngine.UI.Image.Type.Sliced;

                var backgroundRect = backgroundPanel.GetComponent<RectTransform>();
                backgroundRect.sizeDelta = new Vector2(playerName.Length * 6 + 16, 24); // Smaller dimensions
                backgroundRect.anchoredPosition = Vector2.zero;

                // Create text for player name with better visibility
                GameObject textObject = new GameObject("NameText");
                textObject.transform.SetParent(backgroundPanel.transform, false);

                var text = textObject.AddComponent<UnityEngine.UI.Text>();
                text.text = playerName;
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 12; // Smaller font
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = FontStyle.Bold; // Bold text for better visibility

                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                // Add nametag follower component to make it follow the player
                var follower = nametagObject.AddComponent<NametagFollower>();
                follower.Initialize(playerObject, nametagOffsetY);

                return nametagObject;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerNametags] Error creating nametag for {steamId}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name for a Steam user
        /// </summary>
        /// <param name="steamId">Steam ID of the user</param>
        /// <returns>Display name or fallback</returns>
        private string GetPlayerName(ulong steamId)
        {
            // Check cache first
            if (cachedPlayerNames.ContainsKey(steamId))
            {
                return cachedPlayerNames[steamId];
            }

            string playerName = "Unknown Player";

            try
            {
                // Try to get name from Steam Friends
                var allFriends = SteamFriendsHelper.GetSteamFriends();
                var friend = allFriends.FirstOrDefault(f => f.steamId == steamId);
                
                if (!string.IsNullOrEmpty(friend.name) && !friend.name.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    playerName = friend.name;
                }
                else if (!string.IsNullOrEmpty(friend.personaName) && !friend.personaName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    playerName = friend.personaName;
                }
                else
                {
                    // Fallback to a formatted Steam ID
                    playerName = $"Player {steamId.ToString().Substring(Math.Max(0, steamId.ToString().Length - 4))}";
                }

                // Cache the result
                cachedPlayerNames[steamId] = playerName;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerNametags] Error getting name for Steam ID {steamId}: {e.Message}");
                playerName = $"Player {steamId.ToString().Substring(Math.Max(0, steamId.ToString().Length - 4))}";
                cachedPlayerNames[steamId] = playerName;
            }

            return playerName;
        }

        /// <summary>
        /// Update all nametag positions (called in Update)
        /// </summary>
        void Update()
        {
            // The NametagFollower components handle individual position updates
            // This Update method can be used for any global nametag management
            
            // Clean up any null nametags
            CleanupNullNametags();
        }

        /// <summary>
        /// Clean up any nametags that have been destroyed
        /// </summary>
        private void CleanupNullNametags()
        {
            var keysToRemove = new List<ulong>();

            foreach (var kvp in playerNametags)
            {
                if (kvp.Value == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                playerNametags.Remove(key);
            }
        }

        /// <summary>
        /// Clear all nametags (useful for scene transitions)
        /// </summary>
        public void ClearAllNametags()
        {
            try
            {
                foreach (var nametag in playerNametags.Values)
                {
                    if (nametag != null)
                    {
                        Destroy(nametag);
                    }
                }
                playerNametags.Clear();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerNametags] Error clearing nametags: {e.Message}");
            }
        }

        /// <summary>
        /// Update a player's nametag with a new name (if needed)
        /// </summary>
        /// <param name="steamId">Steam ID of the player</param>
        /// <param name="newName">New name to display</param>
        public void UpdatePlayerName(ulong steamId, string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;

            try
            {
                // Update cache
                cachedPlayerNames[steamId] = newName;

                // Update nametag if it exists
                if (playerNametags.ContainsKey(steamId))
                {
                    var nametag = playerNametags[steamId];
                    if (nametag != null)
                    {
                        var textComponent = nametag.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (textComponent != null)
                        {
                            textComponent.text = newName;
                            GungeonTogether.Logging.Debug.Log($"[PlayerNametags] Updated name for {steamId} to '{newName}'");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerNametags] Error updating name for {steamId}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Component that makes a nametag follow a specific player GameObject
    /// </summary>
    public class NametagFollower : MonoBehaviour
    {
        private GameObject targetPlayer;
        private float offsetY;
        private Camera mainCamera;
        private RectTransform rectTransform;
        
        // Position smoothing to ignore sprite flip jitter
        private Vector3 smoothedPosition;
        private bool hasInitialized = false;
        private float smoothingFactor = 12f; // How fast to follow actual movement
        private float flipThreshold = 0.3f; // Ignore position changes smaller than this (sprite flips)

        /// <summary>
        /// Initialize the follower to track a specific player
        /// </summary>
        /// <param name="player">Player GameObject to follow</param>
        /// <param name="yOffset">Y offset above the player</param>
        public void Initialize(GameObject player, float yOffset)
        {
            Initialize(player, yOffset, 0f);
        }

        /// <summary>
        /// Initialize the follower to track a specific player
        /// </summary>
        /// <param name="player">Player GameObject to follow</param>
        /// <param name="yOffset">Y offset above the player</param>
        public void Initialize(GameObject player, float yOffset, float xOffset)
        {
            targetPlayer = player;
            offsetY = yOffset;
            
            // Find the main camera
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }

            // Get our RectTransform
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }
        }

        void Update()
        {
            if (targetPlayer == null)
            {
                // Player was destroyed, destroy this nametag
                Destroy(gameObject);
                return;
            }

            if (mainCamera == null || rectTransform == null)
            {
                // Try to find camera again
                mainCamera = Camera.main ?? FindObjectOfType<Camera>();
                if (mainCamera == null) return;
            }

            try
            {
                // Get current player position
                Vector3 currentPosition = targetPlayer.transform.position;
                
                // Initialize smoothed position on first frame
                if (!hasInitialized)
                {
                    smoothedPosition = currentPosition;
                    hasInitialized = true;
                }
                else
                {
                    // Calculate distance moved
                    float distanceMoved = Vector3.Distance(currentPosition, smoothedPosition);
                    
                    // Only update smoothed position if player has moved significantly (not just sprite flip)
                    if (distanceMoved > flipThreshold)
                    {
                        // Smooth towards the new position
                        smoothedPosition = Vector3.Lerp(smoothedPosition, currentPosition, Time.deltaTime * smoothingFactor);
                    }
                    // If movement is small (likely sprite flip), ignore it and keep using smoothed position
                }
                
                // Use smoothed position for nametag
                Vector3 nametagWorldPosition = new Vector3(smoothedPosition.x, smoothedPosition.y + offsetY, smoothedPosition.z);
                
                // Convert world position to screen position
                Vector3 screenPosition = mainCamera.WorldToScreenPoint(nametagWorldPosition);
                
                // Simple visibility check - just ensure player is in front of camera
                bool shouldShow = screenPosition.z > 0; // Player is in front of camera
                
                if (shouldShow)
                {
                    // Clamp screen position to visible area to prevent nametags from going too far off-screen
                    Vector3 clampedScreenPosition = new Vector3(
                        Mathf.Clamp(screenPosition.x, 0, Screen.width),
                        Mathf.Clamp(screenPosition.y, 0, Screen.height),
                        screenPosition.z
                    );
                    
                    // Convert screen position to local position in the Canvas
                    Canvas canvas = GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        // For screen space overlay, we can directly use screen coordinates
                        rectTransform.position = clampedScreenPosition;
                    }
                    else
                    {
                        // Fallback method using RectTransformUtility
                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvas.transform as RectTransform,
                            clampedScreenPosition,
                            canvas.worldCamera,
                            out localPoint);
                        
                        rectTransform.localPosition = localPoint;
                    }
                    
                    // Show the nametag
                    if (!gameObject.activeSelf)
                    {
                        gameObject.SetActive(true);
                    }
                }
                else
                {
                    // Hide the nametag only when player is behind camera
                    if (gameObject.activeSelf)
                    {
                        gameObject.SetActive(false);
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[NametagFollower] Error updating position: {e.Message}");
            }
        }
    }
}
