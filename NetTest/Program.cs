using Network;
using Network.DataObject;
using Network.NetworkUtility.RPC;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace NetTest;

internal class Program
{
    static readonly NetBase net = new NetBase(30);
    static int isEnd = 0;

    static RPCTestClass rpcTestClass = new RPCTestClass(net);

    static async Task Main(string[] args)
    {
        net.Init(9999, 20);
        net.Run();

        var messageprocessing = Task.Run(ProcessMessageAsync);

        while(true)
        {
            string? command = Console.ReadLine();
            if(command == "exit")
            {
                Interlocked.Exchange(ref isEnd, 1);
                break;
            }
        }

        Console.WriteLine("Waiting message process end.");
        await messageprocessing;
        Console.WriteLine("message process end.");

        net.Dispose();
        Console.WriteLine("server end.");
    }
    
    static async Task ProcessMessageAsync()
    {
        try
        {
            var packetQueue = net.packetChannel;
            await foreach (var packet in packetQueue.Reader.ReadAllAsync())
            {
                uint sessionID = packet.sessionID;
                if(packet.packetInfo.packetID == 10)
                {
                    FunctionCallInfo? info = JsonSerializer.Deserialize<FunctionCallInfo>(
                        Encoding.UTF8.GetString(packet.owner.Memory.Span.Slice(0, packet.packetInfo.dataSize))
                        );
                    if (info.FunctionName == "TestRPC")
                    {
                        rpcTestClass.TestRPC_RemoteCall();
                    }
                    else if (info.FunctionName == "TestRPC2")
                    {
                        rpcTestClass.TestRPC2_RemoteCall(info.ParameterData);
                    }
                }
                else
                {
                    string jsondata = Encoding.UTF8.GetString(packet.owner.Memory.Span);
                    Console.WriteLine($"session {sessionID} data: {jsondata}");
                }

                packet.owner.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception caused. msg: {ex.Message}, {ex.StackTrace}");
        }
    }
}

public class TestData
{
    public int Num { get; set; }
    public string Name { get; set; }
}

public partial class RPCTestClass : IRPCCallable
{
    private readonly NetBase _net;
    public Guid Guid { get; private set; }
    public void GuidReset()
    {
        Guid = Guid.NewGuid();
    }

    public RPCTestClass(NetBase net)
    {
        _net = net;
        GuidReset();
        RPCManager.RegistRPC(this);

    }

    [Server]
    public void TestRPC()
    { 
        Console.WriteLine("Client Called TestRPC Function.");
    }

    [Server]
    public void TestRPC2(int a, int b)
    {
        Console.WriteLine($"Client Called TestRPC2 Function.{a}, {b}");
    }

    [Server]
    public void ServerRPC(string changed, int b, double c)
    {
        Console.WriteLine("");
    }
    [Server]
    public void TestRPC3(TestData testData)
    {

    }
}
