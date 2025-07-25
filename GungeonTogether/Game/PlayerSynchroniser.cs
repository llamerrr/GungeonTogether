using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GungeonTogether.Game
{

    /// <summary>
    /// Handles synchronization of player states across multiplayer sessions
    /// </summary>
    public class PlayerSynchroniser
    {
        private static PlayerSynchroniser instance;
        public static PlayerSynchroniser Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PlayerSynchroniser();
                }
                return instance;
            }
        }

        // Debug mode settings
        public static bool DebugModeSimpleSquares = false;
        public static bool DebugModeShowCharacterLabels = true; // Always show character labels for debugging

        // Thread safety
        private readonly object collectionLock = new object();

        // Remote player tracking
        private readonly Dictionary<ulong, RemotePlayerState> remotePlayers = new Dictionary<ulong, RemotePlayerState>();
        private readonly Dictionary<ulong, GameObject> remotePlayerObjects = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, bool> remotePlayerFacingLeft = new Dictionary<ulong, bool>(); // Track facing direction

        // Local player tracking
        private PlayerController localPlayer;
        private bool isInitialized = false;
        private bool isDelayedInitPending = false;
        private Vector2 lastSentPosition;
        private float lastSentRotation;
        private bool lastSentGrounded;
        private bool lastSentDodgeRolling;
        private const float POSITION_THRESHOLD = 0.1f;
        private const float ROTATION_THRESHOLD = 5f;
        private float lastPositionSentTime = 0f;
        private const float HEARTBEAT_INTERVAL = 1f; // Send heartbeat every 1 second
        private const float TIMEOUT_MULTIPLIER = 30f; // Match NetworkManager - 30 seconds timeout

        // Character change detection
        private int lastBroadcastCharacterId = -999; // Initialize to invalid value to force first broadcast
        private const float CHARACTER_CHANGE_CHECK_INTERVAL = 1.0f; // Check every second
        private float lastCharacterChangeCheck = 0f;

        // Efficient character data tracking
        private int lastSentCharacterId = -999; // Track last character ID sent with position updates
        private string lastSentCharacterName = ""; // Track last character name sent with position updates

        // Scheduled broadcast (replacement for coroutine)
        private bool needsScheduledBroadcast = false;
        private float scheduledBroadcastTime = 0f;

        // Logging spam reduction
        private float lastLogTime = 0f;
        private const float LOG_THROTTLE_INTERVAL = 5f; // Only log every 5 seconds for routine updates

        // Add a field to track the current map/scene for both local and remote players
        private string localMapName;

        // Debug counters for networking
        public static int LastUpdateSentFrame = -1;
        public static int LastUpdateReceivedFrame = -1;
        public static float LastUpdateSentTime = -1f;
        public static float LastUpdateReceivedTime = -1f;

        public struct RemotePlayerState
        {
            public ulong SteamId;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public bool IsGrounded;
            public bool IsDodgeRolling;
            public float LastUpdateTime;
            public Vector2 TargetPosition;
            public float InterpolationSpeed;
            public string MapName; // Track the map/scene name
            public int CharacterId; // Character selection (maps to PlayableCharacters enum)
            public string CharacterName; // Character prefab name for sprite loading
            
            // Animation state data
            public Steam.PlayerAnimationState AnimationState;
            public Vector2 MovementDirection; // Normalized movement direction for directional animations
            public bool IsRunning; // Running vs walking
            public bool IsFalling; // Falling state
            public bool IsTakingDamage; // Taking damage animation
            public bool IsDead; // Death state
            public string CurrentAnimationName; // Current animation clip name for debugging
        }

        private PlayerSynchroniser()
        {
            NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.Instance.OnPlayerLeft += OnPlayerLeft;
        }

        /// <summary>
        /// Initialize the player synchronizer
        /// </summary>
        public void Initialize()
        {
            try
            {
                var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
                var isHost = NetworkManager.Instance?.IsHost() ?? false;
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][INIT] Called for SteamId={localSteamId}, IsHost={isHost}");
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Starting PlayerSynchroniser initialization...");

                // Try immediate initialization
                if (TryInitializePlayer())
                {
                    isInitialized = true;
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] Successfully initialized immediately");
                }
                else
                {
                    // Set up delayed initialization
                    isDelayedInitPending = true;
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] Player not ready, will retry during Update()");
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Initialize error: " + e.Message);
            }
        }

        /// <summary>
        /// Try to load character-specific sprite and animation data
        /// </summary>
        private bool TryLoadCharacterSprite(SpriteRenderer spriteRenderer, out tk2dSpriteAnimator animator, string characterName, int characterId)
        {
            animator = null;

            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] === ATTEMPTING CHARACTER SPRITE LOAD ===");
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Character: {characterName} (ID: {characterId})");

                // First, try to copy from local player if they have the same character
                if (localPlayer != null && TryLoadFromLocalPlayer(spriteRenderer, out animator, characterName, characterId))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully loaded from local player");
                    return true;
                }
                
                // Try to find actual PlayerController prefabs in the game scene
                if (TryLoadFromGamePlayerPrefabs(spriteRenderer, out animator, characterName, characterId))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully loaded from game player prefabs");
                    return true;
                }

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] All character sprite loading methods failed for {characterName}");
                return false;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Exception while loading character sprite: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to find character sprites from existing PlayerController objects in the scene or loaded prefabs
        /// </summary>
        private bool TryLoadFromGamePlayerPrefabs(SpriteRenderer spriteRenderer, out tk2dSpriteAnimator animator, string characterName, int characterId)
        {
            animator = null;
            
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Searching for PlayerController objects in scene for {characterName}");
                
                // Find all PlayerController objects in the scene
                var allPlayerControllers = UnityEngine.Object.FindObjectsOfType<PlayerController>();
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Found {allPlayerControllers.Length} PlayerController objects in scene");
                
                foreach (var controller in allPlayerControllers)
                {
                    if (controller != null && controller.gameObject != null && controller != localPlayer)
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Checking PlayerController: {controller.name}");
                        
                        // Check if this PlayerController matches the character we're looking for
                        var playerSprite = controller.GetComponent<SpriteRenderer>();
                        if (playerSprite != null && playerSprite.sprite != null)
                        {
                            var spriteName = playerSprite.sprite.name.ToLower();
                            var characterNameLower = characterName.ToLower();
                            var characterDisplayName = GetCharacterDisplayName(characterId).ToLower();
                            
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] PlayerController sprite: '{spriteName}'");
                            
                            // Check if this sprite belongs to our target character
                            if (spriteName.Contains(characterNameLower.Replace("player", "")) || 
                                spriteName.Contains(characterDisplayName) ||
                                DoesSpriteBelongToCharacter(spriteName, characterId))
                            {
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Found matching PlayerController sprite for {characterName}");
                                
                                // Copy the sprite
                                spriteRenderer.sprite = playerSprite.sprite;
                                spriteRenderer.sortingLayerName = playerSprite.sortingLayerName;
                                spriteRenderer.sortingOrder = playerSprite.sortingOrder;
                                spriteRenderer.color = new Color(1.0f, 0.9f, 1.0f, 0.95f); // Light purple tint
                                
                                // Copy the animator if available
                                var playerAnimator = controller.GetComponent<tk2dSpriteAnimator>();
                                if (playerAnimator != null && playerAnimator.Library != null)
                                {
                                    animator = spriteRenderer.gameObject.AddComponent<tk2dSpriteAnimator>();
                                    animator.Library = playerAnimator.Library;
                                    TryStartIdleAnimation(animator);
                                    
                                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Copied animator from PlayerController");
                                }
                                
                                return true;
                            }
                        }
                    }
                }
                
                // If no matching PlayerControllers found, try loading character prefabs directly
                return TryLoadFromCharacterPrefabAssembly(spriteRenderer, out animator, characterName, characterId);
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error searching for PlayerController objects: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Scan loaded character prefabs in the assembly to find sprite locations
        /// </summary>
        private bool TryLoadFromCharacterPrefabAssembly(SpriteRenderer spriteRenderer, out tk2dSpriteAnimator animator, string characterName, int characterId)
        {
            animator = null;
            
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Scanning assembly for character prefab structure: {characterName}");
                
                // Try to find character prefabs using BraveResources
                var prefabNames = new string[]
                {
                    characterName,
                    characterName.Replace("Player", ""),
                    GetCharacterDisplayName(characterId)
                };
                
                foreach (var prefabName in prefabNames)
                {
                    if (string.IsNullOrEmpty(prefabName)) continue;
                    
                    var characterPrefab = BraveResources.Load<GameObject>(prefabName, ".prefab");
                    if (characterPrefab == null)
                    {
                        characterPrefab = BraveResources.Load<GameObject>(prefabName);
                    }
                    
                    if (characterPrefab != null)
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Analyzing prefab structure for: {prefabName}");
                        
                        if (AnalyzePrefabStructureAndExtractSprite(characterPrefab, spriteRenderer, out animator, prefabName))
                        {
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error scanning character prefab assembly: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deeply analyze prefab structure to find sprites and understand the hierarchy
        /// </summary>
        private bool AnalyzePrefabStructureAndExtractSprite(GameObject prefab, SpriteRenderer spriteRenderer, out tk2dSpriteAnimator animator, string prefabName)
        {
            animator = null;
            
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] === DEEP PREFAB ANALYSIS: {prefabName} ===");
                
                // Log the complete hierarchy
                LogGameObjectHierarchy(prefab, 0, "");
                
                // Get all components in the entire prefab hierarchy
                var allComponents = prefab.GetComponentsInChildren<Component>(true);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Total components in prefab hierarchy: {allComponents.Length}");
                
                // Log component types and their game objects
                var componentGroups = allComponents.GroupBy(c => c.GetType().Name).ToArray();
                foreach (var group in componentGroups)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Component type '{group.Key}': {group.Count()} instances");
                    foreach (var component in group.Take(3)) // Log first 3 instances
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync]   - {component.GetType().Name} on '{component.gameObject.name}'");
                    }
                }
                
                // Look for sprite-related components
                var spriteRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                var tk2dSprites = prefab.GetComponentsInChildren<tk2dSprite>(true);
                var tk2dAnimators = prefab.GetComponentsInChildren<tk2dSpriteAnimator>(true);
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Found components: {spriteRenderers.Length} SpriteRenderer, {tk2dSprites.Length} tk2dSprite, {tk2dAnimators.Length} tk2dSpriteAnimator");
                
                // PRIORITY: Look for PlayerSprite child object specifically (this is where the main character sprite is!)
                var playerSpriteChild = prefab.transform.Find("PlayerSprite");
                if (playerSpriteChild != null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Found PlayerSprite child object!");
                    
                    var playerTk2dSprite = playerSpriteChild.GetComponent<tk2dSprite>();
                    var playerAnimator = playerSpriteChild.GetComponent<tk2dSpriteAnimator>();
                    
                    if (playerTk2dSprite != null)
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] PlayerSprite has tk2dSprite component");
                        
                        // Store the gameObject reference before potentially destroying the spriteRenderer
                        var targetGameObject = spriteRenderer.gameObject;
                        
                        if (TryCreateSpriteFromTk2dSprite(playerTk2dSprite, spriteRenderer))
                        {
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully created sprite from PlayerSprite tk2dSprite");
                            
                            // Try to copy animation from PlayerSprite
                            if (playerAnimator != null && playerAnimator.Library != null)
                            {
                                try
                                {
                                    animator = targetGameObject.AddComponent<tk2dSpriteAnimator>();
                                    animator.Library = playerAnimator.Library;
                                    
                                    // Initialize the animator properly
                                    if (animator.Library.clips != null && animator.Library.clips.Length > 0)
                                    {
                                        // Try to find an idle animation
                                        var idleClip = Array.Find(animator.Library.clips, clip => 
                                            clip.name.ToLower().Contains("idle"));
                                        
                                        if (idleClip != null)
                                        {
                                            animator.DefaultClipId = animator.GetClipIdByName(idleClip.name);
                                            animator.Play(idleClip.name);
                                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Started idle animation: {idleClip.name}");
                                        }
                                        else
                                        {
                                            // Use first available clip
                                            var firstClip = animator.Library.clips[0];
                                            animator.DefaultClipId = animator.GetClipIdByName(firstClip.name);
                                            animator.Play(firstClip.name);
                                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Started first animation: {firstClip.name}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error setting up PlayerSprite animator: {ex.Message}");
                                }
                            }
                            
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error analyzing prefab structure: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Log the complete GameObject hierarchy
        /// </summary>
        private void LogGameObjectHierarchy(GameObject obj, int depth, string prefix)
        {
            if (obj == null) return;
            
            var indent = new string(' ', depth * 2);
            var components = obj.GetComponents<Component>();
            var componentNames = string.Join(", ", components.Select(c => c.GetType().Name).ToArray());
            
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] {indent}{prefix}{obj.name} [{componentNames}]");
            
            if (depth < 3) // Limit depth to avoid spam
            {
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    var child = obj.transform.GetChild(i);
                    if (child != null)
                    {
                        LogGameObjectHierarchy(child.gameObject, depth + 1, "└─ ");
                    }
                }
            }
        }

        /// <summary>
        /// Get the full path to a GameObject in the hierarchy
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return "null";
            
            var path = obj.name;
            var parent = obj.transform.parent;
            
            while (parent != null && parent.parent != null) // Stop before root
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }







        /// <summary>
        /// Try to copy tk2dSprite component directly instead of converting to Unity Sprite
        /// </summary>
        private bool TryCreateSpriteFromTk2dSprite(tk2dSprite sourceTk2dSprite, SpriteRenderer spriteRenderer)
        {
            try
            {
                // Instead of converting to Unity Sprite, let's add a tk2dSprite component to the remote player
                var remotePlayerObject = spriteRenderer.gameObject;
                
                // IMPORTANT: Remove the SpriteRenderer first, as it conflicts with tk2dSprite (both are rendering components)
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Removing SpriteRenderer component to avoid conflict with tk2dSprite");
                UnityEngine.Object.DestroyImmediate(spriteRenderer);
                
                // Remove any existing tk2dSprite component
                var existingTk2dSprite = remotePlayerObject.GetComponent<tk2dSprite>();
                if (existingTk2dSprite != null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Removing existing tk2dSprite component");
                    UnityEngine.Object.DestroyImmediate(existingTk2dSprite);
                }
                
                // Add tk2dSprite component
                var newTk2dSprite = remotePlayerObject.AddComponent<tk2dSprite>();
                if (newTk2dSprite == null)
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Failed to add tk2dSprite component");
                    return false;
                }
                
                // Copy properties from source tk2dSprite
                newTk2dSprite.Collection = sourceTk2dSprite.Collection;
                newTk2dSprite.SetSprite(sourceTk2dSprite.spriteId);
                
                // Copy transform properties
                newTk2dSprite.transform.localScale = sourceTk2dSprite.transform.localScale;
                
                // Set sorting
                var meshRenderer = newTk2dSprite.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingLayerName = "FG_Critical";
                    meshRenderer.sortingOrder = 10;
                    
                    // Apply tint
                    if (meshRenderer.material != null)
                    {
                        meshRenderer.material.color = new Color(1.0f, 0.9f, 1.0f, 0.95f);
                    }
                }
                
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully copied tk2dSprite component (spriteId: {sourceTk2dSprite.spriteId})");
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error copying tk2dSprite component: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to copy sprite data from local player if possible
        /// </summary>
        private bool TryLoadFromLocalPlayer(SpriteRenderer spriteRenderer, out tk2dSpriteAnimator animator, string characterName, int characterId)
        {
            animator = null;
            
            try
            {
                // Get local player's current character
                var localCharacterInfo = GetCurrentPlayerCharacter();
                
                // Check if remote player has same character as local player
                bool sameCharacter = (localCharacterInfo.CharacterId == characterId) || 
                                   (localCharacterInfo.CharacterName == characterName);
                
                if (sameCharacter)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player has same character as local player, copying sprite data");
                    
                    var localSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
                    {
                        spriteRenderer.sprite = localSpriteRenderer.sprite;
                        spriteRenderer.sortingLayerName = localSpriteRenderer.sortingLayerName;
                        spriteRenderer.sortingOrder = localSpriteRenderer.sortingOrder;
                        spriteRenderer.color = new Color(0.8f, 1.0f, 0.8f, 0.95f); // Green tint for same character
                        
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Copied same character sprite from local player: {spriteRenderer.sprite.name}");
                        
                        // Copy animation data
                        var localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                        if (localAnimator != null && localAnimator.Library != null)
                        {
                            animator = spriteRenderer.gameObject.AddComponent<tk2dSpriteAnimator>();
                            animator.Library = localAnimator.Library;
                            TryStartIdleAnimation(animator);
                        }
                        
                        return true;
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player has different character (local: {localCharacterInfo.CharacterName}/{localCharacterInfo.CharacterId}, remote: {characterName}/{characterId})");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error checking local player character: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Check if a sprite name belongs to a specific character ID
        /// </summary>
        private bool DoesSpriteBelongToCharacter(string spriteName, int characterId)
        {
            var characterKeywords = GetCharacterSpriteKeywords(characterId);
            var spriteNameLower = spriteName.ToLower();

            foreach (var keyword in characterKeywords)
            {
                if (spriteNameLower.Contains(keyword.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get keywords that should appear in sprite names for each character
        /// </summary>
        private string[] GetCharacterSpriteKeywords(int characterId)
        {
            switch (characterId)
            {
                case 0: return new string[] { "rogue", "pilot", "player_rogue", "playerrogue" };
                case 1: return new string[] { "convict", "player_convict", "playerconvict" };
                case 2: return new string[] { "robot", "player_robot", "playerrobot" };
                case 3: return new string[] { "ninja", "hunter", "player_ninja", "playerninja" };
                case 4: return new string[] { "cosmonaut", "player_cosmonaut", "playercosmonaut" };
                case 5: return new string[] { "marine", "soldier", "player_marine", "playermarine" };
                case 6: return new string[] { "guide", "player_guide", "playerguide" };
                case 7: return new string[] { "cultist", "coop", "player_coop", "playercopcultist" };
                case 8: return new string[] { "bullet", "player_bullet", "playerbullet" };
                case 9: return new string[] { "eevee", "paradox", "player_eevee", "playereevee" };
                case 10: return new string[] { "gunslinger", "player_gunslinger", "playergunslinger" };
                default: return new string[] { "rogue", "pilot" };
            }
        }





        /// <summary>
        /// Check if a sprite is likely a valid player character sprite (not UI, particle effects, etc.)
        /// </summary>
        private bool IsValidPlayerSprite(string spriteName, string gameObjectName)
        {
            // Convert to lowercase for comparison
            spriteName = spriteName.ToLower();
            gameObjectName = gameObjectName.ToLower();

            // Reject obvious non-player sprites
            var rejectKeywords = new string[]
            {
                "particle", "effect", "ui", "hud", "menu", "button", "icon", "cursor",
                "bullet", "projectile", "explosion", "spark", "flash", "muzzle",
                "debris", "smoke", "fire", "flame", "glow", "light", "shadow",
                "pickup", "item", "chest", "coin", "key", "heart", "armor",
                "wall", "floor", "ceiling", "door", "portal", "teleporter",
                "enemy", "boss", "turret", "trap", "hazard", "pit",
                "background", "bg", "overlay", "border", "frame", "outline"
            };

            foreach (var keyword in rejectKeywords)
            {
                if (spriteName.Contains(keyword) || gameObjectName.Contains(keyword))
                {
                    return false;
                }
            }

            // Accept sprites that contain player-related keywords
            var acceptKeywords = new string[]
            {
                "player", "character", "rogue", "pilot", "convict", "robot", "ninja",
                "hunter", "cosmonaut", "marine", "soldier", "guide", "cultist",
                "bullet_kin", "eevee", "paradox", "gunslinger", "idle", "walk", "run"
            };

            foreach (var keyword in acceptKeywords)
            {
                if (spriteName.Contains(keyword) || gameObjectName.Contains(keyword))
                {
                    return true;
                }
            }

            // If no specific keywords match, accept sprites from GameObjects that contain "player" in the name
            if (gameObjectName.Contains("player") || gameObjectName.Contains("character"))
            {
                return true;
            }

            // Default to reject if we're not sure
            return false;
        }

        /// <summary>
        /// Try to start idle animation on an animator
        /// </summary>
        private void TryStartIdleAnimation(tk2dSpriteAnimator animator)
        {
            try
            {
                var idleClips = new string[] { "idle_south", "idle", "player_idle_south", "player_idle", "south_idle", "default", "idle_down" };
                foreach (var clipName in idleClips)
                {
                    if (animator.GetClipByName(clipName) != null)
                    {
                        animator.Play(clipName);
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Started character animation: {clipName}");
                        return;
                    }
                }

                // If no specific idle animation found, try the first animation in the library
                if (animator.Library != null && animator.Library.clips != null && animator.Library.clips.Length > 0)
                {
                    var firstClip = animator.Library.clips[0];
                    if (firstClip != null && !string.IsNullOrEmpty(firstClip.name))
                    {
                        animator.Play(firstClip.name);
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Started first available animation: {firstClip.name}");
                        return;
                    }
                }

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] No suitable animation found to play");
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Could not start idle animation: {ex.Message}");
            }
        }

        /// <summary>
        /// Get display name for character (used for alternative prefab searching)
        /// </summary>
        private string GetCharacterDisplayName(int characterId)
        {
            switch (characterId)
            {
                case 0: return "Rogue"; // Pilot
                case 1: return "Convict";
                case 2: return "Robot";
                case 3: return "Ninja"; // Hunter
                case 4: return "Cosmonaut";
                case 5: return "Marine"; // Soldier
                case 6: return "Guide";
                case 7: return "CoopCultist";
                case 8: return "Bullet";
                case 9: return "Eevee"; // Paradox
                case 10: return "Gunslinger";
                default: return "Rogue";
            }
        }

        /// <summary>
        /// Get character prefab name from PlayableCharacters ID
        /// </summary>
        private string GetCharacterNameFromId(int characterId)
        {
            switch (characterId)
            {
                case -1: return "NoCharacterSelected"; // Special case for no selection
                case 0: return "PlayerRogue";      // Pilot
                case 1: return "PlayerConvict";   // Convict
                case 2: return "PlayerRobot";     // Robot
                case 3: return "PlayerNinja";     // Ninja (Hunter)
                case 4: return "PlayerCosmonaut"; // Cosmonaut (Pilot alt)
                case 5: return "PlayerMarine";    // Marine (Soldier)
                case 6: return "PlayerGuide";     // Guide
                case 7: return "PlayerCoopCultist"; // Coop Cultist
                case 8: return "PlayerBullet";    // Bullet
                case 9: return "PlayerEevee";     // Eevee (Paradox)
                case 10: return "PlayerGunslinger"; // Gunslinger
                default: return "PlayerRogue";    // Default to Pilot
            }
        }

        /// <summary>
        /// Helper class to store character information
        /// </summary>
        public class CharacterInfo
        {
            public int CharacterId { get; set; }
            public string CharacterName { get; set; }

            public CharacterInfo(int id, string name)
            {
                CharacterId = id;
                CharacterName = name;
            }
        }

        /// <summary>
        /// Get the current player's character information, with proper handling for no character selected
        /// </summary>
        public CharacterInfo GetCurrentPlayerCharacter()
        {
            try
            {
                /*
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] === GetCurrentPlayerCharacter DEBUG ===");
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] GameManager.Instance: {GameManager.Instance != null}");
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] GameManager.Options: {GameManager.Options != null}");
                */
                // Try to get character info from the actual player object first
                if (localPlayer != null)
                {
                    //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Local player object name: '{localPlayer.name}'");

                    // Try to get character identity from the player object name or components
                    var playerName = localPlayer.name;
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        // Map player object names to character info
                        var characterFromPlayerName = GetCharacterFromPlayerObjectName(playerName);
                        if (characterFromPlayerName != null)
                        {
                            //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Got character from player object name '{playerName}': {characterFromPlayerName.CharacterName} (ID: {characterFromPlayerName.CharacterId})");
                            return characterFromPlayerName;
                        }
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Local player is null");
                }

                // Get character information from GameManager options
                if (GameManager.Options != null)
                {
                    var lastPlayedCharacter = GameManager.Options.LastPlayedCharacter;

                    // Add debug logging to track the issue
                    //GungeonTogether.Logging.Debug.Log($"[PlayerSync] GameManager.Options.LastPlayedCharacter = {lastPlayedCharacter} ({(int)lastPlayedCharacter})");

                    // Debug: Try to access other GameManager options to see what's available
                    try
                    {
                        //GungeonTogether.Logging.Debug.Log($"[PlayerSync] GameManager has PrimaryPlayer: {GameManager.Instance.PrimaryPlayer != null}");
                        if (GameManager.Instance.PrimaryPlayer != null)
                        {
                            var playerController = GameManager.Instance.PrimaryPlayer;
                            //GungeonTogether.Logging.Debug.Log($"[PlayerSync] PrimaryPlayer name: '{playerController.name}'");

                            // Try to get character info from PlayerController
                            var playerStats = playerController.stats;
                            if (playerStats != null)
                            {
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player stats available, character ID might be in stats");
                            }

                            // Try to detect character from the player controller's sprite or animation
                            var playerSprite = playerController.GetComponent<SpriteRenderer>();
                            if (playerSprite != null && playerSprite.sprite != null)
                            {
                                //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player sprite name: '{playerSprite.sprite.name}'");

                                // Try to map sprite name to character
                                var characterFromSprite = GetCharacterFromSpriteName(playerSprite.sprite.name);
                                if (characterFromSprite != null)
                                {
                                    //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Detected character from sprite: {characterFromSprite.CharacterName} (ID: {characterFromSprite.CharacterId})");
                                    return characterFromSprite;
                                }
                            }

                            // Try to detect from animator
                            var playerAnimator = playerController.GetComponent<tk2dSpriteAnimator>();
                            if (playerAnimator != null && playerAnimator.Library != null)
                            {
                                //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player animator library name: '{playerAnimator.Library.name}'");

                                // Try to map library name to character
                                var characterFromLibrary = GetCharacterFromAnimationLibrary(playerAnimator.Library.name);
                                if (characterFromLibrary != null)
                                {
                                    //GungeonTogether.Logging.Debug.Log($"[PlayerSync] Detected character from animation library: {characterFromLibrary.CharacterName} (ID: {characterFromLibrary.CharacterId})");
                                    return characterFromLibrary;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Error accessing GameManager data: {ex.Message}");
                    }

                    // Check if no character has been selected (default/invalid value)
                    // PlayableCharacters enum typically starts at 0, so negative values indicate no selection
                    if ((int)lastPlayedCharacter < 0)
                    {
                        return new CharacterInfo(-1, "NoCharacterSelected"); // Special value indicating no selection
                    }

                    var characterName = CharacterSelectController.GetCharacterPathFromIdentity(lastPlayedCharacter);

                    // If characterName is null or empty, use the ID to get the name
                    if (string.IsNullOrEmpty(characterName))
                    {
                        characterName = GetCharacterNameFromId((int)lastPlayedCharacter);
                      
                    }

                    var result = new CharacterInfo((int)lastPlayedCharacter, characterName);
                    return result;
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] GameManager.Options is null");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Failed to get character info: {ex.Message}");
            }

            // Check if we're in a state where no character selection is expected (main menu, etc.)
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (currentScene.ToLowerInvariant().Contains("menu") && !currentScene.ToLowerInvariant().Contains("foyer"))
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] In main menu, returning no character selected");
                return new CharacterInfo(-1, "NoCharacterSelected");
            }

            // Fallback to Pilot (default character) only if we're in a context where a character is expected
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Using final fallback character: PlayerRogue (ID: 0)");
            return new CharacterInfo(0, "PlayerRogue"); // PlayableCharacters.Pilot = 0, PlayerRogue is the Pilot prefab
        }

        /// <summary>
        /// Creates a simple colored sprite as a fallback when character loading fails
        /// </summary>
        private void CreateSimpleFallbackSprite(SpriteRenderer spriteRenderer, string characterName)
        {
            try
            {
                // Create a simple 16x16 pixel texture with a character-specific color
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);

                // Choose color based on character name
                Color characterColor = GetCharacterColor(characterName);

                // Fill the texture with the character color
                var pixels = new Color[16 * 16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = characterColor;
                }
                texture.SetPixels(pixels);
                texture.Apply();

                // Create sprite from texture
                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingLayerName = "FG_Critical";
                spriteRenderer.sortingOrder = 10;
                spriteRenderer.color = Color.white;

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Created simple fallback sprite for {characterName} with color {characterColor}");
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create simple fallback sprite: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a character-specific color for fallback sprites
        /// </summary>
        private Color GetCharacterColor(string characterName)
        {
            switch (characterName?.ToLower())
            {
                case "playerrogue": return new Color(0.3f, 0.5f, 0.8f, 1.0f); // Blue
                case "playerconvict": return new Color(0.8f, 0.4f, 0.2f, 1.0f); // Orange
                case "playerrobot": return new Color(0.7f, 0.7f, 0.7f, 1.0f); // Silver
                case "playerninja": return new Color(0.2f, 0.7f, 0.3f, 1.0f); // Green
                case "playercosmonaut": return new Color(0.9f, 0.9f, 0.9f, 1.0f); // White
                case "playermarine": return new Color(0.4f, 0.6f, 0.2f, 1.0f); // Olive
                case "playerguide": return new Color(0.7f, 0.3f, 0.7f, 1.0f); // Purple
                case "playercopcultist": return new Color(0.5f, 0.2f, 0.2f, 1.0f); // Dark Red
                case "playerbullet": return new Color(0.9f, 0.8f, 0.3f, 1.0f); // Yellow
                case "playereevee": return new Color(0.8f, 0.5f, 0.9f, 1.0f); // Pink
                case "playergunslinger": return new Color(0.6f, 0.4f, 0.2f, 1.0f); // Brown
                default: return new Color(0.5f, 0.5f, 0.5f, 1.0f); // Gray
            }
        }

        /// <summary>
        /// Try to determine character from sprite name
        /// </summary>
        private CharacterInfo GetCharacterFromSpriteName(string spriteName)
        {
            try
            {
                if (string.IsNullOrEmpty(spriteName)) return null;

                var spriteNameLower = spriteName.ToLower();

                if (spriteNameLower.Contains("rogue") || spriteNameLower.Contains("pilot"))
                    return new CharacterInfo(0, "PlayerRogue");
                if (spriteNameLower.Contains("convict"))
                    return new CharacterInfo(1, "PlayerConvict");
                if (spriteNameLower.Contains("robot"))
                    return new CharacterInfo(2, "PlayerRobot");
                if (spriteNameLower.Contains("ninja") || spriteNameLower.Contains("hunter"))
                    return new CharacterInfo(3, "PlayerNinja");
                if (spriteNameLower.Contains("cosmonaut"))
                    return new CharacterInfo(4, "PlayerCosmonaut");
                if (spriteNameLower.Contains("marine") || spriteNameLower.Contains("soldier"))
                    return new CharacterInfo(5, "PlayerMarine");
                if (spriteNameLower.Contains("guide"))
                    return new CharacterInfo(6, "PlayerGuide");
                if (spriteNameLower.Contains("cultist"))
                    return new CharacterInfo(7, "PlayerCoopCultist");
                if (spriteNameLower.Contains("bullet"))
                    return new CharacterInfo(8, "PlayerBullet");
                if (spriteNameLower.Contains("eevee") || spriteNameLower.Contains("paradox"))
                    return new CharacterInfo(9, "PlayerEevee");
                if (spriteNameLower.Contains("gunslinger"))
                    return new CharacterInfo(10, "PlayerGunslinger");

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Could not determine character from sprite name: '{spriteName}'");
                return null;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error parsing sprite name '{spriteName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to determine character from animation library name
        /// </summary>
        private CharacterInfo GetCharacterFromAnimationLibrary(string libraryName)
        {
            try
            {
                if (string.IsNullOrEmpty(libraryName)) return null;

                var libraryNameLower = libraryName.ToLower();

                if (libraryNameLower.Contains("rogue") || libraryNameLower.Contains("pilot"))
                    return new CharacterInfo(0, "PlayerRogue");
                if (libraryNameLower.Contains("convict"))
                    return new CharacterInfo(1, "PlayerConvict");
                if (libraryNameLower.Contains("robot"))
                    return new CharacterInfo(2, "PlayerRobot");
                if (libraryNameLower.Contains("ninja") || libraryNameLower.Contains("hunter"))
                    return new CharacterInfo(3, "PlayerNinja");
                if (libraryNameLower.Contains("cosmonaut"))
                    return new CharacterInfo(4, "PlayerCosmonaut");
                if (libraryNameLower.Contains("marine") || libraryNameLower.Contains("soldier"))
                    return new CharacterInfo(5, "PlayerMarine");
                if (libraryNameLower.Contains("guide"))
                    return new CharacterInfo(6, "PlayerGuide");
                if (libraryNameLower.Contains("cultist"))
                    return new CharacterInfo(7, "PlayerCoopCultist");
                if (libraryNameLower.Contains("bullet"))
                    return new CharacterInfo(8, "PlayerBullet");
                if (libraryNameLower.Contains("eevee") || libraryNameLower.Contains("paradox"))
                    return new CharacterInfo(9, "PlayerEevee");
                if (libraryNameLower.Contains("gunslinger"))
                    return new CharacterInfo(10, "PlayerGunslinger");

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Could not determine character from library name: '{libraryName}'");
                return null;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error parsing library name '{libraryName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to determine character from player object name
        /// </summary>
        private CharacterInfo GetCharacterFromPlayerObjectName(string playerName)
        {
            try
            {
                // Common player object name patterns (case insensitive check)
                var playerNameLower = playerName.ToLower();

                if (playerNameLower.Contains("rogue") || playerNameLower.Contains("pilot"))
                    return new CharacterInfo(0, "PlayerRogue");
                if (playerNameLower.Contains("convict"))
                    return new CharacterInfo(1, "PlayerConvict");
                if (playerNameLower.Contains("robot"))
                    return new CharacterInfo(2, "PlayerRobot");
                if (playerNameLower.Contains("ninja") || playerNameLower.Contains("hunter"))
                    return new CharacterInfo(3, "PlayerNinja");
                if (playerNameLower.Contains("cosmonaut"))
                    return new CharacterInfo(4, "PlayerCosmonaut");
                if (playerNameLower.Contains("marine") || playerNameLower.Contains("soldier"))
                    return new CharacterInfo(5, "PlayerMarine");
                if (playerNameLower.Contains("guide"))
                    return new CharacterInfo(6, "PlayerGuide");
                if (playerNameLower.Contains("cultist"))
                    return new CharacterInfo(7, "PlayerCoopCultist");
                if (playerNameLower.Contains("bullet"))
                    return new CharacterInfo(8, "PlayerBullet");
                if (playerNameLower.Contains("eevee") || playerNameLower.Contains("paradox"))
                    return new CharacterInfo(9, "PlayerEevee");
                if (playerNameLower.Contains("gunslinger"))
                    return new CharacterInfo(10, "PlayerGunslinger");

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Could not determine character from player name: '{playerName}'");
                return null;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error parsing player object name '{playerName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to initialize the local player
        /// </summary>
        private bool TryInitializePlayer()
        {
            localPlayer = GameManager.Instance?.PrimaryPlayer;
            localMapName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (localPlayer != null)
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Local player found and initialized. Player name: {localPlayer.name}, Position: {localPlayer.transform.position}, Map: {localMapName}");
                HookPlayerEvents();
                return true;
            }
            else
            {
                if (GameManager.Instance == null)
                {
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] GameManager.Instance is null - waiting...");
                }
                return false;
            }
        }

        /// <summary>
        /// Update method to be called from main thread
        /// </summary>
        public void Update()
        {
            var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
            var isHost = NetworkManager.Instance?.IsHost() ?? false;
            try
            {
                // Handle delayed initialization
                if (isDelayedInitPending && !isInitialized)
                {
                    if (TryInitializePlayer())
                    {
                        isInitialized = true;
                        isDelayedInitPending = false;
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Successfully completed delayed initialization");
                    }
                }

                // Try to re-initialize local player if it's still null
                if (localPlayer == null)
                {
                    TryReinitializeLocalPlayer();
                }

                // Check for character selection changes
                if (Time.time - lastCharacterChangeCheck > CHARACTER_CHANGE_CHECK_INTERVAL)
                {
                    CheckForCharacterSelectionChanges();
                    lastCharacterChangeCheck = Time.time;
                }

                // Periodic character info broadcast (much less frequent than position updates)
                // Only send every 10 seconds and only if we have remote players
                if (NetworkManager.Instance != null && Time.time - lastPositionSentTime > 10.0f && remotePlayers.Count > 0)
                {
                    SendCharacterInfo();
                }

                // Handle scheduled broadcast (replacement for coroutine)
                if (needsScheduledBroadcast && Time.time >= scheduledBroadcastTime)
                {
                    try
                    {
                        SendCharacterInfo();
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Broadcasted character info after scheduled delay");
                        needsScheduledBroadcast = false;
                    }
                    catch (Exception e)
                    {
                        GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to broadcast scheduled character info: {e.Message}");
                        needsScheduledBroadcast = false; // Reset to prevent infinite retries
                    }
                }

                if (isInitialized)
                {
                    UpdateLocalPlayer();
                    UpdateRemotePlayers();
                }
            }
            catch (System.InvalidOperationException ex) when (ex.Message.Contains("out of sync") || ex.Message.Contains("Collection was modified"))
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Collection modification detected (out of sync): {ex.Message}");
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] This typically happens when collections are modified during iteration from another thread");
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] The synchronization locks should prevent this - investigating further...");

                // Log collection state for debugging
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Current collection state - Remote players: {remotePlayers?.Count ?? -1}, Remote objects: {remotePlayerObjects?.Count ?? -1}");

                // Skip this frame and continue - the collections will be stable next frame
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Skipping this update frame due to collection modification");
            }
            catch (System.InvalidOperationException ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Other InvalidOperationException: {ex.Message}");
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Stack trace: {ex.StackTrace}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Update error: {e.Message}");
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Update error type: {e.GetType().Name}");
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Send character information even when not in a run
        /// </summary>
        private void SendCharacterInfo()
        {
            try
            {
                var characterInfo = GetCurrentPlayerCharacter();
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                // Send character info as a position update with current position or foyer position
                Vector2 currentPosition = Vector2.zero;
                if (localPlayer != null)
                {
                    currentPosition = localPlayer.transform.position;
                }
                NetworkManager.Instance.OpeningPlayerPacket(
                    currentPosition,
                    Vector2.zero, // No velocity when just broadcasting character info
                    0f, // No rotation
                    true, // Assume grounded
                    false, // Not dodge rolling
                    currentScene,
                    characterInfo.CharacterId,
                    characterInfo.CharacterName
                );

                lastPositionSentTime = Time.time;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error sending character info: {e.Message}");
            }
        }

        /// <summary>
        /// Manually trigger character info broadcast (called when character selection changes)
        /// </summary>
        public void BroadcastCharacterSelectionChange()
        {
            try
            {
                SendCharacterInfo();

                // Show notification to user
                var characterInfo = GetCurrentPlayerCharacter();
                if (characterInfo.CharacterId != -1 && characterInfo.CharacterName != "NoCharacterSelected")
                {
                    UI.MultiplayerUIManager.ShowNotification($"Selected {characterInfo.CharacterName} - other players can now see you!", 3f);
                }
                else
                {
                    UI.MultiplayerUIManager.ShowNotification("Character deselected - you'll appear as a placeholder to other players", 3f);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error broadcasting character selection change: {e.Message}");
            }
        }

        /// <summary>
        /// Check if the player's character selection has changed and broadcast if needed
        /// </summary>
        private void CheckForCharacterSelectionChanges()
        {
            try
            {
                var characterInfo = GetCurrentPlayerCharacter();

                // Check if character ID has changed since last broadcast
                if (characterInfo.CharacterId != lastBroadcastCharacterId)
                {
                    lastBroadcastCharacterId = characterInfo.CharacterId;

                    // Immediately broadcast the change
                    BroadcastCharacterSelectionChange();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error checking for character selection changes: {e.Message}");
            }
        }

        /// <summary>
        /// Try to re-initialize local player if it becomes available
        /// </summary>
        private void TryReinitializeLocalPlayer()
        {
            try
            {
                if (GameManager.Instance?.PrimaryPlayer != null)
                {
                    localPlayer = GameManager.Instance.PrimaryPlayer;
                    localMapName = SceneManager.GetActiveScene().name;
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] RE-INITIALIZED local player! Player name: {localPlayer.name}, Position: {localPlayer.transform.position}, Map: {localMapName}");
                    HookPlayerEvents();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] TryReinitializeLocalPlayer error: " + e.Message);
            }
        }

        #region Local Player

        private void HookPlayerEvents()
        {
            if (localPlayer == null) return;

            try
            {
                // Hook shooting events
                if (localPlayer.CurrentGun != null)
                {
                    // Monitor gun firing - we'll check this in Update instead of hooking directly
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to hook player events: " + e.Message);
            }
        }

        /// <summary>
        /// Get the local player's current animation name directly from their animator - simple 1:1 system
        /// </summary>
        private Steam.PlayerAnimationState GetLocalPlayerAnimationState(out Vector2 movementDirection, out bool isRunning, out bool isFalling, out bool isTakingDamage, out bool isDead, out string currentAnimationName)
        {
            movementDirection = Vector2.zero;
            isRunning = false;
            isFalling = false;
            isTakingDamage = false;
            isDead = false;
            currentAnimationName = "";

            if (localPlayer == null)
            {
                return Steam.PlayerAnimationState.Idle;
            }

            try
            {
                // Get the animation name directly from the local player's animator
                // Try to find the PLAYER CHARACTER animator (not item/effect animators)
                tk2dSpriteAnimator localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                
                // If not found on the main object, check for player sprite animator specifically
                if (localAnimator == null)
                {
                    // Look for the player's sprite renderer first, then get its animator
                    var playerSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (playerSpriteRenderer != null)
                    {
                        localAnimator = playerSpriteRenderer.GetComponent<tk2dSpriteAnimator>();
                    }
                    
                    // If still not found, try to find it on the primary sprite object
                    if (localAnimator == null)
                    {
                        // Look for objects with "sprite" in the name (common ETG pattern)
                        foreach (Transform child in localPlayer.transform)
                        {
                            if (child.name.ToLower().Contains("sprite") || child.name.ToLower().Contains("player"))
                            {
                                localAnimator = child.GetComponent<tk2dSpriteAnimator>();
                                if (localAnimator != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    
                    // Last resort: use GetComponentInChildren but verify it has player animations
                    if (localAnimator == null)
                    {
                        var animators = localPlayer.GetComponentsInChildren<tk2dSpriteAnimator>();
                        foreach (var anim in animators)
                        {
                            if (anim.Library != null && anim.Library.clips != null)
                            {
                                // Check if this library contains player animations (look for common player animation names)
                                bool hasPlayerAnims = false;
                                for (int i = 0; i < anim.Library.clips.Length; i++)
                                {
                                    if (anim.Library.clips[i] != null)
                                    {
                                        string clipName = anim.Library.clips[i].name.ToLower();
                                        if (clipName.Contains("idle") || clipName.Contains("run") || clipName.Contains("walk") || 
                                            clipName.Contains("dodge") || clipName.Contains("pitfall"))
                                        {
                                            hasPlayerAnims = true;
                                            break;
                                        }
                                    }
                                }
                                
                                if (hasPlayerAnims)
                                {
                                    localAnimator = anim;
                
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (localAnimator != null)
                {
                    // Try multiple ways to get the current animation name from tk2d
                    if (localAnimator.CurrentClip != null)
                    {
                        currentAnimationName = localAnimator.CurrentClip.name;
                    }
                    else
                    {
                        // CurrentClip is null - try alternative methods to get the real current animation
                        try
                        {
                            // Try to get the playing clip name through different methods
                            bool foundCurrentAnimation = false;
                            
                            // Method 1: Try to get the current clip through the sprite animator's state
                            try
                            {
                                // Check if the animator is actually playing an animation
                                var sprite = localAnimator.GetComponent<tk2dSprite>();
                                if (sprite != null)
                                {
                                    // Try to access animation properties through reflection if necessary
                                    var animatorType = localAnimator.GetType();
                                    
                                    // Look for properties that might give us the current clip name
                                    var currentClipProperty = animatorType.GetProperty("CurrentClip");
                                    var currentClipField = animatorType.GetField("currentClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    var clipTimeProperty = animatorType.GetProperty("ClipTime");
                                    
                                    // Try to get the actual playing clip through different means
                                    if (currentClipField != null)
                                    {
                                        var clipValue = currentClipField.GetValue(localAnimator);
                                        if (clipValue != null)
                                        {
                                            // Try to get the name property of this clip
                                            var clipType = clipValue.GetType();
                                            var nameProperty = clipType.GetProperty("name");
                                            if (nameProperty != null)
                                            {
                                                var clipName = nameProperty.GetValue(clipValue, null) as string;
                                                if (!string.IsNullOrEmpty(clipName))
                                                {
                                                    currentAnimationName = clipName;
                                                    foundCurrentAnimation = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception reflectionEx)
                            {
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync][LocalAnimDebug] Reflection approach failed: {reflectionEx.Message}");
                            }
                            
                            // If reflection didn't work, fall back to intelligent state-based detection
                            if (!foundCurrentAnimation)
                            {
                                // Log available clips for debugging
                                if (localAnimator.Library != null && localAnimator.Library.clips != null)
                                {
                                   
                                    // Get all clip names for debugging
                                    var allClipNames = new List<string>();
                                    for (int i = 0; i < localAnimator.Library.clips.Length; i++)
                                    {
                                        if (localAnimator.Library.clips[i] != null)
                                        {
                                            allClipNames.Add(localAnimator.Library.clips[i].name);
                                        }
                                    }
                                    
                                    
                                    // Try to determine current animation by checking player state against available clips
                                    if (localPlayer.IsDodgeRolling)
                                    {
                                        // Look for dodge animations
                                        var dodgeClips = allClipNames.Where(name => name.ToLower().Contains("dodge")).ToArray();
                                        if (dodgeClips.Length > 0)
                                        {
                                            currentAnimationName = dodgeClips[0]; // Use first dodge animation found
                                        }
                                        else
                                        {
                                            currentAnimationName = "dodge"; // Fallback
                                        }
                                    }
                                    else if (!localPlayer.IsGrounded)
                                    {
                                        // Look for falling/pitfall animations
                                        var fallClips = allClipNames.Where(name => name.ToLower().Contains("pitfall") || name.ToLower().Contains("fall")).ToArray();
                                        if (fallClips.Length > 0)
                                        {
                                            currentAnimationName = fallClips[0];
                                        }
                                        else
                                        {
                                            currentAnimationName = "pitfall"; // Fallback
                                        }
                                    }
                                    else
                                    {
                                        var playerVelocity = localPlayer.specRigidbody?.Velocity ?? Vector2.zero;
                                        var playerSpeed = playerVelocity.magnitude;
                                        
                                        if (playerSpeed > 0.01f)
                                        {
                                            // Look for movement animations that match current direction
                                            string directionPattern = "";
                                            if (Math.Abs(playerVelocity.x) > Math.Abs(playerVelocity.y))
                                            {
                                                directionPattern = playerVelocity.x > 0 ? "right" : "left";
                                            }
                                            else
                                            {
                                                directionPattern = playerVelocity.y > 0 ? "up" : "down";
                                            }
                                            
                                            // Look for run animations with this direction - try multiple naming patterns
                                            var directionClips = allClipNames.Where(name => 
                                                name.ToLower().Contains("run") && (
                                                    name.ToLower().Contains(directionPattern) ||
                                                    name.ToLower().Contains($"_{directionPattern}") ||
                                                    name.ToLower().Contains($"{directionPattern}_") ||
                                                    name.ToLower().EndsWith(directionPattern)
                                                )).ToArray();
                                            
                                            if (directionClips.Length > 0)
                                            {
                                                currentAnimationName = directionClips[0];
                                            }
                                            else
                                            {
                                                // Try broader search for any run animation
                                                var runClips = allClipNames.Where(name => name.ToLower().Contains("run")).ToArray();
                                                if (runClips.Length > 0)
                                                {
                                                    currentAnimationName = runClips[0];
                                                    
                                                }
                                                else
                                                {
                                                    // Log all available animations to see what we actually have
                                
                                                    currentAnimationName = "idle"; // Use idle instead of constructed name that doesn't exist
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Look for idle animations
                                            var idleClips = allClipNames.Where(name => name.ToLower().Contains("idle")).ToArray();
                                            if (idleClips.Length > 0)
                                            {
                                                currentAnimationName = idleClips[0];
                                               
                                            }
                                            else
                                            {
                                                currentAnimationName = "idle"; // Fallback
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    currentAnimationName = "idle";
                                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][LocalAnimDebug] No animation library available, using fallback");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            currentAnimationName = "idle";
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync][LocalAnimDebug] Error searching for animations: {ex.Message}, using fallback");
                        }
                    }
                }
                else
                {
                    currentAnimationName = "idle"; // Fallback
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][LocalAnimDebug] No animator found, using fallback: '{currentAnimationName}'");
                }

                // Still detect basic movement for network efficiency (but don't use for animation)
                var velocity2D = localPlayer.specRigidbody?.Velocity ?? Vector2.zero;
                var speed = velocity2D.magnitude;
                
                if (speed > 0.01f)
                {
                    movementDirection = velocity2D.normalized;
                    isRunning = speed > 1.5f;
                }

                // Detect basic states for network data
                if (localPlayer.healthHaver != null && localPlayer.healthHaver.IsDead)
                {
                    isDead = true;
                    return Steam.PlayerAnimationState.Dead;
                }

                if (localPlayer.IsDodgeRolling)
                {
                    return Steam.PlayerAnimationState.DodgeRolling;
                }

                if (!localPlayer.IsGrounded && velocity2D.y < -1f)
                {
                    isFalling = true;
                    return Steam.PlayerAnimationState.Falling;
                }

                if (speed > 0.01f)
                {
                    return isRunning ? Steam.PlayerAnimationState.Running : Steam.PlayerAnimationState.Walking;
                }

                // Check actions for state classification (but animation name comes directly from animator)
                if (localPlayer.CurrentGun != null && (localPlayer.CurrentGun.IsFiring || localPlayer.CurrentGun.IsCharging))
                {
                    return Steam.PlayerAnimationState.Shooting;
                }

                if (localPlayer.CurrentGun != null && localPlayer.CurrentGun.IsReloading)
                {
                    return Steam.PlayerAnimationState.Reloading;
                }

                return Steam.PlayerAnimationState.Idle;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error getting local player animation: {ex.Message}");
                currentAnimationName = "idle";
                return Steam.PlayerAnimationState.Idle;
            }
        }

        private void UpdateLocalPlayer()
        {
            if (localPlayer == null)
            {
                return;
            }
            try
            {
                var currentPosition = localPlayer.transform.position;
                var currentRotation = localPlayer.transform.eulerAngles.z;
                var currentGrounded = localPlayer.IsGrounded;
                var currentDodgeRolling = localPlayer.IsDodgeRolling;
                localMapName = SceneManager.GetActiveScene().name;
                bool shouldSendUpdate = false;
                // Send more frequently for responsiveness - reduce threshold for animation changes
                if (Vector2.Distance(currentPosition, lastSentPosition) > POSITION_THRESHOLD ||
                    Mathf.Abs(currentRotation - lastSentRotation) > ROTATION_THRESHOLD ||
                    currentGrounded != lastSentGrounded ||
                    currentDodgeRolling != lastSentDodgeRolling ||
                    (Time.time - lastPositionSentTime > 0.5f)) // Send updates more frequently (every 0.5s instead of 2s)
                {
                    shouldSendUpdate = true;
                }
                if (shouldSendUpdate)
                {
                    // Analyze current animation state
                    Vector2 movementDirection;
                    bool isRunning, isFalling, isTakingDamage, isDead;
                    string currentAnimationName;
                    var animationState = GetLocalPlayerAnimationState(out movementDirection, out isRunning, out isFalling, out isTakingDamage, out isDead, out currentAnimationName);


                    // Check if character data has changed since last position update
                    var characterInfo = GetCurrentPlayerCharacter();
                    bool characterDataChanged = (characterInfo.CharacterId != lastSentCharacterId) ||
                                              (characterInfo.CharacterName != lastSentCharacterName);

                    if (characterDataChanged)
                    {
                        NetworkManager.Instance.OpeningPlayerPacket(
                            currentPosition,
                            localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                            currentRotation,
                            currentGrounded,
                            currentDodgeRolling,
                            localMapName,
                            characterInfo.CharacterId,
                            characterInfo.CharacterName,
                            animationState,
                            movementDirection,
                            isRunning,
                            isFalling,
                            isTakingDamage,
                            isDead,
                            currentAnimationName
                        );

                        // Update last sent character data
                        lastSentCharacterId = characterInfo.CharacterId;
                        lastSentCharacterName = characterInfo.CharacterName;
                    }
                    else
                    {
                        NetworkManager.Instance.RegularPlayerPacket(
                            currentPosition,
                            localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                            currentRotation,
                            currentGrounded,
                            currentDodgeRolling,
                            localMapName,
                            animationState,
                            movementDirection,
                            isRunning,
                            isFalling,
                            isTakingDamage,
                            isDead,
                            currentAnimationName
                        );
                    }

                    lastSentPosition = currentPosition;
                    lastSentRotation = currentRotation;
                    lastSentGrounded = currentGrounded;
                    lastSentDodgeRolling = currentDodgeRolling;
                    lastPositionSentTime = Time.time;
                    // Track last update sent
                    LastUpdateSentFrame = Time.frameCount;
                    LastUpdateSentTime = Time.time;
                }
                CheckLocalPlayerShooting();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Local player update error: " + e.Message);
            }
        }

        private void CheckLocalPlayerShooting()
        {
            if (localPlayer?.CurrentGun == null) return;

            try
            {
                // Check if player is currently shooting
                bool isShooting = localPlayer.CurrentGun.IsFiring;
                bool isCharging = localPlayer.CurrentGun.IsCharging;

                if (isShooting || isCharging)
                {
                    var gunAngle = localPlayer.CurrentGun.CurrentAngle;
                    var shootDirection = new Vector2(Mathf.Cos(gunAngle * Mathf.Deg2Rad), Mathf.Sin(gunAngle * Mathf.Deg2Rad));

                    NetworkManager.Instance.SendPlayerShooting(
                        localPlayer.transform.position,
                        shootDirection,
                        localPlayer.CurrentGun.PickupObjectId,
                        isCharging,
                        0f // TODO: Find correct charge time property
                    );
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting check error: " + e.Message);
            }
        }

        #endregion

        #region Remote Players

        private ulong GetLocalSteamId()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.LocalSteamId : 0UL;
        }

        private void UpdateRemotePlayers()
        {
            var currentTime = Time.time;
            var playersToRemove = new List<ulong>();
            ulong localSteamId = GetLocalSteamId();

            // Create a snapshot to avoid collection modification during iteration
            Dictionary<ulong, RemotePlayerState> remotePlayersSnapshot;
            Dictionary<ulong, GameObject> remotePlayerObjectsSnapshot;

            try
            {
                lock (collectionLock)
                {
                    remotePlayersSnapshot = new Dictionary<ulong, RemotePlayerState>(remotePlayers);
                    remotePlayerObjectsSnapshot = new Dictionary<ulong, GameObject>(remotePlayerObjects);
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create snapshots: {ex.Message}");
                return; // Skip this update frame
            }

            foreach (var kvp in remotePlayersSnapshot)
            {
                var steamId = kvp.Key;
                var playerState = kvp.Value;

                // SKIP local player (host) in timeout check
                if (steamId.Equals(localSteamId))
                    continue;

                bool isDebugFakePlayer = steamId >= 12345678UL && steamId <= 99999999UL; // Range of fake Steam IDs used in debug

                // Keep debug fake players alive by refreshing their update time
                if (isDebugFakePlayer)
                {
                    // Update the original collection safely
                    lock (collectionLock)
                    {
                        if (remotePlayers.ContainsKey(steamId))
                        {
                            var updatedState = remotePlayers[steamId];
                            updatedState.LastUpdateTime = currentTime;
                            remotePlayers[steamId] = updatedState;
                        }
                    }
                }

                // Check for timeout (but skip timeout for debug mode fake players)
                float timeoutDuration = HEARTBEAT_INTERVAL * TIMEOUT_MULTIPLIER;

                if (!isDebugFakePlayer && currentTime - playerState.LastUpdateTime > timeoutDuration)
                {
                    playersToRemove.Add(steamId);
                    continue;
                }

                // Interpolate position and update behavior
                if (remotePlayerObjectsSnapshot.ContainsKey(steamId))
                {
                    var playerObject = remotePlayerObjectsSnapshot[steamId];
                    if (playerObject != null)
                    {
                        // Handle position interpolation directly (consolidated from RemotePlayerBehavior)
                        var currentPos = playerObject.transform.position;
                        var targetPos = playerState.TargetPosition;
                        var newPos = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * playerState.InterpolationSpeed);
                        playerObject.transform.position = newPos;
                        playerObject.transform.rotation = Quaternion.Euler(0, 0, playerState.Rotation);
                    }
                }
            }

            // Remove timed out players (never remove local host)
            foreach (var steamId in playersToRemove)
            {
                RemoveRemotePlayer(steamId);
            }
        }

        private void OnPlayerJoined(ulong steamId)
        {
            var localSteamId = GetLocalSteamId();
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnPlayerJoined called - Remote SteamId: {steamId}, Local SteamId: {localSteamId}");
            if (steamId == localSteamId)
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync][DEBUG] Skipping OnPlayerJoined for local player");
                return;
            }
            CreateRemotePlayer(steamId, localMapName);

            // Schedule a character info broadcast with delay to ensure connection is established
            // This is more efficient than immediate multiple broadcasts
            try
            {
                ScheduleCharacterInfoBroadcast();
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to schedule character broadcast for new player {steamId}: {e.Message}");
            }
        }

        private void OnPlayerLeft(ulong steamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player left event: {steamId}");
            RemoveRemotePlayer(steamId);
        }

        private void CreateRemotePlayer(ulong steamId, string mapName = "Unknown")
        {
            ulong localSteamId = GetLocalSteamId();
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] === CREATING REMOTE PLAYER ===");
            if (steamId.Equals(localSteamId))
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Skipping CreateRemotePlayer for local player.");
                return;
            }

            try
            {
                lock (collectionLock)
                {
                    // DEBUG: Log all connected players for troubleshooting
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Current remote objects: {string.Join(", ", remotePlayerObjects.Keys.Select(k => k.ToString()).ToArray())}");

                    if (remotePlayerObjects.ContainsKey(steamId))
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player object already exists for {steamId}");
                        return;
                    }

                    // Create a placeholder remote player - we'll update the character when we receive position data
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Creating placeholder remote player object for {steamId}...");
                    var remotePlayerObj = CreatePlaceholderRemotePlayer(steamId, mapName);

                    if (remotePlayerObj != null)
                    {
                        remotePlayerObjects[steamId] = remotePlayerObj;
                        remotePlayers[steamId] = new RemotePlayerState
                        {
                            SteamId = steamId,
                            Position = Vector2.zero,
                            Velocity = Vector2.zero,
                            Rotation = 0f,
                            IsGrounded = true,
                            IsDodgeRolling = false,
                            LastUpdateTime = Time.time,
                            TargetPosition = Vector2.zero,
                            InterpolationSpeed = 10f,
                            MapName = localMapName ?? mapName, // Use current local map name for debug players
                            CharacterId = -1, // Unknown character - will be updated when we receive position data
                            CharacterName = "Unknown" // Unknown character - will be updated when we receive position data
                        };
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create remote player object for {steamId} - CreateRemotePlayerLikeObject returned null");
                    }
                }

            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to create remote player " + steamId + ": " + e.Message);
            }
        }

        private void RemoveRemotePlayer(ulong steamId)
        {
            ulong localSteamId = GetLocalSteamId();
            if (steamId.Equals(localSteamId))
                return;
            try
            {
                lock (collectionLock)
                {
                    if (remotePlayerObjects.ContainsKey(steamId))
                    {
                        var playerObject = remotePlayerObjects[steamId];
                        if (playerObject != null)
                        {
                            UnityEngine.Object.Destroy(playerObject);
                        }
                        remotePlayerObjects.Remove(steamId);
                    }
                    remotePlayers.Remove(steamId);
                    remotePlayerFacingLeft.Remove(steamId); // Clean up facing direction tracking
                }
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Removed remote player {steamId}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Failed to remove remote player " + steamId + ": " + e.Message);
            }
        }

        /// <summary>
        /// Creates a placeholder remote player that will be updated with character data later
        /// </summary>
        private GameObject CreatePlaceholderRemotePlayer(ulong steamId, string mapName)
        {
            var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}");
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Creating placeholder remote player for {steamId} in map {mapName} (Debug mode: {DebugModeSimpleSquares})");

            try
            {
                // Check if we're in debug mode - create simple green squares instead
                if (DebugModeSimpleSquares)
                {
                    return CreateDebugSquarePlayer(remotePlayerObj, steamId);
                }

                // Create a placeholder with a waiting sprite
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
                CreatePlaceholderSprite(spriteRenderer);

                // Add a simple collider for interaction (but don't make it interfere with physics)
                var collider = remotePlayerObj.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true; // Don't interfere with game physics

                // Remote player behavior is now handled directly by PlayerSynchroniser

                // Position it initially near the local player if available, otherwise at a visible location
                Vector3 initialPosition = Vector3.zero;
                if (localPlayer != null)
                {
                    // Position near local player with some offset
                    initialPosition = localPlayer.transform.position + new Vector3(2f, 0f, 0f);
                }
                else
                {
                    // Default to a visible position
                    initialPosition = new Vector3(5f, 5f, 0f);
                }
                
                // Use centralized positioning for consistent center anchoring
                SetSpritePositionCentered(remotePlayerObj, initialPosition);
                return remotePlayerObj;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create placeholder remote player for {steamId}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a placeholder sprite that indicates we're waiting for character data
        /// </summary>
        private void CreatePlaceholderSprite(SpriteRenderer spriteRenderer)
        {
            // Create a gray square to indicate we're waiting for character data
            var texture = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    texture.SetPixel(x, y, new Color(0.7f, 0.7f, 0.7f, 1.0f)); // Gray color
            texture.Apply();

            var placeholderSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
            spriteRenderer.sprite = placeholderSprite;
            spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 0.9f); // Gray with slight transparency
            spriteRenderer.sortingLayerName = "FG_Critical";
            spriteRenderer.sortingOrder = 10;

            GungeonTogether.Logging.Debug.Log("[PlayerSync] Created placeholder gray sprite for remote player");
        }

        /// <summary>
        /// Creates a sprite indicating the player hasn't selected a character yet
        /// </summary>
        private void CreateNoCharacterSelectedSprite(SpriteRenderer spriteRenderer)
        {
            // Create a yellow/orange square to indicate no character selection
            var texture = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    texture.SetPixel(x, y, new Color(1.0f, 0.8f, 0.2f, 1.0f)); // Yellow/orange color
            texture.Apply();

            var noCharacterSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
            spriteRenderer.sprite = noCharacterSprite;
            spriteRenderer.color = new Color(1.0f, 0.8f, 0.2f, 0.8f); // Yellow/orange with transparency
            spriteRenderer.sortingLayerName = "FG_Critical";
            spriteRenderer.sortingOrder = 10;

            GungeonTogether.Logging.Debug.Log("[PlayerSync] Created 'no character selected' yellow sprite for remote player");
        }

        /// <summary>
        /// Updates a remote player's character appearance when we receive character data
        /// </summary>
        private void UpdateRemotePlayerCharacter(ulong steamId, int characterId, string characterName)
        {
            try
            {
                // Handle null or empty character name
                if (string.IsNullOrEmpty(characterName))
                {
                    characterName = GetCharacterNameFromId(characterId);
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Character name was null/empty, using fallback from ID {characterId}: {characterName}");
                }

                // Handle case where no character has been selected
                if (characterId == -1 || characterName == "NoCharacterSelected")
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player {steamId} has not selected a character yet, using placeholder");

                    lock (collectionLock)
                    {
                        if (remotePlayerObjects.ContainsKey(steamId))
                        {
                            var playerObject = remotePlayerObjects[steamId];
                            if (playerObject != null)
                            {
                                var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
                                if (spriteRenderer != null)
                                {
                                    CreateNoCharacterSelectedSprite(spriteRenderer);
                                }
                            }
                        }
                    }
                    return;
                }

                lock (collectionLock)
                {
                    if (remotePlayerObjects.ContainsKey(steamId))
                    {
                        var playerObject = remotePlayerObjects[steamId];
                        if (playerObject != null)
                        {
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Updating remote player {steamId} character to {characterName} (ID: {characterId})");

                            // Get the sprite renderer
                            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
                            if (spriteRenderer != null)
                            {
                                // Remove any existing animator
                                var existingAnimator = playerObject.GetComponent<tk2dSpriteAnimator>();
                                if (existingAnimator != null)
                                {
                                    UnityEngine.Object.Destroy(existingAnimator);
                                }

                                // Try to load character-specific sprite
                                tk2dSpriteAnimator animator = null;
                                bool characterSpriteLoaded = false;

                                // Skip complex sprite loading if in simple debug mode
                                if (!DebugModeSimpleSquares)
                                {
                                    characterSpriteLoaded = TryLoadCharacterSprite(spriteRenderer, out animator, characterName, characterId);
                                }

                                if (!characterSpriteLoaded)
                                {
                                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Character-specific sprite loading failed or skipped for {steamId}, using simple fallback");
                                    // Create a simple colored square as a fallback to prevent sprite blob
                                    CreateSimpleFallbackSprite(spriteRenderer, characterName);
                                }
                                else
                                {
                                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully updated character sprite for {steamId}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to update remote player character for {steamId}: {e.Message}");
            }
        }

        /// <summary>
        /// Creates a more realistic remote player representation that mimics actual PlayerController behavior
        /// </summary>
        private GameObject CreateRemotePlayerLikeObject(ulong steamId, string mapName, int characterId = 0, string characterName = "PlayerRogue")
        {
            var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}");
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Creating remote player for {steamId} in map {mapName} with character {characterName} (ID: {characterId}) (Debug mode: {DebugModeSimpleSquares})");

            try
            {
                // Check if we're in debug mode - create simple green squares instead
                if (DebugModeSimpleSquares)
                {
                    return CreateDebugSquarePlayer(remotePlayerObj, steamId);
                }

                // Add basic components that make it behave more like a PlayerController
                var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
                tk2dSpriteAnimator animator = null;

                // Try to load character-specific sprite and animation
                bool characterSpriteLoaded = TryLoadCharacterSprite(spriteRenderer, out animator, characterName, characterId);

                // If character-specific loading failed, try to copy from local player as fallback
                if (!characterSpriteLoaded && localPlayer != null && !DebugModeSimpleSquares)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Character-specific sprite loading failed, falling back to local player copy");

                    // Copy sprite renderer properties
                    var localSpriteRenderer = localPlayer.GetComponent<SpriteRenderer>();
                    if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
                    {
                        spriteRenderer.sprite = localSpriteRenderer.sprite;
                        spriteRenderer.sortingLayerName = localSpriteRenderer.sortingLayerName;
                        spriteRenderer.sortingOrder = localSpriteRenderer.sortingOrder;
                        spriteRenderer.color = Color.white; // Use normal white color like local player
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Copied sprite from local player - sprite: {spriteRenderer.sprite?.name}, layer: {spriteRenderer.sortingLayerName}, order: {spriteRenderer.sortingOrder}");

                        // Only add animator if we have a sprite and can copy from local player
                        var localAnimator = localPlayer.GetComponent<tk2dSpriteAnimator>();
                        if (localAnimator != null && localAnimator.Library != null)
                        {
                            animator = remotePlayerObj.AddComponent<tk2dSpriteAnimator>();
                            animator.Library = localAnimator.Library;
                            // Start with idle animation if available
                            try
                            {
                                var idleClips = new string[] { "idle_south", "idle", "player_idle_south", "player_idle" };
                                foreach (var clipName in idleClips)
                                {
                                    if (animator.GetClipByName(clipName) != null)
                                    {
                                        animator.Play(clipName);
                                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Started animation: {clipName}");
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Could not start idle animation for remote player: {ex.Message}");
                            }
                        }
                    }
                }

                // Final fallback if we can't copy from local player OR if we're in debug mode
                if (spriteRenderer.sprite == null || DebugModeSimpleSquares)
                {
                    CreateFallbackSprite(spriteRenderer);
                    GungeonTogether.Logging.Debug.Log("[PlayerSync] Using fallback sprite for remote player");
                }

                // Add a simple collider for interaction (but don't make it interfere with physics)
                var collider = remotePlayerObj.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true; // Don't interfere with game physics

                // Remote player behavior is now handled directly by PlayerSynchroniser

                // Position it initially near the local player if available, otherwise at a visible location
                Vector3 initialPosition = Vector3.zero;
                if (localPlayer != null)
                {
                    // Position near local player with some offset
                    initialPosition = localPlayer.transform.position + new Vector3(2f, 0f, 0f);
                }
                else
                {
                    // Default to a visible position
                    initialPosition = new Vector3(5f, 5f, 0f);
                }
                
                // Use centralized positioning for consistent center anchoring
                SetSpritePositionCentered(remotePlayerObj, initialPosition);

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Positioned remote player {steamId} at {initialPosition}");

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Successfully created advanced remote player for {steamId}");
                return remotePlayerObj;
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Failed to create advanced remote player for {steamId}: {e.Message}");
                // Fallback to simple sprite if advanced creation fails
                return CreateSimpleRemotePlayer(steamId);
            }
        }

        /// <summary>
        /// Creates a simple green square for debug mode remote players
        /// </summary>
        private GameObject CreateDebugSquarePlayer(GameObject remotePlayerObj, ulong steamId)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Creating debug square player for {steamId}");

            var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();

            // Create a simple green square texture
            var texture = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    texture.SetPixel(x, y, Color.green);
                }
            }
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.green;
            spriteRenderer.sortingLayerName = "FG_Critical";
            spriteRenderer.sortingOrder = 100; // High order to ensure visibility

            // Add a simple collider for interaction (but don't make it interfere with physics)
            var collider = remotePlayerObj.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true; // Don't interfere with game physics

            // Remote player behavior is now handled directly by PlayerSynchroniser

            // Position it initially near the local player if available, otherwise at a visible location
            Vector3 initialPosition = Vector3.zero;
            if (localPlayer != null)
            {
                // Position near local player with some offset
                initialPosition = localPlayer.transform.position + new Vector3(2f, 0f, 0f);
            }
            else
            {
                // Default to a visible position
                initialPosition = new Vector3(5f, 5f, 0f);
            }
            
            // Use centralized positioning for consistent center anchoring
            SetSpritePositionCentered(remotePlayerObj, initialPosition);

            // Make sure the object stays visible and is not destroyed
            UnityEngine.Object.DontDestroyOnLoad(remotePlayerObj);

            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Created debug square player at {initialPosition}, sorting layer: {spriteRenderer.sortingLayerName}, order: {spriteRenderer.sortingOrder}");
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] Debug square object active: {remotePlayerObj.activeInHierarchy}, sprite enabled: {spriteRenderer.enabled}");

            return remotePlayerObj;
        }

        /// <summary>
        /// Creates a simple fallback remote player representation
        /// </summary>
        private GameObject CreateSimpleRemotePlayer(ulong steamId)
        {
            var remotePlayerObj = new GameObject($"RemotePlayer_{steamId}_Simple");
            var spriteRenderer = remotePlayerObj.AddComponent<SpriteRenderer>();
            CreateFallbackSprite(spriteRenderer);

            // Remote player behavior is now handled directly by PlayerSynchroniser

            // Position it at a visible location
            Vector3 initialPosition = Vector3.zero;
            if (localPlayer != null)
            {
                initialPosition = localPlayer.transform.position + new Vector3(2f, 0f, 0f);
            }
            else
            {
                initialPosition = new Vector3(5f, 5f, 0f);
            }
            
            // Use centralized positioning for consistent center anchoring
            SetSpritePositionCentered(remotePlayerObj, initialPosition);

            return remotePlayerObj;
        }

        /// <summary>
        /// Creates a simple sprite as fallback when player sprites are not available
        /// </summary>
        private void CreateFallbackSprite(SpriteRenderer spriteRenderer)
        {
            // Create different sprites based on debug mode
            if (DebugModeSimpleSquares)
            {
                // Debug mode: Create green square
                var texture = new Texture2D(32, 32);
                for (int x = 0; x < 32; x++)
                    for (int y = 0; y < 32; y++)
                        texture.SetPixel(x, y, Color.green);
                texture.Apply();

                var fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
                spriteRenderer.sprite = fallbackSprite;
                spriteRenderer.color = Color.green;
                spriteRenderer.sortingLayerName = "FG_Critical";
                spriteRenderer.sortingOrder = 100; // High order to ensure visibility
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Created DEBUG green square sprite for remote player");
            }
            else
            {
                // Normal mode: Create cyan square
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 16; y++)
                        texture.SetPixel(x, y, Color.cyan);
                texture.Apply();

                var fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                spriteRenderer.sprite = fallbackSprite;
                spriteRenderer.color = Color.cyan;
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Created normal cyan fallback sprite for remote player");
            }
        }

        /// <summary>
        /// Force recreation of all remote players (useful when debug mode is toggled)
        /// </summary>
        public void RecreateAllRemotePlayers()
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Recreating all remote players in debug mode: {DebugModeSimpleSquares}");

            try
            {
                Dictionary<ulong, RemotePlayerState> currentStates;

                lock (collectionLock)
                {
                    // Store current remote player states
                    currentStates = new Dictionary<ulong, RemotePlayerState>(remotePlayers);

                    // Destroy existing remote player objects
                    foreach (var kvp in remotePlayerObjects.ToArray())
                    {
                        if (kvp.Value != null)
                        {
                            UnityEngine.Object.Destroy(kvp.Value);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Destroyed old remote player object for {kvp.Key}");
                        }
                    }
                    remotePlayerObjects.Clear();
                    // Note: We keep remotePlayerFacingLeft to preserve facing directions during recreation
                }

                // Recreate remote players with current states
                foreach (var kvp in currentStates)
                {
                    var steamId = kvp.Key;
                    var state = kvp.Value;

                    GungeonTogether.Logging.Debug.Log($"[PlayerSync] Recreating remote player {steamId} with debug mode: {DebugModeSimpleSquares}");
                    var remotePlayerObj = CreateRemotePlayerLikeObject(steamId, state.MapName, state.CharacterId, state.CharacterName);
                    if (remotePlayerObj != null)
                    {
                        lock (collectionLock)
                        {
                            remotePlayerObjects[steamId] = remotePlayerObj;
                        }
                        
                        // Use centralized positioning for consistent center anchoring
                        Vector3 targetPosition = new Vector3(state.Position.x, state.Position.y, remotePlayerObj.transform.position.z);
                        SetSpritePositionCentered(remotePlayerObj, targetPosition);
                        
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Recreated remote player {steamId} at position {state.Position}");
                    }
                }

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Finished recreating {currentStates.Count} remote players");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error recreating remote players: {e.Message}");
            }
        }

        #endregion

        #region Sprite Positioning Helpers

        /// <summary>
        /// Sets the position of a sprite with center-anchor compensation for both Unity and tk2d sprites
        /// </summary>
        private void SetSpritePositionCentered(GameObject spriteObject, Vector3 targetPosition)
        {
            if (spriteObject == null) return;

            try
            {
                // Simplified approach - just set position directly
                // Unity sprites are already center-anchored, and tk2d sprites will work fine with direct positioning
                spriteObject.transform.position = targetPosition;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error in SetSpritePositionCentered: {ex.Message}");
                // Fallback to direct positioning
                spriteObject.transform.position = targetPosition;
            }
        }

        /// <summary>
        /// Gets the center position of a sprite, accounting for anchor differences
        /// </summary>
        private Vector3 GetSpriteCenterPosition(GameObject spriteObject)
        {
            if (spriteObject == null) return Vector3.zero;

            try
            {
                // Simplified approach - just return the transform position directly
                return spriteObject.transform.position;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error in GetSpriteCenterPosition: {ex.Message}");
                return spriteObject.transform.position;
            }
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// Handle received player position data
        /// </summary>
        public void OnPlayerPositionReceived(PlayerPositionData data)
        {
            ulong localSteamId = GetLocalSteamId();

            if (data.PlayerId.Equals(localSteamId))
            {
                return; // Skip our own packets
            }

            try
            {
                // Track last update received (robust, never for self)
                OnAnyRemotePacketReceived(data.PlayerId);

                lock (collectionLock)
                {
                    if (remotePlayers.ContainsKey(data.PlayerId))
                    {
                        var playerState = remotePlayers[data.PlayerId];

                        // Check if character data was included in this packet
                        bool hasCharacterData = (data.CharacterId != -999) && (data.CharacterName != "NO_CHARACTER_DATA");

                        // Check if character has changed (only if character data was included)
                        bool characterChanged = false;
                        if (hasCharacterData && (playerState.CharacterId != data.CharacterId || playerState.CharacterName != data.CharacterName))
                        {
                            characterChanged = true;
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Character changed for player {data.PlayerId}: {playerState.CharacterName}/{playerState.CharacterId} -> {data.CharacterName}/{data.CharacterId}");

                            // Show notification for character changes
                            if (data.CharacterId == -1 || data.CharacterName == "NoCharacterSelected")
                            {
                                UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} deselected their character", 2f);
                            }
                            else if (playerState.CharacterId == -1 || playerState.CharacterName == "NoCharacterSelected")
                            {
                                UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} selected {data.CharacterName}!", 3f);
                            }
                            else
                            {
                                UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} changed to {data.CharacterName}", 2f);
                            }
                        }

                        // Always update position data
                        playerState.Position = data.Position;
                        playerState.Velocity = data.Velocity;
                        playerState.Rotation = data.Rotation;
                        playerState.IsGrounded = data.IsGrounded;
                        playerState.IsDodgeRolling = data.IsDodgeRolling;
                        playerState.LastUpdateTime = Time.time;
                        playerState.TargetPosition = data.Position;
                        playerState.MapName = data.MapName ?? "Unknown";

                        // Update animation state data
                        playerState.AnimationState = data.AnimationState;
                        playerState.MovementDirection = data.MovementDirection;
                        playerState.IsRunning = data.IsRunning;
                        playerState.IsFalling = data.IsFalling;
                        playerState.IsTakingDamage = data.IsTakingDamage;
                        playerState.IsDead = data.IsDead;
                        playerState.CurrentAnimationName = data.CurrentAnimationName ?? "";

                        // Only update character data if it was included in the packet
                        if (hasCharacterData)
                        {
                            playerState.CharacterId = data.CharacterId;
                            playerState.CharacterName = data.CharacterName ?? "Unknown";
                        }

                        remotePlayers[data.PlayerId] = playerState;

                        // Update character appearance if it changed
                        if (characterChanged)
                        {
                            UpdateRemotePlayerCharacter(data.PlayerId, data.CharacterId, data.CharacterName);
                        }

                        // Apply animation state to the remote player
                        ApplyAnimationState(data.PlayerId, playerState);
                    }
                    else
                    {
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync] Remote player {data.PlayerId} not found in remotePlayers. Creating...");
                        // Create player outside the lock to avoid deadlock
                    }
                }

                // Create player outside the main lock to avoid potential deadlock
                if (!remotePlayers.ContainsKey(data.PlayerId))
                {
                    CreateRemotePlayer(data.PlayerId, data.MapName);
                    // After creating, update with the received data including character info (if provided)
                    lock (collectionLock)
                    {
                        if (remotePlayers.ContainsKey(data.PlayerId))
                        {
                            var playerState = remotePlayers[data.PlayerId];

                            // Always update position data
                            playerState.Position = data.Position;
                            playerState.Velocity = data.Velocity;
                            playerState.Rotation = data.Rotation;
                            playerState.IsGrounded = data.IsGrounded;
                            playerState.IsDodgeRolling = data.IsDodgeRolling;
                            playerState.LastUpdateTime = Time.time;
                            playerState.TargetPosition = data.Position;
                            playerState.MapName = data.MapName ?? "Unknown";

                            // Update animation state data
                            playerState.AnimationState = data.AnimationState;
                            playerState.MovementDirection = data.MovementDirection;
                            playerState.IsRunning = data.IsRunning;
                            playerState.IsFalling = data.IsFalling;
                            playerState.IsTakingDamage = data.IsTakingDamage;
                            playerState.IsDead = data.IsDead;
                            playerState.CurrentAnimationName = data.CurrentAnimationName ?? "";

                            // Check if character data was included in this packet
                            bool hasCharacterData = (data.CharacterId != -999) && (data.CharacterName != "NO_CHARACTER_DATA");

                            if (hasCharacterData)
                            {
                                playerState.CharacterId = data.CharacterId;
                                playerState.CharacterName = data.CharacterName ?? "Unknown";
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Updated newly created remote player {data.PlayerId} with position and character data: {data.CharacterName}/{data.CharacterId}");

                                // Update character appearance with the received character data
                                UpdateRemotePlayerCharacter(data.PlayerId, data.CharacterId, data.CharacterName);

                                // Show notification for new player
                                if (data.CharacterId == -1 || data.CharacterName == "NoCharacterSelected")
                                {
                                    UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} joined (no character selected)", 3f);
                                }
                                else
                                {
                                    UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} joined as {data.CharacterName}!", 4f);
                                }
                            }
                            else
                            {
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Updated newly created remote player {data.PlayerId} with position data only (no character data in packet)");
                                UI.MultiplayerUIManager.ShowNotification($"Player {data.PlayerId} joined (awaiting character data)", 3f);
                            }

                            remotePlayers[data.PlayerId] = playerState;

                            // Apply animation state to the newly created remote player
                            ApplyAnimationState(data.PlayerId, playerState);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Position receive error: " + e.Message);
            }
        }

        /// <summary>
        /// Handle received player shooting data
        /// </summary>
        public void OnPlayerShootingReceived(PlayerShootingData data)
        {
            try
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][DEBUG] OnPlayerShootingReceived called for player: {data.PlayerId}");
                // Track last update received (robust, never for self)
                OnAnyRemotePacketReceived(data.PlayerId);
                CreateShootingEffect(data.Position, data.Direction);
                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Player {data.PlayerId} shooting from {data.Position} towards {data.Direction}");
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting receive error: " + e.Message);
            }
        }

        private void CreateShootingEffect(Vector2 position, Vector2 direction)
        {
            try
            {
                // Create a simple line renderer or particle effect to show shooting
                var effectObj = new GameObject("ShootingEffect");
                effectObj.transform.position = position;

                var lineRenderer = effectObj.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.yellow;
                lineRenderer.endColor = Color.yellow;
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;

                var endPosition = position + direction * 2f; // 2 unit long line
                lineRenderer.SetPosition(0, position);
                lineRenderer.SetPosition(1, endPosition);

                // Destroy the effect after a short time
                UnityEngine.Object.Destroy(effectObj, 0.1f);
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Shooting effect error: " + e.Message);
            }
        }

        /// <summary>
        /// Apply animation state to a remote player using direct animation name - simple 1:1 system
        /// </summary>
        private void ApplyAnimationState(ulong playerId, RemotePlayerState state)
        {
            try
            {
                if (!remotePlayerObjects.ContainsKey(playerId))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] No player object found for {playerId}");
                    return;
                }

                var playerObj = remotePlayerObjects[playerId];
                if (playerObj == null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] Player object is null for {playerId}");
                    return;
                }

                // Find the tk2dSpriteAnimator component
                var animator = playerObj.GetComponent<tk2dSpriteAnimator>();
                if (animator == null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] No tk2dSpriteAnimator found for player {playerId}");
                    return;
                }

                if (animator.Library == null)
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] Animation library is null for player {playerId}");
                    return;
                }

                // Use the animation name directly from the network packet (1:1 system)
                string targetAnimationName = state.CurrentAnimationName;

                // If no animation name provided, fall back to idle
                if (string.IsNullOrEmpty(targetAnimationName))
                {
                    targetAnimationName = "idle";
                }
                // Only change animation if it's different
                if (animator.CurrentClip?.name != targetAnimationName)
                {
                    try
                    {
                        // Check if the animation exists in the library
                        var clipId = animator.GetClipIdByName(targetAnimationName);
                        GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] Clip ID for '{targetAnimationName}': {clipId}");

                        if (clipId >= 0)
                        {
                            // Play the animation directly - no complex state management
                            animator.Play(targetAnimationName);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Playing direct animation '{targetAnimationName}' for player {playerId}");
                        }
                        else
                        {
                            // Only use "idle" as fallback if the specific animation doesn't exist
                            var idleClipId = animator.GetClipIdByName("idle");
                            if (idleClipId >= 0)
                            {
                                animator.Play("idle");
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Animation '{targetAnimationName}' not found, using idle fallback for player {playerId}");
                            }
                            else
                            {
                                GungeonTogether.Logging.Debug.Log($"[PlayerSync][AnimDebug] Neither '{targetAnimationName}' nor 'idle' animation found for player {playerId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error playing animation '{targetAnimationName}' for player {playerId}: {ex.Message}");
                    }
                }

                // Check for movement to update facing direction - try both movement direction AND velocity
                bool shouldFaceLeft = false;
                
                if (state.MovementDirection.magnitude > 0.1f)
                {
                    shouldFaceLeft = state.MovementDirection.x < 0;
                    remotePlayerFacingLeft[playerId] = shouldFaceLeft;
                }
                else if (state.Velocity.magnitude > 0.1f)
                {
                    // Fallback to velocity if MovementDirection is not set
                    shouldFaceLeft = state.Velocity.x < 0;
                    remotePlayerFacingLeft[playerId] = shouldFaceLeft;
                }
                
                // Get current facing direction (saved or default to right)
                bool currentFacingLeft = remotePlayerFacingLeft.ContainsKey(playerId) ? remotePlayerFacingLeft[playerId] : false;

                // Use tk2d sprite system only (ETG's native system)
                var tk2dSprite = playerObj.GetComponent<tk2dSprite>();
                
                if (tk2dSprite != null)
                {
                    // Use tk2d's FlipX property for sprite flipping
                    bool isCurrentlyFlipped = tk2dSprite.FlipX;

                    if (isCurrentlyFlipped != currentFacingLeft)
                    {
                        // Apply flip using tk2d's system
                        tk2dSprite.FlipX = currentFacingLeft;
                        
                    }
                    
                    // Use centralized positioning function that handles center anchoring
                    Vector3 targetNetworkPosition = new Vector3(state.Position.x, state.Position.y, playerObj.transform.position.z);
                    SetSpritePositionCentered(playerObj, targetNetworkPosition);
                }

                // Apply damage visual effects
                ApplyPlayerVisualEffects(playerObj, state);

            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error applying animation state to player {playerId}: {ex.Message}");       
            }
            
             
        }

        /// <summary>
        /// Apply visual effects like damage tinting without changing animations
        /// </summary>
        private void ApplyPlayerVisualEffects(GameObject playerObj, RemotePlayerState state)
        {
            try
            {
                // Get the tk2dSprite component (ETG uses tk2d sprites)
                var tk2dSprite = playerObj.GetComponent<tk2dSprite>();
                
                if (tk2dSprite != null)
                {
                    // Apply damage tint effect (red color) when taking damage
                    if (state.IsTakingDamage)
                    {
                        tk2dSprite.color = Color.red;
                    }
                    else
                    {
                        // Reset to normal color when not taking damage
                        tk2dSprite.color = Color.white;
                    }
                }
                else
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][VisualFX] No tk2dSprite component found for visual effects on player {state.SteamId}");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync] Error applying visual effects: {ex.Message}");
            }
        }

        private void UpdateRemotePlayerVisual(ulong playerId, RemotePlayerState state)
        {
            try
            {
                if (remotePlayerObjects.ContainsKey(playerId))
                {
                    var playerObj = remotePlayerObjects[playerId];
                    if (playerObj != null)
                    {
                        bool isDebugFakePlayer = playerId >= 12345678UL && playerId <= 99999999UL; // Range of fake Steam IDs used in debug

                        // Always show debug fake players, or normal logic for real players
                        if (isDebugFakePlayer || state.MapName == localMapName)
                        {
                            playerObj.SetActive(true);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Rendering remote player {playerId} at pos={state.Position} rot={state.Rotation} in map {state.MapName} (debug: {isDebugFakePlayer})");
                            
                            // Use centralized positioning function for consistent center anchoring
                            Vector3 targetPosition = new Vector3(state.Position.x, state.Position.y, playerObj.transform.position.z);
                            SetSpritePositionCentered(playerObj, targetPosition);
                            
                            playerObj.transform.eulerAngles = new Vector3(0, 0, state.Rotation);
                        }
                        else
                        {
                            playerObj.SetActive(false);
                            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Hiding remote player {playerId} (map mismatch: {state.MapName} != {localMapName})");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError("[PlayerSync] Error updating visual: " + e.Message);
            }
        }

        #endregion

        /// <summary>
        /// Get all remote player states
        /// </summary>
        public Dictionary<ulong, RemotePlayerState> GetRemotePlayers()
        {
            lock (collectionLock)
            {
                return new Dictionary<ulong, RemotePlayerState>(remotePlayers);
            }
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Cleanup()
        {
            lock (collectionLock)
            {
                foreach (var kvp in remotePlayerObjects)
                {
                    if (kvp.Value != null)
                    {
                        UnityEngine.Object.Destroy(kvp.Value);
                    }
                }
                remotePlayerObjects.Clear();
                remotePlayers.Clear();
                remotePlayerFacingLeft.Clear(); // Clean up facing direction tracking
            }
            GungeonTogether.Logging.Debug.Log("[PlayerSync] Cleanup complete");
        }

        /// <summary>
        /// Static initialize method
        /// </summary>
        public static void StaticInitialize()
        {
            Instance.Initialize();
        }

        /// <summary>
        /// Static update method
        /// </summary>
        public static void StaticUpdate()
        {
            // Track for debug UI
            LastStaticUpdateFrame = Time.frameCount;
            var session = GungeonTogetherMod.Instance?._sessionManager;
            if (session == null || !session.IsActive)
                LastStaticUpdateRole = "Singleplayer";
            else if (session.IsHost)
                LastStaticUpdateRole = "HOST";
            else
                LastStaticUpdateRole = "JOINER";

            var localSteamId = NetworkManager.Instance?.LocalSteamId ?? 0UL;
            var isHost = NetworkManager.Instance?.IsHost() ?? false;
            Instance.Update();
        }

        /// <summary>
        /// Tracks the last frame and role (HOST/JOINER) when StaticUpdate() was called, for debug UI.
        /// </summary>
        public static int LastStaticUpdateFrame = -1;
        public static string LastStaticUpdateRole = "Unknown";

        /// <summary>
        /// Update remote player from network data
        /// </summary>
        public static void UpdateRemotePlayer(PlayerPositionData data)
        {
            Instance.OnPlayerPositionReceived(data);
        }

        /// <summary>
        /// Handle remote player shooting
        /// </summary>
        public static void HandleRemotePlayerShooting(PlayerShootingData data)
        {
            Instance.OnPlayerShootingReceived(data);
        }

        // Call this from any handler that receives a packet from a remote player.
        // This will NOT update the timer if the sender is the local player (should never happen).
        public static void OnAnyRemotePacketReceived(ulong senderSteamId)
        {
            ulong localSteamId = NetworkManager.Instance != null ? NetworkManager.Instance.LocalSteamId : 0UL;

            if (senderSteamId == 0UL)
            {
                return; // Defensive: ignore invalid sender
            }
            if (senderSteamId == localSteamId)
            {
                return; // Never update for self
            }

            LastUpdateReceivedFrame = Time.frameCount;
            LastUpdateReceivedTime = Time.time;
        }

        // Handler for map sync packet (to be called from NetworkManager when received)
        public void OnMapSyncReceived(string mapName, ulong senderSteamId)
        {
            // Track last update received (robust, never for self)
            OnAnyRemotePacketReceived(senderSteamId);
            if (localMapName != mapName)
            {
                // Defer scene load until GameManager.Instance and PrimaryPlayer are available
                Instance.DeferSceneLoadIfNeeded(mapName);
            }
            else
            {
                GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Already in correct map: {mapName}");
            }
        }

        // Helper to defer scene load until player is ready
        private void DeferSceneLoadIfNeeded(string mapName)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Checking if scene load can proceed for map: {mapName}");
            GungeonTogether.GungeonTogetherCoroutineRunner.RunCoroutine(WaitForPlayerAndLoadScene(mapName));
        }

        private System.Collections.IEnumerator WaitForPlayerAndLoadScene(string mapName)
        {
            float timeout = 10f;
            float elapsed = 0f;
            while ((GameManager.Instance == null || GameManager.Instance.PrimaryPlayer == null) && elapsed < timeout)
            {
                if (GameManager.Instance == null)
                    GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] Waiting for GameManager.Instance...");
                else if (GameManager.Instance.PrimaryPlayer == null)
                    GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] Waiting for PrimaryPlayer...");
                yield return null;
                elapsed += UnityEngine.Time.unscaledDeltaTime;
            }
            if (GameManager.Instance == null || GameManager.Instance.PrimaryPlayer == null)
            {
                GungeonTogether.Logging.Debug.LogWarning("[PlayerSync][MAPSYNC] Timeout waiting for GameManager/PrimaryPlayer. Scene load may fail.");
            }
            else
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync][MAPSYNC] GameManager and PrimaryPlayer are ready. Proceeding with scene load if needed.");
            }

            // Update local map name first
            localMapName = mapName;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Attempting to sync to scene: {mapName}");

            // Enhanced scene forcing logic for different scenarios
            try
            {
                if (ForceSceneTransition(mapName))
                {
                    GungeonTogether.Logging.Debug.Log($"[PlayerSync][MAPSYNC] Successfully initiated scene transition to {mapName}");
                }
                else
                {
                    GungeonTogether.Logging.Debug.LogWarning($"[PlayerSync][MAPSYNC] Failed to force scene transition to {mapName}");
                }
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync][MAPSYNC] Exception during scene transition: {ex.Message}");
            }
        }

        /// <summary>
        /// Enhanced scene forcing logic that handles all major scene transitions
        /// </summary>
        private bool ForceSceneTransition(string targetScene)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] ForceSceneTransition to {targetScene}");

            if (string.IsNullOrEmpty(targetScene))
            {
                GungeonTogether.Logging.Debug.LogWarning("[PlayerSync] Target scene is null or empty");
                return false;
            }

            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Current scene: {currentScene}, Target: {targetScene}");

            // If already in target scene, no need to transition
            if (string.Equals(currentScene, targetScene, StringComparison.OrdinalIgnoreCase))
            {
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Already in target scene");
                return true;
            }

            // Handle specific scene transitions
            if (targetScene.Equals("tt_foyer", StringComparison.OrdinalIgnoreCase))
            {
                return ForceToFoyer();
            }
            else if (targetScene.StartsWith("tt_") || targetScene.Contains("dungeon") || targetScene.Contains("Dungeon"))
            {
                return ForceToDungeon(targetScene);
            }
            else
            {
                // Generic scene load for other scenes
                return ForceGenericSceneLoad(targetScene);
            }
        }

        /// <summary>
        /// Force transition to the foyer
        /// </summary>
        private bool ForceToFoyer()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerSync] Forcing transition to foyer");

            try
            {
                if (GameManager.Instance != null)
                {
                    // Try using GameManager's ReturnToFoyer method first
                    var gameManagerType = GameManager.Instance.GetType();
                    var returnToFoyerMethod = gameManagerType.GetMethod("ReturnToFoyer",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (returnToFoyerMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.ReturnToFoyer()");
                        returnToFoyerMethod.Invoke(GameManager.Instance, null);

                        // After transitioning to foyer, ensure character info is synchronized
                        ScheduleCharacterInfoBroadcast();
                        return true;
                    }

                    // Try DoMainMenu method as alternative
                    var doMainMenuMethod = gameManagerType.GetMethod("DoMainMenu",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (doMainMenuMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.DoMainMenu()");
                        doMainMenuMethod.Invoke(GameManager.Instance, null);

                        // After transitioning, ensure character info is synchronized
                        ScheduleCharacterInfoBroadcast();
                        return true;
                    }
                }

                // Fallback to direct scene load
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Using direct scene load for foyer");
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("tt_foyer");

                // Schedule character info broadcast after scene transition
                ScheduleCharacterInfoBroadcast();
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error forcing foyer transition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Schedule a character info broadcast for after scene transitions
        /// </summary>
        public void ScheduleCharacterInfoBroadcast()
        {
            // Since this class is not a MonoBehaviour, we'll use a timer-based approach
            // Mark that we need to broadcast after a delay
            scheduledBroadcastTime = Time.time + 2f; // Broadcast in 2 seconds
            needsScheduledBroadcast = true;
        }

        /// <summary>
        /// Force transition to a dungeon scene
        /// </summary>
        private bool ForceToDungeon(string dungeonScene)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Forcing transition to dungeon: {dungeonScene}");

            try
            {
                // For dungeon scenes, try to use the game's proper dungeon loading mechanism
                if (GameManager.Instance != null)
                {
                    // Try to find and use LoadLevel or similar method
                    var gameManagerType = GameManager.Instance.GetType();
                    var loadLevelMethod = gameManagerType.GetMethod("LoadLevel",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (loadLevelMethod != null)
                    {
                        GungeonTogether.Logging.Debug.Log("[PlayerSync] Using GameManager.LoadLevel()");
                        // This method might require specific parameters, so use reflection carefully
                        var parameters = loadLevelMethod.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        {
                            loadLevelMethod.Invoke(GameManager.Instance, new object[] { dungeonScene });
                            return true;
                        }
                    }
                }

                // Fallback to direct scene load
                GungeonTogether.Logging.Debug.Log("[PlayerSync] Using direct scene load for dungeon");
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(dungeonScene);
                return true;
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error forcing dungeon transition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force generic scene load for any other scene
        /// </summary>
        private bool ForceGenericSceneLoad(string sceneName)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Forcing generic scene load: {sceneName}");

            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
                return true;
            }                                              
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error in generic scene load: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process initial state sync from host
        /// </summary>
        public void ProcessInitialStateSync(InitialStateSyncData data)
        {
            GungeonTogether.Logging.Debug.Log($"[PlayerSync] Processing initial state sync: Map={data.MapName}, {data.ConnectedPlayers?.Length ?? 0} players");

            try
            {
                // Create remote players from the initial state
                if (data.ConnectedPlayers != null)
                {
                    foreach (var playerData in data.ConnectedPlayers)
                    {
                        if (playerData.PlayerId != GetLocalSteamId())
                        {
                            CreateRemotePlayer(playerData.PlayerId, data.MapName);

                            // Update their position immediately
                            if (remotePlayers.ContainsKey(playerData.PlayerId))
                            {
                                var remoteState = remotePlayers[playerData.PlayerId];
                                remoteState.Position = playerData.Position;
                                remoteState.TargetPosition = playerData.Position;
                                remoteState.LastUpdateTime = Time.time;
                                remotePlayers[playerData.PlayerId] = remoteState;

                                // Update visual position
                                if (remotePlayerObjects.ContainsKey(playerData.PlayerId))
                                {
                                    var obj = remotePlayerObjects[playerData.PlayerId];
                                    if (obj != null)
                                    {
                                        // Use centralized positioning for consistent center anchoring
                                        Vector3 targetPosition = new Vector3(playerData.Position.x, playerData.Position.y, obj.transform.position.z);
                                        SetSpritePositionCentered(obj, targetPosition);
                                    }
                                }

                                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Set initial position for player {playerData.PlayerId}: {playerData.Position}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PlayerSync] Error processing initial state sync: {e.Message}");
            }
        }

        /// <summary>
        /// Start sending position updates (called after join confirmation)
        /// </summary>
        public void StartSendingUpdates()
        {
            GungeonTogether.Logging.Debug.Log("[PlayerSync] Starting to send position updates after join confirmation");

            // Force an immediate position update to let the host know where we are
            if (localPlayer != null)
            {
                var currentPosition = localPlayer.transform.position;
                var currentRotation = localPlayer.transform.eulerAngles.z;
                var currentGrounded = localPlayer.IsGrounded;
                var currentDodgeRolling = localPlayer.IsDodgeRolling;
                localMapName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                var characterInfo = GetCurrentPlayerCharacter();
                NetworkManager.Instance.OpeningPlayerPacket(
                    currentPosition,
                    localPlayer.specRigidbody?.Velocity ?? Vector2.zero,
                    currentRotation,
                    currentGrounded,
                    currentDodgeRolling,
                    localMapName,
                    characterInfo.CharacterId,
                    characterInfo.CharacterName
                );

                // Update our tracking variables
                lastSentPosition = currentPosition;
                lastSentRotation = currentRotation;
                lastSentGrounded = currentGrounded;
                lastSentDodgeRolling = currentDodgeRolling;
                lastPositionSentTime = Time.time;

                GungeonTogether.Logging.Debug.Log($"[PlayerSync] Sent immediate position update: {currentPosition} in map {localMapName}");
            }
        }
    }
}