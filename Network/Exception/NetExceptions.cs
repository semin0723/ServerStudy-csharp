using System;
using System.Collections.Generic;
using System.Text;

namespace Network.Exceptions
{
    public class ClientShutDownException : Exception
    {
        public ClientShutDownException(string message) : base(message) { }
    }
    public class NoBufferSpace : Exception
    {
        public NoBufferSpace(string message) : base(message) { }
    }

    public class SocketAlreadyClosed : Exception
    {
        public SocketAlreadyClosed(string message) : base(message) { }
    }
    
    public class RPCMethodNotFound : Exception
    {
        public RPCMethodNotFound(string message) : base(message) { }
    }

}
