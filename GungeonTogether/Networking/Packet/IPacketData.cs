using System;

namespace GungeonTogether.Networking.Packet
{
    /// <summary>
    /// Interface for packet data that can be sent over the network.
    /// </summary>
    public interface IPacketData
    {
        /// <summary>
        /// Whether this packet should be sent reliably.
        /// </summary>
        bool IsReliable { get; set; }
        
        /// <summary>
        /// Serialize the packet data to bytes.
        /// </summary>
        byte[] Serialize();
        
        /// <summary>
        /// Deserialize packet data from bytes.
        /// </summary>
        void Deserialize(byte[] data);
        
        /// <summary>
        /// Write data to a packet writer.
        /// </summary>
        void WriteData(IPacketWriter writer);
        
        /// <summary>
        /// Read data from a packet reader.
        /// </summary>
        void ReadData(IPacketReader reader);
    }
}
