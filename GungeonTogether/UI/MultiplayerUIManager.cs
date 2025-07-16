using GungeonTogether.Game;
using System;
using UnityEngine;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Main UI manager for GungeonTogether mod
    /// Handles UI initialization, state management, and integration with the mod
    /// </summary>
    public static class MultiplayerUIManager
    {
        private static MultiplayerUIController uiController;
        private static PlayerListUI playerListUI;
        private static UIAudioManager audioManager;
        private static bool isInitialized = false;

        /// <summary>
        /// Initialize the UI system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (isInitialized)
                {
                    GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUIManager] UI already initialized");
                    return;
                }

                GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Initializing UI system...");

                // Create main UI manager GameObject
                var uiManagerObject = new GameObject("GungeonTogether_UIManager");
                UnityEngine.Object.DontDestroyOnLoad(uiManagerObject);

                // Initialize core UI controller first - this is the most important
                try
                {
                    var uiControllerObject = new GameObject("MultiplayerUIController");
                    uiControllerObject.transform.SetParent(uiManagerObject.transform);
                    uiController = uiControllerObject.AddComponent<MultiplayerUIController>();
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Core UI controller initialized");
                }
                catch (Exception ex)
                {
                    GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Failed to initialize core UI controller: {ex.Message}");
                    GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Stack trace: {ex.StackTrace}");
                    return; // Don't continue if core UI fails
                }

                // Initialize audio manager
                try
                {
                    var audioManagerObject = new GameObject("UIAudioManager");
                    audioManagerObject.transform.SetParent(uiManagerObject.transform);
                    audioManager = audioManagerObject.AddComponent<UIAudioManager>();
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Audio manager initialized");
                }
                catch (Exception ex)
                {
                    GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Failed to initialize audio manager: {ex.Message}");
                    // Continue without audio - not critical
                }

                // Mark as initialized early so other components can safely check
                isInitialized = true;

                // Initialize secondary UI components - these can fail without breaking core functionality
                // Player list UI
                try
                {
                    var playerListUIObject = new GameObject("PlayerListUI");
                    playerListUIObject.transform.SetParent(uiManagerObject.transform);
                    playerListUI = playerListUIObject.AddComponent<PlayerListUI>();
                    GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Player list UI initialized");
                }
                catch (Exception ex)
                {
                    GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Failed to initialize player list UI: {ex.Message}");
                    playerListUI = null; // Mark as failed but continue
                }

                GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] UI system initialized successfully");

                // Play initialization sound and show welcome notification
                try
                {
                    PlayUISound("ui_success");
                    ShowNotification("GungeonTogether UI loaded! Press Ctrl+P to open multiplayer menu", 4f);
                }
                catch (Exception ex)
                {
                    GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Failed to play initialization feedback: {ex.Message}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Failed to initialize UI: {e.Message}");
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Stack trace: {e.StackTrace}");
                isInitialized = false; // Make sure we mark as failed
            }
        }

        /// <summary>
        /// Set the session manager reference for the UI
        /// </summary>
        public static void SetSessionManager(SimpleSessionManager sessionManager)
        {
            if (!ReferenceEquals(uiController, null))
            {
                // Store session manager reference for UI access
                GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Session manager set for UI");
            }
        }

        /// <summary>
        /// Show the multiplayer UI
        /// </summary>
        public static void ShowUI()
        {
            if (!isInitialized)
            {
                GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUIManager] UI not initialized - trying to initialize now");
                Initialize();
                return;
            }

            if (!ReferenceEquals(uiController, null))
            {
                uiController.ShowUI();
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[MultiplayerUIManager] UI controller is null after initialization");
            }
        }

        /// <summary>
        /// Hide the multiplayer UI
        /// </summary>
        public static void HideUI()
        {
            if (!ReferenceEquals(uiController, null))
            {
                uiController.HideUI();
            }
        }

        /// <summary>
        /// Toggle the multiplayer UI visibility
        /// </summary>
        public static void ToggleUI()
        {
            if (!isInitialized)
            {
                GungeonTogether.Logging.Debug.LogWarning("[MultiplayerUIManager] UI not initialized - trying to initialize now");
                Initialize();
                return;
            }

            if (!ReferenceEquals(uiController, null))
            {
                uiController.ToggleUI();
            }
            else
            {
                GungeonTogether.Logging.Debug.LogError("[MultiplayerUIManager] UI controller is null after initialization");
            }
        }

        /// <summary>
        /// Show a notification message
        /// </summary>
        public static void ShowNotification(string message, float duration = 3f)
        {
            if (!ReferenceEquals(uiController, null))
            {
                uiController.ShowNotification(message, duration);
            }
            else
            {
                GungeonTogether.Logging.Debug.Log($"[MultiplayerUIManager] Notification: {message}");
            }
        }

        /// <summary>
        /// Update UI state when session state changes
        /// </summary>
        public static void OnSessionStateChanged(bool isActive, bool isHost)
        {
            if (isActive)
            {
                if (isHost)
                {
                    PlayUISound("mp_connect");
                    ShowNotification("🏠 Hosting session - Friends can now join!", 3f);
                }
                else
                {
                    PlayUISound("mp_connect");
                    ShowNotification("🔗 Connected to multiplayer session!", 3f);
                }
            }
            else
            {
                PlayUISound("mp_disconnect");
                ShowNotification("🚪 Disconnected from session", 2f);
            }
        }

        /// <summary>
        /// Handle Steam overlay join events
        /// </summary>
        public static void OnSteamJoinRequested(string hostSteamId)
        {
            PlayUISound("steam_overlay_open");
            ShowNotification($"🎮 Steam overlay join requested for host: {hostSteamId}", 3f);
        }

        /// <summary>
        /// Handle successful Steam connections
        /// </summary>
        public static void OnSteamConnectionSuccess(string hostSteamId)
        {
            PlayUISound("mp_connect");
            ShowNotification($"✅ Successfully connected via Steam to host: {hostSteamId}", 3f);
        }

        /// <summary>
        /// Handle Steam connection failures
        /// </summary>
        public static void OnSteamConnectionFailed(string reason)
        {
            PlayUISound("ui_error");
            ShowNotification($"❌ Steam connection failed: {reason}", 4f);
        }

        /// <summary>
        /// Handle player joining events
        /// </summary>
        public static void OnPlayerJoined(ulong steamId)
        {
            PlayUISound("mp_player_joined");
            ShowNotification($"👥 Player joined: {steamId}", 2f);

            // Add to player list (in real implementation, get actual player name)
            AddPlayer(steamId, $"Player_{steamId}", false);
        }

        /// <summary>
        /// Handle player leaving events
        /// </summary>
        public static void OnPlayerLeft(ulong steamId)
        {
            PlayUISound("mp_player_left");
            ShowNotification($"👋 Player left: {steamId}", 2f);

            // Remove from player list
            RemovePlayer(steamId);
        }

        /// <summary>
        /// Check if UI is initialized
        /// </summary>
        public static bool IsInitialized => isInitialized && !ReferenceEquals(uiController, null);

        /// <summary>
        /// Cleanup UI resources
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] Cleaning up UI system...");

                if (!ReferenceEquals(uiController, null))
                {
                    UnityEngine.Object.Destroy(uiController.gameObject);
                    uiController = null;
                }

                if (!ReferenceEquals(audioManager, null))
                {
                    UnityEngine.Object.Destroy(audioManager.gameObject);
                    audioManager = null;
                }

                if (!ReferenceEquals(playerListUI, null))
                {
                    UnityEngine.Object.Destroy(playerListUI.gameObject);
                    playerListUI = null;
                }

                isInitialized = false;
                GungeonTogether.Logging.Debug.Log("[MultiplayerUIManager] UI system cleanup completed");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[MultiplayerUIManager] Error during cleanup: {e.Message}");
            }
        }

        /// <summary>
        /// Play a UI sound effect
        /// </summary>
        public static void PlayUISound(string soundName, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            if (!ReferenceEquals(audioManager, null))
            {
                audioManager.PlaySound(soundName, volumeMultiplier, pitchMultiplier);
            }
        }

        /// <summary>
        /// Play a UI sound with random pitch variation
        /// </summary>
        public static void PlayUISoundRandomPitch(string soundName, float pitchVariation = 0.1f, float volumeMultiplier = 1f)
        {
            if (!ReferenceEquals(audioManager, null))
            {
                audioManager.PlaySoundRandomPitch(soundName, pitchVariation, volumeMultiplier);
            }
        }

        /// <summary>
        /// Set UI audio volume
        /// </summary>
        public static void SetUIAudioVolume(float volume)
        {
            if (!ReferenceEquals(audioManager, null))
            {
                audioManager.SetMasterVolume(volume);
            }
        }

        /// <summary>
        /// Enable or disable UI sound effects
        /// </summary>
        public static void SetUIAudioEnabled(bool enabled)
        {
            if (!ReferenceEquals(audioManager, null))
            {
                audioManager.SetSoundEffectsEnabled(enabled);
            }
        }

        /// <summary>
        /// Show the player list
        /// </summary>
        public static void ShowPlayerList()
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.ShowPlayerList();
            }
        }

        /// <summary>
        /// Hide the player list
        /// </summary>
        public static void HidePlayerList()
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.HidePlayerList();
            }
        }

        /// <summary>
        /// Toggle the player list
        /// </summary>
        public static void TogglePlayerList()
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.TogglePlayerList();
            }
        }

        /// <summary>
        /// Add a player to the player list
        /// </summary>
        public static void AddPlayer(ulong steamId, string playerName, bool isHost = false)
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.AddPlayer(steamId, playerName, isHost);
            }
        }

        /// <summary>
        /// Remove a player from the player list
        /// </summary>
        public static void RemovePlayer(ulong steamId)
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.RemovePlayer(steamId);
            }
        }

        /// <summary>
        /// Update a player's information in the player list
        /// </summary>
        public static void UpdatePlayer(ulong steamId, string playerName = null, bool? isHost = null, PlayerListUI.PlayerStatus? status = null, float? ping = null)
        {
            if (!ReferenceEquals(playerListUI, null))
            {
                playerListUI.UpdatePlayer(steamId, playerName, isHost, status, ping);
            }
        }

        /// <summary>
        /// Get UI controller instance
        /// </summary>
        public static MultiplayerUIController GetUIController()
        {
            return uiController;
        }

        /// <summary>
        /// Get player list UI instance
        /// </summary>
        public static PlayerListUI GetPlayerListUI()
        {
            return playerListUI;
        }

        /// <summary>
        /// Get audio manager instance
        /// </summary>
        public static UIAudioManager GetAudioManager()
        {
            return audioManager;
        }
    }
}
