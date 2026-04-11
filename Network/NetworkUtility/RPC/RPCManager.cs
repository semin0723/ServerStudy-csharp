using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Network.DataObject;
using Network.Exceptions;

namespace Network.NetworkUtility.RPC
{
    public class RPCManager
    {
        private static readonly Dictionary<Type, MethodInfo[]> _rpcMap = new();
        private static readonly Dictionary<Guid, IRPCCallable> _rpcObjectMap = new();

        public static void RegistRPC(IRPCCallable callable)
        {
            Type objectType = callable.GetType();
            if (!_rpcMap.TryGetValue(objectType, out _))
            {
                var methods = objectType.GetMethods().Where(m => m.GetCustomAttribute<ServerReceiveAttribute>() != null).ToArray();
                _rpcMap[objectType] = methods;
            }
            _rpcObjectMap.Add(callable.Guid, callable);
        }
        public static MethodInfo? GetMethodInfo(Type type, string methodname)
        {
            if (_rpcMap.TryGetValue(type, out var methods))
            {
                return methods.FirstOrDefault(m => m.Name == methodname);
            }
            return null;
        }
        
        public static void CallRPC(FunctionCallInfo callInfo)
        {
            var remoteCallName = $"{callInfo.FunctionName}_RemoteCall";
            if(_rpcObjectMap.TryGetValue(callInfo.Guid, out var callable))
            {
                var methodInfo = GetMethodInfo(callable.GetType(), remoteCallName);
                if(methodInfo == null)
                {
                    throw new RPCMethodNotFound("RPC 함수를 찾을 수 없습니다.");
                }
                methodInfo.Invoke(callable, new object[] { callInfo.ParameterData });
            }
        }
    }
}
