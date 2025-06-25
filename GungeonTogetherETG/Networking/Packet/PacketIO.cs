using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GungeonTogether.Networking.Packet
{
    /// <summary>
    /// Binary packet writer implementation.
    /// </summary>
    public class BinaryPacketWriter : IPacketWriter, IDisposable
    {
        private readonly BinaryWriter _writer;
        private readonly MemoryStream _stream;
        
        public BinaryPacketWriter()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream, Encoding.UTF8);
        }
        
        public void Write(bool value) => _writer.Write(value);
        public void Write(byte value) => _writer.Write(value);
        public void Write(ushort value) => _writer.Write(value);
        public void Write(int value) => _writer.Write(value);
        public void Write(float value) => _writer.Write(value);
        
        public void Write(string value)
        {
            if (value == null)
            {
                _writer.Write((ushort)0);
                return;
            }
            
            var bytes = Encoding.UTF8.GetBytes(value);
            _writer.Write((ushort)bytes.Length);
            _writer.Write(bytes);
        }
        
        public void Write(Vector2 value)
        {
            _writer.Write(value.x);
            _writer.Write(value.y);
        }
        
        public void Write(Vector3 value)
        {
            _writer.Write(value.x);
            _writer.Write(value.y);
            _writer.Write(value.z);
        }
        
        public void Write(byte[] data)
        {
            _writer.Write(data.Length);
            _writer.Write(data);
        }
        
        public byte[] ToArray()
        {
            return _stream.ToArray();
        }
        
        public void Dispose()
        {
            _writer?.Dispose();
            _stream?.Dispose();
        }
    }
    
    /// <summary>
    /// Binary packet reader implementation.
    /// </summary>
    public class BinaryPacketReader : IPacketReader, IDisposable
    {
        private readonly BinaryReader _reader;
        
        public BinaryPacketReader(byte[] data)
        {
            var stream = new MemoryStream(data);
            _reader = new BinaryReader(stream, Encoding.UTF8);
        }
        
        public bool ReadBool() => _reader.ReadBoolean();
        public byte ReadByte() => _reader.ReadByte();
        public ushort ReadUShort() => _reader.ReadUInt16();
        public int ReadInt() => _reader.ReadInt32();
        public float ReadFloat() => _reader.ReadSingle();
        
        public string ReadString()
        {
            var length = _reader.ReadUInt16();
            if (length == 0) return null;
            
            var bytes = _reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
        
        public Vector2 ReadVector2()
        {
            return new Vector2(_reader.ReadSingle(), _reader.ReadSingle());
        }
        
        public Vector3 ReadVector3()
        {
            return new Vector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        }
        
        public byte[] ReadBytes(int length)
        {
            return _reader.ReadBytes(length);
        }
        
        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
