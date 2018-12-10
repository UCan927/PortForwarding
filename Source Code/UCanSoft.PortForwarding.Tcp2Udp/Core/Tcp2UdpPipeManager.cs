using Mina.Core.Service;
using Mina.Core.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCanSoft.PortForwarding.Common.Utility.Helper;

namespace UCanSoft.PortForwarding.Tcp2Udp.Core
{
    class Tcp2UdpPipeManager : SingleInstanceHelper<Tcp2UdpPipeManager>, ISingleInstance
    {
        private ConcurrentDictionary<Int64, Tcp2UdpPipe> _pipes = new ConcurrentDictionary<Int64, Tcp2UdpPipe>();

        void ISingleInstance.Init()
        { }

        public Tcp2UdpPipe Create(IoSession tcpSession, IoConnector udpConnector)
        {
            Tcp2UdpPipe retVal = null;
            var key = tcpSession?.Id ?? -1L;
            if (key == -1L)
                return retVal;
            if(!_pipes.TryGetValue(key, out retVal)
                || retVal == null)
            {
                retVal = new Tcp2UdpPipe(tcpSession, udpConnector);
                retVal = _pipes.GetOrAdd(key, retVal);
            }
            return retVal;
        }


        public Tcp2UdpPipe GetPipe(IoSession tcpSession)
        {
            return GetPipe(tcpSession?.Id ?? -1L);
        }

        public Tcp2UdpPipe GetPipe(Int64 tcpSessionId)
        {
            Tcp2UdpPipe retVal = null;
            var key = tcpSessionId;
            if (key == -1L)
                return retVal;
            _pipes.TryGetValue(key, out retVal);
            return retVal;
        }

        public Tcp2UdpPipe Remove(IoSession tcpSession)
        {
            return Remove(tcpSession?.Id ?? -1L);
        }

        public Tcp2UdpPipe Remove(Int64 tcpSessionId)
        {
            Tcp2UdpPipe retVal = null;
            var key = tcpSessionId;
            if (key == -1L)
                return retVal;
            _pipes.TryRemove(key, out retVal);
            return retVal;
        }
    }
}
