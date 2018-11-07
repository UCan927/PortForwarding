using Mina.Core.Session;
using Mina.Filter.Codec;
using System;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class DatagramEncoder : ProtocolEncoderAdapter
    {
        public override void Encode(IoSession session, object message, IProtocolEncoderOutput output)
        {
            if (!(message is DatagramModel model))
                return;
            var buffer = model.ToIoBuffer();
            output.Write(buffer);
            model.LastTrySendTime = DateTime.Now;
        }
    }
}
