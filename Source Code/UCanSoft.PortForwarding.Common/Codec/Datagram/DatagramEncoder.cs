﻿using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class DatagramEncoder : ProtocolEncoderAdapter
    {
        public override void Encode(IoSession session, object message, IProtocolEncoderOutput output)
        {
            if (!(message is IoBuffer buffer)
                || buffer.Remaining <= 0)
                return;
            output.Write(buffer);
        }
    }
}
