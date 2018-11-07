using System;
using System.Security.Cryptography;
using System.Text;
using UCanSoft.PortForwarding.Common.Extended;

namespace UCanSoft.PortForwarding.Common.Utility.Algorithms
{
    #region 密码算法类
    /// <summary>
    /// 密码算法类
    /// </summary>
    public static class Cryptography
    {
        #region Field Define
        private static Byte[] _emptyBytesMD5 = null;
        #endregion

        #region .Ctor
        /// <summary>
        /// .Ctor
        /// </summary>
        static Cryptography()
        {
            using (MD5 md5 = MD5.Create())
            {
                _emptyBytesMD5 = md5.ComputeHash(new Byte[0]);
                md5.Clear();
            }
        }
        #endregion

        #region AES加密(128位密钥)
        /// <summary>
        /// AES加密(128位密钥)
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="pwd"></param>
        /// <returns></returns>
        public static String AESEncrypt(String msg, String pwd)
        {
            String retVal = null;
            msg = msg ?? String.Empty;
            pwd = pwd ?? String.Empty;
            using (MD5 md5 = MD5.Create())
            {
                Byte[] pwdBytes = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(pwd));
                Byte[] msgBytes = UTF8Encoding.UTF8.GetBytes(msg);
                using (RijndaelManaged rDel = new RijndaelManaged())
                {
                    rDel.Key = pwdBytes;
                    rDel.Mode = CipherMode.ECB;//ECB加密模式
                    rDel.Padding = PaddingMode.PKCS7;//PKCS7填充模式
                    using (ICryptoTransform cTransform = rDel.CreateEncryptor())
                    {
                        Byte[] encryptedMsgBytes = cTransform.TransformFinalBlock(msgBytes, 0, msgBytes.Length);
                        retVal = Convert.ToBase64String(encryptedMsgBytes, 0, encryptedMsgBytes.Length);
                    }
                    rDel.Clear();
                }
                md5.Clear();
            }
            return retVal;
        }
        #endregion

        #region AES解密(128位密钥)
        /// <summary>
        /// 尝试AES解密(128位密钥)
        /// </summary>
        /// <param name="encryptedMsg"></param>
        /// <param name="pwd"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Boolean TryAESDecrypt(String encryptedMsg, String pwd, out String msg)
        {
            Boolean retVal = true;
            msg = null;
            try { msg = AESDecrypt(encryptedMsg, pwd); }
            catch { retVal = false; }
            return retVal;
        }

        /// <summary>
        /// AES解密(128位密钥)
        /// </summary>
        /// <param name="encryptedMsg"></param>
        /// <param name="pwd"></param>
        /// <returns></returns>
        public static String AESDecrypt(String encryptedMsg, String pwd)
        {
            String retVal = null;
            pwd = pwd ?? String.Empty;
            using (MD5 md5 = MD5.Create())
            {
                Byte[] pwdBytes = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(pwd));
                Byte[] encryptedMsgBytes = Convert.FromBase64String(encryptedMsg);
                using (RijndaelManaged rDel = new RijndaelManaged())
                {
                    rDel.Key = pwdBytes;
                    rDel.Mode = CipherMode.ECB;//ECB加密模式
                    rDel.Padding = PaddingMode.PKCS7;//PKCS7填充模式
                    using (ICryptoTransform cTransform = rDel.CreateDecryptor())
                    {
                        Byte[] msgBytes = cTransform.TransformFinalBlock(encryptedMsgBytes, 0, encryptedMsgBytes.Length);
                        retVal = UTF8Encoding.UTF8.GetString(msgBytes);
                    }
                    rDel.Clear();
                }
                md5.Clear();
            }
            return retVal;
        }
        #endregion

        #region 比对HASH是否匹配
        /// <summary>
        /// 比对HASH是否匹配
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="macKey"></param>
        /// <param name="msgMac"></param>
        /// <returns></returns>
        public static Boolean IsMsgMacMatch(String msg, String macKey, String msgMac)
        {
            return GetMsgMac(msg, macKey) == (msgMac ?? String.Empty);
        }
        #endregion

        #region 带密钥HASH
        /// <summary>
        /// 带密钥HASH
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="macKey"></param>
        /// <returns></returns>
        public static String GetMsgMac(String msg, String macKey)
        {
            String retVal = null;
            msg = msg ?? String.Empty;
            macKey = macKey ?? String.Empty;
            using (MD5 md5 = MD5.Create())
            {
                Byte[] macKeyBytes = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(macKey));
                Byte[] msgBytes = UTF8Encoding.UTF8.GetBytes(msg);
                using (HMACMD5 mac = new HMACMD5(macKeyBytes))
                {
                    Byte[] hashData = mac.ComputeHash(msgBytes);
                    retVal = Convert.ToBase64String(hashData, 0, hashData.Length);
                    mac.Clear();
                }
                md5.Clear();
            }
            return retVal;
        }
        #endregion

        #region 计算MD5哈希值
        /// <summary>
        /// 计算MD5哈希值
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Byte[] ComputeMD5Hash(Byte[] data = null)
        {
            Byte[] retVal = null;
            if (data.IsNullOrEmpty())
            {
                retVal = ComputeEmptyArrayMD5Hash();
                return retVal;
            }
            using (MD5 md5 = MD5.Create())
            {
                retVal = md5.ComputeHash(data);
                md5.Clear();
            }
            return retVal;
        }

        /// <summary>
        /// 计算MD5哈希值
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static Byte[] ComputeMD5Hash(Byte[] data, Int32 offset, Int32 count)
        {
            Byte[] retVal = null;
            if (count == 0)
            {
                retVal = ComputeMD5Hash();
                return retVal;
            }
            using (MD5 md5 = MD5.Create())
            {
                retVal = md5.ComputeHash(data, offset, count);
                md5.Clear();
            }
            return retVal;
        }

        /// <summary>
        /// 计算空字节数组MD5哈希值
        /// </summary>
        /// <returns></returns>
        private static Byte[] ComputeEmptyArrayMD5Hash()
        {
            Byte[] retVal = new Byte[_emptyBytesMD5.Length];
            Buffer.BlockCopy(_emptyBytesMD5, 0, retVal, 0, _emptyBytesMD5.Length);
            return retVal;
        }
        #endregion
    }
    #endregion
}
