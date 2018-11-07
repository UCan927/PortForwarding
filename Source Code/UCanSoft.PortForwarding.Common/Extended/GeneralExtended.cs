using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace UCanSoft.PortForwarding.Common.Extended
{
    #region 基本扩展
    /// <summary>
    /// 基本扩展
    /// </summary>
    public static class GeneralExtended
    {
        #region 获取对象的属性的值
        /// <summary>
        /// 获取对象的属性的值
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Object GetPropertyValue(this Object obj, String propertyName)
        {
            var type = obj.GetType();
            var pi = type.GetProperty(propertyName);
            if (pi == null)
                return null;
            return pi.GetValue(obj);
        }
        #endregion

        #region DataTable是否有数据
        /// <summary>
        /// DataTable是否有数据
        /// </summary>
        /// <param name="srcDataTable"></param>
        /// <returns></returns>
        public static Boolean IsNullOrEmpty(this DataTable srcDataTable)
        {
            Boolean retVal = false;
            retVal = (srcDataTable?.Rows).IsNullOrEmpty()
                     || (srcDataTable?.Columns).IsNullOrEmpty();
            return retVal;
        }
        #endregion

        #region DataSet是否有数据
        /// <summary>
        /// DataSet是否有数据
        /// </summary>
        /// <param name="srcDataSet"></param>
        /// <returns></returns>
        public static Boolean IsNullOrEmpty(this DataSet srcDataSet)
        {
            Boolean retVal = false;
            retVal = (srcDataSet?.Tables).IsNullOrEmpty()
                     || (srcDataSet.Tables[0]).IsNullOrEmpty();
            return retVal;
        }
        #endregion

        #region IEnumerable是否有数据
        /// <summary>
        /// IEnumerable是否有数据
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Boolean IsNullOrEmpty(this IEnumerable source)
        {
            Boolean retVal = false;
            retVal = source == null;
            if (retVal)
                return retVal;
            ICollection collection = source as ICollection;
            if (collection != null)
                retVal = collection.Count <= 0;
            else
            {
                IEnumerator enumerator = source.GetEnumerator();
                retVal = enumerator == null ||
                         !enumerator.MoveNext();
            }
            return retVal;
        }
        #endregion

        #region 合并相邻相同的字符
        /// <summary>
        /// 合并相邻相同的字符
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static String Merger(this String source, params Char[] mergerChars)
        {
            String retVal = String.Empty;
            if (String.IsNullOrWhiteSpace(source))
                return retVal;
            if (mergerChars.IsNullOrEmpty())
            {
                StringBuilder strBld = new StringBuilder();
                Char previous = source[0];
                strBld.Append(previous);
                foreach (Char current in source)
                {
                    try
                    {
                        if (current == previous)
                            continue;
                        strBld.Append(current);
                    }
                    finally
                    {
                        previous = current;
                    }
                }
                retVal = strBld.ToString();
            }
            else
            {
                foreach (var @char in mergerChars)
                {
                    String oldValue = new String(@char, 2);
                    String newValue = new String(@char, 1);
                    while (source.Contains(oldValue))
                    {
                        source = source.Replace(oldValue, newValue);
                    }
                }
                retVal = source;
            }
            return retVal;
        }
        #endregion

        #region 将字节数组输出为16进制字符串
        /// <summary>
        /// 将字节数组输出为16进制字符串
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static String ToHex(this Byte[] src)
        {
            String retVal = String.Empty;
            if (src.IsNullOrEmpty())
                return retVal;
            retVal = BitConverter.ToString(src);
            return retVal;
        }

        /// <summary>
        /// 将字节数组输出为16进制字符串
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static String ToHex(this Byte[] src, Int32 index, Int32 count)
        {
            String retVal = String.Empty;
            if (src.IsNullOrEmpty())
                return retVal;
            retVal = BitConverter.ToString(src, index, count);
            return retVal;
        }
        #endregion

        #region 将字符串按指定长度分割成列表
        /// <summary>
        /// 将字符串按指定长度分割成列表
        /// </summary>
        /// <param name="src"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public static List<String> Split(this String src, Int32 len)
        {
            List<String> retVal = new List<String>();
            if (String.IsNullOrEmpty(src))
                return retVal;
            Int32 offset = 0;
            do
            {
                String item = src.Substring(offset, Math.Min(src.Length - offset, len));
                retVal.Add(item);
                offset += len;
            } while (offset < src.Length);
            return retVal;
        }
        #endregion

        #region 将16进制字符串转换为字节数组
        /// <summary>
        /// 将16进制字符串转换为字节数组
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static Byte[] ToHex(this String src)
        {
            return src.ToHex(0, src == null ? 0 : src.Length);
        }
        #endregion

        #region 将16进制字符串转换为字节数组
        /// <summary>
        /// 将16进制字符串转换为字节数组
        /// </summary>
        /// <param name="src"></param>
        /// <param name="index"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public static Byte[] ToHex(this String src, Int32 index, Int32 len)
        {
            List<Byte> retVal = new List<Byte>();
            if (String.IsNullOrWhiteSpace(src)
                || src.Length % 2 != 0
                || index < 0
                || len <= 0
                || (index + len) > src.Length)
                return retVal.ToArray();
            src = src.Substring(index, len);
            List<String> hexList = src.Split(2);
            foreach (String item in hexList)
                retVal.Add(Convert.ToByte(item, 16));
            return retVal.ToArray();
        }
        #endregion

        #region 扩展CoboBox方法:选择具有Value的Item
        /// <summary>
        /// 扩展CoboBox方法:选择具有Value的Item
        /// </summary>
        /// <param name="cbBox"></param>
        /// <param name="value"></param>
        public static void SelectByValue(this ComboBox cbBox, Object value)
        {
            if (cbBox == null
                || cbBox.Items.IsNullOrEmpty()
                || value == null)
                return;
            Object item;
            KeyValuePair<String, Object> keyValuePair;
            for (Int32 i = 0; i < cbBox.Items.Count; i++)
            {
                item = cbBox.Items[i];
                if (item == null
                    || !(item is KeyValuePair<String, Object>))
                    continue;
                keyValuePair = (KeyValuePair<String, Object>)item;
                if (keyValuePair.Value != null
                    && keyValuePair.Value.ToString() == value.ToString())
                {
                    cbBox.SelectedIndex = i;
                    break;
                }
            }
        }
        #endregion

        #region 扩展TextBox方法:连续追加文本
        /// <summary>
        /// 扩展TextBox方法:连续追加文本
        /// </summary>
        /// <param name="txtBox"></param>
        /// <param name="values"></param>
        public static void AppendTexts(this TextBox txtBox, String[] values)
        {
            if (values == null || values.Length <= 0)
                return;
            if (txtBox.Multiline)
            {
                for (Int32 i = 0; i < values.Length - 1; i++)
                {
                    txtBox.AppendText(String.Format("{0}{1}", values[i], Environment.NewLine));
                }
                txtBox.AppendText(values[values.Length - 1]);
            }
            else
            {
                for (Int32 i = 0; i < values.Length - 1; i++)
                {
                    txtBox.AppendText(String.Format("{0}    ", values[i]));
                }
                txtBox.AppendText(values[values.Length - 1]);
            }
        }
        #endregion

        #region 扩展Form方法:显示警告对话框
        /// <summary>
        /// 扩展Form方法:显示警告对话框
        /// </summary>
        /// <param name="fromObj"></param>
        /// <param name="msg"></param>
        /// <param name="title"></param>
        public static DialogResult ShowMsg(this Form fromObj
                                  , String msg
                                  , String title = "警告"
                                  , MessageBoxButtons msgBtn = MessageBoxButtons.OK
                                  , MessageBoxIcon msgIcon = MessageBoxIcon.Warning)
        {
            return MessageBox.Show(msg, title, msgBtn, msgIcon);
        }
        #endregion

        #region 扩展JsonSerializer方法:匿名反序列化JSON
        /// <summary>
        /// 扩展JsonSerializer方法:匿名反序列化JSON
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="reader"></param>
        /// <param name="anonymousTypeObject"></param>
        /// <returns></returns>
        public static T DeserializeAnonymousType<T>(this Newtonsoft.Json.JsonSerializer serializer
                                                   , Newtonsoft.Json.JsonReader reader, T anonymousTypeObject)
        {
            return serializer.Deserialize<T>(reader);
        }
        #endregion

        public static int NextInt32(this Random rng)
        {
            unchecked
            {
                int firstBits = rng.Next(0, 1 << 4) << 28;
                int lastBits = rng.Next(0, 1 << 28);
                return firstBits | lastBits;
            }
        }

        public static decimal NextDecimalSample(this Random random)
        {
            var sample = 1m;
            //After ~200 million tries this never took more than one attempt but it is possible to generate combinations of a, b, and c with the approach below resulting in a sample >= 1.
            while (sample >= 1)
            {
                var a = random.NextInt32();
                var b = random.NextInt32();
                //The high bits of 0.9999999999999999999999999999m are 542101086.
                var c = random.Next(542101087);
                sample = new Decimal(a, b, c, false, 28);
            }
            return sample;
        }

        public static decimal NextDecimal(this Random random)
        {
            return NextDecimal(random, decimal.MaxValue);
        }

        public static decimal NextDecimal(this Random random, decimal maxValue)
        {
            return NextDecimal(random, decimal.Zero, maxValue);
        }

        public static decimal NextDecimal(this Random random, decimal minValue, decimal maxValue)
        {
            var nextDecimalSample = NextDecimalSample(random);
            return maxValue * nextDecimalSample + minValue * (1 - nextDecimalSample);
        }
    }
    #endregion
}
