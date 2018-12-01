using Mina.Core.Buffer;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
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
        private static readonly Boolean _checkData = true && _fillMD5;
        public static readonly String ConstHeaderFlag = "OTW";
        public static readonly ReadOnlyCollection<Byte> HeaderFlagBytes = new ReadOnlyCollection<Byte>(Encoding.ASCII.GetBytes(ConstHeaderFlag));
        public static readonly Int32 HeaderFlagIndex = 0;
        public static readonly Int32 HeaderFlagLength = HeaderFlagBytes.Count;                          //03
        public static readonly Int32 TypeIndex = HeaderFlagIndex + HeaderFlagLength;                    //03
        public static readonly Int32 TypeLength = sizeof(Byte);                                         //01
        public static readonly Int32 IdIndex = TypeIndex + TypeLength;                                  //04
        public static readonly Int32 IdLength = Guid.NewGuid().ToByteArray().Length;                    //16
        public static readonly Int32 Md5Index = IdIndex + IdLength;                                     //20
        public static readonly Int32 Md5Length = Cryptography.ComputeMD5Hash().Length;                  //16
        public static readonly Int32 SrcTcpSessionIdIndex = Md5Index + Md5Length;                       //36
        public static readonly Int32 SrcTcpSessionIdLength = sizeof(Int64);                             //08
        public static readonly Int32 DatasLengthIndex = SrcTcpSessionIdIndex + SrcTcpSessionIdLength;   //44
        public static readonly Int32 DatasLengthLength = sizeof(UInt16);                                //02
        public static readonly Int32 DatasIndex = DatasLengthIndex + DatasLengthLength;                 //46
        public static readonly Int32 HeaderLength = HeaderFlagLength + TypeLength
                                                    + IdLength + Md5Length
                                                    + SrcTcpSessionIdLength + DatasLengthLength;        //46
        public static readonly Int32 MaxDatasLength = 1024;
        private static readonly TimeSpan _cooldown = TimeSpan.FromSeconds(30.0D);

        private readonly ManualResetEvent _mEvt = new ManualResetEvent(false);
        private Byte[] _datagram = null;

        public String HeaderFlag { get; private set; }
        public DatagramTypeEnum Type { get; private set; }
        public String Id { get; private set; }
        public String Md5 { get; private set; }
        public Int64 SrcTcpSessionId { get; private set; } = -1024;
        public UInt16 DatasLength { get { return (UInt16)Datas.Count; } }
        public ReadOnlyCollection<Byte> Datas { get; private set; }
        public DateTime? LastSendTime { get; set; } = null;
        public TimeSpan Cooldown { get { return GetCooldown(); } }

        private DatagramModel()
        { }

        public static DatagramModel Create(Byte[] datagram)
        {
            DatagramModel retVal = null;
            if (datagram.IsNullOrEmpty()
                || datagram.Length < HeaderLength)
                return retVal;
            retVal = new DatagramModel();
            retVal.HeaderFlag = Encoding.ASCII.GetString(datagram, HeaderFlagIndex, HeaderFlagLength);
            if (retVal.HeaderFlag != ConstHeaderFlag)
                throw new BadImageFormatException("数据包包头不匹配.");
            retVal.Type = (DatagramTypeEnum)datagram[TypeIndex];
            retVal.Id = datagram.ToHex(IdIndex, IdLength);
            retVal.Md5 = datagram.ToHex(Md5Index, Md5Length);
            retVal.SrcTcpSessionId = BitConverter.ToInt64(datagram, SrcTcpSessionIdIndex);
            var datasLength = BitConverter.ToUInt16(datagram, DatasLengthIndex);
            if (datasLength > MaxDatasLength)
                throw new IndexOutOfRangeException("数据包长度超长.");
            if (datasLength != datagram.Length - HeaderLength)
                throw new IndexOutOfRangeException("数据包描述长度与实际长度不匹配.");
            var datas = new Byte[datasLength];
            Array.Copy(datagram, DatasIndex, datas, 0, datasLength);
            var realMd5 = GetMd5(datas);
            if (_checkData && realMd5 != retVal.Md5)
                throw new BadImageFormatException("MD5效验失败!");
            retVal.Datas = new ReadOnlyCollection<Byte>(datas);
            retVal._datagram = datagram;
            return retVal;
        }

        public static DatagramModel Create(String id, Int64 srcTcpSessionId, Byte[] datas)
        {
            datas = datas ?? new Byte[0];
            if (datas.Length > MaxDatasLength)
                throw new IndexOutOfRangeException("buffer长度超长.");
            DatagramModel retVal = new DatagramModel
            {
                HeaderFlag = ConstHeaderFlag,
                Type = DatagramTypeEnum.SYN,
                Id = id,
                Md5 = GetMd5(datas),
                SrcTcpSessionId = srcTcpSessionId,
                Datas = new ReadOnlyCollection<Byte>(datas)
            };
            return retVal;
        }

        public static DatagramModel Create(String id, Int64 srcTcpSessionId, String ackId, DatagramTypeEnum type)
        {
            if (type != DatagramTypeEnum.ACK
                && type != DatagramTypeEnum.SYNACK)
                throw new ArgumentException("该方法只能创建SYNACK或ACK数据包");
            var buffer = ackId.ToHex();
            DatagramModel retVal = new DatagramModel
            {
                HeaderFlag = ConstHeaderFlag,
                Type = type,
                Id = id,
                Md5 = GetMd5(buffer),
                SrcTcpSessionId = srcTcpSessionId,
                Datas = new ReadOnlyCollection<Byte>(buffer)
            };
            return retVal;
        }

        public IoBuffer ToIoBuffer()
        {
            IoBuffer retVal = null;
            if (_datagram == null)
            {
                _datagram = new Byte[HeaderLength + this.DatasLength];
                var buffer = Encoding.ASCII.GetBytes(this.HeaderFlag);
                Array.Copy(buffer, 0, _datagram, HeaderFlagIndex, HeaderFlagLength);
                _datagram[TypeIndex] = (Byte)this.Type;
                buffer = this.Id.ToHex();
                Array.Copy(buffer, 0, _datagram, IdIndex, IdLength);
                buffer = this.Md5.ToHex();
                Array.Copy(buffer, 0, _datagram, Md5Index, Md5Length);
                buffer = BitConverter.GetBytes(this.SrcTcpSessionId);
                Array.Copy(buffer, 0, _datagram, SrcTcpSessionIdIndex, SrcTcpSessionIdLength);
                buffer = BitConverter.GetBytes(this.DatasLength);
                Array.Copy(buffer, 0, _datagram, DatasLengthIndex, DatasLengthLength);
                this.Datas.CopyTo(_datagram, DatasIndex);
            }
            retVal = IoBuffer.Wrap(_datagram, 0, _datagram.Length);
            return retVal;
        }
        
        public Boolean Wait()
        {
            return _mEvt.WaitOne(Cooldown);
        }

        public void CancelWait()
        {
            _mEvt.Set();
        }

        private static String GetMd5(Byte[] buffer)
        {
            String retVal = null;
            var md5Bytes = new Byte[0];
            if (_fillMD5)
                md5Bytes = Cryptography.ComputeMD5Hash(buffer);
            else
                md5Bytes = Cryptography.ComputeMD5Hash();
            retVal = md5Bytes.ToHex();
            return retVal;
        }

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
    }
}
