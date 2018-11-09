using Mina.Core.Buffer;
using System;
using System.Collections.ObjectModel;
using System.Text;
using UCanSoft.PortForwarding.Common.Extended;
using UCanSoft.PortForwarding.Common.Utility.Algorithms;

namespace UCanSoft.PortForwarding.Common.Codec.Datagram
{
    public class DatagramModel
    {
        public enum DatagramTypeEnum : Byte
        {
            SYN = 0,
            SYNACK = 1,
            ACK = 2
        }

        private static readonly Boolean _fillMD5 = true;
        private static readonly Boolean _checkData = true;
        public static readonly String ConstHeaderFlag = "OTW";
        public static readonly ReadOnlyCollection<Byte> HeaderFlagBytes = new ReadOnlyCollection<Byte>(Encoding.ASCII.GetBytes(ConstHeaderFlag));
        public static readonly Int32 HeaderFlagIndex = 0;
        public static readonly Int32 HeaderFlagLength = HeaderFlagBytes.Count; //3
        public static readonly Int32 DatagramTyepIndex = HeaderFlagIndex + HeaderFlagLength; //3
        public static readonly Int32 DatagramTypeLength = 1;
        public static readonly Int32 DatagramIdIndex = DatagramTyepIndex + DatagramTypeLength; //4
        public static readonly Int32 DatagramIdLength = 8;
        public static readonly Int32 DatagramMD5Index = DatagramIdIndex + DatagramIdLength; //12
        public static readonly Int32 DatagramMD5Length = 10;
        public static readonly Int32 DatagramLengthIndex = DatagramMD5Index + DatagramMD5Length;
        public static readonly Int32 DatagramLengthLength = sizeof(UInt16);
        public static readonly Int32 DatagramIndex = DatagramLengthIndex + DatagramLengthLength;
        public static readonly Int32 MaxDatagramLength = 1000;
        public static readonly Int32 HeaderLength = HeaderFlagLength + DatagramTypeLength + DatagramIdLength + DatagramMD5Length + DatagramLengthLength;
        private static readonly TimeSpan _cooldown = TimeSpan.FromSeconds(5.0D);

        private Byte[] _buffer = null;
        private Int64? _ackId = null;

        public String HeaderFlag { get; private set; }
        public DatagramTypeEnum Type { get; private set; }
        public Int64 Id { get; private set; }
        public String ShorMd5 { get; private set; } 
        public UInt16 DatagramLength { get { return (UInt16)Datagram.Count; } }
        public DateTime? LastSendTime { get; set; } = null;
        public TimeSpan Cooldown { get { return GetCooldown(); } }

        private TimeSpan GetCooldown()
        {
            TimeSpan retVal = TimeSpan.Zero;
            var lastSendTime = LastSendTime ?? DateTime.MinValue;
            var interval = DateTime.Now - lastSendTime;
            if (interval >= _cooldown)
                return retVal;
            retVal = _cooldown - interval;
            return retVal;
        }

        public ReadOnlyCollection<Byte> Datagram { get; private set; }

        private DatagramModel()
        { }

        public static DatagramModel Create(IoBuffer buffer)
        {
            DatagramModel retVal = null;
            if (buffer == null
                || buffer.Remaining < HeaderLength)
                return retVal;
            retVal = new DatagramModel();
            try
            {
                buffer.Mark();
                var bytes = buffer.GetRemaining().Array;
                retVal.HeaderFlag = Encoding.ASCII.GetString(bytes, HeaderFlagIndex, HeaderFlagLength);
                if (retVal.HeaderFlag != ConstHeaderFlag)
                    throw new BadImageFormatException("数据包包头不匹配.");
                retVal.Type = (DatagramTypeEnum)bytes[DatagramTyepIndex];
                retVal.Id = BitConverter.ToInt64(bytes, DatagramIdIndex);
                var shortMd5Bytes = new Byte[DatagramMD5Length];
                Array.Copy(bytes, DatagramMD5Index, shortMd5Bytes, 0, DatagramMD5Length);
                retVal.ShorMd5 = BitConverter.ToString(shortMd5Bytes).Replace("-", String.Empty);
                var datagramLength = BitConverter.ToUInt16(bytes, DatagramLengthIndex);
                if (datagramLength > MaxDatagramLength)
                    throw new IndexOutOfRangeException("数据包长度超长.");
                if (datagramLength != bytes.Length - HeaderLength)
                    throw new IndexOutOfRangeException("数据包描述长度与实际长度不匹配.");
                var datagram = new Byte[datagramLength];
                Array.Copy(bytes, DatagramIndex, datagram, 0, datagramLength);
                var realShorMd5 = GetShorMd5(datagram);
                if (_checkData && realShorMd5 != retVal.ShorMd5)
                    throw new BadImageFormatException("MD5效验失败!");
                retVal.Datagram = new ReadOnlyCollection<Byte>(datagram);
                if ((retVal.Type == DatagramTypeEnum.SYNACK
                    || retVal.Type == DatagramTypeEnum.ACK)
                    && retVal.DatagramLength == sizeof(Int64))
                {
                    var ackId = BitConverter.ToInt64(datagram, 0);
                    retVal._ackId = ackId;
                }
            }
            finally
            {
                buffer.Reset();
            }
            return retVal;
        }

