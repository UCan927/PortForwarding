using Mina.Core.Buffer;
using Mina.Core.Future;
using Mina.Core.Service;
using Mina.Core.Session;
using Mina.Filter.Codec;
using Mina.Transport.Socket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UCanSoft.PortForwarding.Common.Codec.Datagram;
using UCanSoft.PortForwarding.Common.Codec.Direct;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Udp2Tcp.Core
{
    class AcceptorHandler : IoHandlerAdapter, ISingleInstance
    {
        public class Context
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly ConcurrentQueue<Int64> synAckQueue = new ConcurrentQueue<Int64>();
            private readonly ConcurrentDictionary<Int64, DatagramModel> synAcks = new ConcurrentDictionary<Int64, DatagramModel>();
            private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseModelId = new ConcurrentDictionary<Int64, DatagramModel>();
            private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseSynAckId = new ConcurrentDictionary<Int64, DatagramModel>();
            private readonly IoSession _session = null;
            private readonly ManualResetEventSlim _mEvt = new ManualResetEventSlim(false);
            private readonly SpinWait _spin = new SpinWait();
            private Int64 _idGenerator = 0L;
            private Task _sendDatagramTask;
            private Int32 _isSending = 0;

            public Context(IoSession session)
            {
                _session = session;
            }

            public void Enqueue(DatagramModel model)
            {
                if (datagramsUseModelId.ContainsKey(model.Id))
                    return;
                var id = GenerateId();
                var synAckModel = DatagramModel.Create(id, model.Id, DatagramModel.DatagramTypeEnum.SYNACK);
                synAckQueue.Enqueue(id);
                synAcks.TryAdd(id, synAckModel);
                datagramsUseModelId.TryAdd(model.Id, model);
                datagramsUseSynAckId.TryAdd(id, model);
                if (Interlocked.CompareExchange(ref _isSending, 1, 0) == 1)
                    return;
                var token = _cts.Token;
                _sendDatagramTask = Task.Delay(TimeSpan.FromSeconds(1.0D), token)
                                        .ContinueWith((t) => SendDatagram(token));
            }

            private void SendDatagram(CancellationToken token)
            {
                _sendDatagramTask = _sendDatagramTask.ContinueWith((t) => {
                    TimeSpan cooldown = TimeSpan.Zero;
                    try
                    {
                        if (!synAckQueue.TryPeek(out Int64 id))
                            return;
                        if (!synAcks.TryGetValue(id, out DatagramModel model)
                            || model == null)
                            return;
                        cooldown = model.Cooldown;
                        if (cooldown != TimeSpan.Zero)
                            return;
                        _session.Write(model);
                    }
                    finally
                    {
                        if (cooldown != TimeSpan.Zero)
                            _mEvt.Wait(cooldown, token);
                        else
                            _spin.SpinOnce();
                        SendDatagram(token);
                    }
                }, token);
            }

            private Int64 GenerateId()
            {
                return Interlocked.Increment(ref _idGenerator);
            }
        }

        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(AcceptorHandler).FullName);
        private readonly AttributeKey _pipelineSessionKey = new AttributeKey(typeof(AcceptorHandler), "PipelineSessionKey");
        private readonly AttributeKey _contextKey = new AttributeKey(typeof(AcceptorHandler), "ContextKey");
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
            if (!(message is DatagramModel model))
                return;
            var pipeSession = session.GetAttribute<IoSession>(_pipelineSessionKey);
            if (pipeSession == null)
                return;
            if(model.Type == DatagramModel.DatagramTypeEnum.SYN)
            {
                var context = this.GetContext(session, _contextKey);
                context.Enqueue(model);
            }
            else if(model.Type == DatagramModel.DatagramTypeEnum.SYNACK)
            {

            }
            else if(model.Type == DatagramModel.DatagramTypeEnum.ACK)
            {

            }
            //pipeSession.Write(bytes);
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

        private Context GetContext(IoSession session, AttributeKey key)
        {
            var retVal = session?.GetAttribute<Context>(key);
            if (retVal == null)
            {
                retVal = new Context(session);
                session?.SetAttribute(key, retVal);
            }
            return retVal;
        }
    }
}
