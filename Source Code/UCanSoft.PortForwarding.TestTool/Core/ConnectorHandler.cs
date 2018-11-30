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
        { }
    }
}
