using System.IO;
using UnityEngine;
using GungeonTogether.Networking.Interfaces;
using GungeonTogether.Networking.Enums;

namespace GungeonTogether.Networking.Packets
{
    public class PlayerPositionPacket : INetworkPacket
    {
        public PacketType Type => PacketType.PlayerPosition;

        public ulong PlayerId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public bool IsGrounded;
        public bool IsDodgeRolling;
        public int AnimationState;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Velocity.x);
            writer.Write(Velocity.y);
            writer.Write(Rotation);
            writer.Write(IsGrounded);
            writer.Write(IsDodgeRolling);
            writer.Write(AnimationState);
        }

        public void Deserialize(BinaryReader reader)
        {
            PlayerId = reader.ReadUInt64();
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Rotation = reader.ReadSingle();
            IsGrounded = reader.ReadBoolean();
            IsDodgeRolling = reader.ReadBoolean();
            AnimationState = reader.ReadInt32();
        }
    }
}
