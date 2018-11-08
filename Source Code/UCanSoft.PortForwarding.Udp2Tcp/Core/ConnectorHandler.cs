using Mina.Core.Buffer;
using Mina.Core.Service;
using Mina.Core.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UCanSoft.PortForwarding.Common.Codec.Datagram;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Udp2Tcp.Core
{
    class ConnectorHandler : IoHandlerAdapter, ISingleInstance
    {
        public class Context
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly ConcurrentQueue<Int64> datagramQueue = new ConcurrentQueue<Int64>();
            private readonly ConcurrentDictionary<Int64, DatagramModel> datagrams = new ConcurrentDictionary<Int64, DatagramModel>();
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

            public void Enqueue(ArraySegment<Byte> bytes)
            {
                foreach (var buffer in Slice(bytes))
                {
                    var id = GenerateId();
                    var model = DatagramModel.Create(id, bytes.Array);
                    datagramQueue.Enqueue(id);
                    datagrams.TryAdd(id, model);
                }
                if (Interlocked.CompareExchange(ref _isSending, 1, 0) == 1)
                    return;
                var token = _cts.Token;
                _sendDatagramTask = Task.Delay(TimeSpan.FromSeconds(1.0D), token)
                                        .ContinueWith((t) => SendDatagram(token));
            }

            private IEnumerable<Byte[]> Slice(ArraySegment<Byte> bytes)
            {
                if (bytes.Count <= DatagramModel.MaxDatagramLength)
                    yield return bytes.Array;
                else
                {
                    Byte[] retVal = new Byte[DatagramModel.MaxDatagramLength];
                    var buffer = IoBuffer.Wrap(bytes.Array);
                    while (buffer.HasRemaining)
                    {
                        var remaining = buffer.Remaining;
                        if (remaining <= DatagramModel.MaxDatagramLength)
                            retVal = new Byte[remaining];
                        buffer.Get(retVal, 0, retVal.Length);
                        yield return retVal;
                    }
                }
            }

            private void SendDatagram(CancellationToken token)
            {
                _sendDatagramTask = _sendDatagramTask.ContinueWith((t) => {
                    TimeSpan cooldown = TimeSpan.Zero;
                    try
                    {
                        if (!datagramQueue.TryPeek(out Int64 id))
                            return;
                        if (!datagrams.TryGetValue(id, out DatagramModel model)
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

        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(ConnectorHandler).FullName);

        public AttributeKey PipelineSessionKey { get; } = new AttributeKey(typeof(ConnectorHandler), "PipelineSessionKey");

        public override void SessionOpened(IoSession session)
        {
            _logger.Debug("已建立与[{0}]连接.", session.RemoteEndPoint);
        }

        public override void MessageReceived(IoSession session, Object message)
        {
            _logger.Debug("收到[{0}]的消息", session.RemoteEndPoint);
            if (!(message is DatagramModel model))
                return;
            var pipeSession = session.GetAttribute<IoSession>(PipelineSessionKey);
            if (pipeSession == null)
                return;
            if (model.Type == DatagramModel.DatagramTypeEnum.SYN)
            { }
            else if (model.Type == DatagramModel.DatagramTypeEnum.SYNACK)
            { }
            else if (model.Type == DatagramModel.DatagramTypeEnum.ACK)
            {

            }
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
