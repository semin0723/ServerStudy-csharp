using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;


namespace TestConsole;

internal class Program
{
    static bool disposed = false;

    static void Main(string[] args)
    {
        ArrayPool<byte> pool = ArrayPool<byte>.Create();
        var buf = ArrayPool<byte>.Shared.Rent(10);
        var buffer = pool.Rent(10);
        Console.WriteLine(buffer.Length);

        
        var buffer2 = pool.Rent(8);

        Console.WriteLine(buffer2.Length);
        pool.Return(buffer, true);
    }
}
