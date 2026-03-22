using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Network
{
    public partial class NetBase
    {
        private void DispatchData()
        {
            while (_msgRecvQueue.Count > 0)
            {
                NetMessage message;
                if(_msgRecvQueue.TryDequeue(out message))
                {
                    uint sessionID = message.sessionID;
                    if(message.MessageState == 1)
                    {
                        _combinatorMap.Add(sessionID, new DataCombinator(_streamBufferSize));
                    }
                    else if(message.MessageState == -1)
                    {
                        _combinatorMap.Remove(sessionID);
                    }
                    else
                    {
                        DataCombinator combinator = _combinatorMap[message.sessionID];
                        if(!combinator.InsertMessage(message))
                        {
                            throw new NoBufferSpace("No bufferSpace.");
                        }

                        Packet combinated;
                        if(combinator.ExtractPacket(out combinated))
                        {
                            recvPacketQueue.Enqueue(combinated);
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}
