using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class ConnectionAcceptedPacket : INetworkPacket
    {
        public PacketType Type => PacketType.ConnectionAccepted;

        public ulong HostId;
        public int ProtocolVersion;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(HostId);
            writer.Write(ProtocolVersion);
        }

        public void Deserialize(BinaryReader reader)
        {
            HostId = reader.ReadUInt64();
            ProtocolVersion = reader.ReadInt32();
        }
    }
}
