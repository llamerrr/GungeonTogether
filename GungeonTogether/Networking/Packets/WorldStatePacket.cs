using System.IO;
using UnityEngine;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class WorldStatePacket : INetworkPacket
    {
        public PacketType Type => PacketType.WorldState;

        public bool IsFoyer;
        public int FloorIndex;          // -1 if foyer
        public string RoomIdentifier;   // room name or unique ID
        public Vector2 Position;
        public float Rotation;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(IsFoyer);
            writer.Write(FloorIndex);
            writer.Write(RoomIdentifier ?? "");
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Rotation);
        }

        public void Deserialize(BinaryReader reader)
        {
            IsFoyer = reader.ReadBoolean();
            FloorIndex = reader.ReadInt32();
            RoomIdentifier = reader.ReadString();
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Rotation = reader.ReadSingle();
        }
    }
}