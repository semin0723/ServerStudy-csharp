using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        async Task AcceptClientAsync(AcceptWithCancel sender)
        {
            if (sender == null)
            {
                throw new InvalidOperationException("sender is null.");
            }

            Socket listenSocket = sender.socket;

            while (true)
            {
                try
                {
                    var remoteSocket = await listenSocket.AcceptAsync(cancellationToken: sender.token);

                    Session newSession;
                    if (_sessionPool.Count == 0)
                    {
                        newSession = new Session
                        {
                            socket = remoteSocket, 
                            sessionID = ++_globalSessionID 
                        };
                    }
                    else
                    {
                        newSession = _sessionPool.First<Session>();
                        newSession.socket = remoteSocket;
                        _sessionPool.RemoveAt(0);
                    }

                    NetMessage netMsg = _netMessagePool.Rent();
                    netMsg.MessageState = 1;
                    netMsg.sessionID = newSession.sessionID;
                    netMsg.socket = remoteSocket;

                    await _connectionEventChannel.Writer.WriteAsync(netMsg);

                    _ = Task.Run(() => RecvAsync(newSession));
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

        async Task RecvAsync(Session session)
        {
            if(session == null)
            {
                throw new InvalidOperationException("sender is null.");
            }
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

                    await _recvChannel.Writer.WriteAsync(netMsg);
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

                        await _connectionEventChannel.Writer.WriteAsync(netMsg);

                        _sessionPool.Add(session);
                    }
                }

                if(result == -1)
                {
                    break;
                }
            }
        }

        async Task SendAsync()
        {
            await foreach(NetMessage sendMessage in _sendChannel.Reader.ReadAllAsync())
            {
                try
                {
                    uint sessionID = sendMessage.sessionID;
                    if (_socketMap.TryGetValue(sessionID, out var socket))
                    {
                        var sliced = sendMessage.data.Memory.Slice(0, sendMessage.byteCount);
                        var byteTransferred = await socket.SendAsync(sliced, SocketFlags.None);
                        Console.WriteLine($"{byteTransferred}byte sent.");
                    }
                    else
                    {
                        throw new SocketAlreadyClosed("이미 종료된 소켓입니다.");
                    }
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
        }
    }
}
