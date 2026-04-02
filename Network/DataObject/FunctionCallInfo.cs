using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Network.DataObject
{
    public class FunctionCallInfo
    {
        public Guid Guid { get; set; }
        public string FunctionName {  get; set; }
        public byte[] ParameterData { get; set; }

    }
}
