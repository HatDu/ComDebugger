using System;
using System.Collections.Generic;
using System.Text;

namespace SerialTestConsole
{
    class TransformFactory
    {
        /// <summary>
        /// 把数字字符串转换为十六进制数组
        /// </summary>
        /// <param name="DataStr">待转换的字符串</param>
        /// <returns>一个byte数组</returns>
        public static byte[] StrToHexArray(string DataStr)
        {
            byte[] target = null;
            DataStr = DataStr.Replace(" ", "");
            DataStr = DataStr.Replace("\r", "");
            DataStr = DataStr.Replace("\n", "");

            if (DataStr.Length == 1)
            {
                DataStr = "0" + DataStr;
            }
            else if (DataStr.Length % 2 != 0)
            {
                DataStr = DataStr.Remove(DataStr.Length - 1, 1);
            }

            List<string> DataStrHex = new List<string>();

            for (int i = 0; i < DataStr.Length; i += 2)
            {
                DataStrHex.Add(DataStr.Substring(i, 2));
            }

            target = new byte[DataStrHex.Count];

            int index = 0;
            foreach (string i in DataStrHex)
                target[index++] = (byte)(Convert.ToInt32(i, 16));
            return target;
        }
        /// <summary>
        /// CRC16循环冗余校验
        /// </summary>
        /// <param name="subBuff"></param>
        /// <returns>true 表示该条数据接收正确</returns>
        public static bool CRC16Check(byte[] subBuff)
        {
            if (subBuff.Length < 10)
                return false;
            ushort CRC_Temp = 0xffff;
            int k = 0;
            for (; k < 8; k++)
            {
                CRC_Temp ^= subBuff[k];
                for (int l = 0; l < 8; l++)
                {
                    if ((CRC_Temp & 0x01) != 0x00)
                    {
                        ushort t = (ushort)(CRC_Temp >> 1);
                        CRC_Temp = (ushort)(t ^ 0xa001);
                    }
                    else
                        CRC_Temp = (ushort)(CRC_Temp >> 1);
                }
            }
            ushort CRC16 = subBuff[k + 1];
            CRC16 <<= 8;
            CRC16 |= subBuff[k];
            if (CRC16 == CRC_Temp)
                return true;
            return false;
        }
    }
}
