using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

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

        private Socket? _mainSocket;
        private CancellationTokenSource _acceptCancellationTokenSource;

        private List<Session> _sessionPool;

        private ConcurrentQueue<NetMessage> _msgRecvQueue;
        public ConcurrentQueue<NetMessage> _msgSendQueue;
        private ConcurrentQueue<SendSet>? pendingSendQueue;

        private NetMessageFactory _netMessagePool;
        private Dictionary<uint, DataCombinator> _combinatorMap;
        private Dictionary<uint, uint> _packetSequenceMap;
        private Dictionary<uint, Socket> _socketMap;

        public ConcurrentQueue<Packet> recvPacketQueue { get; private set; }

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

            _msgRecvQueue = new ConcurrentQueue<NetMessage>();
            _msgSendQueue = new ConcurrentQueue<NetMessage>();
            pendingSendQueue = new ConcurrentQueue<SendSet>();
            recvPacketQueue = new ConcurrentQueue<Packet>();
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

            ThreadPool.QueueUserWorkItem(AcceptClientAsync, new AcceptWithCancel 
            { 
                socket = _mainSocket, 
                token = _acceptCancellationTokenSource.Token
            });

            return true;
        }

        public bool Init(string ip, int port)
        {
            _timerSystem.Init();
            try
            {
                _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _mainSocket.Connect(ip, port);
                _socketMap.TryAdd(1, _mainSocket);

                ThreadPool.QueueUserWorkItem(RecvAsync, 
                    new Session
                    { 
                        socket = _mainSocket, 
                        sessionID = 1 
                    });
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
                _loopTask?.Wait();
                _loopTask = null;
            }
        }

        async Task RunAsync()
        {
            float elapsedTime = 0;
            while(Interlocked.Equals(_disposed, 0))
            {
                _timerSystem.Update();
                elapsedTime += _timerSystem.deltaTime;
                if(elapsedTime >= _tickInterval)
                {
                    elapsedTime -= _tickInterval;
                    DispatchData();
                    SendMessage();
                }
            }
            _mainSocket.Dispose();
        }

        async void SendMessage()
        {
            while (_msgSendQueue.Count > 0)
            {
                NetMessage sendMessage;
                if (_msgSendQueue.TryDequeue(out sendMessage))
                {
                    Socket sender;
                    if(_socketMap.TryGetValue(sendMessage.sessionID, out sender))
                    {
                        try
                        {
                            var byteTransferred = await sender.SendAsync(sendMessage.data.Memory, SocketFlags.None);
                            Console.WriteLine($"{byteTransferred}byte sent.");
                        }
                        catch (Exception ex) when (ex is SocketException)
                        {
                            SocketException socketException = ex as SocketException;
                            Console.WriteLine($"Socket Exception. Message: {ex.Message}, SocketError No. {socketException.ErrorCode}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unknown Exception. Message: {ex.Message}, {ex.StackTrace}");
                        }
                        finally
                        {
                            sendMessage.Return();
                        }
                    }
                    sendMessage.Return();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}
