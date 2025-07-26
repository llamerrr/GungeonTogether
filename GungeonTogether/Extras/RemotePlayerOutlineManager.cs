using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Manages outline effects for remote players using the base game's outline system
    /// This makes remote players more visible and distinguishable from NPCs
    /// </summary>
    public class RemotePlayerOutlineManager
    {
        private static RemotePlayerOutlineManager instance;
        public static RemotePlayerOutlineManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RemotePlayerOutlineManager();
                }
                return instance;
            }
        }

        // Outline configuration
        private readonly Dictionary<ulong, OutlineData> playerOutlines = new Dictionary<ulong, OutlineData>();
        
        // Outline colors for different players (cycling through distinct colors)
        private readonly Color[] outlineColors = new Color[]
        {
            new Color(0f, 1f, 0f, 1f),      // Bright Green
            new Color(0f, 0.5f, 1f, 1f),   // Bright Blue  
            new Color(1f, 0.5f, 0f, 1f),   // Orange
            new Color(1f, 0f, 1f, 1f),     // Magenta
            new Color(1f, 1f, 0f, 1f),     // Yellow
            new Color(0f, 1f, 1f, 1f),     // Cyan
            new Color(0.5f, 1f, 0f, 1f),   // Lime
            new Color(1f, 0f, 0.5f, 1f)    // Pink
        };

        // Settings
        private bool outlinesEnabled = true;
        private float outlineZOffset = 0.1f;
        private float outlineLuminanceCutoff = 0.1f;

        private struct OutlineData
        {
            public ulong SteamId;
            public Color OutlineColor;
            public bool IsActive;
            public GameObject PlayerObject;
            public SpriteRenderer TargetSprite;
            public GameObject OutlineObject;
        }

        private RemotePlayerOutlineManager()
        {
            GungeonTogether.Logging.Debug.Log("[RemoteOutlines] RemotePlayerOutlineManager initialized");
        }

        /// <summary>
        /// Enable or disable outlines for all remote players
        /// </summary>
        public void SetOutlinesEnabled(bool enabled)
        {
            outlinesEnabled = enabled;
            
            if (enabled)
            {
                // Re-apply outlines to all tracked players
                RefreshAllOutlines();
            }
            else
            {
                // Remove outlines from all players
                RemoveAllOutlines();
            }
            
            GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Outlines {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Add outline to a remote player
        /// </summary>
        public void AddOutlineToPlayer(ulong steamId, GameObject playerObject)
        {
            if (!outlinesEnabled || playerObject == null)
            {
                return;
            }

            try
            {
                // Get the SpriteRenderer component - remote players use Unity SpriteRenderer
                SpriteRenderer targetSprite = GetPlayerSpriteRenderer(playerObject);
                
                if (targetSprite == null)
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[RemoteOutlines] Could not find SpriteRenderer on player {steamId}. Available components: {string.Join(", ", playerObject.GetComponents<Component>().Select(c => c.GetType().Name).ToArray())}");
                    return;
                }

                // Remove existing outline if present
                RemoveOutlineFromPlayer(steamId);

                // Get unique color for this player
                Color outlineColor = GeneratePlayerOutlineColor(steamId);
                
                // Create outline using Unity SpriteRenderer outline effect
                GameObject outlineObject = CreateUnityOutlineForSprite(targetSprite, outlineColor);
                
                if (outlineObject != null)
                {
                    // Store outline data
                    var outlineData = new OutlineData
                    {
                        SteamId = steamId,
                        OutlineColor = outlineColor,
                        IsActive = true,
                        PlayerObject = playerObject,
                        TargetSprite = targetSprite,
                        OutlineObject = outlineObject
                    };
                    
                    playerOutlines[steamId] = outlineData;
                    
                    GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Added outline to player {steamId} with color {outlineColor}");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[RemoteOutlines] Failed to add outline to player {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Remove outline from a remote player
        /// </summary>
        public void RemoveOutlineFromPlayer(ulong steamId)
        {
            if (!playerOutlines.ContainsKey(steamId))
            {
                return;
            }

            try
            {
                var outlineData = playerOutlines[steamId];
                
                if (outlineData.OutlineObject != null)
                {
                    // Destroy the outline GameObject
                    UnityEngine.Object.DestroyImmediate(outlineData.OutlineObject);
                }
                
                playerOutlines.Remove(steamId);
                
                GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Removed outline from player {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[RemoteOutlines] Failed to remove outline from player {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Update outline when player sprite changes (e.g., character change, animation)
        /// </summary>
        public void UpdatePlayerOutline(ulong steamId, GameObject playerObject)
        {
            if (!outlinesEnabled || !playerOutlines.ContainsKey(steamId))
            {
                return;
            }

            // Re-apply outline with current sprite
            AddOutlineToPlayer(steamId, playerObject);
        }

        /// <summary>
        /// Refresh all player outlines (useful after scene transitions)
        /// </summary>
        public void RefreshAllOutlines()
        {
            if (!outlinesEnabled)
            {
                return;
            }

            GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Refreshing {playerOutlines.Count} player outlines");

            // Get current remote players from PlayerSynchroniser
            var remotePlayers = PlayerSynchroniser.Instance.GetRemotePlayerObjects();
            
            // Remove outlines for players that no longer exist
            var playersToRemove = new List<ulong>();
            foreach (var kvp in playerOutlines)
            {
                if (!remotePlayers.ContainsKey(kvp.Key) || remotePlayers[kvp.Key] == null)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var steamId in playersToRemove)
            {
                RemoveOutlineFromPlayer(steamId);
            }
            
            // Add outlines for new players
            foreach (var kvp in remotePlayers)
            {
                if (kvp.Value != null)
                {
                    AddOutlineToPlayer(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Remove all outlines
        /// </summary>
        public void RemoveAllOutlines()
        {
            var steamIds = new List<ulong>(playerOutlines.Keys);
            foreach (var steamId in steamIds)
            {
                RemoveOutlineFromPlayer(steamId);
            }
            
            GungeonTogether.Logging.Debug.Log("[RemoteOutlines] Removed all player outlines");
        }

        /// <summary>
        /// Get the SpriteRenderer component from a player object
        /// </summary>
        private SpriteRenderer GetPlayerSpriteRenderer(GameObject playerObject)
        {
            // Try to get SpriteRenderer directly
            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer;
            }
            
            // Try to find in children (sometimes sprites are nested)
            var childSpriteRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
            if (childSpriteRenderer != null && childSpriteRenderer.sprite != null)
            {
                return childSpriteRenderer;
            }
            
            return null;
        }

        /// <summary>
        /// Create an outline effect for a Unity SpriteRenderer using a custom approach
        /// </summary>
        private GameObject CreateUnityOutlineForSprite(SpriteRenderer targetSprite, Color outlineColor)
        {
            try
            {
                // First, fix the target sprite's appearance to match local player
                FixRemotePlayerAppearance(targetSprite);
                
                // Create a new GameObject for the outline
                var outlineObject = new GameObject($"Outline_{targetSprite.gameObject.name}");
                
                // Position it as a child of the target sprite directly (so it follows exactly)
                outlineObject.transform.SetParent(targetSprite.transform);
                outlineObject.transform.localPosition = Vector3.zero;
                outlineObject.transform.localRotation = Quaternion.identity;
                outlineObject.transform.localScale = Vector3.one;
                
                // Create multiple outline sprites for a nice thick outline effect
                var outlineOffsets = new Vector2[]
                {
                    new Vector2(-0.08f, 0f),    // Left
                    new Vector2(0.08f, 0f),     // Right
                    new Vector2(0f, -0.08f),    // Down
                    new Vector2(0f, 0.08f),     // Up
                    new Vector2(-0.06f, -0.06f), // Bottom-left diagonal
                    new Vector2(0.06f, -0.06f),  // Bottom-right diagonal
                    new Vector2(-0.06f, 0.06f),  // Top-left diagonal
                    new Vector2(0.06f, 0.06f)    // Top-right diagonal
                };
                
                foreach (var offset in outlineOffsets)
                {
                    var outlineSprite = new GameObject($"OutlineSprite_{offset.x}_{offset.y}");
                    outlineSprite.transform.SetParent(outlineObject.transform);
                    outlineSprite.transform.localPosition = offset;
                    outlineSprite.transform.localRotation = Quaternion.identity;
                    outlineSprite.transform.localScale = Vector3.one;
                    
                    var outlineSpriteRenderer = outlineSprite.AddComponent<SpriteRenderer>();
                    outlineSpriteRenderer.sprite = targetSprite.sprite;
                    outlineSpriteRenderer.color = outlineColor;
                    
                    // Set sorting to render behind the main sprite
                    outlineSpriteRenderer.sortingLayerName = targetSprite.sortingLayerName;
                    outlineSpriteRenderer.sortingOrder = targetSprite.sortingOrder - 1;
                    
                    // Use the same material as target sprite but with outline color
                    if (targetSprite.material != null)
                    {
                        var outlineMaterial = new Material(targetSprite.material);
                        outlineMaterial.color = outlineColor;
                        outlineSpriteRenderer.material = outlineMaterial;
                    }
                    
                    GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Created outline sprite at offset {offset} with color {outlineColor} on layer {outlineSpriteRenderer.sortingLayerName} order {outlineSpriteRenderer.sortingOrder}");
                }
                
                GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Created Unity outline with {outlineOffsets.Length} outline sprites for player sprite");
                return outlineObject;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[RemoteOutlines] Failed to create Unity outline: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fix remote player sprite appearance to match local player
        /// </summary>
        private void FixRemotePlayerAppearance(SpriteRenderer targetSprite)
        {
            try
            {
                // Get local player for comparison
                var localPlayer = GameManager.Instance?.PrimaryPlayer;
                if (localPlayer != null)
                {
                    var localSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (localSpriteRenderer != null)
                    {
                        // Match the local player's rendering properties
                        targetSprite.sortingLayerName = localSpriteRenderer.sortingLayerName;
                        targetSprite.sortingOrder = localSpriteRenderer.sortingOrder;
                        
                        // Use the same material if possible
                        if (localSpriteRenderer.material != null)
                        {
                            targetSprite.material = localSpriteRenderer.material;
                        }
                        
                        // Ensure proper color (white for normal rendering)
                        targetSprite.color = Color.white;
                        
                        GungeonTogether.Logging.Debug.Log($"[RemoteOutlines] Fixed remote player appearance - layer: {targetSprite.sortingLayerName}, order: {targetSprite.sortingOrder}");
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[RemoteOutlines] Could not fix remote player appearance: {e.Message}");
            }
        }

        /// <summary>
        /// Get a unique outline color for a player based on their Steam ID
        /// </summary>
        private Color GeneratePlayerOutlineColor(ulong steamId)
        {
            // Use Steam ID to pick a consistent color for each player
            int colorIndex = (int)(steamId % (ulong)outlineColors.Length);
            return outlineColors[colorIndex];
        }

        /// <summary>
        /// Set custom outline color for a specific player
        /// </summary>
        public void SetPlayerOutlineColor(ulong steamId, Color color)
        {
            if (playerOutlines.ContainsKey(steamId))
            {
                var outlineData = playerOutlines[steamId];
                outlineData.OutlineColor = color;
                playerOutlines[steamId] = outlineData;
                
                // Re-apply outline with new color
                if (outlineData.PlayerObject != null)
                {
                    AddOutlineToPlayer(steamId, outlineData.PlayerObject);
                }
            }
        }

        /// <summary>
        /// Get outline color for a player
        /// </summary>
        public Color GetPlayerOutlineColor(ulong steamId)
        {
            if (playerOutlines.ContainsKey(steamId))
            {
                return playerOutlines[steamId].OutlineColor;
            }
            
            return GeneratePlayerOutlineColor(steamId); // Generate default color
        }

        /// <summary>
        /// Check if a player has an active outline
        /// </summary>
        public bool HasOutline(ulong steamId)
        {
            return playerOutlines.ContainsKey(steamId) && playerOutlines[steamId].IsActive;
        }

        /// <summary>
        /// Cleanup when shutting down
        /// </summary>
        public void Shutdown()
        {
            RemoveAllOutlines();
            GungeonTogether.Logging.Debug.Log("[RemoteOutlines] RemotePlayerOutlineManager shutdown");
        }
    }
}
