using Network;
using System.Collections.Concurrent;
using System.Text;

namespace NetTest_Client;

internal class Program
{
    static void Main(string[] args)
    {
        NetBase net = new NetBase();
        net.Init("127.0.0.1", 9999);
        net.Run();

        ConcurrentQueue<NetMessage>? sendMessageQueue = net.msgSendQueue;
        ConcurrentQueue<NetMessage>? recvMessageQueue = net.msgRecvQueue;

        int count = 0;

        while(true)
        {
            Thread.Sleep(1000);

            if(count == 60)
            {
                break;
            }

            while(recvMessageQueue.Count > 0)
            {
                NetMessage msg;
                bool res = recvMessageQueue.TryDequeue(out msg);
                if(!res)
                {
                    continue;
                }
                string serverMsg = Encoding.UTF8.GetString(msg.data, 0, msg.byteCount);
                Console.WriteLine($"Server Message: {serverMsg}");
            }
            byte[] message = Encoding.UTF8.GetBytes("Client Message.");

            sendMessageQueue.Enqueue(new NetMessage
            {
                MessageState = 0,
                byteCount = message.Length,
                data = message,
                sessionID = 1
            });

            count++;
        }

        net.Dispose();
    }
}
