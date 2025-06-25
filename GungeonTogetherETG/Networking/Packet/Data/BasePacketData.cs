using UnityEngine;

namespace GungeonTogether.Networking.Packet.Data
{
    /// <summary>
    /// Base class for packet data with common functionality.
    /// </summary>
    public abstract class BasePacketData : IPacketData
    {
        public bool IsReliable { get; set; } = true;
        
        public virtual byte[] Serialize()
        {
            using (var writer = new BinaryPacketWriter())
            {
                WriteData(writer);
                return writer.ToArray();
            }
        }
        
        public virtual void Deserialize(byte[] data)
        {
            using (var reader = new BinaryPacketReader(data))
            {
                ReadData(reader);
            }
        }
        
        public abstract void WriteData(IPacketWriter writer);
        public abstract void ReadData(IPacketReader reader);
    }
    
    /// <summary>
    /// Base class for client-specific packet data.
    /// </summary>
    public abstract class ClientPacketData : BasePacketData
    {
        /// <summary>
        /// The client ID that this packet is associated with.
        /// </summary>
        public ushort ClientId { get; set; }
        
        public override void WriteData(IPacketWriter writer)
        {
            writer.Write(ClientId);
            WriteClientData(writer);
        }
        
        public override void ReadData(IPacketReader reader)
        {
            ClientId = reader.ReadUShort();
            ReadClientData(reader);
        }
        
        protected abstract void WriteClientData(IPacketWriter writer);
        protected abstract void ReadClientData(IPacketReader reader);
    }
}
