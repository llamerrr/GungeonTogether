using System;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Creates a debug character that mirrors the local player's position but uses
    /// the same rendering system as networked players for offline testing
    /// </summary>
    public class DebugCharacterController : MonoBehaviour
    {
        private PlayerController localPlayer;
        private GameObject debugCharacter;
        private tk2dSprite debugSprite;
        private tk2dSpriteAnimator debugAnimator;
        private Vector3 offsetFromPlayer = new Vector3(2f, 0f, 0f);
        private bool isInitialized = false;
        
        // Track facing direction like networked players
        private bool debugPlayerFacingLeft = false;
        
        /// <summary>
        /// Initialize the debug character that mirrors local player movement
        /// </summary>
        public void Initialize(PlayerController player)
        {
            if (player == null)
            {
                GungeonTogether.Logging.Debug.LogError("[DebugChar] Cannot initialize with null player");
                return;
            }
            
            localPlayer = player;
            CreateDebugCharacter();
        }
        
        /// <summary>
        /// Create the debug character using the same system as networked players
        /// </summary>
        private void CreateDebugCharacter()
        {
            try
            {
                GungeonTogether.Logging.Debug.Log("[DebugChar] Creating debug character...");
                
                // Create the debug character GameObject
                debugCharacter = new GameObject("DebugNetworkedPlayer");
                debugCharacter.transform.SetParent(transform);
                
                // Position it near the local player
                Vector3 initialPosition = localPlayer.transform.position + offsetFromPlayer;
                debugCharacter.transform.position = initialPosition;
                
                // Copy the local player's sprite setup using tk2d system (like networked players)
                var localPlayerSprite = localPlayer.GetComponent<tk2dSprite>();
                if (localPlayerSprite != null)
                {
                    // Create tk2dSprite component (same as networked players)
                    debugSprite = debugCharacter.AddComponent<tk2dSprite>();
                    debugSprite.Collection = localPlayerSprite.Collection;
                    debugSprite.SetSprite(localPlayerSprite.spriteId);
                    
                    // Set sorting to ensure visibility
                    var meshRenderer = debugSprite.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.sortingLayerName = "FG_Critical";
                        meshRenderer.sortingOrder = 10;
                        
                        // Apply tint to distinguish from local player
                        if (meshRenderer.material != null)
                        {
                            meshRenderer.material.color = new Color(1.0f, 0.5f, 0.5f, 0.95f); // Red tint
                        }
                    }
                    
                    GungeonTogether.Logging.Debug.Log("[DebugChar] Created tk2dSprite component");
                }
                else
                {
                    // Try to find tk2dSprite on PlayerSprite child (common ETG pattern)
                    var playerSpriteChild = localPlayer.transform.Find("PlayerSprite");
                    if (playerSpriteChild != null)
                    {
                        var playerSpriteComponent = playerSpriteChild.GetComponent<tk2dSprite>();
                        if (playerSpriteComponent != null)
                        {
                            debugSprite = debugCharacter.AddComponent<tk2dSprite>();
                            debugSprite.Collection = playerSpriteComponent.Collection;
                            debugSprite.SetSprite(playerSpriteComponent.spriteId);
                            
                            var meshRenderer = debugSprite.GetComponent<MeshRenderer>();
                            if (meshRenderer != null)
                            {
                                meshRenderer.sortingLayerName = "FG_Critical";
                                meshRenderer.sortingOrder = 10;
                                
                                if (meshRenderer.material != null)
                                {
                                    meshRenderer.material.color = new Color(1.0f, 0.5f, 0.5f, 0.95f);
                                }
                            }
                            
                            GungeonTogether.Logging.Debug.Log("[DebugChar] Created tk2dSprite from PlayerSprite child");
                        }
                    }
                }
                
                // If no tk2d sprite was created, fall back to Unity sprite
                if (debugSprite == null)
                {
                    var spriteRenderer = debugCharacter.AddComponent<SpriteRenderer>();
                    var localSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
                    {
                        spriteRenderer.sprite = localSpriteRenderer.sprite;
                        spriteRenderer.sortingLayerName = "FG_Critical";
                        spriteRenderer.sortingOrder = 10;
                        spriteRenderer.color = new Color(1.0f, 0.5f, 0.5f, 0.95f); // Red tint
                    }
                    
                    GungeonTogether.Logging.Debug.Log("[DebugChar] Created Unity SpriteRenderer component as fallback");
                }
                
                // Copy animation system
                var localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                if (localAnimator != null && localAnimator.Library != null)
                {
                    debugAnimator = debugCharacter.AddComponent<tk2dSpriteAnimator>();
                    debugAnimator.Library = localAnimator.Library;
                    
                    // Start with idle animation
                    try
                    {
                        var idleClips = new string[] { "idle_south", "idle", "player_idle_south", "player_idle" };
                        foreach (var clipName in idleClips)
                        {
                            var clipId = debugAnimator.GetClipIdByName(clipName);
                            if (clipId >= 0)
                            {
                                debugAnimator.DefaultClipId = clipId;
                                debugAnimator.Play(clipName);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[DebugChar] Could not start idle animation: {ex.Message}");
                    }
                    
                    GungeonTogether.Logging.Debug.Log("[DebugChar] Created tk2dSpriteAnimator component");
                }
                
                // Add a visual marker
                var collider = debugCharacter.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true;
                
                isInitialized = true;
                GungeonTogether.Logging.Debug.Log($"[DebugChar] Debug character initialized at position: {initialPosition}");
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[DebugChar] Error creating debug character: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update the debug character to mirror local player movement and animation
        /// </summary>
        private void Update()
        {
            if (!isInitialized || localPlayer == null || debugCharacter == null)
                return;
                
            try
            {
                // Mirror facing direction FIRST (same as networked players)
                MirrorLocalPlayerFacing();
                
                // THEN set position after flip is applied
                Vector3 targetPosition = localPlayer.transform.position + offsetFromPlayer;
                SetDebugCharacterPosition(targetPosition);
                
                // Mirror animation state
                MirrorLocalPlayerAnimation();
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[DebugChar] Error in Update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set debug character position using the same method as networked players
        /// </summary>
        private void SetDebugCharacterPosition(Vector3 targetPosition)
        {
            if (debugCharacter == null) return;
            
            // Use the exact same positioning system as PlayerSynchroniser.SetSpritePositionCentered
            // to replicate potential issues for testing
            if (debugSprite != null)
            {
                Vector3 adjustedPosition = targetPosition;
                
                if (debugSprite.FlipX)
                {
                    try
                    {
                        var bounds = debugSprite.GetUntrimmedBounds();
                        float spriteWidth = bounds.size.x;
                        float flipOffset = spriteWidth * 0.5f; // Same compensation as networked players
                        adjustedPosition.x += flipOffset;
                        
                        GungeonTogether.Logging.Debug.Log($"[DebugChar][FlipFix] Applied flip compensation: spriteWidth={spriteWidth:F2}, flipOffset={flipOffset:F2}, adjusted={adjustedPosition.x:F2}");
                    }
                    catch (Exception ex)
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[DebugChar][FlipFix] Could not compensate for flip: {ex.Message}");
                    }
                }
                
                debugCharacter.transform.position = adjustedPosition;
            }
            else
            {
                debugCharacter.transform.position = targetPosition;
            }
        }
        
        /// <summary>
        /// Mirror the local player's current animation
        /// </summary>
        private void MirrorLocalPlayerAnimation()
        {
            if (debugAnimator == null || localPlayer == null) return;
            
            try
            {
                var localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                if (localAnimator != null && localAnimator.CurrentClip != null)
                {
                    string currentAnimName = localAnimator.CurrentClip.name;
                    
                    // Only change animation if it's different
                    if (debugAnimator.CurrentClip == null || debugAnimator.CurrentClip.name != currentAnimName)
                    {
                        var clipId = debugAnimator.GetClipIdByName(currentAnimName);
                        if (clipId >= 0)
                        {
                            debugAnimator.Play(currentAnimName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[DebugChar] Error mirroring animation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mirror the local player's facing direction using tk2d FlipX like networked players
        /// </summary>
        private void MirrorLocalPlayerFacing()
        {
            if (debugSprite == null || localPlayer == null) return;
            
            try
            {
                // Determine facing direction from velocity (like networked players)
                Vector2 velocity = Vector2.zero;
                if (localPlayer.specRigidbody != null)
                {
                    velocity = localPlayer.specRigidbody.Velocity;
                }
                
                // Update facing direction based on movement
                if (velocity.magnitude > 0.1f)
                {
                    bool shouldFaceLeft = velocity.x < 0;
                    debugPlayerFacingLeft = shouldFaceLeft;
                }
                
                // Apply flip using tk2d system (same as networked players)
                bool isCurrentlyFlipped = debugSprite.FlipX;
                if (isCurrentlyFlipped != debugPlayerFacingLeft)
                {
                    debugSprite.FlipX = debugPlayerFacingLeft;
                    GungeonTogether.Logging.Debug.Log($"[DebugChar] Applied flip: FlipX = {debugPlayerFacingLeft}");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[DebugChar] Error mirroring facing direction: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clean up debug character
        /// </summary>
        public void Cleanup()
        {
            if (debugCharacter != null)
            {
                DestroyImmediate(debugCharacter);
                debugCharacter = null;
            }
            
            isInitialized = false;
            GungeonTogether.Logging.Debug.Log("[DebugChar] Debug character cleaned up");
        }
        
        /// <summary>
        /// Toggle the debug character visibility
        /// </summary>
        public void ToggleVisibility()
        {
            if (debugCharacter != null)
            {
                debugCharacter.SetActive(!debugCharacter.activeSelf);
                GungeonTogether.Logging.Debug.Log($"[DebugChar] Debug character visibility: {debugCharacter.activeSelf}");
            }
        }
        
        /// <summary>
        /// Get debug info about positioning differences
        /// </summary>
        public string GetDebugInfo()
        {
            if (!isInitialized || debugCharacter == null || localPlayer == null)
                return "Debug character not initialized";
                
            Vector3 localPos = localPlayer.transform.position;
            Vector3 debugPos = debugCharacter.transform.position;
            Vector3 expectedPos = localPos + offsetFromPlayer;
            Vector3 actualOffset = debugPos - localPos;
            
            return $"Local: {localPos:F2}, Debug: {debugPos:F2}, Expected: {expectedPos:F2}, ActualOffset: {actualOffset:F2}";
        }
        
        /// <summary>
        /// Destroy the component when the GameObject is destroyed
        /// </summary>
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
