using Mina.Core.Buffer;
using Mina.Core.Service;
using Mina.Core.Session;
using System;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Tcp2Tcp.Core
{
    class ConnectorHandler : IoHandlerAdapter, ISingleInstance
    {
        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(ConnectorHandler).FullName);

        public AttributeKey PipelineSessionKey { get; } = new AttributeKey(typeof(ConnectorHandler), "PipelineSessionKey");
        
        public override void SessionOpened(IoSession session)
        {
            _logger.Debug("已建立与[{0}]连接.", session.RemoteEndPoint);
        }

        public override void MessageReceived(IoSession session, Object message)
        {
            _logger.Debug("收到[{0}]的消息", session.RemoteEndPoint);
            if (!(message is ArraySegment<Byte> bytes))
                return;
            var pipeSession = session.GetAttribute<IoSession>(PipelineSessionKey);
            if (pipeSession == null)
                return;
            pipeSession.Write(bytes);
        }

        public override void SessionClosed(IoSession session)
        {
            session.RemoveAttribute(PipelineSessionKey);
            _logger.Debug("与[{0}]的连接已断开.", session.RemoteEndPoint);
        }

        public override void ExceptionCaught(IoSession session, Exception cause)
        {
            _logger.Error("与[{0}]交互数据时发生异常:\r\n{1}."
                         , session.RemoteEndPoint
                         , cause);
        }

        void ISingleInstance.Init()
        { }
    }
}
