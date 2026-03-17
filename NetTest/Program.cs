using Network;
using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace NetTest;

internal class Program
{
    static readonly NetBase net = new NetBase();
    static ConcurrentDictionary<uint, int> clients = new ConcurrentDictionary<uint, int>();
    static void Main(string[] args)
    {
        net.Init(9999, 20);
        net.Run();

        ConcurrentQueue<NetMessage>? sendMessageQueue = net.msgSendQueue;

        CancellationTokenSource recvCancelTokenSource = new();

        Task.Run(ProcessRecvDataAsync, recvCancelTokenSource.Token);

        while (true)
        {
            Thread.Sleep(1000);
            byte[] msg = Encoding.UTF8.GetBytes("Server SendMessage.");
            foreach (var client in clients)
            {
                sendMessageQueue.Enqueue(
                    new NetMessage
                    {
                        MessageState = 0,
                        byteCount = msg.Length,
                        data = msg,
                        sessionID = client.Key
                    }
                );
            }
        }
    }
    static async void ProcessRecvDataAsync()
    {
        ConcurrentQueue<NetMessage>? recvMessageQueue = net.msgRecvQueue;
        while(true)
        {
            if (recvMessageQueue.Count < 1)
            {
                continue;
            }
            while(recvMessageQueue.Count > 0)
            {
                NetMessage msg;
                bool res = recvMessageQueue.TryDequeue(out msg);
                if(!res)
                {
                    continue;
                }

                switch(msg.MessageState)
                {
                    case -1:
                        int value;
                        clients.TryRemove(msg.sessionID, out value);
                        Console.WriteLine($"Client{msg.sessionID} Disconnected.");
                        break;
                    case 0:
                        string clientMessage = Encoding.UTF8.GetString(msg.data, 0, msg.byteCount);
                        Console.WriteLine($"Recv ClientNum: {msg.sessionID}, Msg: {clientMessage}");
                        break;
                    case 1:
                        clients.TryAdd(msg.sessionID, 0);
                        Console.WriteLine($"Client{msg.sessionID} Connected.");
                        break;
                }
            }
        }
    }
}
