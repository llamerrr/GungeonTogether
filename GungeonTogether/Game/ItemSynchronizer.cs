using GungeonTogether.Steam;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.Game
{
    /// <summary>
    /// Synchronizes item/chest reward drops and pickups between host and clients.
    /// Host is authoritative for which items exist for configured synced categories.
    /// </summary>
    public class ItemSynchronizer
    {
        private static ItemSynchronizer _instance;
        public static ItemSynchronizer Instance => _instance ??= new ItemSynchronizer();

        // Track items by instance ID -> metadata
    private readonly Dictionary<int, TrackedItem> _trackedItems = new Dictionary<int, TrackedItem>();

        // Configuration: categories to sync vs duplicate
    private readonly HashSet<ItemCategory> _syncedCategories = new HashSet<ItemCategory>();
    private readonly HashSet<ItemCategory> _duplicatedCategories = new HashSet<ItemCategory>();

        private bool _initialized;
        private bool _isHost;
    private float _lastScanTime;
    private const float SCAN_INTERVAL = 0.5f; // seconds for primary pickup component scan
    private const float FALLBACK_SCAN_INTERVAL = 2.0f; // slower interval for broad fallback scan
    private float _lastFallbackScanTime;

    // Cache classification per pickupId/instance to avoid repeat reflection
        private readonly Dictionary<int, ItemCategory> _categoryCache = new Dictionary<int, ItemCategory>();
        private readonly Dictionary<int, ItemMeta> _metadataCache = new Dictionary<int, ItemMeta>();

        private struct ItemMeta
        {
            public int SpriteId;
            public int Quality;
        }

        private ItemSynchronizer() { }

        public enum ItemCategory
        {
            Gun = 0,
            Passive = 1,
            Active = 2,
            Consumable = 3,
            Currency = 4,
            Key = 5,
            Heart = 6,
            Armor = 7,
            Other = 99
        }

        private struct TrackedItem
        {
            public int InstanceId;
            public int ItemId; // internal ETG pickup ID if known
            public ItemCategory Category;
            public GameObject GameObject;
            public bool PickedUp;
        }

        /// <summary>
        /// Initialize with role + config string (comma separated categories to sync)
        /// </summary>
    public void Initialize(bool isHost, string syncedCategoryList, string duplicatedCategoryList = null)
        {
            if (_initialized)
            {
                // Use Reconfigure if already initialized
        Reconfigure(isHost, syncedCategoryList, duplicatedCategoryList);
                return;
            }
            _isHost = isHost;
            _syncedCategories.Clear();
            ParseSyncedCategories(syncedCategoryList);
        _duplicatedCategories.Clear();
        ParseDuplicatedCategories(duplicatedCategoryList);
            _initialized = true;
            GungeonTogether.Logging.Debug.Log($"[ItemSync] Initialized as {(isHost ? "HOST" : "CLIENT")} | Synced: {JoinCategories(_syncedCategories)}");
        }

        /// <summary>
        /// Reconfigure host/client role or synced categories at runtime (e.g., after starting to host)
        /// </summary>
        public void Reconfigure(bool isHost, string syncedCategoryList, string duplicatedCategoryList = null)
        {
            _isHost = isHost;
            _syncedCategories.Clear();
            ParseSyncedCategories(syncedCategoryList);
            _duplicatedCategories.Clear();
            ParseDuplicatedCategories(duplicatedCategoryList);
            // Do not clear tracked items when switching to host to avoid duplicate spawn packets
            if (!_isHost)
            {
                // On becoming client, purge tracked items so they can come from host
                _trackedItems.Clear();
            }
            GungeonTogether.Logging.Debug.Log($"[ItemSync] Reconfigured as {(isHost ? "HOST" : "CLIENT")} | Synced: {JoinCategories(_syncedCategories)}");
        }

        public void Update()
        {
            if (!_initialized) return;
            // Host performs regular scanning for new items
            if (_isHost)
            {
                if (Time.time - _lastScanTime >= SCAN_INTERVAL)
                {
                    ScanForNewItems();
                    _lastScanTime = Time.time;
                }
            }

            // All peers run passive pickup detection (fallback when Harmony patch not applied)
            DetectPickedUpItems();
        }

        private static bool IsNullOrWhiteSpace(string s)
        {
            return string.IsNullOrEmpty(s) || s.Trim().Length == 0;
        }

        private static string JoinCategories(IEnumerable<ItemCategory> cats)
        {
            var parts = new List<string>();
            foreach (var c in cats) parts.Add(c.ToString());
            return string.Join(",", parts.ToArray());
        }

        private bool TryParseCategory(string token, out ItemCategory cat)
        {
            cat = ItemCategory.Other;
            if (string.IsNullOrEmpty(token)) return false;
            try
            {
                foreach (ItemCategory v in Enum.GetValues(typeof(ItemCategory)))
                {
                    if (string.Equals(v.ToString(), token, StringComparison.OrdinalIgnoreCase))
                    {
                        cat = v;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void ParseSyncedCategories(string list)
        {
            if (IsNullOrWhiteSpace(list))
            {
                // Default: sync guns, passives, actives
                _syncedCategories.Add(ItemCategory.Gun);
                _syncedCategories.Add(ItemCategory.Passive);
                _syncedCategories.Add(ItemCategory.Active);
                return;
            }
            var tokens = list.Split(',');
            foreach (var raw in tokens)
            {
                var token = raw.Trim();
                ItemCategory cat;
                if (TryParseCategory(token, out cat)) _syncedCategories.Add(cat);
            }
        }

        private void ParseDuplicatedCategories(string list)
        {
            if (IsNullOrWhiteSpace(list)) return;
            var tokens = list.Split(',');
            foreach (var raw in tokens)
            {
                var token = raw.Trim();
                ItemCategory cat;
                if (TryParseCategory(token, out cat))
                {
                    if (!_syncedCategories.Contains(cat)) _duplicatedCategories.Add(cat);
                }
            }
        }

        private void ScanForNewItems()
        {
            try
            {
                // Heuristic: find all loot/pickup objects that have a specRigidbody & tk2dSprite and are tagged appropriately.
                // Targeted search: try to get all PickupObject components via reflection to narrow set
                var pickupType = Type.GetType("PickupObject, Assembly-CSharp");
                if (pickupType != null)
                {
                    var comps = UnityEngine.Object.FindObjectsOfType(typeof(Component));
                    foreach (var obj in comps)
                    {
                        if (obj == null) continue;
                        var comp = obj as Component;
                        if (comp == null) continue;
                        if (!pickupType.IsAssignableFrom(comp.GetType())) continue;
                        var go = comp.gameObject;
                        ProcessPotentialItem(go);
                    }
                }
                else
                {
                    if (Time.time - _lastFallbackScanTime >= FALLBACK_SCAN_INTERVAL)
                    {
                        _lastFallbackScanTime = Time.time;
                        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                        foreach (var go in allObjects)
                        {
                            ProcessPotentialItem(go);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ItemSync] Scan error: {e.Message}");
            }
        }

        /// <summary>
        /// Fallback detection of item pickups without relying on a Harmony postfix on PickupObject.Pickup.
        /// Heuristics: tracked GameObject destroyed, deactivated, or parented under a player object.
        /// </summary>
        private void DetectPickedUpItems()
        {
            if (_trackedItems.Count == 0) return;
            List<int> ids = null;
            try
            {
                ids = new List<int>(_trackedItems.Keys);
            }
            catch { return; }
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                TrackedItem tracked;
                if (!_trackedItems.TryGetValue(id, out tracked)) continue;
                if (tracked.PickedUp) continue;
                var go = tracked.GameObject;
                bool picked = false;
                if (go == null)
                {
                    picked = true; // destroyed
                }
                else
                {
                    try
                    {
                        if (!go.activeInHierarchy) picked = true;
                        else if (go.transform != null && go.transform.parent != null)
                        {
                            string pname = go.transform.parent.name.ToLowerInvariant();
                            if (pname.Contains("player")) picked = true;
                        }
                    }
                    catch { picked = true; }
                }
                if (!picked) continue;

                // Mark picked locally to avoid repeat; send network if needed
                tracked.PickedUp = true;
                _trackedItems[id] = tracked;

                bool isSynced = _syncedCategories.Contains(tracked.Category);
                bool isDuplicated = _duplicatedCategories.Contains(tracked.Category);
                int key = tracked.ItemId != 0 ? tracked.ItemId : id;
                ItemMeta meta;
                if (!_metadataCache.TryGetValue(key, out meta)) meta = new ItemMeta { SpriteId = 0, Quality = 0 };

                if (isSynced)
                {
                    try
                    {
                        NetworkManager.Instance.SendItemPickup(key, (int)tracked.Category, SteamReflectionHelper.GetLocalSteamId(), meta.SpriteId, meta.Quality, 0, 0, 0);
                    }
                    catch (Exception e)
                    {
                        GungeonTogether.Logging.Debug.LogError("[ItemSync] Fallback pickup send failed: " + e.Message);
                    }
                }
                else if (isDuplicated)
                {
                    // For duplicated categories we keep available for other players; mark unpicked again locally
                    tracked.PickedUp = false; // allow others to also trigger when their instance disappears
                    _trackedItems[id] = tracked;
                }
            }
        }

        private void ProcessPotentialItem(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;
            if (go.GetComponent<tk2dBaseSprite>() == null) return; // needs a sprite
            int id = go.GetInstanceID();
            if (_trackedItems.ContainsKey(id)) return;

            var cat = Classify(go, out int pickupId, out int spriteId, out int quality, out int ammo, out int maxAmmo, out int charges);
            bool isSynced = _syncedCategories.Contains(cat);
            bool isDuplicated = _duplicatedCategories.Contains(cat);
            if (!isSynced && !isDuplicated) return;

            _categoryCache[pickupId != 0 ? pickupId : id] = cat;
            _metadataCache[pickupId != 0 ? pickupId : id] = new ItemMeta { SpriteId = spriteId, Quality = quality };

            _trackedItems[id] = new TrackedItem
            {
                InstanceId = id,
                ItemId = pickupId,
                Category = cat,
                GameObject = go,
                PickedUp = false
            };
            if (isSynced && _isHost)
            {
                NetworkManager.Instance.SendItemSpawn(pickupId != 0 ? pickupId : id, go.transform.position, (int)cat, spriteId, quality, ammo, maxAmmo, charges);
            }
        }

        private ItemCategory Classify(GameObject go, out int pickupId, out int spriteId, out int quality, out int ammo, out int maxAmmo, out int charges)
        {
            pickupId = 0;
            spriteId = 0;
            quality = 0;
            ammo = 0;
            maxAmmo = 0;
            charges = 0;
            try
            {
                // Try reflection for a PickupObject component (Enter the Gungeon class name)
                var pickupType = Type.GetType("PickupObject, Assembly-CSharp");
                if (pickupType != null)
                {
                    var comp = go.GetComponent(pickupType);
                    if (comp != null)
                    {
                        // read pickupId field if exists
                        var idField = pickupType.GetField("PickupObjectId");
                        if (idField != null)
                        {
                            var val = idField.GetValue(comp);
                            if (val is int intId) pickupId = intId;
                        }
                        var qualityField = pickupType.GetField("quality") ?? pickupType.GetField("Quality");
                        if (qualityField != null)
                        {
                            var qv = qualityField.GetValue(comp);
                            if (qv is int qInt) quality = qInt;
                        }
                        // Gun specific data via Gun type
                        var gunType = Type.GetType("Gun, Assembly-CSharp");
                        if (gunType != null && gunType.IsAssignableFrom(comp.GetType()))
                        {
                            var ammoField = gunType.GetField("CurrentAmmo") ?? gunType.GetField("m_currentAmmo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var maxAmmoField = gunType.GetField("AdjustedMaxAmmo") ?? gunType.GetField("m_maxAmmo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (ammoField != null)
                            {
                                var av = ammoField.GetValue(comp);
                                if (av is int aInt) ammo = aInt;
                            }
                            if (maxAmmoField != null)
                            {
                                var mv = maxAmmoField.GetValue(comp);
                                if (mv is int mInt) maxAmmo = mInt;
                            }
                        }
                        // Active item charges (PlayerItem type in ETG)
                        var playerItemType = Type.GetType("PlayerItem, Assembly-CSharp");
                        if (playerItemType != null && playerItemType.IsAssignableFrom(comp.GetType()))
                        {
                            var remainingChargesField = playerItemType.GetField("remainingUses") ?? playerItemType.GetField("RemainingUses");
                            if (remainingChargesField != null)
                            {
                                var cv = remainingChargesField.GetValue(comp);
                                if (cv is int cInt) charges = cInt;
                            }
                        }
                        // attempt sprite id via tk2dBaseSprite
                        var sprite = go.GetComponent<tk2dBaseSprite>();
                        if (sprite != null) spriteId = sprite.spriteId;

                        if (comp.GetType().Name == "Gun") return ItemCategory.Gun;
                        var typeNameLower = comp.GetType().Name.ToLowerInvariant();
                        if (typeNameLower.Contains("passive")) return ItemCategory.Passive;
                        if (typeNameLower.Contains("active")) return ItemCategory.Active;
                    }
                }

                // Fallback heuristics
                var nameLower = go.name.ToLowerInvariant();
                if (nameLower.Contains("gun")) return ItemCategory.Gun;
                if (nameLower.Contains("key")) return ItemCategory.Key;
                if (nameLower.Contains("heart")) return ItemCategory.Heart;
                if (nameLower.Contains("armor")) return ItemCategory.Armor;
                if (nameLower.Contains("ammo") || nameLower.Contains("blank")) return ItemCategory.Consumable;
            }
            catch { }
            return ItemCategory.Other;
        }

        public void OnItemSpawnReceived(ItemData data)
        {
            if (_isHost) return; // host already has authoritative object
            if (!_syncedCategories.Contains((ItemCategory)data.ItemType)) return;

            // Avoid duplicating existing
            foreach (var kvp in _trackedItems)
            {
                if (kvp.Value.ItemId == data.ItemId) return; // already have
            }

            // Client should spawn a placeholder / attempt to replicate appearance via reflection if possible
            try
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"RemoteItem_{data.ItemId}";
                go.transform.position = data.Position;
                // Scale/marker for debug
                go.transform.localScale = Vector3.one * 0.6f;
                var marker = go.AddComponent<RemoteItemMarker>();
                marker.ItemId = data.ItemId;
                marker.Category = (int)data.ItemType;
                marker.Quality = data.Quality;
                _trackedItems[go.GetInstanceID()] = new TrackedItem
                {
                    InstanceId = go.GetInstanceID(),
                    ItemId = data.ItemId,
                    Category = (ItemCategory)data.ItemType,
                    GameObject = go,
                    PickedUp = false
                };
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[ItemSync] Client spawn replicate error: {e.Message}");
            }
        }

        public void OnItemPickupReceived(ItemData data)
        {
            // Remove/despawn item locally if tracked
            var toRemove = new List<int>();
            foreach (var kvp in _trackedItems)
            {
                if (kvp.Value.ItemId == data.ItemId || kvp.Key == data.ItemId)
                {
                    if (kvp.Value.GameObject != null)
                    {
                        UnityEngine.Object.Destroy(kvp.Value.GameObject);
                    }
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove) _trackedItems.Remove(id);
        }

        /// <summary>
        /// Called by patch when a local player picks up an item.
        /// </summary>
        public void NotifyLocalPickup(GameObject go)
        {
            if (!_initialized) return;
            int inst = go.GetInstanceID();
            if (!_trackedItems.TryGetValue(inst, out var tracked)) return; // not syncing this item
            if (tracked.PickedUp) return;
            tracked.PickedUp = true;
            _trackedItems[inst] = tracked;
            bool isSynced = _syncedCategories.Contains(tracked.Category);
            bool isDuplicated = _duplicatedCategories.Contains(tracked.Category);

            int key = tracked.ItemId != 0 ? tracked.ItemId : inst;
            ItemMeta meta;
            if (!_metadataCache.TryGetValue(key, out meta))
            {
                meta = new ItemMeta { SpriteId = 0, Quality = 0 };
            }

            if (isSynced)
            {
                // Broadcast removal
                NetworkManager.Instance.SendItemPickup(key, (int)tracked.Category, SteamReflectionHelper.GetLocalSteamId(), meta.SpriteId, meta.Quality, 0, 0, 0);
            }
            else if (isDuplicated)
            {
                // Local duplication logic: do nothing network-wise; allow other players to still pick
                tracked.PickedUp = false; // keep available for others (other clients track their own GameObject instance)
            }
        }
    }
}
