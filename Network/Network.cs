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
        private int _disposed;
        private Task? _loopTask;

        private Socket? _mainSocket;
        private CancellationTokenSource _acceptCancellationTokenSource;

        private List<Session> _sessionPool;
        private ConcurrentDictionary<uint, Socket>? _socketMap;

        public ConcurrentQueue<NetMessage>? msgRecvQueue { get; private set; }
        // Client에서 데이터를 전송 시 사용하는 SessionID는 1번 입니다.
        public ConcurrentQueue<NetMessage>? msgSendQueue { get; private set; }
        private ConcurrentQueue<SendSet>? pendingSendQueue;

        private NetMessageFactory _netMessagePool;

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
            _timerSystem = new TimerSystem();
            _disposed = 0;
            _acceptCancellationTokenSource = new CancellationTokenSource();
            _sessionPool = new List<Session>();
            _netMessagePool = new NetMessageFactory(100);
            _socketMap = new ConcurrentDictionary<uint, Socket>();

            msgRecvQueue = new ConcurrentQueue<NetMessage>();
            msgSendQueue = new ConcurrentQueue<NetMessage>();
            pendingSendQueue = new ConcurrentQueue<SendSet>();
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

                ThreadPool.QueueUserWorkItem(RecvAsync, new Session { socket = _mainSocket, sessionID = 1 });
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
                    SendMessage();
                }
            }
            _mainSocket.Dispose();
        }

        async void SendMessage()
        {
            float elapsedTime = 0;
            while (pendingSendQueue.Count > 0)
            {
                if(elapsedTime >= _sendTimeoutInterval)
                {
                    break;
                }

                SendSet ss;
                bool res = pendingSendQueue.TryDequeue(out ss);
                if (res == false)
                {
                    Thread.Sleep(1);
                    continue;
                }

                ThreadPool.QueueUserWorkItem(SendAsync, ss);

                elapsedTime += _timerSystem.deltaTime;
            }

            elapsedTime = 0f;

            while(msgSendQueue.Count > 0)
            {
                if (elapsedTime >= _sendTimeoutInterval)
                {
                    break;
                }

                NetMessage message;
                bool res = msgSendQueue.TryDequeue(out message);
                if (res == false)
                {
                    Thread.Sleep(1);
                    continue;
                }
                Socket sender;
                res = _socketMap.TryGetValue(message.sessionID, out sender);
                if(!res)
                {
                    Console.WriteLine($"Invalid SessionID. ID: {message.sessionID}");
                    continue;
                }

                /*SendSet ss = new SendSet()
                {
                    senderSocket = sender,
                    data = message.data,
                };*/

                //ThreadPool.QueueUserWorkItem(SendAsync, ss);

                elapsedTime += _timerSystem.deltaTime;
            }
        }
    }
}
