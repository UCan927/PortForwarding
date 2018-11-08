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
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentQueue<Int64> datagramQueue = new ConcurrentQueue<Int64>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagrams = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentQueue<Int64> synAckQueue = new ConcurrentQueue<Int64>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> synAcks = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseModelId = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly ConcurrentDictionary<Int64, DatagramModel> datagramsUseSynAckId = new ConcurrentDictionary<Int64, DatagramModel>();
        private readonly IoSession _session = null;
        private readonly AttributeKey _pipelineSessionKey = null;
        private readonly ManualResetEventSlim _mEvt = new ManualResetEventSlim(false);
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
            if (Interlocked.CompareExchange(ref _isSendingDatagram, 1, 0) == 1)
                return;
            var token = _cts.Token;
            _sendDatagramTask = Task.Delay(TimeSpan.FromSeconds(1.0D), token)
                                    .ContinueWith((t) => SendDatagram(token));
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
            if (Interlocked.CompareExchange(ref _isSendingSynAck, 1, 0) == 1)
                return;
            var token = _cts.Token;
            _sendSynAckTask = Task.Delay(TimeSpan.FromSeconds(1.0D), token)
                                  .ContinueWith((t) => SendSynAck(token));
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

        private void SendSynAck(CancellationToken token)
        {
            _sendSynAckTask = _sendSynAckTask.ContinueWith((t) =>
            {
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

        private Int64 GenerateId()
        {
            return Interlocked.Increment(ref _idGenerator);
        }
    }
}
