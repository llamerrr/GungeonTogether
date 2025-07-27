using System;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Network packet types for GungeonTogether multiplayer
    /// </summary>
    public enum PacketType : byte
    {
        // Connection management
        PlayerJoin = 1,
        PlayerLeave = 2,

        // Player state
        PlayerPosition = 10,
        PlayerMovement = 11,
        PlayerShootRequest = 12,  // Client requests to shoot (Client -> Server)
        PlayerReload = 13,
        PlayerHealth = 14,
        PlayerAnimation = 15,
        PlayerInteraction = 16,

        // Enemy state (Host -> Clients)
        EnemySpawn = 20,
        EnemyPosition = 21,
        EnemyMovement = 22,
        EnemyShooting = 23,
        EnemyHealth = 24,
        EnemyDeath = 25,
        EnemyAnimation = 26,
        EnemyPathUpdate = 27,

        // Dungeon/Room state (Host -> Clients)
        DungeonGenerated = 30,
        RoomEntered = 31,
        RoomCleared = 32,
        DoorOpened = 33,
        DoorClosed = 34,

        // Items and pickups
        ItemSpawn = 40,
        ItemPickup = 41,
        ItemDrop = 42,
        ChestOpened = 43,

        // Projectiles
        ProjectileSpawn = 50,
        ProjectileUpdate = 51,
        ProjectileDestroy = 52,

        // Game events
        GameStart = 60,
        GamePause = 61,
        GameEnd = 62,

        // Synchronization
        StateSync = 70,
        HeartBeat = 71,
        InitialStateSync = 72,  // New: Send complete game state to new players
        PlayerJoinConfirm = 73, // New: Confirm player joined successfully

        // Map/Scene sync
        MapSync = 100 // Add map/scene sync packet type
    }

    [Serializable]
    public struct NetworkPacket
    {
        public PacketType Type;
        public ulong SenderId;
        public ulong TargetSteamId; // 0 means send to all
        public float Timestamp;
        public byte[] Data;

        public NetworkPacket(PacketType type, ulong senderId, byte[] data)
        {
            Type = type;
            SenderId = senderId;
            TargetSteamId = 0UL; // Default to send to all
            Timestamp = Time.time;
            Data = data;
        }
    }

    [Serializable]
    public struct PlayerPositionData
    {
        public ulong PlayerId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public bool IsGrounded;
        public bool IsDodgeRolling;
        public string MapName; // Add map/scene name for sync
        public int CharacterId; // Character selection (maps to PlayableCharacters enum)
        public string CharacterName; // Character prefab name for sprite loading
        
        // Animation state data
        public PlayerAnimationState AnimationState;
        public Vector2 MovementDirection; // Normalized movement direction for directional animations
        public bool IsRunning; // Running vs walking
        public bool IsFalling; // Falling state
        public bool IsTakingDamage; // Taking damage animation
        public bool IsDead; // Death state
        public string CurrentAnimationName; // Current animation clip name for debugging
    }

    [Serializable]
    public enum PlayerAnimationState : byte
    {
        Idle = 0,
        Walking = 1,
        Running = 2,
        DodgeRolling = 3,
        Falling = 4,
        TakingDamage = 5,
        Dead = 6,
        Interacting = 7,
        Shooting = 8,
        Reloading = 9
    }

    [Serializable]
    public struct PlayerShootRequestData
    {
        public ulong PlayerId;
        public Vector2 Position;
        public Vector2 Direction;
        public int WeaponId;
        public bool IsCharging;
        public float ChargeAmount;
        public float RequestTimestamp;  // For validation
    }

    [Serializable]
    public struct PlayerShootingData
    {
        public ulong PlayerId;
        public Vector2 Position;
        public Vector2 Direction;
        public int WeaponId;
        public bool IsCharging;
        public float ChargeAmount;
    }

    [Serializable]
    public struct EnemyStateData
    {
        public int EnemyId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float Health;
        public int AnimationState;
        public bool IsActive;
    }

    [Serializable]
    public struct EnemyPositionData
    {
        public int EnemyId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float Health;
        public int AnimationState;
        public bool IsActive;
    }

    [Serializable]
    public struct EnemyShootingData
    {
        public int EnemyId;
        public Vector2 Position;
        public Vector2 Direction;
        public Vector2 TargetPosition;
        public int ProjectileId;
    }

    [Serializable]
    public struct EnemyPathData
    {
        public int EnemyId;
        public Vector2[] PathPoints;
        public int CurrentPathIndex;
        public float MovementSpeed;
        public float MoveSpeed;
        public bool IsPatrolling;
    }

    [Serializable]
    public struct ProjectileData
    {
        public int ProjectileId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public int ProjectileType;
        public ulong OwnerId;
        public bool IsPlayerProjectile;
    }

    [Serializable]
    public struct ProjectileSpawnData
    {
        public int ProjectileId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public ulong OwnerId;
        public bool IsPlayerProjectile;
        public float Damage;
        public int ProjectileType;
        public int WeaponId;  // What weapon spawned this
        public bool IsServerAuthoritative;  // Server-spawned vs client visual
    }

    [Serializable]
    public struct ProjectileUpdateData
    {
        public int ProjectileId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public bool IsDestroyed;
    }

    [Serializable]
    public struct DungeonSyncData
    {
        public int Seed;
        public string FloorName;
        public Vector2[] RoomPositions;
        public int[] RoomTypes;
        public Vector2 PlayerSpawnPosition;
    }

    [Serializable]
    public struct RoomStateData
    {
        public Vector2 RoomPosition;
        public bool IsCleared;
        public bool IsVisited;
        public int[] EnemyIds;
        public Vector2[] ItemPositions;
        public int[] ItemTypes;
    }

    [Serializable]
    public struct ItemData
    {
        public int ItemId;
        public Vector2 Position;
        public int ItemType;
        public bool IsPickedUp;
        public ulong PickedUpBy;
    }

    [Serializable]
    public struct GameStateSync
    {
        public float GameTime;
        public int CurrentFloor;
        public Vector2 CurrentRoomPosition;
        public bool IsPaused;
    }

    [Serializable]
    public struct InitialStateSyncData
    {
        public string MapName;
        public Vector2 HostPosition;
        public PlayerPositionData[] ConnectedPlayers;
        public GameStateSync GameState;
    }

    [Serializable]
    public struct PlayerJoinData
    {
        public ulong PlayerId;
        public string PlayerName;
        public Vector2 Position;
        public string MapName;
    }
}
