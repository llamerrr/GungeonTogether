using System.IO;
using UnityEngine;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Enums;

public class PlayerJoinPacket : INetworkPacket
{
    public PacketType Type => PacketType.PlayerJoin;
    public ulong PlayerId;
    public Vector2 Position;
    public float Rotation;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        writer.Write(Position.x);
        writer.Write(Position.y);
        writer.Write(Rotation);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt64();
        Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        Rotation = reader.ReadSingle();
    }
}