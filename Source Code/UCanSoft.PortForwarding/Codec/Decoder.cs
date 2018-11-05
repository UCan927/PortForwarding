using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Codec
{
    class Decoder : ProtocolDecoderAdapter
    {
        public override void Decode(IoSession session, IoBuffer input, IProtocolDecoderOutput output)
        {
            var remaining = input.Remaining;
            if (remaining <= 0)
                return;
            IoBuffer buffer = IoBuffer.Allocate(remaining);
            buffer.AutoExpand = true;
            buffer.Put(input);
            buffer.Flip();
            output.Write(buffer);
        }
    }
}
