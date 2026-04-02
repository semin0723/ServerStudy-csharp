using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Network.DataObject;

namespace Network.NetworkUtility
{
    public class RPCManager
    {
        private static readonly Dictionary<Type, MethodInfo[]> _rpcMap = new();

        public static void RegistRPC(Type type)
        {
            if (!_rpcMap.TryGetValue(type, out _))
            {
                var methods = type.GetMethods().Where(m => m.GetCustomAttribute<ServerAttribute>() != null).ToArray();
                _rpcMap[type] = methods;
            }
        }
        public static MethodInfo? GetMethodInfo(Type type, string methodname)
        {
            if (_rpcMap.TryGetValue(type, out var methods))
            {
                return methods.FirstOrDefault(m => m.Name == methodname);
            }
            return null;
        }
        
    }
}
