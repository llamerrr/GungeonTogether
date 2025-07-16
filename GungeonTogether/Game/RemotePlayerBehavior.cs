using System;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Behavior component for remote player objects to handle movement, animations, and interactions
    /// </summary>
    public class RemotePlayerBehavior : MonoBehaviour
    {
        private ulong steamId;
        private tk2dSpriteAnimator spriteAnimator;
        private SpriteRenderer spriteRenderer;
        private Vector2 lastPosition;
        private Vector2 currentVelocity;
        private bool isMoving = false;
        private bool isDodgeRolling = false;
        private float lastAnimationTime = 0f;
        private const float ANIMATION_UPDATE_INTERVAL = 0.1f; // Update animations every 100ms

        // Animation state tracking
        private string currentAnimationName = "";
        private bool animationDirty = false;

        public void Initialize(ulong playerSteamId)
        {
            steamId = playerSteamId;
            spriteAnimator = GetComponent<tk2dSpriteAnimator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            lastPosition = transform.position;

            GungeonTogether.Logging.Debug.Log($"[RemotePlayerBehavior] Initialized for player {steamId}");
        }

        void Update()
        {
            try
            {
                UpdateMovementState();
                UpdateAnimations();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[RemotePlayerBehavior] Error in Update for player {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Updates the remote player's position and state from network data
        /// </summary>
        public void UpdateFromNetworkData(Vector2 position, Vector2 velocity, float rotation, bool isGrounded, bool isDodgeRolling)
        {
            // Smooth position interpolation
            transform.position = Vector2.Lerp(transform.position, position, Time.deltaTime * 10f);

            // Update velocity for animation purposes
            currentVelocity = velocity;
            this.isDodgeRolling = isDodgeRolling;

            // Update sprite direction based on movement
            if (velocity.magnitude > 0.1f)
            {
                // Flip sprite based on movement direction
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = velocity.x < 0;
                }
            }
        }

        private void UpdateMovementState()
        {
            Vector2 currentPosition = transform.position;
            Vector2 deltaPosition = currentPosition - lastPosition;

            // Determine if player is moving
            bool wasMoving = isMoving;
            isMoving = deltaPosition.magnitude > 0.01f || currentVelocity.magnitude > 0.1f;

            // Mark animation as dirty if movement state changed
            if (wasMoving != isMoving)
            {
                animationDirty = true;
            }

            lastPosition = currentPosition;
        }

        private void UpdateAnimations()
        {
            if (spriteAnimator == null || Time.time - lastAnimationTime < ANIMATION_UPDATE_INTERVAL)
                return;

            lastAnimationTime = Time.time;

            try
            {
                string targetAnimation = GetTargetAnimationName();

                if (animationDirty || !string.Equals(currentAnimationName, targetAnimation))
                {
                    PlayAnimation(targetAnimation);
                    currentAnimationName = targetAnimation;
                    animationDirty = false;
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[RemotePlayerBehavior] Animation error for player {steamId}: {e.Message}");
            }
        }

        private string GetTargetAnimationName()
        {
            // Priority order: dodge rolling > moving > idle
            if (isDodgeRolling)
            {
                return GetBestMatchingAnimation(new string[] { "dodge_roll", "roll", "player_dodge", "dodge" });
            }
            else if (isMoving)
            {
                // Choose run/walk animation based on movement direction
                Vector2 movement = currentVelocity;
                if (Mathf.Abs(movement.y) > Mathf.Abs(movement.x))
                {
                    // Vertical movement
                    if (movement.y > 0)
                        return GetBestMatchingAnimation(new string[] { "run_north", "walk_north", "player_run_north", "player_walk_north", "run_up", "walk_up" });
                    else
                        return GetBestMatchingAnimation(new string[] { "run_south", "walk_south", "player_run_south", "player_walk_south", "run_down", "walk_down" });
                }
                else
                {
                    // Horizontal movement - use east/west animations
                    return GetBestMatchingAnimation(new string[] { "run_east", "walk_east", "player_run_east", "player_walk_east", "run_side", "walk_side", "run", "walk" });
                }
            }
            else
            {
                // Idle animation
                return GetBestMatchingAnimation(new string[] { "idle_south", "idle", "player_idle_south", "player_idle" });
            }
        }

        private string GetBestMatchingAnimation(string[] preferredNames)
        {
            if (spriteAnimator == null || spriteAnimator.Library == null)
                return "";

            // Try each preferred name in order
            foreach (string name in preferredNames)
            {
                try
                {
                    if (spriteAnimator.GetClipByName(name) != null)
                    {
                        return name;
                    }
                }
                catch
                {
                    // Ignore errors and try next name
                }
            }

            // Fallback to currently playing animation if nothing matches
            return currentAnimationName;
        }

        private void PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName) || spriteAnimator == null)
                return;

            try
            {
                if (spriteAnimator.GetClipByName(animationName) != null)
                {
                    spriteAnimator.Play(animationName);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[RemotePlayerBehavior] Failed to play animation '{animationName}' for player {steamId}: {e.Message}");
            }
        }

        void OnDestroy()
        {
            GungeonTogether.Logging.Debug.Log($"[RemotePlayerBehavior] Destroyed for player {steamId}");
        }
    }
}
