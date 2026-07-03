using System.Collections.Generic;
using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Networking.Packets;
using Dungeonator;

public class RoomSyncManager : MonoBehaviour
{
    private static RoomSyncManager _instance;
    public static RoomSyncManager Instance => _instance ??= new GameObject("RoomSyncManager").AddComponent<RoomSyncManager>();

    private string _currentRoomName = "";
    private float _lastSyncTime = 0f;
    private const float SYNC_INTERVAL = 0.2f;

    private void Awake() { DontDestroyOnLoad(gameObject); }

    private void Start()
    {
        ETGReflectionHelper.Initialise();
    }

    private void Update()
    {
        if (!NetworkManager.Instance.IsHost && !NetworkManager.Instance.IsClient) return;

        // Host: sync room state
        if (NetworkManager.Instance.IsHost)
        {
            SyncHost();
        }

        // Client: handle incoming sync packets (already handled via NetworkManager)
    }

    private void SyncHost()
    {
        // Check if room changed
        var room = ETGReflectionHelper.GetCurrentRoomHandler();
        if (room == null) return;

        var roomHandler = room as RoomHandler;
        if (roomHandler == null) return;

        string roomName = roomHandler.GetRoomName();

        if (roomName != _currentRoomName)
        {
            _currentRoomName = roomName;
            // Broadcast RoomChangePacket
            NetworkManager.Instance.Host.Broadcast(new RoomChangePacket { RoomName = roomName });

            // Spawn enemies in this room and broadcast
            SpawnAndBroadcastEnemies(roomHandler);
        }

        // Sync enemy states periodically
        if (Time.time - _lastSyncTime > SYNC_INTERVAL)
        {
            _lastSyncTime = Time.time;
            SyncEnemyStates();
        }
    }

    private void SpawnAndBroadcastEnemies(RoomHandler room)
    {
        var enemies = ETGReflectionHelper.GetActiveEnemies();
        foreach (var enemy in enemies)
        {
            var enemyComponent = enemy as Component;
            if (enemyComponent == null) continue;

            // Assign a network ID if not already assigned
            int id = GetOrAssignEnemyId(enemyComponent);
            Vector3 pos = enemyComponent.transform.position;
            float rot = enemyComponent.transform.eulerAngles.z;
            int health = ETGReflectionHelper.GetEnemyHealth(enemyComponent);
            string prefabName = enemyComponent.name; // or a more robust type identifier

            var packet = new EnemySpawnPacket
            {
                EnemyId = id,
                PrefabName = prefabName,
                Position = new Vector2(pos.x, pos.y),
                Rotation = rot,
                Health = health
            };
            NetworkManager.Instance.Host.Broadcast(packet, reliable: true);
        }
    }

    private void SyncEnemyStates()
    {
        var enemies = ETGReflectionHelper.GetActiveEnemies();
        foreach (var enemy in enemies)
        {
            var enemyComponent = enemy as Component;
            if (enemyComponent == null) continue;

            int id = GetOrAssignEnemyId(enemyComponent);
            Vector3 pos = enemyComponent.transform.position;
            float rot = enemyComponent.transform.eulerAngles.z;
            int health = ETGReflectionHelper.GetEnemyHealth(enemyComponent);
            int aiState = GetEnemyAIState(enemyComponent); // implement reflection

            var packet = new EnemyStatePacket
            {
                EnemyId = id,
                Position = new Vector2(pos.x, pos.y),
                Rotation = rot,
                Health = health,
                AIState = aiState
            };
            NetworkManager.Instance.Host.Broadcast(packet, reliable: false);
        }
    }

    private Dictionary<Component, int> _enemyIdMap = new Dictionary<Component, int>();

    private int GetOrAssignEnemyId(Component enemy)
    {
        if (_enemyIdMap.TryGetValue(enemy, out int id)) return id;
        id = NetworkEntityManager.Instance.AssignId(enemy);
        _enemyIdMap[enemy] = id;
        return id;
    }

    private int GetEnemyAIState(object enemy)
    {
        // Use reflection to get AI state (e.g., EnemyController.m_aiState)
        return 0; // placeholder
    }

    // Client side: handle packets via NetworkManager
}