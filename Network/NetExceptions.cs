using System;
using System.Collections.Generic;
using System.Text;

namespace Network
{
    internal class ClientShutDownException : Exception
    {
        public ClientShutDownException(string message) : base(message) { }
    }
    internal class NoBufferSpace : Exception
    {
        public NoBufferSpace(string message) : base(message) { }
    }

    internal class SocketAlreadyClosed : Exception
    {
        public SocketAlreadyClosed(string message) : base(message) { }
    }
}
