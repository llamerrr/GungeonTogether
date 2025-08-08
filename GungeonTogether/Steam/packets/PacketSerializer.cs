using System;
using System.IO;
using UnityEngine;

namespace GungeonTogether.Steam
{
    /// <summary>
    /// Handles serialization and deserialization of network packets
    /// </summary>
    public static class PacketSerializer
    {
        /// <summary>
        /// Serialize a NetworkPacket to byte array
        /// </summary>
        public static byte[] SerializePacket(NetworkPacket packet)
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)packet.Type);
                    writer.Write(packet.SenderId);
                    writer.Write(packet.Timestamp);
                    writer.Write(packet.Data.Length);
                    writer.Write(packet.Data);
                    return stream.ToArray();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PacketSerializer] Failed to serialize packet: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserialize byte array to NetworkPacket
        /// </summary>
        public static NetworkPacket? DeserializePacket(byte[] data)
        {
            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    var type = (PacketType)reader.ReadByte();
                    var senderId = reader.ReadUInt64();
                    var timestamp = reader.ReadSingle();
                    var dataLength = reader.ReadInt32();
                    var packetData = reader.ReadBytes(dataLength);

                    return new NetworkPacket(type, senderId, packetData);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PacketSerializer] Failed to deserialize packet: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Serialize object to byte array
        /// </summary>
        public static byte[] SerializeObject<T>(T obj) where T : struct
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeStruct(writer, obj);
                    return stream.ToArray();
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PacketSerializer] Failed to serialize object {typeof(T).Name}: {e.Message}");
                return new byte[0];
            }
        }

        /// <summary>
        /// Deserialize byte array to object
        /// </summary>
        public static T DeserializeObject<T>(byte[] data) where T : struct
        {
            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    return DeserializeStruct<T>(reader);
                }
            }
            catch (Exception e)
            {
                GungeonTogether.Logging.Debug.LogError($"[PacketSerializer] Failed to deserialize object {typeof(T).Name}: {e.Message}");
                return default(T);
            }
        }

        private static void SerializeStruct<T>(BinaryWriter writer, T obj) where T : struct
        {
            var type = typeof(T);

            if (type.Equals(typeof(PlayerPositionData)))
            {
                var data = (PlayerPositionData)(object)obj;
                writer.Write(data.PlayerId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Velocity.x);
                writer.Write(data.Velocity.y);
                writer.Write(data.Rotation);
                writer.Write(data.IsGrounded);
                writer.Write(data.IsDodgeRolling);

                // Serialize the missing fields: MapName, CharacterId, CharacterName
                writer.Write(data.MapName ?? "");
                writer.Write(data.CharacterId);
                writer.Write(data.CharacterName ?? "");
                
                // Serialize animation state data
                writer.Write((byte)data.AnimationState);
                writer.Write(data.MovementDirection.x);
                writer.Write(data.MovementDirection.y);
                writer.Write(data.IsRunning);
                writer.Write(data.IsFalling);
                writer.Write(data.IsTakingDamage);
                writer.Write(data.IsDead);
                writer.Write(data.CurrentAnimationName ?? "");
            }
            else if (type.Equals(typeof(PlayerShootRequestData)))
            {
                var data = (PlayerShootRequestData)(object)obj;
                writer.Write(data.PlayerId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Direction.x);
                writer.Write(data.Direction.y);
                writer.Write(data.WeaponId);
                writer.Write(data.IsCharging);
                writer.Write(data.ChargeAmount);
                writer.Write(data.RequestTimestamp);
            }
            else if (type.Equals(typeof(PlayerShootingData)))
            {
                var data = (PlayerShootingData)(object)obj;
                writer.Write(data.PlayerId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Direction.x);
                writer.Write(data.Direction.y);
                writer.Write(data.WeaponId);
                writer.Write(data.IsCharging);
                writer.Write(data.ChargeAmount);
            }
            else if (type.Equals(typeof(EnemyStateData)))
            {
                var data = (EnemyStateData)(object)obj;
                writer.Write(data.EnemyId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Velocity.x);
                writer.Write(data.Velocity.y);
                writer.Write(data.Rotation);
                writer.Write(data.Health);
                writer.Write(data.AnimationState);
                writer.Write(data.IsActive);
            }
            else if (type.Equals(typeof(EnemyPathData)))
            {
                var data = (EnemyPathData)(object)obj;
                writer.Write(data.EnemyId);
                writer.Write(data.PathPoints.Length);
                foreach (var point in data.PathPoints)
                {
                    writer.Write(point.x);
                    writer.Write(point.y);
                }
                writer.Write(data.CurrentPathIndex);
                writer.Write(data.MoveSpeed);
                writer.Write(data.IsPatrolling);
            }
            else if (type.Equals(typeof(ProjectileData)))
            {
                var data = (ProjectileData)(object)obj;
                writer.Write(data.ProjectileId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Velocity.x);
                writer.Write(data.Velocity.y);
                writer.Write(data.Damage);
                writer.Write(data.ProjectileType);
                writer.Write(data.OwnerId);
                writer.Write(data.IsPlayerProjectile);
            }
            else if (type.Equals(typeof(ProjectileSpawnData)))
            {
                var data = (ProjectileSpawnData)(object)obj;
                writer.Write(data.ProjectileId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.Velocity.x);
                writer.Write(data.Velocity.y);
                writer.Write(data.Rotation);
                writer.Write(data.OwnerId);
                writer.Write(data.IsPlayerProjectile);
                writer.Write(data.Damage);
                writer.Write(data.ProjectileType);
                writer.Write(data.WeaponId);
                writer.Write(data.IsServerAuthoritative);
            }
            else if (type.Equals(typeof(ItemData)))
            {
                var data = (ItemData)(object)obj;
                writer.Write(data.ItemId);
                writer.Write(data.Position.x);
                writer.Write(data.Position.y);
                writer.Write(data.ItemType);
                writer.Write(data.IsPickedUp);
                writer.Write(data.PickedUpBy);
                writer.Write(data.SpriteId);
                writer.Write(data.Quality);
                writer.Write(data.Ammo);
                writer.Write(data.MaxAmmo);
                writer.Write(data.Charges);
            }
            // Add more types as needed
        }

        private static T DeserializeStruct<T>(BinaryReader reader) where T : struct
        {
            var type = typeof(T);

            if (type.Equals(typeof(PlayerPositionData)))
            {
                var data = new PlayerPositionData
                {
                    PlayerId = reader.ReadUInt64(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = reader.ReadSingle(),
                    IsGrounded = reader.ReadBoolean(),
                    IsDodgeRolling = reader.ReadBoolean(),

                    // Deserialize the missing fields: MapName, CharacterId, CharacterName
                    MapName = reader.ReadString(),
                    CharacterId = reader.ReadInt32(),
                    CharacterName = reader.ReadString(),
                    
                    // Deserialize animation state data
                    AnimationState = (PlayerAnimationState)reader.ReadByte(),
                    MovementDirection = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    IsRunning = reader.ReadBoolean(),
                    IsFalling = reader.ReadBoolean(),
                    IsTakingDamage = reader.ReadBoolean(),
                    IsDead = reader.ReadBoolean(),
                    CurrentAnimationName = reader.ReadString()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(PlayerShootRequestData)))
            {
                var data = new PlayerShootRequestData
                {
                    PlayerId = reader.ReadUInt64(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Direction = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    WeaponId = reader.ReadInt32(),
                    IsCharging = reader.ReadBoolean(),
                    ChargeAmount = reader.ReadSingle(),
                    RequestTimestamp = reader.ReadSingle()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(PlayerShootingData)))
            {
                var data = new PlayerShootingData
                {
                    PlayerId = reader.ReadUInt64(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Direction = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    WeaponId = reader.ReadInt32(),
                    IsCharging = reader.ReadBoolean(),
                    ChargeAmount = reader.ReadSingle()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(EnemyStateData)))
            {
                var data = new EnemyStateData
                {
                    EnemyId = reader.ReadInt32(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = reader.ReadSingle(),
                    Health = reader.ReadSingle(),
                    AnimationState = reader.ReadInt32(),
                    IsActive = reader.ReadBoolean()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(EnemyPathData)))
            {
                var pathLength = reader.ReadInt32();
                var pathPoints = new Vector2[pathLength];
                for (int i = 0; i < pathLength; i++)
                {
                    pathPoints[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }

                var data = new EnemyPathData
                {
                    EnemyId = reader.ReadInt32(),
                    PathPoints = pathPoints,
                    CurrentPathIndex = reader.ReadInt32(),
                    MoveSpeed = reader.ReadSingle(),
                    IsPatrolling = reader.ReadBoolean()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(ProjectileData)))
            {
                var data = new ProjectileData
                {
                    ProjectileId = reader.ReadInt32(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Damage = reader.ReadSingle(),
                    ProjectileType = reader.ReadInt32(),
                    OwnerId = reader.ReadUInt64(),
                    IsPlayerProjectile = reader.ReadBoolean()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(ProjectileSpawnData)))
            {
                var data = new ProjectileSpawnData
                {
                    ProjectileId = reader.ReadInt32(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = reader.ReadSingle(),
                    OwnerId = reader.ReadUInt64(),
                    IsPlayerProjectile = reader.ReadBoolean(),
                    Damage = reader.ReadSingle(),
                    ProjectileType = reader.ReadInt32(),
                    WeaponId = reader.ReadInt32(),
                    IsServerAuthoritative = reader.ReadBoolean()
                };
                return (T)(object)data;
            }
            else if (type.Equals(typeof(ItemData)))
            {
                var data = new ItemData
                {
                    ItemId = reader.ReadInt32(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    ItemType = reader.ReadInt32(),
                    IsPickedUp = reader.ReadBoolean(),
                    PickedUpBy = reader.ReadUInt64(),
                    SpriteId = reader.ReadInt32(),
                    Quality = reader.ReadInt32(),
                    Ammo = reader.ReadInt32(),
                    MaxAmmo = reader.ReadInt32(),
                    Charges = reader.ReadInt32()
                };
                return (T)(object)data;
            }

            return default(T);
        }

        /// <summary>
        /// Initialize the packet serializer
        /// </summary>
        public static void Initialize()
        {
            GungeonTogether.Logging.Debug.Log("[PacketSerializer] Initialized");
        }
    }
}
