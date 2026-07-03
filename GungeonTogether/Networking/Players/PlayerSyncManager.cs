using UnityEngine;
using GungeonTogether.Networking.Packets;
using GungeonTogether.Networking.Steam;
using System.Reflection;

namespace GungeonTogether.Networking.Sync
{
    public class PlayerSyncManager : MonoBehaviour
    {
        private static PlayerSyncManager _instance;
        public static PlayerSyncManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("PlayerSyncManager");
                    _instance = go.AddComponent<PlayerSyncManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private float _lastSyncTime = 0f;
        private const float SYNC_INTERVAL = 0.5f;

        private void Awake()
        {
            ETGReflectionHelper.Initialise();
        }

        private void Update()
        {
            if (!NetworkManager.Instance.IsHost && !NetworkManager.Instance.IsClient) return;

            if (NetworkManager.Instance.IsHost)
            {
                if (Time.time - _lastSyncTime > SYNC_INTERVAL)
                {
                    _lastSyncTime = Time.time;
                    BroadcastPlayerState();
                }
            }
        }

        private void BroadcastPlayerState()
        {
            var gm = ETGReflectionHelper.GetGameManager();
            if (gm == null) return;

            ulong localId = SteamReflectionHelper.GetLocalSteamId();
            float health = ETGReflectionHelper.GetPlayerHealth();
            float maxHealth = ETGReflectionHelper.GetPlayerMaxHealth();
            float armor = ETGReflectionHelper.GetPlayerArmor();
            float maxArmor = ETGReflectionHelper.GetPlayerMaxArmor();
            int ammo = ETGReflectionHelper.GetPlayerAmmo();
            int maxAmmo = ETGReflectionHelper.GetPlayerMaxAmmo();
            int gunIndex = ETGReflectionHelper.GetPlayerCurrentGunIndex();
            string activeItem = ETGReflectionHelper.GetActiveItemName();

            var packet = new PlayerStatePacket
            {
                PlayerId = localId,
                Health = health,
                MaxHealth = maxHealth,
                Armor = armor,
                MaxArmor = maxArmor,
                Ammo = ammo,
                MaxAmmo = maxAmmo,
                CurrentGunIndex = gunIndex,
                ActiveItemName = activeItem
            };

            NetworkManager.Instance.Host.Broadcast(packet, reliable: false);
        }

        // Client apply
        public void ApplyPlayerState(PlayerStatePacket packet)
        {
            // Only apply if it's not our own player (host syncs, we are client)
            ulong local = SteamReflectionHelper.GetLocalSteamId();
            if (packet.PlayerId == local) return;

            Debug.Log($"[PlayerSync] Applying state for {packet.PlayerId}: health={packet.Health}, armor={packet.Armor}, gun={packet.CurrentGunIndex}");

            // For now, we only sync the local client's player? Actually we want to sync remote players too.
            // But we don't have remote player controllers yet. We'll just apply to local player for testing.
            // In a full implementation, you'd apply to the remote player objects.
            // For simplicity, we'll set the local player's stats to match the host's (since we're client).
            // However, we should NOT override local player if we are client and we receive from host.
            // Actually, this packet is broadcast by host to all clients. Clients should apply it to their local player
            // to sync with host's state.
            // So if we are client and we receive this packet from host, we set our own player to match.
            if (NetworkManager.Instance.IsClient)
            {
                var player = ETGReflectionHelper.GetPrimaryPlayer();
                if (player != null)
                {
                    // Set health, armor, ammo, etc. via reflection
                    SetPlayerStats(player, packet);
                }
            }
        }

        private void SetPlayerStats(object player, PlayerStatePacket packet)
        {
            // Use reflection to set health, armor, ammo, etc.
            // We need setter properties or fields.
            // For health: player.Health = packet.Health;
            // We can use the same properties we read, but use SetValue.
            var healthProp = player.GetType().GetProperty("Health", BindingFlags.Public | BindingFlags.Instance);
            if (healthProp != null && healthProp.CanWrite) healthProp.SetValue(player, packet.Health, null);
            var armorProp = player.GetType().GetProperty("Armor", BindingFlags.Public | BindingFlags.Instance);
            if (armorProp != null && armorProp.CanWrite) armorProp.SetValue(player, packet.Armor, null);
            // Similarly for ammo, etc.
            // For guns, we need to switch to the correct gun index.
            var gunInvField = player.GetType().GetField("GunInventory", BindingFlags.Public | BindingFlags.Instance);
            if (gunInvField != null)
            {
                var inv = gunInvField.GetValue(player) as System.Collections.IList;
                if (inv != null && packet.CurrentGunIndex >= 0 && packet.CurrentGunIndex < inv.Count)
                {
                    var gun = inv[packet.CurrentGunIndex];
                    var currentGunField = player.GetType().GetField("CurrentGun", BindingFlags.Public | BindingFlags.Instance);
                    if (currentGunField != null) currentGunField.SetValue(player, gun);
                }
            }
        }
    }
}