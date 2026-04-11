using Network;
using Network.DataObject;
using Network.NetworkUtility.RPC;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace NetTest_Client;

internal class Program
{
    static async Task Main(string[] args)
    {
        NetBase net = new NetBase(30);
        net.Init("127.0.0.1", 9999);
        net.Run();

        int sendcount = 0;

        RPCTestClass testclass = new(net);

        while(true)
        {
            string input = Console.ReadLine();
            if(input == "exit")
            {
                break;
            }
            if (input == "rpc1")
            {
                testclass.TestRPC();
            }
            else if(input == "rpc2")
            {
                testclass.TestRPC2(10, 20);
            }

        }

        net.Dispose();
        Console.WriteLine("All clients end.");
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
    public partial void TestRPC();
    [Server]
    public partial void TestRPC2(int a, int b);
    [Server]
    public partial void ServerRPC(string changed, int b, double c);
    [Server]
    public partial void TestRPC3(TestData data);
}