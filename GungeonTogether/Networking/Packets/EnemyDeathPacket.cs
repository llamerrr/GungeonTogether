using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class EnemyDeathPacket : INetworkPacket
    {
        public PacketType Type => PacketType.EnemyDeath;

        public int EnemyId;

        public void Serialize(BinaryWriter writer) => writer.Write(EnemyId);

        public void Deserialize(BinaryReader reader) => EnemyId = reader.ReadInt32();
    }
}