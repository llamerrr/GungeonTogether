using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class DisconnectPacket : INetworkPacket
    {
        public PacketType Type => PacketType.Disconnect;

        public void Serialize(BinaryWriter writer) { } // empty
        public void Deserialize(BinaryReader reader) { }
    }
}