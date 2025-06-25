using UnityEngine;

namespace GungeonTogether.Networking.Packet.Data
{
    /// <summary>
    /// Packet data for player position and movement updates.
    /// </summary>
    public class PlayerUpdatePacket : ClientPacketData
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public bool IsFacingRight { get; set; }
        public bool IsGrounded { get; set; }
        public bool IsRolling { get; set; }
        public bool IsShooting { get; set; }
        public float AimDirection { get; set; }
        public string CurrentAnimation { get; set; }
        public string CurrentRoom { get; set; }
        
        public PlayerUpdatePacket()
        {
            IsReliable = false; // Position updates don't need to be reliable
        }
        
        protected override void WriteClientData(IPacketWriter writer)
        {
            writer.Write(Position);
            writer.Write(Velocity);
            writer.Write(IsFacingRight);
            writer.Write(IsGrounded);
            writer.Write(IsRolling);
            writer.Write(IsShooting);
            writer.Write(AimDirection);
            writer.Write(CurrentAnimation ?? "");
            writer.Write(CurrentRoom ?? "");
        }
        
        protected override void ReadClientData(IPacketReader reader)
        {
            Position = reader.ReadVector2();
            Velocity = reader.ReadVector2();
            IsFacingRight = reader.ReadBool();
            IsGrounded = reader.ReadBool();
            IsRolling = reader.ReadBool();
            IsShooting = reader.ReadBool();
            AimDirection = reader.ReadFloat();
            CurrentAnimation = reader.ReadString();
            CurrentRoom = reader.ReadString();
        }
    }
      /// <summary>
    /// Packet data for player login requests.
    /// </summary>
    public class LoginRequestPacket : BasePacketData
    {
        public string PlayerName { get; set; }
        public string ModVersion { get; set; }
        
        public override void WriteData(IPacketWriter writer)
        {
            writer.Write(PlayerName ?? "Player");
            writer.Write(ModVersion ?? "1.0.0");
        }
        
        public override void ReadData(IPacketReader reader)
        {
            PlayerName = reader.ReadString();
            ModVersion = reader.ReadString();
        }
    }
    
    /// <summary>
    /// Packet data for login responses.
    /// </summary>
    public class LoginResponsePacket : BasePacketData
    {        public bool Success { get; set; }
        public string Message { get; set; }
        public ushort AssignedClientId { get; set; }
        
        public override void WriteData(IPacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(Message ?? "");
            writer.Write(AssignedClientId);
        }
        
        public override void ReadData(IPacketReader reader)
        {
            Success = reader.ReadBool();
            Message = reader.ReadString();
            AssignedClientId = reader.ReadUShort();
        }
    }
    
    /// <summary>
    /// Packet data for room transitions.
    /// </summary>
    public class PlayerEnterRoomPacket : ClientPacketData
    {
        public string RoomName { get; set; }
        public Vector2 SpawnPosition { get; set; }
        
        protected override void WriteClientData(IPacketWriter writer)
        {
            writer.Write(RoomName ?? "");
            writer.Write(SpawnPosition);
        }
        
        protected override void ReadClientData(IPacketReader reader)
        {
            RoomName = reader.ReadString();
            SpawnPosition = reader.ReadVector2();
        }
    }
    
    /// <summary>
    /// Packet data for weapon switching.
    /// </summary>
    public class PlayerWeaponSwitchPacket : ClientPacketData
    {
        public int WeaponId { get; set; }
        public string WeaponName { get; set; }
        
        protected override void WriteClientData(IPacketWriter writer)
        {
            writer.Write(WeaponId);
            writer.Write(WeaponName ?? "");
        }
        
        protected override void ReadClientData(IPacketReader reader)
        {
            WeaponId = reader.ReadInt();
            WeaponName = reader.ReadString();
        }
    }
}
