using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Tcp2Tcp.Codec
{
    class CodecFactory : IProtocolCodecFactory
    {
        private readonly Encoder _encoder;
        private readonly Decoder _decoder;

        public CodecFactory()
        {
            _encoder = new Encoder();
            _decoder = new Decoder();
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
