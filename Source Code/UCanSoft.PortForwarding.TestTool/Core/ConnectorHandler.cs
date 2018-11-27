using Mina.Core.Service;
using Mina.Core.Session;
using System;
using System.IO;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.TestTool.Core
{
    class ConnectorHandler : IoHandlerAdapter, ISingleInstance
    {
        void ISingleInstance.Init()
        { }

        public override void MessageSent(IoSession session, object message)
        {
            if (!(message is ArraySegment<Byte> bytes))
                return;
            using (var stream = File.Open(@".\Save2.data", FileMode.Append))
            {
                stream.Write(bytes.Array, bytes.Offset, bytes.Count);
            }
        }
    }
}
