using Network.Base;
using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Network.NetworkUtility
{
    internal class DataCombinator : IDisposable
    {
        private readonly int _packetInfoSize;
        private readonly int _streamBufferSize;
        private int _totalDataSize;
        private int _startDataOffset;
        private int _endDataOffset;
        private PacketInfo _packetInfo;
        private IMemoryOwner<byte>? _byteStreamBuffer;
        public DataCombinator(int streamSize)
        {
            _packetInfoSize = Marshal.SizeOf(typeof(PacketInfo));
            _streamBufferSize = streamSize;
            _startDataOffset = 0;
            _endDataOffset = 0;
            _byteStreamBuffer = MemoryPool<byte>.Shared.Rent(_streamBufferSize);
        }

        public void Dispose()
        {
            _byteStreamBuffer?.Dispose();
            _byteStreamBuffer = null;
        }

        public bool ExtractPacket(out Packet? packet)
        {
            if(_packetInfoSize > _totalDataSize)
            {
                packet = null;
                return false;
            }

            Span<byte> packetInfoSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _packetInfo, 1));
            int copiedLength = 0;

            int copySize = Math.Min(_packetInfoSize, _streamBufferSize - _startDataOffset);
            ReadOnlySpan<byte> dataSpan = _byteStreamBuffer.Memory.Span.Slice(_startDataOffset, copySize);
            dataSpan.CopyTo(packetInfoSpan);
            copiedLength += copySize;

            if(copiedLength < _packetInfoSize)
            {
                int require = _packetInfoSize - copiedLength;
                dataSpan = _byteStreamBuffer.Memory.Span.Slice(0, require);
                dataSpan.CopyTo(packetInfoSpan.Slice(copiedLength));
            }

            int totalPacketSize = _packetInfoSize + _packetInfo.dataSize;
            if (totalPacketSize > _totalDataSize)
            {
                packet = null;
                return false;
            }

            // PacketInfo는 뽑았으니, 데이터만 뽑으면 됨.
            _startDataOffset = (_startDataOffset + _packetInfoSize) % _streamBufferSize;
            var dataBuffer = MemoryPool<byte>.Shared.Rent(_packetInfo.dataSize);
            copiedLength = 0;

            copySize = Math.Min(_packetInfo.dataSize, _streamBufferSize - _startDataOffset);
            dataSpan = _byteStreamBuffer.Memory.Span.Slice(_startDataOffset, copySize);
            dataSpan.CopyTo(dataBuffer.Memory.Span);
            copiedLength += copySize;
            _startDataOffset = (_startDataOffset + copySize) % _streamBufferSize;

            if (copiedLength < _packetInfo.dataSize)
            {
                int require = _packetInfo.dataSize - copiedLength;
                dataSpan = _byteStreamBuffer.Memory.Span.Slice(0, require);
                dataSpan.CopyTo(dataBuffer.Memory.Span.Slice(copiedLength));
                _startDataOffset = require;
            }

            Packet newPacket = new Packet
            {
                packetInfo = _packetInfo,
                owner = dataBuffer
            };

            packet = newPacket;

            _totalDataSize -= totalPacketSize;

            return true;
        }

        public bool InsertMessage(NetMessage message)
        {
            int remainedSize = _streamBufferSize - _totalDataSize;
            int memorySize = message.byteCount;
            if(remainedSize < memorySize)
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan = message.data.Memory.Span.Slice(0, message.byteCount);
            int storedCount = 0;

            int storeSize = Math.Min(memorySize, _streamBufferSize - _endDataOffset);
            Span<byte> bufferSpan = _byteStreamBuffer.Memory.Span.Slice(_endDataOffset, storeSize);
            ReadOnlySpan<byte> sourceSpan = dataSpan.Slice(storedCount, storeSize);
            sourceSpan.CopyTo(bufferSpan);
            storedCount += storeSize;
            _endDataOffset = (_endDataOffset + storeSize) % _streamBufferSize;

            if(storeSize < memorySize)
            {
                int require = memorySize - storeSize;
                bufferSpan = _byteStreamBuffer.Memory.Span.Slice(0, require);
                sourceSpan = dataSpan.Slice(storeSize, require);
                sourceSpan.CopyTo(bufferSpan);
                _endDataOffset = require;
            }

            _totalDataSize += memorySize;

            return true;
        }
    }
}
