using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Network
{
    public class NetMessage
    {
        public int MessageState;
        public uint sessionID;
        public int byteCount;
        public IMemoryOwner<byte>? data;

        internal readonly NetMessageFactory _factory;
        private int _isInPool = 0;

        internal NetMessage(NetMessageFactory factory)
        {
            _factory = factory;
            Reset();
        }

        public void Reset()
        {
            _isInPool = 0;
            MessageState = 0;
            sessionID = 0;
            byteCount = 0;
        }

        public void Return()
        {
            if(Interlocked.Exchange(ref _isInPool, 1) == 1)
            {
                return;
            }
            data?.Dispose();
            data = null;

            _factory.Return(this);
        }
    }

    internal class NetMessageFactory
    {
        private readonly int _defaultObjectCount;
        private ConcurrentBag<NetMessage> _netMessagePool;
        public NetMessageFactory(int defaultInitCount) 
        {
            _netMessagePool = new ConcurrentBag<NetMessage>();
            _defaultObjectCount = defaultInitCount;
            for (int i = 0; i < _defaultObjectCount; i++)
            {
                _netMessagePool.Add(new NetMessage(this));
            }
        }

        public void Return(NetMessage message)
        {
            _netMessagePool.Add(message);
        }

        public NetMessage Rent()
        {
            if (_netMessagePool.TryTake(out var newMessage))
            {
                newMessage.Reset();
                return newMessage;
            }
            else
            {
                return new NetMessage(this);
            }
        }
    }
}
