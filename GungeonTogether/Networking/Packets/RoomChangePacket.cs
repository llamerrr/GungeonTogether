using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class RoomChangePacket : INetworkPacket
    {
        public PacketType Type => PacketType.RoomChange;
        public string RoomName;
        public void Serialize(BinaryWriter w) => w.Write(RoomName);
        public void Deserialize(BinaryReader r) => RoomName = r.ReadString();
    }
}