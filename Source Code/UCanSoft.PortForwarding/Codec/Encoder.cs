using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Codec
{
    class Encoder : ProtocolEncoderAdapter
    {
        public override void Encode(IoSession session, object message, IProtocolEncoderOutput output)
        {
            IoBuffer buffer = message as IoBuffer;
            if (buffer == null
                || buffer.Remaining <= 0)
                return;
            output.Write(buffer);
        }
    }
}
