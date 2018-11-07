using Mina.Core.Buffer;
using Mina.Core.Future;
using Mina.Core.Service;
using Mina.Core.Session;
using Mina.Filter.Codec;
using Mina.Transport.Socket;
using System;
using System.Configuration;
using System.Net;
using UCanSoft.PortForwarding.Common.Codec.Direct;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Tcp2Tcp.Core
{
    class AcceptorHandler : IoHandlerAdapter, ISingleInstance
    {
        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(AcceptorHandler).FullName);
        private readonly AttributeKey _pipelineSessionKey = new AttributeKey(typeof(AcceptorHandler), "PipelineSessionKey");
        private readonly IPAddress _forwardingHost = null;
        private readonly Int32? _forwardingPort = null;

        public AcceptorHandler()
        {
            if (!IPAddress.TryParse(ConfigurationManager.AppSettings["ForwardingHost"], out _forwardingHost))
                return;
            if (Int32.TryParse(ConfigurationManager.AppSettings["ForwardingPort"], out Int32 forwardingPort))
                _forwardingPort = forwardingPort;
        }

        public override void SessionOpened(IoSession session)
        {
            _logger.Debug("已建立与[{0}]连接.", session.RemoteEndPoint);
            var forwardingHost = _forwardingHost;
            var forwardingPort = _forwardingPort ?? -1024;
            var remoteHost = session.RemoteEndPoint.ToString();
            if (forwardingHost == null
                || forwardingPort <= 0)
                return;
            var connectorHandler = SingleInstanceHelper<ConnectorHandler>.Instance;
            IoConnector connector = new AsyncSocketConnector();
            connector.FilterChain.AddLast("codec", new ProtocolCodecFilter(new DirectCodecFactory()));
            connector.Handler = connectorHandler;
            IConnectFuture future = connector.Connect(new IPEndPoint(forwardingHost, forwardingPort)).Await();
            var pipeSession = future.Session;
            session.SetAttribute(_pipelineSessionKey, pipeSession);
            pipeSession.SetAttribute(connectorHandler.PipelineSessionKey, session);
        }

        public override void MessageReceived(IoSession session, Object message)
        {
            _logger.Debug("收到[{0}]的消息", session.RemoteEndPoint);
            if (!(message is ArraySegment<Byte> bytes))
                return;
            var pipeSession = session.GetAttribute<IoSession>(_pipelineSessionKey);
            if (pipeSession == null)
                return;
            pipeSession.Write(bytes);
        }

        public override void SessionClosed(IoSession session)
        {
            session.RemoveAttribute(_pipelineSessionKey);
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
