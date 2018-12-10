using Mina.Core.Buffer;
using Mina.Core.Service;
using Mina.Core.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UCanSoft.PortForwarding.Common.Codec.Datagram;
using UCanSoft.PortForwarding.Common.Extended;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Tcp2Udp.Core
{
    class Tcp2UdpPipe
    {
        private readonly NLog.ILogger _logger = null;
        private readonly ConcurrentQueue<DatagramModel> _tcpDataQueue = new ConcurrentQueue<DatagramModel>();

        private IoSession _tcpSession = null;
        public IoSession TcpSession { get { return this._tcpSession; } }

        private IoSession _udpSession = null;
        public IoSession UdpSession { get { return this._udpSession; } }

        public Int64 TcpSessionId { get { return TcpSession?.Id ?? -1; } }
        public Int64 UdpSessionId { get { return UdpSession?.Id ?? -1; } }

        public Tcp2UdpPipe(IoSession tcpSession, IoConnector udpConnector)
        {
            Interlocked.Exchange(ref _tcpSession, tcpSession);
            _logger = NLog.LogManager.GetLogger($"{tcpSession.Handler.GetType().FullName}.SessionId.{tcpSession.Id}");
        }

        public IoSession ChangeUdpSession(IoSession udpSession)
        {
            return Interlocked.Exchange(ref _udpSession, udpSession);
        }

        public void Enqueue(Byte[] datas)
        {
            if (datas.IsNullOrEmpty())
                return;
            foreach (var data in Slice(datas))
            {
                var id = DatagramModel.GenerateId();
                var model = DatagramModel.Create(id, TcpSessionId, data);
                _tcpDataQueue.Enqueue(model);
                _logger.Debug("数据包[{0}]进入队列.", model.Id);
            }
        }

        public void Clear()
        {
            var session = Interlocked.Exchange(ref _tcpSession, null);
            session?.CloseNow();
            session = Interlocked.Exchange(ref _udpSession, null);
            session?.CloseNow();
            while(!_tcpDataQueue.IsEmpty)
            {
                _tcpDataQueue.TryDequeue(out DatagramModel model);
            }
        }

        private IEnumerable<Byte[]> Slice(Byte[] datas)
        {
            Byte[] retVal = null;
            var buffer = IoBuffer.Wrap(datas, 0, datas.Length);
            while (buffer.HasRemaining)
            {
                var remaining = buffer.Remaining;
                if (remaining <= DatagramModel.MaxDatasLength)
                    retVal = new Byte[remaining];
                else
                    retVal = new Byte[DatagramModel.MaxDatasLength];
                buffer.Get(retVal, 0, retVal.Length);
                yield return retVal;
            }
        }
    }
}