        public static DatagramModel Create(Int64 id, Byte[] buffer)
        {
            buffer = buffer ?? new Byte[0];
            if (buffer.Length > MaxDatagramLength)
                throw new IndexOutOfRangeException("buffer长度超长.");
            DatagramModel retVal = new DatagramModel
            {
                HeaderFlag = ConstHeaderFlag,
                Type = DatagramTypeEnum.SYN,
                Id = id,
                ShorMd5 = GetShorMd5(buffer),
                Datagram = new ReadOnlyCollection<Byte>(buffer)
            };
            return retVal;
        }

        public static DatagramModel Create(Int64 id, Int64 ackId, DatagramTypeEnum type)
        {
            if (type != DatagramTypeEnum.ACK
                && type != DatagramTypeEnum.SYNACK)
                throw new ArgumentException("该只能创建SYNACK或ACK数据包");
            var buffer = BitConverter.GetBytes(ackId);
            DatagramModel retVal = new DatagramModel
            {
                HeaderFlag = ConstHeaderFlag,
                Type = type,
                Id = id,
                ShorMd5 = GetShorMd5(buffer),
                Datagram = new ReadOnlyCollection<Byte>(buffer),
                _ackId = ackId
            };
            return retVal;
        }

        public IoBuffer ToIoBuffer()
        {
            IoBuffer retVal = null;
            if (_buffer != null)
                retVal = IoBuffer.Wrap(_buffer);
            else
            {
                retVal = IoBuffer.Allocate(HeaderLength + this.DatagramLength);
                var buffer = Encoding.ASCII.GetBytes(this.HeaderFlag);
                retVal.Put(buffer);
                retVal.Put((Byte)this.Type);
                buffer = BitConverter.GetBytes(this.Id);
                retVal.Put(buffer);
                buffer = this.ShorMd5.ToHex();
                retVal.Put(buffer);
                buffer = BitConverter.GetBytes(this.DatagramLength);
                retVal.Put(buffer);
                buffer = new Byte[this.DatagramLength];
                this.Datagram.CopyTo(buffer, 0);
                retVal.Put(buffer);
                retVal.Flip();
                _buffer = retVal.GetRemaining().Array;
            }
            return retVal;
        }

        public Boolean TryGetAckId(out Int64 ackId)
        {
            Boolean retVal = false;
            ackId = 0L;
            if (!this._ackId.HasValue)
                return retVal;
            retVal = true;
            ackId = this._ackId.Value;
            return retVal;
        }
        
        private static String GetShorMd5(Byte[] buffer)
        {
            String retVal = null;
            var md5Bytes = new Byte[0];
            if (_fillMD5)
                md5Bytes = Cryptography.ComputeMD5Hash(buffer);
            else
                md5Bytes = Cryptography.ComputeMD5Hash();
            var shortMd5Bytes = new Byte[DatagramMD5Length];
            Array.Copy(md5Bytes, 0, shortMd5Bytes, 0, 3);
            Array.Copy(md5Bytes, 6, shortMd5Bytes, 3, 4);
            Array.Copy(md5Bytes, 13, shortMd5Bytes, 7, 3);
            retVal = BitConverter.ToString(shortMd5Bytes).Replace("-", String.Empty);
            return retVal;
        }
    }
}
