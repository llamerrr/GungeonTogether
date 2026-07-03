using System.IO;
using GungeonTogether.Networking.Enums;
using GungeonTogether.Networking.Interfaces;

namespace GungeonTogether.Networking.Packets
{
    public class PlayerStatePacket : INetworkPacket
    {
        public PacketType Type => PacketType.PlayerState;
        public ulong PlayerId;
        public float Health;
        public float MaxHealth;
        public float Armor;
        public float MaxArmor;
        public int Ammo;
        public int MaxAmmo;
        public int CurrentGunIndex;
        public string ActiveItemName;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Health);
            writer.Write(MaxHealth);
            writer.Write(Armor);
            writer.Write(MaxArmor);
            writer.Write(Ammo);
            writer.Write(MaxAmmo);
            writer.Write(CurrentGunIndex);
            writer.Write(ActiveItemName ?? "");
        }

        public void Deserialize(BinaryReader reader)
        {
            PlayerId = reader.ReadUInt64();
            Health = reader.ReadSingle();
            MaxHealth = reader.ReadSingle();
            Armor = reader.ReadSingle();
            MaxArmor = reader.ReadSingle();
            Ammo = reader.ReadInt32();
            MaxAmmo = reader.ReadInt32();
            CurrentGunIndex = reader.ReadInt32();
            ActiveItemName = reader.ReadString();
        }
    }
}