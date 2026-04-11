using System;
using System.Collections.Generic;
using System.Text;

namespace Network.NetworkUtility.RPC
{
    public interface IRPCCallable
    {
        public Guid Guid { get; }
        public void GuidReset();
    }
}
