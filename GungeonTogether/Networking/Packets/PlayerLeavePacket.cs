using System.IO;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Enums;

public class PlayerLeavePacket : INetworkPacket
{
    public PacketType Type => PacketType.PlayerLeave;
    public ulong PlayerId;

    public void Serialize(BinaryWriter writer) => writer.Write(PlayerId);
    public void Deserialize(BinaryReader reader) => PlayerId = reader.ReadUInt64();
}