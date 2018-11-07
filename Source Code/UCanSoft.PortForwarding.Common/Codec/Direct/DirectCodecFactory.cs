using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Common.Codec.Direct
{
    public class DirectCodecFactory : IProtocolCodecFactory
    {
        private readonly DirectEncoder _encoder;
        private readonly DirectDecoder _decoder;

        public DirectCodecFactory()
        {
            _encoder = new DirectEncoder();
            _decoder = new DirectDecoder();
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
