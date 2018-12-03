using Mina.Core.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UCanSoft.PortForwarding.Common.Codec.Datagram;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Tcp2Udp.Core
{
    class Tcp2UdpPipe
    {
        private readonly ConcurrentQueue<DatagramModel> _tcpDataQueue = new ConcurrentQueue<DatagramModel>();

        private IoSession _tcpSession = null;
        public IoSession TcpSession { get { return this._tcpSession; } }

        private IoSession _udpSession = null;
        public IoSession UdpSession { get { return this._udpSession; } }

        public Int64 TcpSessionId { get { return TcpSession?.Id ?? -1; } }
        public Int64 UdpSessionId { get { return UdpSession?.Id ?? -1; } }

        public Tcp2UdpPipe(IoSession tcpSession)
        {
            Interlocked.Exchange(ref _tcpSession, tcpSession);
            Interlocked.Exchange(ref _udpSession, udpSession);
        }

        public IoSession ChangeUdpSession(IoSession udpSession)
        {
            return Interlocked.Exchange(ref _udpSession, udpSession);
        }

        public void Clear()
        {
            var session = Interlocked.Exchange(ref _tcpSession, null);
            session?.CloseNow();
            session = Interlocked.Exchange(ref _udpSession, null);
            session?.CloseNow();
        }
    }
}
