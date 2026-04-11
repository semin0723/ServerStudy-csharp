using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Network.NetworkUtility;
using Network.Base;
using Network.DataObject;

namespace Network
{
    public partial class NetBase : IDisposable
    {
        private readonly TimerSystem _timerSystem;
        private readonly int _bufferSize;
        private readonly int _bufferPoolDefaultSize;
        private readonly float _sendTimeoutInterval;
        private readonly int _tickFrame;
        private readonly float _tickInterval;
        private readonly int _streamBufferSize;
        private int _disposed;
        private Task? _loopTask;
        private Task? _acceptTask;

        private Socket? _mainSocket;
        private CancellationTokenSource _acceptCancellationTokenSource;

        private List<Session> _sessionPool;

        private Channel<NetMessage> _connectionEventChannel;
        private Channel<NetMessage> _recvChannel;
        private Channel<NetMessage> _sendChannel;
        public Channel<Packet> packetChannel { get; private set; }

        private NetMessageFactory _netMessagePool;
        private Dictionary<uint, DataCombinator> _combinatorMap;
        private Dictionary<uint, uint> _packetSequenceMap;
        private Dictionary<uint, Socket> _socketMap;

        private class AcceptWithCancel
        {
            public Socket socket;
            public CancellationToken token;
        }

        public NetBase(int fps)
        {
            _tickFrame = fps;
            _tickInterval = 1.0f / _tickFrame;
            _sendTimeoutInterval = 0.2f;
            _bufferSize = 256;
            _bufferPoolDefaultSize = 30;
            _streamBufferSize = 2048;
            _timerSystem = new TimerSystem();
            _disposed = 0;
            _acceptCancellationTokenSource = new CancellationTokenSource();
            _sessionPool = new List<Session>();
            _netMessagePool = new NetMessageFactory(100);
            _socketMap = new Dictionary<uint, Socket>();
            _combinatorMap = new Dictionary<uint, DataCombinator>();
            _packetSequenceMap = new Dictionary<uint, uint>();

            _connectionEventChannel = Channel.CreateUnbounded<NetMessage>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            _recvChannel = Channel.CreateUnbounded<NetMessage>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = true
                });

            _sendChannel = Channel.CreateBounded<NetMessage>(
                new BoundedChannelOptions(1000)
                {
                    SingleWriter = false,
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait
                });

            packetChannel = Channel.CreateBounded<Packet>(
                new BoundedChannelOptions(1000)
                {
                    SingleReader = true,
                    SingleWriter = true
                });
        }

        public bool Init(int port, int backlog)
        {
            _timerSystem.Init();
            try
            {
                _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _mainSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                _mainSocket.Listen(backlog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Network Init Exception. Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }

            _acceptTask = Task.Run(() => AcceptClientAsync(new AcceptWithCancel 
            { 
                socket = _mainSocket, 
                token = _acceptCancellationTokenSource.Token
            }));

            return true;
        }

        public bool Init(string ip, int port)
        {
            _timerSystem.Init();
            try
            {
                _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _mainSocket.Connect(ip, port);
                _socketMap.Add(1, _mainSocket);
                _packetSequenceMap.Add(1, 0);
                _combinatorMap.Add(1, new DataCombinator(_streamBufferSize));

                Task.Run(() => RecvAsync(new Session
                    {
                        socket = _mainSocket,
                        sessionID = 1
                    })
                );

            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception. Msg: {ex.Message}, {ex.StackTrace}");
                return false;
            }

            return true;
        }

        public void Run()
        {
            _loopTask = Task.Run(RunAsync);
        }

        public void Dispose()
        {
            if (_loopTask != null)
            {
                Interlocked.Increment(ref _disposed);
                _acceptCancellationTokenSource.Cancel();
                _loopTask?.Wait();
                _loopTask = null;
                _acceptTask?.Wait();   
                _acceptTask = null;
            }
        }

        async Task RunAsync()
        {
            _ = Task.Run(SendAsync);
            while(Interlocked.Equals(_disposed, 0))
            {
                try
                {
                    await DispatchData();
                    await Task.Delay(TimeSpan.FromMilliseconds(_tickInterval));
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Exception caused. msg: {ex.Message}, {ex.StackTrace}");
                }
            }
            _mainSocket.Dispose();
        }
    }
}
