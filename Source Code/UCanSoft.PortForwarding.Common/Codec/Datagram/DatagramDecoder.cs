using Mina.Core.Buffer;
using Mina.Core.Session;
using Mina.Filter.Codec;
using System;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class DatagramDecoder : CumulativeProtocolDecoder
    {
        private readonly AttributeKey _headerFlagBufferKey = new AttributeKey(typeof(DatagramDecoder), "HeaderFlagBufferKey");
        private readonly AttributeKey _totalPackageLengthBufferKey = new AttributeKey(typeof(DatagramDecoder), "TotalPackageLengthBufferKey");

        protected override Boolean DoDecode(IoSession session, IoBuffer input, IProtocolDecoderOutput output)
        {
            Boolean retVal = false;
            var headerFlagBuffer = this.GetHeaderFlagBuffer(session);
            var headerIndex = this.SearchHeader(input, headerFlagBuffer);
            if (headerIndex == -1)
                return retVal;
            input.Position = headerIndex;
            if (input.Remaining < DatagramModel.HeaderLength)
                return retVal;
            else
            {
                var datagramLengthBytes = this.GetDatagramLengthBuffer(session);
                input.Skip(DatagramModel.HeaderLength - DatagramModel.DatagramLengthLength);
                input.Get(datagramLengthBytes, 0, datagramLengthBytes.Length);
                var datagramLength = BitConverter.ToUInt16(datagramLengthBytes, 0);
                if (input.Remaining < datagramLength)
                    input.Position = headerIndex;
                else
                {
                    var buffer = input.GetSlice(headerIndex, DatagramModel.HeaderLength + datagramLength);
                    input.Skip(datagramLength);
                    var msg = DatagramModel.Create(buffer);
                    output.Write(msg);
                    retVal = true;
                }
            }
            return retVal;
        }

        private Int32 SearchHeader(IoBuffer input, Byte[] headerFlagBuffer)
        {
            Int32 retVal = -1;
            while (input.Remaining >= DatagramModel.HeaderFlagLength)
            {
                input.Mark();
                input.Get(headerFlagBuffer, 0, headerFlagBuffer.Length);
                Boolean different = false;
                for (Int32 i = 0; i < DatagramModel.HeaderFlagLength; i++)
                {
                    different = headerFlagBuffer[i] != DatagramModel.HeaderFlagBytes[i];
                    if (different)
                        break;
                }
                if (different)
                {
                    input.Reset();
                    input.Position++;
                    continue;
                }
                else
                {
                    retVal = input.Position - DatagramModel.HeaderFlagLength;
                    break;
                }
            }
            return retVal;
        }

        private Byte[] GetHeaderFlagBuffer(IoSession session)
        {
            Byte[] retVal = session.GetAttribute<Byte[]>(_headerFlagBufferKey);
            if (retVal == null)
            {
                retVal = new Byte[DatagramModel.HeaderFlagLength];
                session.SetAttribute(_headerFlagBufferKey, retVal);
            }
            return retVal;
        }

        private Byte[] GetDatagramLengthBuffer(IoSession session)
        {
            Byte[] retVal = session.GetAttribute<Byte[]>(_totalPackageLengthBufferKey);
            if (retVal == null)
            {
                retVal = new Byte[DatagramModel.DatagramLengthLength];
                session.SetAttribute(_totalPackageLengthBufferKey, retVal);
            }
            return retVal;
        }
    }
}
