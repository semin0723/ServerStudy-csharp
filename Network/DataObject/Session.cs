using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Network.DataObject
{
    internal class Session
    {
        public uint sessionID { get; set; } = 0;
        public float lastCommunicateTime { get; set; } = 0f;
        public Socket socket { get; set; }
        public uint packetSerialNum { get; set; } = 0;
    }
}
