using System.IO;
using GungeonTogether.Networking.Enums;

namespace GungeonTogether.Networking.Interfaces
{
    public interface INetworkPacket
    {
        PacketType Type { get; }
        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }
}
