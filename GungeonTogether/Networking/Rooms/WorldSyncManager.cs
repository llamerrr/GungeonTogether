using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Packets;

namespace GungeonTogether.Networking.Sync
{
    public class WorldSyncManager : MonoBehaviour
    {
        private static WorldSyncManager _instance;
        public static WorldSyncManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("WorldSyncManager");
                    _instance = go.AddComponent<WorldSyncManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private string _lastWorldState = ""; // string cache to detect changes

        private void Awake()
        {
            ETGReflectionHelper.Initialise();
        }

        private void Update()
        {
            if (!NetworkManager.Instance.IsHost && !NetworkManager.Instance.IsClient) return;

            if (NetworkManager.Instance.IsHost)
            {
                SyncHostWorldState();
            }
        }

        private void SyncHostWorldState()
        {
            var gm = ETGReflectionHelper.GetGameManager();
            if (gm == null) return;

            bool isFoyer = ETGReflectionHelper.IsInFoyer();
            int floorIndex = ETGReflectionHelper.GetCurrentFloorIndex();
            string roomId = ETGReflectionHelper.GetCurrentRoomIdentifier();
            Vector3 pos = ETGReflectionHelper.GetPlayerPosition();
            float rot = ETGReflectionHelper.GetPlayerRotation();

            // Build a unique string representing the state
            string currentState = $"{isFoyer}|{floorIndex}|{roomId}|{pos.x:F2}|{pos.y:F2}";

            if (currentState != _lastWorldState)
            {
                _lastWorldState = currentState;
                Debug.Log($"[WorldSync] Host world state changed: isFoyer={isFoyer}, floor={floorIndex}, room={roomId}, pos={pos}");

                var packet = new WorldStatePacket
                {
                    IsFoyer = isFoyer,
                    FloorIndex = floorIndex,
                    RoomIdentifier = roomId,
                    Position = new Vector2(pos.x, pos.y),
                    Rotation = rot
                };
                NetworkManager.Instance.Host.Broadcast(packet, reliable: true);
            }
        }

        // Client applies the world state when packet arrives (handled in NetworkManager)
        public void ApplyWorldState(WorldStatePacket packet)
        {
            Debug.Log($"[WorldSync] Client applying world state: isFoyer={packet.IsFoyer}, floor={packet.FloorIndex}, room={packet.RoomIdentifier}, pos={packet.Position}");

            // If we need to change floor or foyer
            if (packet.IsFoyer)
            {
                // Only teleport if not already in foyer
                if (!ETGReflectionHelper.IsInFoyer())
                {
                    ETGReflectionHelper.TeleportToFoyer();
                    // We need to wait for the load to complete before setting position.
                    // We'll use a coroutine or delay; for simplicity, we'll set position after a short delay.
                    StartCoroutine(SetPositionAfterDelay(packet.Position, packet.Rotation, 0.5f));
                }
                else
                {
                    // Already in foyer, just move
                    ETGReflectionHelper.TeleportToPosition(packet.Position, packet.Rotation);
                }
            }
            else
            {
                int currentFloor = ETGReflectionHelper.GetCurrentFloorIndex();
                if (currentFloor != packet.FloorIndex)
                {
                    ETGReflectionHelper.TeleportToFloor(packet.FloorIndex);
                    StartCoroutine(SetPositionAfterDelay(packet.Position, packet.Rotation, 1.0f)); // floor load takes longer
                }
                else
                {
                    // Same floor, just move position (and possibly room)
                    ETGReflectionHelper.TeleportToPosition(packet.Position, packet.Rotation);
                    // Optionally, we could force room entry if we have room identifier
                }
            }
        }

        private System.Collections.IEnumerator SetPositionAfterDelay(Vector2 pos, float rot, float delay)
        {
            yield return new WaitForSeconds(delay);
            ETGReflectionHelper.TeleportToPosition(pos, rot);
        }
    }
}