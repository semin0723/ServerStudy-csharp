using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Network
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketInfo
    {
        public uint packetSequence;
        public short packetID;
        public int dataSize;
    }
    
    public class Packet
    {
        public PacketInfo packetInfo;
        public IMemoryOwner<byte>? owner;
    }
}
