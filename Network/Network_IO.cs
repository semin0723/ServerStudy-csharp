using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Network
{
    public partial class NetBase
    { 
        internal class SendSet
        {
            public Socket senderSocket;
            public byte[]? data;
        }

        private uint _globalSessionID = 0;

        async void AcceptClientAsync(object? sender)
        {
            var senderData = sender as AcceptWithCancel;
            if (senderData == null)
            {
                throw new InvalidOperationException("sender is null.");
            }

            Socket listenSocket = senderData.socket;

            while (true)
            {
                try
                {
                    var clientSocket = await listenSocket.AcceptAsync();

                    Session newSession;
                    if (_sessionPool.Count == 0)
                    {
                        newSession = new Session
                        {
                            socket = clientSocket, 
                            sessionID = ++_globalSessionID 
                        };
                    }
                    else
                    {
                        newSession = _sessionPool.First<Session>();
                        newSession.socket = clientSocket;
                        _sessionPool.RemoveAt(0);
                    }
                    bool res = _socketMap.TryAdd(newSession.sessionID, clientSocket);
                    if(!res)
                    {
                        _socketMap[newSession.sessionID] = clientSocket;
                    }

                    NetMessage netMsg = _netMessagePool.Rent();
                    netMsg.MessageState = 1;
                    netMsg.sessionID = newSession.sessionID;

                    _msgRecvQueue.Enqueue(netMsg);

                    ThreadPool.QueueUserWorkItem(RecvAsync, newSession);
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    Console.WriteLine($"Accept Canceled.");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AcceptClientAsync Exception. Message: {ex.Message}, {ex.StackTrace}");
                    return;
                }
            }
        }

        async void RecvAsync(object? sender)
        {
            if(sender == null)
            {
                throw new InvalidOperationException("sender is null.");
            }
            Session session = sender as Session;
            Socket client = session.socket;

            while(true)
            {
                int result = 0;
                try
                {
                    IMemoryOwner<byte> recvBuffer = MemoryPool<byte>.Shared.Rent(_bufferSize);

                    var byteTransferred = await client.ReceiveAsync(recvBuffer.Memory, SocketFlags.None);
                    if(byteTransferred == 0)
                    {
                        throw new ClientShutDownException("Client shutdown socket.");
                    }

                    NetMessage netMsg = _netMessagePool.Rent();
                    netMsg.MessageState = 0;
                    netMsg.sessionID = session.sessionID;
                    netMsg.byteCount = byteTransferred;
                    netMsg.data = recvBuffer;

                    _msgRecvQueue.Enqueue(netMsg);
                }
                catch(Exception ex) when (ex is SocketException)
                {
                    SocketException socketException = ex as SocketException;
                    Console.WriteLine($"Socket Exception. Message: {ex.Message}, SocketError No. {socketException.ErrorCode}");
                    result = -1;
                }
                catch(Exception ex) when (ex is ClientShutDownException)
                {
                    Console.WriteLine($"Client Disconnected.");
                    result = -1;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unknown Exception. Message: {ex.Message}, {ex.StackTrace}");
                    result = -1;
                }
                finally
                {
                    if(result == -1)
                    {
                        NetMessage netMsg = _netMessagePool.Rent();
                        netMsg.MessageState = -1;
                        netMsg.sessionID = session.sessionID;

                        _msgRecvQueue.Enqueue(netMsg);

                        Socket socket;
                        _socketMap.TryRemove(session.sessionID, out socket);

                        socket.Close();
                        socket.Dispose();

                        _sessionPool.Add(session);
                    }
                }

                if(result == -1)
                {
                    break;
                }
            }
        }

        async void SendAsync(object? sender)
        {
            SendSet sendData = sender as SendSet;

            Socket socket = sendData.senderSocket;
            try
            {
                var byteTransferred = await socket.SendAsync(sendData.data, SocketFlags.None);
                Console.WriteLine($"{byteTransferred}byte sent.");
            }
            catch (Exception ex) when (ex is SocketException)
            {
                SocketException socketException = ex as SocketException;
                Console.WriteLine($"Socket Exception. Message: {ex.Message}, SocketError No. {socketException.ErrorCode}");

                pendingSendQueue.Enqueue(sendData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unknown Exception. Message: {ex.Message}, {ex.StackTrace}");
            }
        }
    }
}
