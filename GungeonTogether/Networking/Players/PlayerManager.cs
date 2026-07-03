using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking.Steam;

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

        private Dictionary<ulong, GameObject> _remotePlayers = new Dictionary<ulong, GameObject>();

        public void SpawnRemotePlayer(ulong steamId, Vector2 position, float rotation)
        {
            if (_remotePlayers.ContainsKey(steamId)) return;

            // Create a simple cube or capsule for testing
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.transform.position = new Vector3(position.x, position.y, 0f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            player.name = $"RemotePlayer_{steamId}";

            // Add a label above it (optional)
            // For now, just a colored material to distinguish
            var renderer = player.GetComponent<Renderer>();
            renderer.material.color = Color.green;

            _remotePlayers[steamId] = player;
            Debug.Log($"[PlayerManager] Spawned remote player {steamId} at {position}");
        }

        public void UpdateRemotePlayer(ulong steamId, Vector2 position, float rotation)
        {
            if (_remotePlayers.TryGetValue(steamId, out GameObject player))
            {
                player.transform.position = new Vector3(position.x, position.y, 0f);
                player.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            }
        }

        public void RemoveRemotePlayer(ulong steamId)
        {
            if (_remotePlayers.TryGetValue(steamId, out GameObject player))
            {
                Destroy(player);
                _remotePlayers.Remove(steamId);
                Debug.Log($"[PlayerManager] Removed remote player {steamId}");
            }
        }

        public void ClearAll()
        {
            foreach (var kvp in _remotePlayers)
                Destroy(kvp.Value);
            _remotePlayers.Clear();
        }
    }
}