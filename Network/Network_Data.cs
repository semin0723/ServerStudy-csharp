using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Network
{
    public partial class NetBase
    {
        private async Task DispatchData()
        {
            while(_connectionEventChannel.Reader.TryRead(out var msg))
            {
                uint sessionID = msg.sessionID;
                if (msg.MessageState == 1)
                {
                    _combinatorMap.Add(sessionID, new DataCombinator(_streamBufferSize));
                    _packetSequenceMap.Add(sessionID, 0);
                    _socketMap.Add(sessionID, msg.socket);
                }
                else if (msg.MessageState == -1)
                {
                    _combinatorMap.Remove(sessionID);
                    Socket remoteSocket;
                    _packetSequenceMap.Remove(sessionID);
                    _socketMap.Remove(sessionID, out remoteSocket);

                    remoteSocket.Close();
                    remoteSocket.Dispose();
                }
                msg.Return();
            }

            while (_recvChannel.Reader.TryRead(out var msg))
            {
                uint sessionID = msg.sessionID;
                DataCombinator combinator;
                if(_combinatorMap.TryGetValue(sessionID, out combinator))
                {
                    if (!combinator.InsertMessage(msg))
                    {
                        throw new NoBufferSpace("No bufferSpace.");
                    }

                    Packet combinated;
                    while (combinator.ExtractPacket(out combinated))
                    {
                        combinated.sessionID = sessionID;
                        //recvPacketQueue.Enqueue(combinated);
                        await packetChannel.Writer.WriteAsync(combinated);
                    }
                }
                msg.Return();
            }
        }

        public async Task RegistSendData<IData>(uint sessionID, short packetID, IData data) where IData : class
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

            await _sendChannel.Writer.WriteAsync(newMessage);
        }
    }
}
