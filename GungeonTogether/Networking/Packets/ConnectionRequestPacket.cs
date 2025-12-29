using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class ConnectionRequestPacket : INetworkPacket
    {
        public PacketType Type => PacketType.ConnectionRequest;

        public ulong ClientId;
        public int ProtocolVersion;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ClientId);
            writer.Write(ProtocolVersion);
        }

        public void Deserialize(BinaryReader reader)
        {
            ClientId = reader.ReadUInt64();
            ProtocolVersion = reader.ReadInt32();
        }
    }
}
