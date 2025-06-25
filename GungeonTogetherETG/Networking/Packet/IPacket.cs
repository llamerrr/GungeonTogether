using System;
using UnityEngine;

namespace GungeonTogether.Networking.Packet
{
    /// <summary>
    /// Interface for writing packet data.
    /// </summary>
    public interface IPacketWriter
    {
        void Write(bool value);
        void Write(byte value);
        void Write(ushort value);
        void Write(int value);
        void Write(float value);
        void Write(string value);
        void Write(Vector2 value);
        void Write(Vector3 value);
        void Write(byte[] data);
    }
    
    /// <summary>
    /// Interface for reading packet data.
    /// </summary>
    public interface IPacketReader
    {
        bool ReadBool();
        byte ReadByte();
        ushort ReadUShort();
        int ReadInt();
        float ReadFloat();
        string ReadString();
        Vector2 ReadVector2();
        Vector3 ReadVector3();
        byte[] ReadBytes(int length);
    }
}
