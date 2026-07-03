using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class LoadingStatePacket : INetworkPacket
    {
        public PacketType Type => PacketType.LoadingState;
        public bool IsLoading;

        public void Serialize(BinaryWriter writer) => writer.Write(IsLoading);
        public void Deserialize(BinaryReader reader) => IsLoading = reader.ReadBoolean();
    }
}