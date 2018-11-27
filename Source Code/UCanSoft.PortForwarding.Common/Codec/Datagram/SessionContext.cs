using Mina.Core.Buffer;
using Mina.Core.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class SessionContext
    {
        private readonly NLog.ILogger _logger = null;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentQueue<Int64> datagramQueue = new ConcurrentQueue<Int64>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagrams = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentQueue<Int64> synAckQueue = new ConcurrentQueue<Int64>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> synAcks = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseModelId = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseSynAckId = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly IoSession _session = null;
        private readonly AttributeKey _pipelineSessionKey = null;
        private readonly SpinWait _spin = new SpinWait();
        private Int64 _idGenerator = 0L;
        private Task _sendDatagramTask;
        private Task _sendSynAckTask;
        private Int32 _isSendingSynAck = 0;
        private Int32 _isSendingDatagram = 0;

        public SessionContext(IoSession session, AttributeKey pipelineSessionKey)
        {
            _session = session;
            _pipelineSessionKey = pipelineSessionKey;
            _logger = NLog.LogManager.GetLogger($"{session.Handler.GetType().FullName}.SessionId.{session.Id}");
        }

        public void Enqueue(ArraySegment<Byte> bytes)
        {
            foreach (var buffer in Slice(bytes))
            {
                var id = GenerateId();
                var model = DatagramModel.Create(id, buffer);
                datagramQueue.Enqueue(id);
                datagrams.TryAdd(id, model);
                _logger.Debug("数据包[{0}:{1}]进入队列.", model.Id, model.ShorMd5);
            }
            if (Interlocked.CompareExchange(ref _isSendingDatagram, 1, 0) == 1)
                return;
            var token = _cts.Token;
            _sendDatagramTask = Task.Run(() => SendDatagram(token), token);
        }

        public void HandleSYN(DatagramModel model)
        {
            if (model.Type != DatagramModel.DatagramTypeEnum.SYN)
                return;
            if (datagramsUseModelId.ContainsKey(model.Id))
                return;
            var id = GenerateId();
            var synAckModel = DatagramModel.Create(id, model.Id, DatagramModel.DatagramTypeEnum.SYNACK);
            synAckQueue.Enqueue(id);
            synAcks.TryAdd(id, synAckModel);
            datagramsUseModelId.TryAdd(model.Id, model);
            datagramsUseSynAckId.TryAdd(id, model);
            _logger.Debug("已收到数据包:[{0}:{1}],并将SYNACK[{2}:{3}]加入到队列",
                         model.Id, model.ShorMd5, synAckModel.Id, synAckModel.ShorMd5);
            if (Interlocked.CompareExchange(ref _isSendingSynAck, 1, 0) == 1)
                return;
            var token = _cts.Token;
            _sendSynAckTask = Task.Run(() => SendSynAck(token), token);
        }

        public void HandleSynAck(DatagramModel model)
        {
            if (model.Type != DatagramModel.DatagramTypeEnum.SYNACK)
                return;
            var id = GenerateId();
            var ackModel = DatagramModel.Create(id, model.Id, DatagramModel.DatagramTypeEnum.ACK);
            _session.Write(ackModel);
            if (!model.TryGetAckId(out Int64 modelAckId))
                throw new FormatException("ACK数据包格式不正确.");
            if (!datagrams.ContainsKey(modelAckId))
                return;
            if (datagramQueue.TryDequeue(out Int64 datagramId)
                && datagramId != modelAckId)
                throw new FormatException("ACK数据包Id与Datagram队列不匹配.");
            datagrams.TryRemove(datagramId, out DatagramModel datagram);
            datagram.CancelWait();
            _logger.Debug("收到数据包[{0}:{1}]的确认包，并已将ACK[{2}:{3}]发往远程主机[{4}]",
                         datagram.Id, datagram.ShorMd5, ackModel.Id, ackModel.ShorMd5, _session.RemoteEndPoint);
        }

        public void HandleACK(DatagramModel model)
        {
            if (model.Type != DatagramModel.DatagramTypeEnum.ACK)
                return;
            if (!model.TryGetAckId(out Int64 modelAckId))
                throw new FormatException("ACK数据包格式不正确.");
            if (synAckQueue.TryDequeue(out Int64 ackId)
                && ackId != modelAckId)
                throw new FormatException("ACK数据包Id与SynACK队列不匹配.");
            synAcks.TryRemove(ackId, out DatagramModel synAckModel);
            synAckModel?.CancelWait();
            _logger.Debug("收到ACK[{0}:{1}], SYNACK[{2}:{3}]已被确认.", model.Id, model.ShorMd5, synAckModel?.Id, synAckModel?.ShorMd5);
            if (datagramsUseSynAckId.TryRemove(ackId, out DatagramModel datagram)
                && datagram != null)
            {
                datagramsUseModelId.TryRemove(datagram.Id, out DatagramModel tmpDatagram);
                var pipeSession = _session?.GetAttribute<IoSession>(_pipelineSessionKey);
                if (pipeSession == null)
                    return;
                var slice = datagram.ToIoBuffer().GetSlice(DatagramModel.HeaderLength, datagram.DatagramLength);
                pipeSession.Write(slice.GetRemaining());
            }
        }

        public void Clear()
        {   
            _cts.Cancel();
            while (!datagramQueue.IsEmpty)
            {
                datagramQueue.TryDequeue(out Int64 tmp);
                _logger.Debug("连接已断开, 数据包[{0}]还未发送.", tmp);
            }
            datagrams.Clear();
            while (!synAckQueue.IsEmpty)
            {
                synAckQueue.TryDequeue(out Int64 tmp);
                _logger.Debug("连接已断开, SYNACK[{0}]还未发送.", tmp);
            }
            synAcks.Clear();
            datagramsUseModelId.Clear();
            datagramsUseSynAckId.Clear();
        }

        private void SendDatagram(CancellationToken token)
        {
            _sendDatagramTask = _sendDatagramTask.ContinueWith((t) => {
                DatagramModel model = null;
                try
                {
                    if (!datagramQueue.TryPeek(out Int64 id))
                        return;
                    if (!datagrams.TryGetValue(id, out model)
                        || model == null)
                        return;
                    _session.Write(model);
                    _logger.Debug("数据包[{0}:{1}]已被发送到远程主机[{2}]", model.Id, model.ShorMd5, _session.RemoteEndPoint);
                }
                finally
                {
                    if (!(model?.Wait() ?? false))
                        _spin.SpinOnce();
                    SendDatagram(token);
                }
            }, token);
        }

        private void SendSynAck(CancellationToken token)
        {
            _sendSynAckTask = _sendSynAckTask.ContinueWith((t) =>
            {
                DatagramModel model = null;
                try
                {
                    if (!synAckQueue.TryPeek(out Int64 id))
                        return;
                    if (!synAcks.TryGetValue(id, out model)
                        || model == null)
                        return;
                    _session.Write(model);
                    _logger.Debug("SYNACK[{0}:{1}]已被发送到远程主机[{2}]", model.Id, model.ShorMd5, _session.RemoteEndPoint);
                }
                finally
                {
                    if (!(model?.Wait() ?? false))
                        _spin.SpinOnce();
                    SendSynAck(token);
                }
            }, token);
        }

        private IEnumerable<Byte[]> Slice(ArraySegment<Byte> bytes)
        {
            if (bytes.Count <= DatagramModel.MaxDatagramLength)
                yield return bytes.Array;
            else
            {
                Byte[] retVal = null;
                var buffer = IoBuffer.Wrap(bytes.Array, bytes.Offset, bytes.Count);
                while (buffer.HasRemaining)
                {
                    var remaining = buffer.Remaining;
                    if (remaining <= DatagramModel.MaxDatagramLength)
                        retVal = new Byte[remaining];
                    else
                        retVal = new Byte[DatagramModel.MaxDatagramLength];
                    buffer.Get(retVal, 0, retVal.Length);
                    yield return retVal;
                }
            }
        }

        private Int64 GenerateId()
        {
            return Interlocked.Increment(ref _idGenerator);
        }
    }
}
