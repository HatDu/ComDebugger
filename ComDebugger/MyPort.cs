using System;
using System.IO;
using System.IO.Ports;

namespace SerialTestConsole
{
    class MyPort : SerialPort
    {
        /// <summary>
        /// 无参构造函数，串口选取一般设置
        /// </summary>
        public MyPort() { }

        public new string Open()
        {
            try
            {
                base.Open();
            }catch
            {
                return "UNABLE";
            }
            return "OPEN";
        }
        public new string Close()
        {
            try
            {
                //DiscardInBuffer();
                base.Close();
            }
            catch (IOException e)
            {
                return "UNABLE";
            }

            return "CLOSED";
        }

        private int Send(byte[] sendBuffer)
        {
            int sendCount = 0;
            int sendtimes = (sendBuffer.Length / 1000);

            for (int i = 0; i < sendtimes; i++)
            {
                this.Write(sendBuffer, i * 1000, 1000);
                sendCount += 1000;
            }

            if (sendBuffer.Length % 1000 != 0)
            {
                this.Write(sendBuffer, sendtimes * 1000, sendBuffer.Length % 1000);
                sendCount += sendBuffer.Length % 1000;
            }
            return sendCount;
        }

        /// <summary>
        /// 发送十六进制数据
        /// </summary>
        /// <param name="sendBuffer">十六进制数据数组</param>
        public int SendHex(string sendData)
        {
            byte[] sendBuffer = TransformFactory.StrToHexArray(sendData);
            return Send(sendBuffer);
        }

        /// <summary>
        /// 发送ASCII数据
        /// </summary>
        /// <param name="sendData"></param>
        public int SendASCII(string sendData)
        {
            byte[] sendBuffer = System.Text.Encoding.Default.GetBytes(sendData);
            return Send(sendBuffer);
        }
    }
}
