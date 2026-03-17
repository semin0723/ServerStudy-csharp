using System;
using System.Collections.Generic;
using System.Text;

namespace Network
{
    public class NetMessage
    {
        public int MessageState;
        public uint sessionID;
        public int byteCount;
        public byte[]? data;
    }
}
