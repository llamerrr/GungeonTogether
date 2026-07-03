using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class ConnectionRejectedPacket : INetworkPacket
    {
        public PacketType Type => PacketType.ConnectionRejected;

        public int ProtocolVersion; // why rejected

        public void Serialize(BinaryWriter writer) => writer.Write(ProtocolVersion);
        public void Deserialize(BinaryReader reader) => ProtocolVersion = reader.ReadInt32();
    }
}