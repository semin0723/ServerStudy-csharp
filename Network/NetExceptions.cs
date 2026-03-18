using System;
using System.Collections.Generic;
using System.Text;

namespace Network
{
    internal class ClientShutDownException : Exception
    {
        public ClientShutDownException(string message) : base(message) { }
    }
}
