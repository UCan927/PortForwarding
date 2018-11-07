using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class DatagramCodecFactory : IProtocolCodecFactory
    {
        private readonly DatagramEncoder _encoder;
        private readonly DatagramDecoder _decoder;

        public DatagramCodecFactory()
        {
            _encoder = new DatagramEncoder();
            _decoder = new DatagramDecoder();
        }

        IProtocolEncoder IProtocolCodecFactory.GetEncoder(IoSession session)
        {
            return _encoder;
        }

        IProtocolDecoder IProtocolCodecFactory.GetDecoder(IoSession session)
        {
            return _decoder;
        }
    }
}
