using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Packets;
using System.Collections;

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
            if (LoadingSyncManager.Instance != null && LoadingSyncManager.Instance.IsClientLoading)
            {
                StartCoroutine(DelayedApply(packet));
            }
            else
            {
                ApplyNow(packet);
            }
            // If we are loading, delay teleport until done
            if (LoadingSyncManager.Instance.IsClientLoading)
            {
                Debug.Log("[WorldSync] Client is loading, delaying world state apply.");
                StartCoroutine(DelayedApply(packet));
                return;
            }
            ApplyNow(packet);
        }

        private IEnumerator DelayedApply(WorldStatePacket packet)
        {
            while (LoadingSyncManager.Instance.IsClientLoading)
                yield return new WaitForSeconds(0.1f);
            ApplyNow(packet);
        }

        private void ApplyNow(WorldStatePacket packet)
        {
            Debug.Log($"[WorldSync] Client applying world state: isFoyer={packet.IsFoyer}, floor={packet.FloorIndex}, room={packet.RoomIdentifier}, pos={packet.Position}");

            // If we need to change floor or foyer
            if (packet.IsFoyer)
            {
                if (!ETGReflectionHelper.IsInFoyer())
                {
                    ETGReflectionHelper.TeleportToFoyer();
                    StartCoroutine(SetPositionAfterDelay(packet.Position, packet.Rotation, 0.5f));
                }
                else
                {
                    ETGReflectionHelper.TeleportToPosition(packet.Position, packet.Rotation);
                }
            }
            else
            {
                int currentFloor = ETGReflectionHelper.GetCurrentFloorIndex();
                if (currentFloor != packet.FloorIndex)
                {
                    ETGReflectionHelper.TeleportToFloor(packet.FloorIndex);
                    StartCoroutine(SetPositionAfterDelay(packet.Position, packet.Rotation, 1.0f));
                }
                else
                {
                    ETGReflectionHelper.TeleportToPosition(packet.Position, packet.Rotation);
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