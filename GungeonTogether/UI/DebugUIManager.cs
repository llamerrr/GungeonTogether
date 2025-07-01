using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Dungeonator;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Debug UI Manager for GungeonTogether
    /// Provides comprehensive debugging tools with tabbed interface
    /// F1: Toggle full debug UI with tabs for Dungeon, Enemies, Variables, Seeds, and Network
    /// </summary>
    public class DebugUIManager : MonoBehaviour
    {
        private static DebugUIManager _instance;
        public static DebugUIManager Instance => _instance;

        [Header("Debug UI Settings")]
        public bool debugUIEnabled = false;
        public KeyCode toggleKey = KeyCode.F1;
        
        // UI State
        private Vector2 scrollPos = Vector2.zero;
        private string searchFilter = "";
        private int selectedTab = 0;
        private readonly string[] tabs = { "Dungeon", "Enemies", "Variables", "Seeds", "Network" };
        
        // Window properties
        private Rect windowRect = new Rect(50, 50, 800, 600);
        private bool isDragging = false;
        
        // Cache for reflection lookups to improve performance
        private static Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private static Dictionary<Type, PropertyInfo[]> propertyCache = new Dictionary<Type, PropertyInfo[]>();
        
        // Network debug data
        private float networkUpdateTimer = 0f;
        private const float NETWORK_REFRESH_RATE = 1f; // Update network data every second
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                 UnityEngine.Debug.Log("[DebugUIManager] Debug UI Manager initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Update()
        {
            // Update network timer
            networkUpdateTimer += Time.unscaledDeltaTime;
        }
        
        void OnGUI()
        {
            if (!debugUIEnabled) return;
            
            // Create a dark theme style
            SetupDebugUIStyle();
            
            // Main debug window
            windowRect = GUI.Window(12345, windowRect, DrawDebugWindow, "GungeonTogether Debug Tools v1.0");
        }
        
        private void DrawDebugWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // Header with close button
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Debug Tools - {tabs[selectedTab]} Tab", GUI.skin.box);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(25)))
            {
                debugUIEnabled = false;
            }
            GUILayout.EndHorizontal();
            
            // Tab selection
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            
            // Search filter
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(50));
            searchFilter = GUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchFilter = "";
            }
            GUILayout.EndHorizontal();
            
            // Quick action buttons
            DrawQuickActions();
            
            GUILayout.Space(5);
            
            // Content area with scroll
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(450));
            
            try
            {
                switch (selectedTab)
                {
                    case 0: DrawDungeonTab(); break;
                    case 1: DrawEnemiesTab(); break;
                    case 2: DrawVariablesTab(); break;
                    case 3: DrawSeedsTab(); break;
                    case 4: DrawNetworkTab(); break;
                }
            }
            catch (Exception ex)
            {
                GUILayout.Label($"Error in tab {tabs[selectedTab]}: {ex.Message}", GUI.skin.box);
                GUILayout.Label($"Stack: {ex.StackTrace}", GUI.skin.textArea);
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            
            // Make window draggable
            GUI.DragWindow();
        }
        
        private void DrawQuickActions()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Quick Actions:", GUILayout.Width(100));
            
            // Save/Load buttons
            GUI.color = Color.green;
            if (GUILayout.Button("F6: Save Dungeon", GUILayout.Width(120)))
            {
                GungeonTogether.Debug.DungeonSaveLoad.SaveCurrentDungeon();
            }
            
            GUI.color = Color.cyan;
            if (GUILayout.Button("F5: Load Dungeon", GUILayout.Width(120)))
            {
                GungeonTogether.Debug.DungeonSaveLoad.LoadDungeon();
            }
            
            GUI.color = Color.yellow;
            if (GUILayout.Button("F7: Compare", GUILayout.Width(100)))
            {
                GungeonTogether.Debug.DungeonSaveLoad.CompareDungeonWithSaved();
            }
            
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        
        #region Tab Drawing Methods
        
        private void DrawDungeonTab()
        {
            GUILayout.Label("=== DUNGEON DEBUG ===", GUI.skin.box);
            
            if (GameManager.Instance?.Dungeon != null)
            {
                var dungeon = GameManager.Instance.Dungeon;
                
                // Basic dungeon info
                DrawInfoBox("Basic Information", () => {
                    DrawLabelValue("Dungeon Seed", dungeon.GetDungeonSeed().ToString());
                    DrawLabelValue("Dimensions", $"{dungeon.Width} x {dungeon.Height}");
                    DrawLabelValue("Floor Name", dungeon.DungeonFloorName ?? "Unknown");
                    DrawLabelValue("Tileset ID", dungeon.tileIndices?.tilesetId.ToString() ?? "Unknown");
                    DrawLabelValue("Rooms Count", (dungeon.data?.rooms?.Count ?? 0).ToString());
                });
                
                // Room details
                if (dungeon.data?.rooms != null && dungeon.data.rooms.Count > 0)
                {
                    DrawInfoBox($"Room Information ({dungeon.data.rooms.Count} rooms)", () => {
                        foreach (var room in dungeon.data.rooms.Take(10)) // Limit to first 10 rooms
                        {
                            if (room == null) continue;
                            
                            string roomName = $"Room_{room.GetHashCode()}";
                            if (ShouldShow(roomName))
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label($"Room: {roomName}", GUILayout.Width(200));
                                if (room.area != null)
                                {
                                    GUILayout.Label($"Position: {room.area.basePosition}", GUILayout.Width(150));
                                    GUILayout.Label($"Size: {room.area.dimensions}", GUILayout.Width(100));
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                        
                        if (dungeon.data.rooms.Count > 10)
                        {
                            GUILayout.Label($"... and {dungeon.data.rooms.Count - 10} more rooms");
                        }
                    });
                }
            }
            else
            {
                GUILayout.Label("No dungeon available", GUI.skin.box);
            }
        }
        
        private void DrawEnemiesTab()
        {
            GUILayout.Label("=== ENEMIES DEBUG ===", GUI.skin.box);
            
            var enemies = FindObjectsOfType<AIActor>();
            
            DrawInfoBox($"Enemy Information ({enemies.Length} enemies)", () => {
                foreach (var enemy in enemies.Take(20)) // Limit to first 20 enemies
                {
                    if (enemy == null) continue;
                    
                    string enemyName = enemy.GetActorName() ?? enemy.name;
                    if (ShouldShow(enemyName))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Enemy: {enemyName}", GUILayout.Width(200));
                        
                        if (enemy.healthHaver != null)
                        {
                            float health = enemy.healthHaver.GetCurrentHealth();
                            float maxHealth = enemy.healthHaver.GetMaxHealth();
                            GUILayout.Label($"HP: {health:F1}/{maxHealth:F1}", GUILayout.Width(100));
                            
                            if (GUILayout.Button("Kill", GUILayout.Width(50)))
                            {
                                enemy.healthHaver.ApplyDamage(9999f, Vector2.zero, "Debug", CoreDamageTypes.None, DamageCategory.Normal);
                            }
                        }
                        else
                        {
                            GUILayout.Label("No HealthHaver", GUILayout.Width(100));
                        }
                        
                        GUILayout.EndHorizontal();
                    }
                }
                
                if (enemies.Length > 20)
                {
                    GUILayout.Label($"... and {enemies.Length - 20} more enemies");
                }
            });
            
            // Kill all enemies button
            GUILayout.BeginHorizontal();
            GUI.color = Color.red;
            if (GUILayout.Button("Kill All Enemies"))
            {
                foreach (var enemy in enemies)
                {
                    if (enemy?.healthHaver != null)
                    {
                        enemy.healthHaver.ApplyDamage(9999f, Vector2.zero, "Debug", CoreDamageTypes.None, DamageCategory.Normal);
                    }
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        
        private void DrawVariablesTab()
        {
            GUILayout.Label("=== VARIABLES DEBUG ===", GUI.skin.box);
            
            // GameManager variables
            if (GameManager.Instance != null)
            {
                DrawInfoBox("GameManager", () => {
                    DrawObjectReflection(GameManager.Instance, "GameManager.Instance");
                });
            }
            
            // PlayerController variables
            var players = FindObjectsOfType<PlayerController>();
            foreach (var player in players)
            {
                if (player != null)
                {
                    DrawInfoBox($"Player {player.PlayerIDX}", () => {
                        DrawObjectReflection(player, $"Player[{player.PlayerIDX}]");
                    });
                }
            }
        }
        
        private void DrawSeedsTab()
        {
            GUILayout.Label("=== SEEDS DEBUG ===", GUI.skin.box);
            
            DrawInfoBox("Random Number Generation", () => {
                if (GameManager.Instance?.Dungeon != null)
                {
                    var dungeon = GameManager.Instance.Dungeon;
                    DrawLabelValue("Current Dungeon Seed", dungeon.GetDungeonSeed().ToString());
                }
                
                // Unity's random state
                var randomState = UnityEngine.Random.state;
                DrawLabelValue("Unity Random State", randomState.ToString());
                
                // Sample some random values for testing
                GUILayout.Label("Random Test Values:");
                for (int i = 0; i < 5; i++)
                {
                    float randValue = UnityEngine.Random.value;
                    GUILayout.Label($"  Random[{i}]: {randValue:F6}");
                }
            });
            
            DrawInfoBox("Seed Management", () => {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset Random Seed"))
                {
                    UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
                     UnityEngine.Debug.Log("[DebugUI] Random seed reset");
                }
                if (GUILayout.Button("Set Seed to 12345"))
                {
                    UnityEngine.Random.InitState(12345);
                     UnityEngine.Debug.Log("[DebugUI] Random seed set to 12345");
                }
                GUILayout.EndHorizontal();
            });
        }
        
        private void DrawNetworkTab()
        {
            GUILayout.Label("=== NETWORK DEBUG ===", GUI.skin.box);
            
            var sessionManager = GungeonTogetherMod.Instance?._sessionManager;
            
            DrawInfoBox("Session Status", () => {
                if (sessionManager != null)
                {
                    DrawLabelValue("Session Active", sessionManager.IsActive.ToString());
                    DrawLabelValue("Is Host", sessionManager.IsHost.ToString());
                    DrawLabelValue("Status", sessionManager.Status ?? "Unknown");
                }
                else
                {
                    GUILayout.Label("No session manager available");
                }
            });
            
            // Steam networking info
            DrawInfoBox("Steam Networking", () => {
                try
                {
                    // Add Steam-specific debugging here
                    GUILayout.Label("Steam networking status would go here");
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"Steam info error: {ex.Message}");
                }
            });
        }
        
        #endregion
        
        #region Helper Methods
        
        private void DrawInfoBox(string title, System.Action content)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, GUI.skin.box);
            content?.Invoke();
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        
        private void DrawLabelValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}:", GUILayout.Width(150));
            GUILayout.Label(value ?? "null");
            GUILayout.EndHorizontal();
        }
        
        private void DrawObjectReflection(object obj, string objName)
        {
            if (obj == null)
            {
                GUILayout.Label($"{objName} is null");
                return;
            }
            
            Type type = obj.GetType();
            
            // Get cached fields or create them
            if (!fieldCache.ContainsKey(type))
            {
                fieldCache[type] = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(f => IsDisplayableType(f.FieldType))
                    .Take(20) // Limit to prevent UI overflow
                    .ToArray();
            }
            
            // Get cached properties or create them
            if (!propertyCache.ContainsKey(type))
            {
                propertyCache[type] = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && IsDisplayableType(p.PropertyType))
                    .Take(20) // Limit to prevent UI overflow
                    .ToArray();
            }
            
            // Display fields
            foreach (var field in fieldCache[type])
            {
                if (!ShouldShow(field.Name)) continue;
                
                try
                {
                    var value = field.GetValue(obj);
                    DrawLabelValue(field.Name, value?.ToString() ?? "null");
                }
                catch (Exception ex)
                {
                    DrawLabelValue(field.Name, $"Error: {ex.Message}");
                }
            }
            
            // Display properties
            foreach (var prop in propertyCache[type])
            {
                if (!ShouldShow(prop.Name)) continue;
                
                try
                {
                    var value = prop.GetValue(obj, null);
                    DrawLabelValue(prop.Name, value?.ToString() ?? "null");
                }
                catch (Exception ex)
                {
                    DrawLabelValue(prop.Name, $"Error: {ex.Message}");
                }
            }
        }
        
        private bool IsDisplayableType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(Vector2) || 
                   type == typeof(Vector3) || 
                   type == typeof(DateTime) ||
                   type.IsEnum;
        }
        
        private bool ShouldShow(string name)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;
            return name.ToLowerInvariant().Contains(searchFilter.ToLowerInvariant());
        }
        
        private void SetupDebugUIStyle()
        {
            // Setup dark theme for debug UI
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            GUI.contentColor = Color.white;
        }
        
        #endregion
        
        #region Static Methods
        
        /// <summary>
        /// Initialize the debug UI system
        /// </summary>
        public static void Initialize()
        {
            if (_instance == null)
            {
                // Create a GameObject for the debug UI manager
                GameObject debugUIObj = new GameObject("DebugUIManager");
                _instance = debugUIObj.AddComponent<DebugUIManager>();
                 UnityEngine.Debug.Log("[DebugUIManager] Debug UI system initialized");
            }
        }
        
        /// <summary>
        /// Toggle the visibility of the debug UI
        /// </summary>
        public static void ToggleVisibility()
        {
            if (_instance != null)
            {
                _instance.ToggleDebugUI();
            }
            else
            {
                 UnityEngine.Debug.LogWarning("[DebugUIManager] Cannot toggle visibility - Debug UI not initialized");
            }
        }
        
        #endregion
        
        #region Instance Methods
        
        private void ToggleDebugUI()
        {
            debugUIEnabled = !debugUIEnabled;
             UnityEngine.Debug.Log($"[DebugUIManager] Debug UI {(debugUIEnabled ? "enabled" : "disabled")}");
        }
        
        #endregion
        
        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
