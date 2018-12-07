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

        public Tcp2UdpPipe GetPipe(IoSession tcpSession)
        {
            Tcp2UdpPipe retVal = null;
            var key = tcpSession?.Id ?? -1L;
            if (key == -1L)
                return retVal;
            retVal = new Tcp2UdpPipe(tcpSession);
            retVal = _pipes.GetOrAdd(key, retVal);
            return retVal;
        }

        public Tcp2UdpPipe Remove(IoSession tcpSession)
        {
            Tcp2UdpPipe retVal = null;
            var key = tcpSession?.Id ?? -1L;
            if (key == -1L)
                return retVal;
            _pipes.TryRemove(key, out retVal);
            return retVal;
        }
    }
}
