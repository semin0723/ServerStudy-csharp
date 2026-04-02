using Network;
using Network.DataObject;
using Network.NetworkUtility;
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


public partial class RPCTestClass
{
    private readonly NetBase _net;
    public Guid _guid;

    public RPCTestClass(NetBase net)
    {
        _net = net;
        _guid = Guid.NewGuid();
        RPCManager.RegistRPC(this.GetType());
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