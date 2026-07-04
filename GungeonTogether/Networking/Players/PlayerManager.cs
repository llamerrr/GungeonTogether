using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Networking.Packets;

namespace GungeonTogether.Networking
{
    public class PlayerManager : MonoBehaviour
    {
        private static PlayerManager _instance;
        public static PlayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("PlayerManager");
                    _instance = go.AddComponent<PlayerManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<ulong, RemotePlayerAvatar> _remotePlayers = new Dictionary<ulong, RemotePlayerAvatar>();

        public PlayerPositionPacket CreateLocalPositionPacket(ulong playerId)
        {
            GameManager gameManager = Object.FindObjectOfType<GameManager>();
            PlayerController player = gameManager != null ? gameManager.PrimaryPlayer : null;
            if (player == null) return null;

            Vector3 pos3 = player.transform.position;
            var packet = new PlayerPositionPacket
            {
                PlayerId = playerId,
                Position = new Vector2(pos3.x, pos3.y),
                Velocity = Vector2.zero,
                Rotation = player.transform.eulerAngles.z,
                IsGrounded = true,
                IsDodgeRolling = player.IsDodgeRolling,
                AnimationState = player.spriteAnimator != null ? player.spriteAnimator.CurrentFrame : 0,
                SpriteId = player.sprite != null ? player.sprite.spriteId : -1,
                FlipX = player.sprite != null && player.sprite.FlipX
            };

            return packet;
        }

        public void SpawnRemotePlayer(ulong steamId, Vector2 position, float rotation)
        {
            if (_remotePlayers.ContainsKey(steamId)) return;

            RemotePlayerAvatar player = RemotePlayerAvatar.Create(steamId, position, rotation);
            _remotePlayers[steamId] = player;
            Debug.Log($"[PlayerManager] Spawned remote player {steamId} at {position}");
        }

        public void UpdateRemotePlayer(ulong steamId, Vector2 position, float rotation, int spriteId = -1, bool flipX = false)
        {
            if (!_remotePlayers.ContainsKey(steamId))
            {
                SpawnRemotePlayer(steamId, position, rotation);
            }

            RemotePlayerAvatar player;
            if (_remotePlayers.TryGetValue(steamId, out player))
            {
                player.Apply(position, rotation, spriteId, flipX);
            }
        }

        public void RemoveRemotePlayer(ulong steamId)
        {
            RemotePlayerAvatar player;
            if (_remotePlayers.TryGetValue(steamId, out player))
            {
                Destroy(player.gameObject);
                _remotePlayers.Remove(steamId);
                Debug.Log($"[PlayerManager] Removed remote player {steamId}");
            }
        }

        public void ClearAll()
        {
            foreach (var kvp in _remotePlayers)
                Destroy(kvp.Value.gameObject);
            _remotePlayers.Clear();
        }
    }
}
