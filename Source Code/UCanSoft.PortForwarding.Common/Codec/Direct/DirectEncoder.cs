using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;
using System;

namespace UCanSoft.PortForwarding.Common.Codec.Direct
{
    public class DirectEncoder : ProtocolEncoderAdapter
    {
        public override void Encode(IoSession session, object message, IProtocolEncoderOutput output)
        {
            if (!(message is Byte[] datas)
                || datas.Length <= 0)
                return;
            var buffer = IoBuffer.Wrap(datas, 0, datas.Length);
            output.Write(buffer);
        }
    }
}
