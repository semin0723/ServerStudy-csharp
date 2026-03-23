using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
                        _packetSequenceMap.Add(sessionID, 0);
                        _socketMap.Add(sessionID, message.socket);
                    }
                    else if(message.MessageState == -1)
                    {
                        _combinatorMap.Remove(sessionID);
                        Socket remoteSocket;
                        _packetSequenceMap.Remove(sessionID);
                        _socketMap.Remove(sessionID, out remoteSocket);

                        remoteSocket.Close();
                        remoteSocket.Dispose();
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
                            combinated.sessionID = sessionID;
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

        

        public void RegistSendData<IData>(uint sessionID, short packetID, IData data) where IData : class
        {
            string jsonData = JsonSerializer.Serialize(data);
            byte[] serialized = Encoding.UTF8.GetBytes(jsonData);
            int dataSize = serialized.Length;

            PacketInfo info;
            info.dataSize = dataSize;
            info.packetID = packetID;
            info.packetSequence = _packetSequenceMap[sessionID]++;

            int infoSize = Marshal.SizeOf(typeof(PacketInfo));
            Span<byte> infoSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref info, 1));

            int totalSize = infoSize + dataSize;

            IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent(totalSize);
            infoSpan.CopyTo(memoryOwner.Memory.Span);
            var sliced = memoryOwner.Memory.Slice(infoSize);
            serialized.CopyTo(sliced);

            NetMessage newMessage = _netMessagePool.Rent();
            newMessage.sessionID = sessionID;
            newMessage.byteCount = totalSize;
            newMessage.data = memoryOwner;

            _msgSendQueue.Enqueue(newMessage);
        }
    }
}
