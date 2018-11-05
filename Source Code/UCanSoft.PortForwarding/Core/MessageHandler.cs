using Mina.Core.Buffer;
using Mina.Core.Future;
using Mina.Core.Service;
using Mina.Core.Session;
using Mina.Filter.Codec;
using Mina.Transport.Socket;
using System;
using System.Configuration;
using System.Net;
using UCanSoft.PortForwarding.Codec;

namespace UCanSoft.PortForwarding.Core
{
    class MessageHandler : IoHandlerAdapter
    {
        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(MessageHandler).FullName);
        private readonly AttributeKey _pipelineSessionKey = new AttributeKey(typeof(MessageHandler), "PipelineSessionKey");
        private readonly IPAddress _forwardingHost = null;
        private readonly Int32? _forwardingPort = null;

        public MessageHandler()
        {
            if (!IPAddress.TryParse(ConfigurationManager.AppSettings["ForwardingHost"], out _forwardingHost))
                return;
            if (Int32.TryParse(ConfigurationManager.AppSettings["ForwardingPort"], out Int32 forwardingPort))
                _forwardingPort = forwardingPort;
        }

        public override void SessionOpened(IoSession session)
        {
            _logger.Debug("已建立与[{0}]连接.", session.RemoteEndPoint);
            var pipeSession = session.GetAttribute<IoSession>(_pipelineSessionKey);
            var forwardingHost = _forwardingHost;
            var forwardingPort = _forwardingPort ?? -1024;
            if (pipeSession != null
                || forwardingHost == null
                || forwardingPort <= 0)
                return;
            IoConnector connector = new AsyncSocketConnector();
            connector.FilterChain.AddLast("codec", new ProtocolCodecFilter(new CodecFactory()));
            IConnectFuture future = connector.Connect(new IPEndPoint(forwardingHost, forwardingPort)).Await();
            pipeSession = future.Session;
            session.SetAttribute(_pipelineSessionKey, pipeSession);
            pipeSession.SetAttribute(_pipelineSessionKey, session);
            connector.Handler = new MessageHandler();
        }

        public override void MessageSent(IoSession session, object message)
        {
            base.MessageSent(session, message);
        }

        public override void MessageReceived(IoSession session, object message)
        {
            _logger.Debug("收到[{0}]的消息", session.RemoteEndPoint);
            IoBuffer buffer = message as IoBuffer;
            if (buffer == null)
                return;
            var pipeSession = session.GetAttribute<IoSession>(_pipelineSessionKey);
            if (pipeSession == null)
                return;
            pipeSession.Write(buffer);
        }

        public override void SessionClosed(IoSession session)
        {
            _logger.Debug("与[{0}]的连接已断开.", session.RemoteEndPoint);
            session.RemoveAttribute(_pipelineSessionKey);
        }
    }
}
