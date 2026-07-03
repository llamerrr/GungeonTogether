using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;
using UnityEngine;

namespace GungeonTogether.Networking.Packets
{
    public class EnemySpawnPacket : INetworkPacket
    {
        public PacketType Type => PacketType.EnemySpawn;

        public int EnemyId;
        public string PrefabName;
        public Vector2 Position;
        public float Rotation;
        public int Health;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(EnemyId);
            writer.Write(PrefabName ?? string.Empty);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Rotation);
            writer.Write(Health);
        }

        public void Deserialize(BinaryReader reader)
        {
            EnemyId = reader.ReadInt32();
            PrefabName = reader.ReadString();
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Rotation = reader.ReadSingle();
            Health = reader.ReadInt32();
        }
    }
}