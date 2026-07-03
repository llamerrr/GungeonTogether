using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;
using UnityEngine;

namespace GungeonTogether.Networking.Packets
{
    public class EnemyStatePacket : INetworkPacket
    {
        public PacketType Type => PacketType.EnemyState;

        public int EnemyId;
        public Vector2 Position;
        public float Rotation;
        public int Health;
        public int AIState;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(EnemyId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Rotation);
            writer.Write(Health);
            writer.Write(AIState);
        }

        public void Deserialize(BinaryReader reader)
        {
            EnemyId = reader.ReadInt32();
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Rotation = reader.ReadSingle();
            Health = reader.ReadInt32();
            AIState = reader.ReadInt32();
        }
    }
}