using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;
using System;

namespace UCanSoft.PortForwarding.Common.Codec.Direct
{
    public class DirectDecoder : ProtocolDecoderAdapter
    {
        public override void Decode(IoSession session, IoBuffer input, IProtocolDecoderOutput output)
        {
            var remaining = input.Remaining;
            if (remaining <= 0)
                return;
            Byte[] buffer = new Byte[remaining];
            input.Get(buffer, 0, remaining);
            var bytes = new ArraySegment<Byte>(buffer, 0, remaining);
            output.Write(bytes);
        }
    }
}
