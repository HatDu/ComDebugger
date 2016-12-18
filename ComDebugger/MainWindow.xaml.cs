using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using SerialTestConsole;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;
using GMap.NET;
using GMap.NET.MapProviders;
using System.Device;
using System.Device.Location;

namespace ComDebugger
{
    public partial class MainWindow : Window
    {
        #region Normal Poet Variable
        private int TABINDEX = 0;
        private MyPort ComPort = new MyPort();
        private string[] ports;
        IList<Customer> comList = new List<Customer>(); //可用串口集合  
        private bool isFixFunctionTab = false;          //是否可以直接切换TabControl
        private DispatcherTimer autoSendTimer = new DispatcherTimer();  //自动发送定时器
        #endregion

        #region 虚拟示波器变量定义区
        List<byte[]> recBuffers = new List<byte[]>();   //串口缓冲区的数据，待CRC16循环冗余验证
        private DispatcherTimer WaveDataDeal = new DispatcherTimer();  //定时处理缓冲区数据
        private double DATADEALTimeSpan = 10;                              //定时处理周期
        List<byte> SB = new List<byte>();
        List<byte[]> waveDatas = new List<byte[]>();    //已处理好的缓冲数据，等待Plotter展示
        private double WAVESHOWTimeSpan = 10;                               //波形展示周期

        ulong XAXIS = 0;                                                //XAXIS横坐标偏移变量

        List<LineGraph> LineGraphs = new List<LineGraph>();
        List<CheckBox> WaveCheckBoxs = new List<CheckBox>();
        ObservableDataSource<Point> []DataSources = new ObservableDataSource<Point>[4];
        #endregion

        #region Camera
        int DpiX, DpiY;
        private int imageMode = 0;
        WriteableBitmap WB1;
        WriteableBitmap WB2;
        List<byte[]> ImageBuffData= new List<byte[]>();         //待处理的图像数据
        List<byte[]> ImageData = new List<byte[]>();      //二值化图像
                                                          //List<byte[]> GrayImageData = new List<byte[]>();        //灰度图像数据
                                                          //DispatcherTimer ImageDataDealTimer = new DispatcherTimer(); //图像Buffer处理
                                                          //DispatcherTimer ImageShowTimer = new DispatcherTimer();     //图像展示
        #endregion

        #region 网络调试
        Color systemColor = Colors.Teal;
        Color normalTextColor = Colors.Black;
        Color recNotifyColor = Colors.LimeGreen;
        Color sendNotifyColor = Colors.Blue;
        IPAddress LocalIP;
        const int TcpBufferSize = 8192;    //缓存大小
        const int MaxClientCount = 50;
        TcpListener TcpSever;
        bool isTcpListening = false;
        TcpClient[] remoteClients;  //远程连接客户端
        Thread[] Workers;           //用于启用或关闭与客户端连接的线程
        List<NetworkStream> clientNetworkStream = new List<NetworkStream>();
        List<TcpClient> connectedRemoteClient=new List<TcpClient>();
        public delegate void ComboxUIFunc(string itemContent);

        private TcpClient LocalClient;    //本地客户端
        private Thread clientThread;      //客户端线程
        private NetworkStream clientStreamToSever = null;
        private bool isClientRun = false;
        int ClientConnectPortNum = 8500;
        string ConnectSeverIP;
        int NETTabIndex;

        private UdpClient UDPScend=null;
        private bool isUDPListening=false;
        private UdpClient UDPReceive =null;
        private List<IPEndPoint> UDPRemoteIpeps=new List<IPEndPoint>();
        private int UDPLocalIPPort;
        #endregion

        #region GPS定位

        #endregion
        public MainWindow()
        {
            InitializeComponent();
        }

        #region 初始化区
        private void NetInit()
        {
            #region Sever
            LocalIP = GetLocalAddressIP();
            TcpSever = new TcpListener(LocalIP, Convert.ToInt32(SeverPortNumTbx.Text));
            ///TcpSever.ReceiveTimeout = 2000;
            remoteClients = new TcpClient[Convert.ToInt32(MaxClientrCountTbx.Text)];
            Workers = new Thread[Convert.ToInt32(MaxClientrCountTbx.Text)];
            TCPSeverIPTbx.Text = LocalIP.ToString();
            TCPClientIPTbx.Text = LocalIP.ToString();
            #endregion

            #region Client
            LocalClient = new TcpClient();
            //LocalClient.ReceiveTimeout = clientReceiveTimeout;
            ClientConnectPortNum = Convert.ToInt32(TCPClientPortNumTbx.Text);
            ConnectSeverIP = TCPClientIPTbx.Text;
            #endregion

            #region UDP
            UDPLocalIPTbx.Text = TCPClientIPTbx.Text;
            UDPTargetIPTbx.Text = TCPClientIPTbx.Text;
            UDPLocalIPPort = Convert.ToInt32(UDPLocalPort.Text);
            IPEndPoint localEP = new IPEndPoint(LocalIP, UDPLocalIPPort);
            UDPScend = new UdpClient(localEP);

            #endregion
        }

        private void CameraVariableInit()
        {
            //ImageDataDealTimer.Interval = TimeSpan.FromMilliseconds(100);
            CameraVariableSet();
        }
        private void WaveVariableInit()
        {
            //设置定时器
            WaveDataDeal.Interval = TimeSpan.FromMilliseconds(DATADEALTimeSpan);
            WaveDataDeal.Tick += new EventHandler(BufferDataDeal);
            setWaveTimer(WAVESHOWTimeSpan);
        
            //初始化LineGraphs
            LineGraphs.Add(Line1);
            LineGraphs.Add(Line2);
            LineGraphs.Add(Line3);
            LineGraphs.Add(Line4);

            //初始化CheckBoxs
            WaveCheckBoxs.Add(waveCH1);
            WaveCheckBoxs.Add(waveCH2);
            WaveCheckBoxs.Add(waveCH3);
            WaveCheckBoxs.Add(waveCH4);

            for(int i=0;i<WaveCheckBoxs.Count;i++)
                WaveCheckBoxs[i].IsChecked = true;
            InitWaveData(); //初始化图标线条数据
        }

        private void AddPortEventHandler()
        {
            ComPort.DataReceived += new SerialDataReceivedEventHandler(Port_NormalReceived);
            ComPort.DataReceived += new SerialDataReceivedEventHandler(Port_WaveShow);
            ComPort.DataReceived += new SerialDataReceivedEventHandler(Port_CameraDebug);
        }
        private void PortOptionInit()
        {
            //↓↓↓↓↓↓↓↓↓可用串口下拉控件↓↓↓↓↓↓↓↓↓ 
            ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    comList.Add(new Customer() { com = ports[i] });
                }
                portName.ItemsSource = comList;
                portName.DisplayMemberPath = "com";
                portName.SelectedValuePath = "com";
                portName.SelectedValue = ports[0];
            }
            else//未检测到串口  
            {
                //MessageBox.Show("无可用串口");
            }

            //↓↓↓↓↓↓↓↓↓波特率下拉控件↓↓↓↓↓↓↓↓↓
            IList<Customer> rateList = new List<Customer>();//可用波特率集合
            rateList.Add(new Customer() { BaudRate = "9600" });
            rateList.Add(new Customer() { BaudRate = "19200" });
            rateList.Add(new Customer() { BaudRate = "38400" });
            rateList.Add(new Customer() { BaudRate = "57600" });
            rateList.Add(new Customer() { BaudRate = "115200" });
            rateList.Add(new Customer() { BaudRate = "230400" });
            baudRate.ItemsSource = rateList;
            baudRate.DisplayMemberPath = "BaudRate";
            baudRate.SelectedValuePath = "BaudRate";

            //↓↓↓↓↓↓↓↓↓校验位下拉控件↓↓↓↓↓↓↓↓↓  
            IList<Customer> comParity = new List<Customer>();//可用校验位集合  
            comParity.Add(new Customer() { Parity = "None", ParityValue = "0" });
            comParity.Add(new Customer() { Parity = "Odd", ParityValue = "1" });
            comParity.Add(new Customer() { Parity = "Even", ParityValue = "2" });
            comParity.Add(new Customer() { Parity = "Mark", ParityValue = "3" });
            comParity.Add(new Customer() { Parity = "Space", ParityValue = "4" });
            parity.ItemsSource = comParity;
            parity.DisplayMemberPath = "Parity";
            parity.SelectedValuePath = "ParityValue";

            //↓↓↓↓↓↓↓↓↓数据位下拉控件↓↓↓↓↓↓↓↓↓  
            IList<Customer> dataBits = new List<Customer>();//数据位集合  
            dataBits.Add(new Customer() { Dbits = "8" });
            dataBits.Add(new Customer() { Dbits = "7" });
            dataBits.Add(new Customer() { Dbits = "6" });
            DataBits.ItemsSource = dataBits;
            DataBits.SelectedValuePath = "Dbits";
            DataBits.DisplayMemberPath = "Dbits";
            //↑↑↑↑↑↑↑↑↑数据位下拉控件↑↑↑↑↑↑↑↑↑  

            //↓↓↓↓↓↓↓↓↓停止位下拉控件↓↓↓↓↓↓↓↓↓  
            IList<Customer> stopBits = new List<Customer>();//停止位集合  
            stopBits.Add(new Customer() { Sbits = "1" });
            stopBits.Add(new Customer() { Sbits = "1.5" });
            stopBits.Add(new Customer() { Sbits = "2" });
            StopBits.ItemsSource = stopBits;
            StopBits.SelectedValuePath = "Sbits";
            StopBits.DisplayMemberPath = "Sbits";
            //↑↑↑↑↑↑↑↑↑停止位下拉控件↑↑↑↑↑↑↑↑↑  

            //↓↓↓↓↓↓↓↓↓默认设置↓↓↓↓↓↓↓↓↓  
            baudRate.SelectedValue = "9600";//波特率默认设置9600  
            parity.SelectedValue = "0";//校验位默认设置值为0，对应NONE  
            DataBits.SelectedValue = "8";//数据位默认设置8位  
            StopBits.SelectedValue = "1";//停止位默认设置1  
            ComPort.ReadTimeout = 8000;//串口读超时8秒  
            ComPort.WriteTimeout = 8000;//串口写超时8秒，在1ms自动发送数据时拔掉串口，写超时5秒后，会自动停止发送，如果无超时设定，这时程序假死  
            ComPort.ReadBufferSize = 1024;//数据读缓存  
            ComPort.WriteBufferSize = 1024;//数据写缓存  
        }

        private void SendVariableInit()
        {
            autoSendTimer.Tick += new EventHandler(AutoSend);
        }
        private void UIInit()
        {
            sendBtn.IsEnabled = false;
        }
        #endregion

        #region 窗体事件总汇
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PortOptionInit();       //COM口选项卡初始化
            AddPortEventHandler();  //添加串口事件
            SendVariableInit();     //发送数据有关初始化
            WaveVariableInit();     //示波器有关初始化
            WaveVariableInit();     //Camera有关变量初始化
            UIInit();               //UI初始化  
            NetInit();
        }
        /// <summary>
        /// 检测键盘按键，打开或者关闭串口设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
                
            switch (e.Key) {
                case Key.F1: {
                                SettingExpander.IsExpanded = !SettingExpander.IsExpanded;
                        } break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            ComPort.Close();
        }
        #endregion

        #region 串口总开关
        /// <summary>
        /// 串口关
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PortSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (PortSwitch.IsChecked == true)
                return;
       
            try
            {
                string str= ComPort.Close();
                ClosePortUISet();
                UpdateStatusBar(str);
            }
            catch 
            {
                UpdateStatusBar("UNABLE");
            }
        }
        /// <summary>
        /// 串口开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PortSwitch_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                PortOpenSet();
                string str = ComPort.Open();
                UpdateStatusBar(str);
                OpenPortUISet();
            }
            catch {
                UpdateStatusBar("UNABLE");
            }
        }
        /// <summary>
        /// 成功打开串口后UI设置，禁用或者启用当前Tab页上的控件
        /// </summary>
        private void OpenPortUISet()
        {
            TabItem tb = (TabItem)functionTab.SelectedItem;

            switch (tb.Name) {
                case "NormalTab": {
                        saveFileBtn.IsEnabled = false;
                        sendBtn.IsEnabled = true;
                    } break;
                case "WaveTab": {
                        WaveDataDeal.Start();
                        refreshCycleTbx.IsEnabled = false;
                    } break;
                case "CameraTab": {
                        //禁用或启用一些控件
                        CameraVariableSet();    //每次启动前刷新一次变量
                        BoolModeRbtn.IsEnabled = false;
                        GrayModeRbtn.IsEnabled = false;
                        ColorModeRbtn.IsEnabled = false;
                        dpiXSlider.IsEnabled = false;
                        dpiYSlider.IsEnabled = false;
                    } break;
            }

            SettingExpander.IsExpanded = false;
            SettingExpander.IsEnabled = false;
            isFixFunctionTab = false;
        }

        /// <summary>
        /// 成功关闭串口后UI设置，禁用或者启用当前Tab页上的控件
        /// </summary>
        private void ClosePortUISet()
        {
            TabItem tb = (TabItem)functionTab.SelectedItem;
            
            switch (tb.Name)
            {
                case "NormalTab": {
                        sendBtn.IsChecked = false;
                        sendBtn_Click(new object(),new RoutedEventArgs());

                        saveFileBtn.IsEnabled = true;
                        sendBtn.IsEnabled = false;      //当关闭串口时禁用发送按钮
                        
                        autoSendTimer.Stop();           //当关闭串口时停止定时器自动发送数据
                    } break;
                case "WaveTab": {
                        WaveDataDeal.Stop();
                        waveDatas.RemoveRange(0,waveDatas.Count);
                        refreshCycleTbx.IsEnabled = true;
                    } break;
                case "CameraTab": {
                        //禁用或启用一些控件
                        BoolModeRbtn.IsEnabled = true;
                        GrayModeRbtn.IsEnabled = true;
                        ColorModeRbtn.IsEnabled = true;
                        dpiXSlider.IsEnabled = true ;
                        dpiYSlider.IsEnabled = true ;
                    } break;
            }
            SettingExpander.IsEnabled = true;
        }
        /// <summary>
        /// 打开串口之前，对串口属性更新进行同步
        /// </summary>
        private void PortOpenSet()
        {
            ComPort.PortName = portName.SelectedValue.ToString(); 
            ComPort.BaudRate = Convert.ToInt32(baudRate.SelectedValue); 
            ComPort.Parity = (Parity)Convert.ToInt32(parity.SelectedValue); 
            ComPort.DataBits = Convert.ToInt32(DataBits.SelectedValue);
            ComPort.StopBits = (StopBits)Convert.ToDouble(StopBits.SelectedValue); 
        }
        #endregion

        #region Port_NormalReceived
        private void Port_NormalReceived(object sender, SerialDataReceivedEventArgs e){
            if (TABINDEX != 0)
                return;
            try
            {
                Thread.Sleep(10);
                string recData;
                byte[] recBuffer = null;
                if (ComPort.IsOpen)
                    recBuffer = new byte[ComPort.BytesToRead];
                else return;
                ComPort.Read(recBuffer, 0, recBuffer.Length);
                recData = System.Text.Encoding.Default.GetString(recBuffer);
                this.recTbx.Dispatcher.Invoke(
                    new Action(
                        delegate
                        {
                            recByte.Text = (Convert.ToInt32(recByte.Text) + recBuffer.Length).ToString();//接收数据字节数  
                            if (ASCII_recMode.IsChecked == true)//接收模式为ASCII文本模式  
                            {
                                recTbx.AppendText(recData);//加显到接收区  
                            }
                            else
                            {
                                StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存  
                                for (int i = 0; i < recBuffer.Length; i++)
                                {
                                    recBuffer16.AppendFormat("{0:X2}"+" ", recBuffer[i]);//X2表示十六进制格式（大写），域宽2位，不足的左边填0。  
                                }
                                recTbx.AppendText(recBuffer16.ToString());
                            }
                            recTbx.ScrollToEnd();//接收文本框滚动至底部  
                        }
                    )
                 );
            }
            finally
            {
                //Listening = false;//UI使用结束，用于关闭串口时判断，避免自动发送时拔掉串口，陷入死循环  
            }
        }
        private void recClear_btn_Click(object sender, RoutedEventArgs e)
        {
            recTbx.Text = "";
            if(recCountClear_cbx.IsChecked==true)
                recByte.Text = "0";
        }
        private void saveFileBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog Save_fd = new SaveFileDialog();//调用系统保存文件窗口
            Save_fd.Filter = "TXT文本|*.txt";//文件过滤器
            if (Save_fd.ShowDialog() == true)//选择了新文件
            {
                System.IO.File.WriteAllText(Save_fd.FileName, recTbx.Text);//写入新的数据
                System.IO.File.AppendAllText(Save_fd.FileName, "\r\n-------" + DateTime.Now.ToString() + "\r\n"); //数据后面写入时间戳
                MessageBox.Show("保存成功！");
            }
        }
        /// <summary>
        /// 刷新串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void portName_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            comList.Clear();//情况控件链接资源  
            portName.DisplayMemberPath = "com1";
            portName.SelectedValuePath = null;//路径都指为空，清空下拉控件显示，下面重新添加  

            ports = new string[SerialPort.GetPortNames().Length];//重新定义可用串口数组长度  
            ports = SerialPort.GetPortNames();//获取可用串口  
            if (ports.Length > 0)//有可用串口  
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    comList.Add(new Customer() { com = ports[i] });//下拉控件里添加可用串口  
                }
                portName.ItemsSource = comList;//可用串口下拉控件资源路径  
                portName.DisplayMemberPath = "com";//可用串口下拉控件显示路径  
                portName.SelectedValuePath = "com";//可用串口下拉控件值路径  
            }
        }
        #endregion

        #region 发送数据
        private void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sendBtn.IsChecked == true)
                return;
            hexSendMode.IsEnabled = true;
            autoSendMode.IsEnabled = true;
            openFile_btn.IsEnabled = true;
            clearSendArea.IsEnabled = true;
            sendIntervalTbx.IsEnabled = true;
            SendTbx.IsEnabled = true;

            autoSendTimer.Stop();
        }
        private void sendBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (SendTbx.Text == "") return;
            ComSend();
            ToggleButton tbtn = (ToggleButton)sender;
            if (autoSendMode.IsChecked == false)
                return;

            autoSendTimer.Interval = TimeSpan.FromMilliseconds(Convert.ToInt32(sendIntervalTbx.Text));
            autoSendTimer.Start();

            hexSendMode.IsEnabled = false;
            autoSendMode.IsEnabled = false;
            openFile_btn.IsEnabled = false;
            clearSendArea.IsEnabled = false;
            sendIntervalTbx.IsEnabled = false;
            SendTbx.IsEnabled = false;
        }
        private void clearSendArea_Click(object sender, RoutedEventArgs e)
        {
            SendTbx.Text = "";
        }

        private void sendCountClearCbx_Click(object sender, RoutedEventArgs e)
        {
            sendByte.Text = "0";
        }
        private void AutoSend(object sender, EventArgs e)
        {
            ComSend();
        }
        private void ComSend()
        {
            string sendData = SendTbx.Text;
            int sendCount = 0;
            if (hexSendMode.IsChecked == true)
            {
                try
                {
                    sendCount=ComPort.SendHex(sendData);
                }
                catch {
                    MessageBox.Show("请输入正确的十六进制数!");
                }  
            }
            else
            {
                sendCount=ComPort.SendASCII(sendData);
            }
            int temp = Convert.ToInt32(sendByte.Text);
            sendCount += temp;
            sendByte.Text = sendCount.ToString();
        }

        private void sendInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sendIntervalTbx.Text.Length == 0 || Convert.ToInt32(sendIntervalTbx.Text) == 0)//时间为空或时间等于0，设置为1000  
            {
                sendIntervalTbx.Text = "1000";
            }
            autoSendTimer.Interval = TimeSpan.FromMilliseconds(Convert.ToInt32(sendIntervalTbx.Text));//设置自动发送周期  
        }

        /// <summary>
        /// 检测发送间隔设置文本框的用户输入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sendInterval_TextChanged(object sender, TextChangedEventArgs e)
        {

            TextBox textBox = sender as TextBox;
            string str = textBox.Text;
            string regular = "^[0-9]*$";
            
              
            TextChange[] change = new TextChange[e.Changes.Count];
            byte[] checkText = new byte[textBox.Text.Length];
            bool result = true;
            e.Changes.CopyTo(change, 0);
            int offset = change[0].Offset;
            checkText = System.Text.Encoding.Default.GetBytes(textBox.Text);

            if (!Regex.IsMatch(str, regular))
            {
                textBox.Text = textBox.Text.Remove(offset, change[0].AddedLength);
                textBox.Select(offset, 0);
                return;
            }
            for (int i = 0; i < textBox.Text.Length; i++)
            {
                result &= 0x2F < (Convert.ToInt32(checkText[i])) & (Convert.ToInt32(checkText[i])) < 0x3A;//0x2f-0x3a之间是数字0-10的ASCII码
            }
            if (change[0].AddedLength > 0)
            {
                if (!result || Convert.ToInt32(textBox.Text) > 100000)
                {
                    textBox.Text = textBox.Text.Remove(offset, change[0].AddedLength);
                    textBox.Select(offset, 0);
                }
            }
        }
        private void openFile_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog Open_fd = new OpenFileDialog();//调用系统保存文件窗口
            Open_fd.Filter = "TXT文本|*.txt";//文件过滤器
            if (Open_fd.ShowDialog() == true)//选择了新文件
            {
                string fileData = Open_fd.ToString();
                Stream []files = Open_fd.OpenFiles();
                foreach (Stream temp in files)
                {
                    StreamReader sr = new StreamReader(temp);
                    fileData = sr.ReadToEnd();
                    SendTbx.Text += fileData;
                }
            }
        }
        #endregion

        #region Port_WaveShow
        /// <summary>
        /// 以虚拟示波器的方式接受串口数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Port_WaveShow(object sender, SerialDataReceivedEventArgs e)
        {
            if (TABINDEX != 1)
                return;
            Thread.Sleep(10);
             byte[] buffer=null;
            if (ComPort.IsOpen)           
                buffer = new byte[ComPort.BytesToRead];//接收数据缓存 
            else return;

            int count=ComPort.Read(buffer, 0, buffer.Length);//读取数据 
            foreach(byte i in buffer)
                SB.Add(i);  
        }
        /// <summary>
        /// 定时器事件，定时处理缓存的串口数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BufferDataDeal(object sender, EventArgs e)
        {
            #region 废弃代码 
            /*
            if (recBuffers.Count <= 0)
                return;
            byte[] buffer = recBuffers[0];
            List<byte> listbuff = buffer.ToList();
            byte[] subBuf = null;

            while (listbuff.Count >= 10)
            {
                subBuf = new byte[10];
                for (int temp = 0; temp < 10; temp++)
                {
                    subBuf[temp] = listbuff[temp];
                }
                if (TransformFactory.CRC16Check(subBuf) == true)   //检验接收到的一组数据是否有效
                {
                    waveDatas.Add(subBuf);
                    listbuff.RemoveRange(0, 10);
                    continue;
                }
                listbuff.RemoveAt(0);
            }
             */
            #endregion
            while (SB.Count >= 20)
            {
                byte[] subBuf = new byte[10];
                SB.CopyTo(0, subBuf, 0, 10);
                if (TransformFactory.CRC16Check(subBuf) == true)   //检验接收到的一组数据是否有效
                {
                    AnimatedPlot(subBuf);
                    SB.RemoveRange(0, 10);
                }
                else SB.RemoveAt(0);
            }
        }
        /// <summary>
        /// 定时器事件，定时刷新Plotter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AnimatedPlot(byte[]buffer)
        {
            this.Plotter.Dispatcher.Invoke(
                new Action(
                        delegate {
                            Point[] CH = new Point[4];
                            for (int k = 0; k < 4; k++)
                            {
                                Int16 temp = (Int16)buffer[2 * k + 1];
                                temp <<= 8;
                                temp |= buffer[2 * k];
                                CH[k] = new Point(XAXIS, temp);
                            }
                            for(int i = 0; i < 4; i++)
                                DataSources[i].AppendAsync(Plotter.Dispatcher, CH[i]);
                            XAXIS++;
                        }
                    )
                );
        }
        /// <summary>
        /// 修改示波器刷新周期的文本框输入验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void refreshCycleTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            string str = refreshCycleTbx.Text.Replace(" ", "");//去掉空格   
            if (str.Length == 0)
            {
                setWaveTimer(1);
                return;
            }

            Regex regex = new Regex("^(-?\\d+)(\\.\\d+)?$");
            if (!regex.IsMatch(str))
            {
                setWaveTimer(1);
                return;
            }

            double interval = Convert.ToDouble(str);
            if (interval < 0.1 || interval > 1000)
                setWaveTimer(1);
            else setWaveTimer(interval);
        }
        /// <summary>
        /// 设置定时器间隔，以及相应的文本框显示
        /// </summary>
        /// <param name="interval"></param>
        private void setWaveTimer(double interval)
        {
            refreshCycleTbx.Text = interval.ToString();
            WAVESHOWTimeSpan = interval;
        }
        /// <summary>
        /// 重置Plotter
        /// </summary>
        private void InitWaveData()
        {
            for (int i = 0; i < 4; i++)
            {
                DataSources[i] = new ObservableDataSource<Point>();
                DataSources[i].AppendAsync(Plotter.Dispatcher, new Point(-100, 0));
                DataSources[i].AppendAsync(Plotter.Dispatcher, new Point(0, 0));
                LineGraphs[i].DataSource = DataSources[i];
            }
        }
        private void setLineGraph(LineGraph line,Color color,string description)
        {
            line.Stroke = new SolidColorBrush(color);
            line.Description = new PenDescription(description);
        }
        private void setLineGraph(LineGraph line, Brush brush, string description)
        {
            line.Stroke = brush;
            line.Description = new PenDescription(description);
        }

        private void WaveChannelChecked(object sender, RoutedEventArgs e)
        {
            CheckBox cbx = sender as CheckBox;
            int index = WaveCheckBoxs.IndexOf(cbx);
            setLineGraph(LineGraphs[index], cbx.Foreground, "CH" + (index + 1).ToString());
        }

        private void WaveChannelClick(object sender, RoutedEventArgs e)
        {
            CheckBox cbx = sender as CheckBox;
            int index = WaveCheckBoxs.IndexOf(cbx);
            if (cbx.IsChecked == true) return;
            setLineGraph(LineGraphs[index], new SolidColorBrush(), "CH" + (index + 1).ToString());
        }
        /// <summary>
        /// Plotter 按下F5触发刷新事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Plotter_KeyDown(object sender, KeyEventArgs e)
        {
            if (ComPort.IsOpen) return;
            if (e.Key == Key.F3) InitWaveData();
            XAXIS = 0;
            Plotter.FitToView();
        }
        #endregion

        #region Port_CameraDebug
        private void Port_CameraDebug(object sender, SerialDataReceivedEventArgs e)
        {
            if (TABINDEX != 2)
                return;
            Thread.Sleep(10);
            byte[] buffer = null;
            if (ComPort.IsOpen == false) return;
            if (ComPort.BytesToRead < DpiX*DpiY*2)    return;

            buffer = new byte[ComPort.BytesToRead];     //接收串口数据缓存 
            int count = ComPort.Read(buffer, 0, buffer.Length);//读取数据 
            if (count < DpiX*DpiY*2) return;

            ImageBuffData.Add(buffer);              //把图像信息存储到缓冲队列中
            this.canvas1.Dispatcher.Invoke(new Action(ImageDataBufferDeal));    //唤醒图像数据处理事件委托
        }
        
        
        private void ImageDataBufferDeal()
        {
            if (ImageBuffData.Count <= 0) return;
            byte[] buff = ImageBuffData[0];
            byte[] oneImage = new byte[DpiX*DpiY];
            int i;
            for (i = 0; i < buff.Length; i++)
                if (buff[i] == 0xff) break;
            if (i >= buff.Length) return;
            if (buff.Length - i < oneImage.Length) return;
            for (int k = 0; k < oneImage.Length;)
                oneImage[k++] = buff[++i];

            ImageData.Add(oneImage);
            this.cameraImage1.Dispatcher.Invoke(new Action(ImageShow)); //唤醒图像展示事件委托
            ImageBuffData.Remove(buff);
        }

        private void ImageShow()
        {
            WB1 = new WriteableBitmap (DpiX,DpiY,DpiY* (1 / ZoomSlider.Value), DpiX*(1/ZoomSlider.Value),getPixelFormat(imageMode),null);

            byte[] oneImage = ImageData.Last();
            Int32Rect rect = new Int32Rect(0, 0, 1, 1);
            int stride = WB1.PixelWidth * WB1.Format.BitsPerPixel / 8;
            for (int i = 0; i < DpiY; i++)//WB1.PixelHeight
            {
                for (int j = 0; j < DpiX; j++)//WB1.PixelWidth
                {
                    rect.X = j;
                    rect.Y = i;
                    byte[] colorData={ oneImage[i * WB1.PixelWidth + j] };
                    WB1.WritePixels(rect, colorData, stride, 0);
                }
            }
            cameraImage1.Source = WB1;
        }
        
        private PixelFormat getPixelFormat(int mode)
        {
            switch (mode)
            {
                case 0: {
                        return PixelFormats.Gray2;
                    } ;
                case 1: {
                        return PixelFormats.Gray8;
                    } ;
                case 2: {
                        return PixelFormats.Indexed8;
                    };
                default:return PixelFormats.Gray2; ;
            }
        }
        /// <summary>
        /// 程序初始化，或打开串口前对CameraTest的一些有关变量进行初始化
        /// </summary>
        private void CameraVariableSet()
        {
            DpiX = (int)dpiXSlider.Value;
            DpiY = (int)dpiYSlider.Value;
            ImageBuffData.RemoveRange(0, ImageBuffData.Count);
        }
        private void ImageModeSelect(object sender, RoutedEventArgs e)
        {
            RadioButton rbtn = sender as RadioButton;
            switch (rbtn.Name)
            {
                case "BoolModeRbtn": {
                        imageMode = 0;
                    } break;
                case "GrayModeRbtn": {
                        imageMode = 1;
                    } break;
                case "ColorModeRbtn": {
                        imageMode = 2;
                    } break;
            }
        }
        #endregion

        #region 网络调试

        #region Common
        /// <summary>
        /// 获取本机的IP地址
        /// </summary>
        /// <returns>返回本机的IP地址</returns> 
        private IPAddress GetLocalAddressIP()
        {
            ///获取本地的IP地址

            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    return _IPAddress;
                }
            }
            return null;
        }
        /// <summary>
        /// 启动TextBox UI写线程
        /// </summary>
        /// <param name="tbx">文本框名</param>
        /// <param name="str">要写入的文本</param>
        private void TextBoxWrite(TextBox tbx, string str)
        {
            SeverRecTbx.Dispatcher.Invoke(new Action(delegate
            {
                tbx.Text += str;
            }));
        }
        private void TextBoxWrite(RichTextBox rcbx, Color color, string text)
        {
            rcbx.Dispatcher.Invoke(new Action(delegate {
                var r = new Run(text);
                var p = new Paragraph();
                p.Inlines.Add(r);
                p.Foreground = new SolidColorBrush(color);
                rcbx.Document.Blocks.Add(p);
            }));
        }

        private void WriteTextBoxHexMode(RichTextBox tbx, byte[] buffer, int count)
        {
            StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存  
            for (int i = 0; i < count; i++)
            {
                recBuffer16.AppendFormat("{0:X2}" + " ", buffer[i]);//X2表示十六进制格式（大写），域宽2位，不足的左边填0。  
            }
            TextBoxWrite(tbx, normalTextColor, recBuffer16.ToString());
        }

        /// <summary>
        /// 滚动文本框至末尾
        /// </summary>
        /// <param name="tbx">需要滚动的文本框</param>
        private void TextBoxScrollToEnd(TextBox tbx)
        {
            tbx.Dispatcher.Invoke(new Action(delegate { tbx.ScrollToEnd(); }));
        }
        private void TextBoxScrollToEnd(RichTextBox rcbx)
        {
            rcbx.Dispatcher.Invoke(new Action(delegate {
                rcbx.ScrollToEnd();
            }));
        }

        private void TextBoxClear(RichTextBox tbx) {
            SeverRecTbx.Document.Blocks.Clear();
        }

        /// <summary>
        /// 启动UI线程判断CheckBox是否选中
        /// </summary>
        /// <param name="cbx"></param>
        /// <returns></returns>
        private bool isCheckBtnChecked(CheckBox cbx)
        {
            bool isChecked = false;
            cbx.Dispatcher.Invoke(new Action(delegate
            {
                if (cbx.IsChecked == true) isChecked = true;

            }));
            return isChecked;
        }

        /// <summary>
        /// 检验文本框IP地址输入有效性
        /// </summary>
        /// <param name="tbx"></param>
        private void IPTextBoxCheck(TextBox tbx) {
            bool flag = Regex.IsMatch(tbx.Text, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
            if (!flag)
                tbx.Text = LocalIP.ToString();
        }
        /// <summary>
        /// 检验文本框IP端口号输入的有效性
        /// </summary>
        /// <param name="tbx">需要检验的文本框</param>
        /// <returns>IP端口号是否合法</returns>
        private bool IPPortTextBoxCheck(TextBox tbx) {
            Regex reg = new Regex("\\d{4,5}");
            string str = tbx.Text;
            if (!reg.IsMatch(str)){
                tbx.Text = "8500";
                return false;
            }
            else{
                int temp = Convert.ToInt32(tbx.Text);
                if (temp > 65535 || temp < 1024) {
                    tbx.Text = "8500";
                    return false;
                }
                return true; 
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NetTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NETTabIndex = NetTabControl.SelectedIndex;
        }
        #endregion

        #region TCPSever
        /// <summary>
        /// 服务器开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SeverNetSwitch_Checked(object sender, RoutedEventArgs e)
        {
            //if (isTcpListening) return;
            TcpSever.Start();
            SeverStartSetting();
            try
            {
                for(int i = 0; i < remoteClients.Length; i++)
                {
                    ParameterizedThreadStart sever = new ParameterizedThreadStart(SeverReadThread);
                    Workers[i] = new Thread(sever);
                    Workers[i].Start(i);
                }
            }
            catch
            {
                string msg = "没有足够的可用内存来开启侦听线程 ...";
                
                TextBoxWrite(SeverRecTbx, systemColor, msg);
            }

        }
        
        
        /// <summary>
        /// 服务器关
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SeverSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (SeverSwitch.IsChecked == true) return;
            SeverStopSetting();
            TcpSever.Stop();
        }
        /// <summary>
        /// 成功启动TCP服务器后的设置
        /// </summary>
        private void SeverStartSetting()
        {
            isTcpListening = true;
            string msg = "启动监听...";
            TextBoxWrite(SeverRecTbx, systemColor, msg);
            TextBoxScrollToEnd(SeverRecTbx);
            SeverSwitch.Content = "停止";
            SeverPortNumTbx.IsEnabled = false;
            MaxClientrCountTbx.IsEnabled = false;
            TCPSeverSendBtn.IsEnabled = true;
        }

        /// <summary>
        /// 成功关闭TCP服务器后的设置
        /// </summary>
        private void SeverStopSetting()
        {
            isTcpListening = false;  //服务器监听状态字设为false
            SeverPortNumTbx.IsEnabled = true;
            MaxClientrCountTbx.IsEnabled = true;
            TCPSeverSendBtn.IsEnabled = false;
            for (int i = 0; i < connectedRemoteClient.Count; i++) {
                if (clientNetworkStream[i] != null)
                    clientNetworkStream[i].Dispose();
                connectedRemoteClient[i].Close();
            }
            clientNetworkStream.Clear();
            connectedRemoteClient.Clear();
            for (int i = 0; i < SeverConnectWay.Items.Count; i++) {
                if (i > 0) SeverConnectWay.Items.RemoveAt(i);
            }
            string msg = "停止监听成功";
            TextBoxWrite(SeverRecTbx, systemColor, msg);
            TextBoxScrollToEnd(SeverRecTbx);
            SeverSwitch.Content = "监 听";
        }
        /// <summary>
        /// 服务器读线程，用于侦听远程客户端发送的消息
        /// </summary>
        /// <param name="clientIndex">远程客户端编号</param>
        private void SeverReadThread(object clientIndex)
        {
            
            if (!isTcpListening) return ;
            int index = (int)clientIndex;
            try{
                remoteClients[index] = TcpSever.AcceptTcpClient();
            }
            catch {
                return;
            }
            ConnectSuccess(index);
            NetworkStream streamToClient = remoteClients[index].GetStream();
            clientNetworkStream.Add(streamToClient);
            connectedRemoteClient.Add(remoteClients[index]);
            //向广播方式ComboBox中添加Item的UI线程
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal,new Action(delegate {
                ComboBoxItem newItem = new ComboBoxItem();
                newItem.Content = remoteClients[index].Client.RemoteEndPoint;
                SeverConnectWay.Items.Add(newItem);
            }));
            do{
                try{
                    byte[] buffer = new byte[TcpBufferSize];
                    int bytesRead = streamToClient.Read(buffer, 0, TcpBufferSize);
                    if (bytesRead == 0){
                        string msg = "下线通知：[" + remoteClients[index].Client.RemoteEndPoint + "] " + getNowTime();
                        TextBoxWrite(SeverRecTbx, systemColor, msg);
                        
                        remoteClients[index] = null;    //远程客户端设空
                        break;
                    }
                    this.SeverHexNETRec.Dispatcher.Invoke(new Action(delegate {
                        string msg = "[" + remoteClients[index].Client.RemoteEndPoint + "] " + getNowTime();
                        
                        TextBoxWrite(SeverRecTbx, recNotifyColor, msg);
                        if (SeverHexNETRec.IsChecked == true) {
                            WriteTextBoxHexMode(SeverRecTbx, buffer, bytesRead);
                        }else{
                            msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            TextBoxWrite(SeverRecTbx, normalTextColor, msg);
                        }
                    }));
                    TextBoxScrollToEnd(SeverRecTbx);
                }
                catch
                {
                    //WriteTextUIAction(SeverRecTbx, ex.Message + "\n");
                    //WriteTextUIAction(SeverRecTbx, "停止监听成功\n");
                    TextBoxScrollToEnd(SeverRecTbx);
                }
            } while (isTcpListening);
            //WriteTextUIAction(SeverRecTbx, "停止监听成功\n");
            TextBoxScrollToEnd(SeverRecTbx);
        }
        /// <summary>
        /// 服务器写线程，向一个客户端发送信息
        /// </summary>
        private void SeverWrite(object sender, RoutedEventArgs e)
        { 
            if (!isTcpListening) return;
            string text = SeverSendTbx.Text;
            if (string.IsNullOrWhiteSpace(text)) {
                MessageBox.Show("请输入发送内容！");
                return;
            }
            byte[] buffer = new byte[TcpBufferSize];
            if (SeverHexNETSend.IsChecked == true)
                try {
                    buffer = TransformFactory.StrToHexArray(text);
                } catch {
                    MessageBox.Show("请输入正确的十六进制数");
                    return;
                }
            else
                buffer = Encoding.ASCII.GetBytes(text);
            int comboIndex = SeverConnectWay.SelectedIndex;
            if (comboIndex == 0)
                SeverBroadcast(buffer);
            else
                SeverSendToClient(comboIndex,buffer);
            TextBoxScrollToEnd(SeverRecTbx);
        }
        /// <summary>
        /// 以广播的方式向每一个客户端发送消息
        /// </summary>
        /// <param name="buffer"></param>
        private void SeverBroadcast(byte[] buffer) {
            
            foreach (NetworkStream streamToClient in clientNetworkStream)
                streamToClient.Write(buffer, 0, buffer.Length);
            string msg = "[" + LocalIP.ToString() + "]:" + SeverPortNumTbx.Text + "  Broadcast " + getNowTime();
            TextBoxWrite(SeverRecTbx, sendNotifyColor, msg);
            writeToTbx(buffer);
        }
        /// <summary>
        /// 向某个具体的客户端发送消息
        /// </summary>
        /// <param name="index"></param>
        /// <param name="buffer"></param>
        private void SeverSendToClient(int index,byte[] buffer) {
            TcpClient client = connectedRemoteClient[index - 1];
            NetworkStream streamToClient = clientNetworkStream[index - 1];
            streamToClient.Write(buffer, 0, buffer.Length);
            string msg = "[" + LocalIP.ToString() + "]:" + SeverPortNumTbx.Text + "  To  [" + client.Client.RemoteEndPoint + "] " + getNowTime();
            TextBoxWrite(SeverRecTbx,sendNotifyColor,msg);
            writeToTbx(buffer);
        }
        private void writeToTbx(byte[] buffer) {
            if (SeverHexNETSend.IsChecked == false)
                TextBoxWrite(SeverRecTbx,normalTextColor, SeverSendTbx.Text);
            else {
                TextBoxWrite(SeverRecTbx, normalTextColor, SeverSendTbx.Text);
            }
            if (SeverAutoClear.IsChecked == true) SeverSendTbx.Text = "";
        }
        private string getHexStr(byte[] buffer) {
            StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存  
            for (int i = 0; i < buffer.Length; i++)
            {
                recBuffer16.AppendFormat("{0:X2}" + " ", buffer[i]);//X2表示十六进制格式（大写），域宽2位，不足的左边填0。  
            }
            return recBuffer16.ToString();
        }
        /// <summary>
        /// 成功连接远程客户端后的设置
        /// </summary>
        /// <param name="index"></param>
        private void ConnectSuccess(int index)
        {
            string connectMessage = "上线通知："+"["+ remoteClients[index].Client.RemoteEndPoint + "] " +getNowTime();
            TextBoxWrite(SeverRecTbx,systemColor, connectMessage);
        }
        
        /// <summary>
        /// 获取当前时间
        /// </summary>
        /// <returns>当前时间，格式:hh-mm-ss</returns>
        private string getNowTime() {
            DateTime now = DateTime.Now;
            string hour = timeTransform(now.Hour.ToString());
            string minute = timeTransform(now.Minute.ToString());
            string second = timeTransform(now.Second.ToString());
            return hour + ":"+minute+":"+second;
        }

        private string timeTransform(string str) {
            if (str.Length == 1) return 0.ToString() + str;
            else return str;
        }
        /// <summary>
        /// 客户端最大连接数正则表达式验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MaxConnectTbxInputCheck(object sender, RoutedEventArgs e) {
            Regex reg = new Regex("\\d{1,2}");
            string str = MaxClientrCountTbx.Text;
            if (!reg.IsMatch(str))
            {
                MaxClientrCountTbx.Text = "10";
            }
            else {
                int temp = Convert.ToInt32(MaxClientrCountTbx.Text);
                if (temp>30) MaxClientrCountTbx.Text = "30";
                else if(temp<1) MaxClientrCountTbx.Text = "1";
            }
            remoteClients = new TcpClient[Convert.ToInt32(MaxClientrCountTbx.Text)];
            Workers = new Thread[Convert.ToInt32(MaxClientrCountTbx.Text)];
        }
        
        /// <summary>
        /// TcpSever端口号正则表达式输入验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SeverPortumTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbx = sender as TextBox;
            IPPortTextBoxCheck(tbx);
            TcpSever = new TcpListener(LocalIP, Convert.ToInt32(tbx.Text));
        }
        private void SeverClearBtn_Click(object sender, RoutedEventArgs e)
        {                     
            TextBoxClear(TCPClientRecTbx);
        }
        #endregion

        #region TCPClient
        /// <summary>
        /// 客户端连接服务器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientConnectBtn_Checked(object sender, RoutedEventArgs e){
            if (isClientRun) return;
            ParameterizedThreadStart client = new ParameterizedThreadStart(ClientReadThread);
            clientThread = new Thread(client);
            clientThread.Start();
            TCPClientPortNumTbx.IsEnabled = false;
            TCPClientIPTbx.IsEnabled = false;
            TCPClientSendBtn.IsEnabled = true;
            ClientConnectTgbtn.Content = "断开连接";
        }
        /// <summary>
        /// 客户端断开连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientConnectTgbtn_Click(object sender, RoutedEventArgs e){
            if (ClientConnectTgbtn.IsChecked == true) return;
            if (clientStreamToSever != null)
                clientStreamToSever.Dispose();
            LocalClient.Close();
            clientThread.Abort();
            isClientRun = false;
            
            TCPClientPortNumTbx.IsEnabled = true;
            TCPClientIPTbx.IsEnabled = true;
            TCPClientSendBtn.IsEnabled = false;
            ClientConnectTgbtn.Content = "连接";
        }
        /// <summary>
        /// TCP 客户端读线程
        /// </summary>
        /// <param name="clientIndex"></param>
        private void ClientReadThread(object clientIndex) {
            bool flag=false;  //用于检验是否正常断开
            try
            {
                LocalClient = new TcpClient();
                LocalClient.Connect(ConnectSeverIP, ClientConnectPortNum);
                isClientRun = true;
                string msg = "服务器连接成功：local:";
                msg += "[" + LocalClient.Client.LocalEndPoint + "]";
                msg += " --> [" + LocalClient.Client.RemoteEndPoint + "] ";
                msg += getNowTime();
                TextBoxWrite(TCPClientRecTbx,systemColor, msg);
                clientStreamToSever = LocalClient.GetStream();
                do
                {
                    byte[] buffer = new byte[TcpBufferSize];
                    int bytesToRead=0;
                    try {
                        bytesToRead = clientStreamToSever.Read(buffer, 0, TcpBufferSize);
                    } catch {
                        msg = "断开连接成功";
                        TextBoxWrite(TCPClientRecTbx, systemColor, msg);
                        flag = true;
                        break;
                    }
                    
                    if (bytesToRead == 0)
                    {
                        msg = "[" + LocalClient.Client.RemoteEndPoint + "] ";
                        msg = "Sever：" + msg + " Offline!";
                        TextBoxWrite(TCPClientRecTbx, systemColor, msg);
                        break;
                    }
                    this.TCPClientRecTbx.Dispatcher.Invoke(new Action(delegate{
                        msg = "sever:[" + LocalClient.Client.RemoteEndPoint + "] " + getNowTime();
                        TextBoxWrite(TCPClientRecTbx, recNotifyColor, msg);
                        string receive = Encoding.ASCII.GetString(buffer, 0, bytesToRead);
                        if (ClientHexRecRbtn.IsChecked == false)
                            TextBoxWrite(TCPClientRecTbx, normalTextColor, receive);
                        else
                            WriteTextBoxHexMode(TCPClientRecTbx, buffer, bytesToRead);
                    }));
                    TextBoxScrollToEnd(TCPClientRecTbx);
                } while (isClientRun);
            }
            catch
            {
                if (!flag) {
                    string msg = "服务器连接失败 " + getNowTime();
                    TextBoxWrite(TCPClientRecTbx, systemColor, msg);
                    this.TCPClientSendBtn.Dispatcher.Invoke(new Action(delegate{
                        TCPClientSendBtn.IsEnabled = false;
                    }));
                }
            }
            isClientRun = false;
            if(clientStreamToSever!=null)
                clientStreamToSever.Dispose();
            LocalClient.Close();
            TextBoxScrollToEnd(TCPClientRecTbx);
        }
        /// <summary>
        /// 客户端发送
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TCPClientSendBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isClientRun) return;
            string sendText = ClientSendTbx.Text;
            if (string.IsNullOrWhiteSpace(sendText))
            {
                MessageBox.Show("请输入发送内容！");
                return;
            }
            ParameterizedThreadStart tcpClientSend = new ParameterizedThreadStart(TCPClientSendThread);
            Thread thrSend = new Thread(TCPClientSendThread);
            thrSend.Start(ClientSendTbx.Text);
        }
        private void TCPClientSendThread(object send) {
            string sendText = send as string;
            byte[] buffer = null;
            if (isCheckBtnChecked(ClientHexModeRbtn) == false)
                buffer = Encoding.ASCII.GetBytes(sendText);
            else try
                {
                    buffer = TransformFactory.StrToHexArray(sendText);
                }
                catch
                {
                    MessageBox.Show("请输入正确的十六进制数！");
                }
            string msg = "local:[" + LocalClient.Client.LocalEndPoint + "] " + getNowTime();
            TextBoxWrite(TCPClientRecTbx, sendNotifyColor, msg);
            try
            {
                clientStreamToSever.Write(buffer, 0, buffer.Length);
                if (isCheckBtnChecked(ClientHexModeRbtn) == true)
                    WriteTextBoxHexMode(TCPClientRecTbx, buffer, sendText.Length);
                else
                {
                    msg = sendText;
                    TextBoxWrite(TCPClientRecTbx, normalTextColor, msg);
                }
                if (isCheckBtnChecked(ClientAutoClearTbx) == true) ClientSendTbx.Clear();
            }
            catch
            {
                TextBoxWrite(TCPClientRecTbx, systemColor, "发送失败");
            }
            TextBoxScrollToEnd(TCPClientRecTbx);
        } 
        private void TCPClientClearBtn_Click(object sender, RoutedEventArgs e)
        {
            TextBoxClear(sender as RichTextBox);
        }
        /// <summary>
        /// IP地址输入验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TCPClientIPTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbx = sender as TextBox;
            IPTextBoxCheck(tbx);
            ConnectSeverIP = tbx.Text;
        }
        /// <summary>
        /// 客户端端口号正则表达式验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TCPClientPortNumTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbx = sender as TextBox;
            IPPortTextBoxCheck(tbx);
            ClientConnectPortNum = Convert.ToInt32(tbx.Text);
        }
        #endregion

        #region UDP
        /// <summary>
        /// 关闭UDP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UDPConnectTbtn_Click(object sender, RoutedEventArgs e)
        {
            if (UDPConnectTbtn.IsChecked == true) return;
            TextBoxWrite(UDPRichTbx, systemColor, "关闭端口成功");
            TextBoxScrollToEnd(UDPRichTbx);
            UDPLocalIPTbx.IsEnabled = true;
            UDPLocalPort.IsEnabled = true;
            UDPSendBtn.IsEnabled = false;
            UDPConnectTbtn.Content = "断开连接";
        }
        /// <summary>
        /// 打开UDP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UDPConnectTbtn_Checked(object sender, RoutedEventArgs e)
        {
            UDPScend.Close();
            int portNum = Convert.ToInt32(UDPLocalPort.Text);
            IPEndPoint localIP = new IPEndPoint(LocalIP, 8000);
            UDPScend = new UdpClient(localIP);
            //UDPReceive = new UdpClient(localIP);
            StartListeningThread();
            UDPSendBtn.IsEnabled = true;
            UDPLocalIPTbx.IsEnabled = false;
            UDPLocalPort.IsEnabled = false;
            TextBoxWrite(UDPRichTbx, systemColor, "打开端口成功");
            TextBoxScrollToEnd(UDPRichTbx);
            UDPConnectTbtn.Content = "连接";
        }
        /// <summary>
        /// 开启侦听线程
        /// </summary>
        private void StartListeningThread()
        {
            if (isUDPListening) return;
            else
                isUDPListening = true;
            ParameterizedThreadStart udpRec = new ParameterizedThreadStart(UDPReceiveThread);
            Thread thrSend = new Thread(udpRec);
            thrSend.Start(1);
        }
        private void UDPSendBtn_Click(object sender, RoutedEventArgs e){
            int portNum = Convert.ToInt32(UDPTargetIPPortTbx.Text);
            bool flag = false;
            foreach (IPEndPoint it in UDPRemoteIpeps) {
                if (it.Port == portNum) {
                    flag = true;
                    break;
                }
            }
            if (!flag) {
                IPEndPoint remoteIP = new IPEndPoint(LocalIP, portNum);
                UDPRemoteIpeps.Add(remoteIP);
                ComboBoxItem item = new ComboBoxItem();
                item.Content = remoteIP.ToString();
                REmoteIpepsCbx.Items.Add(item);
            }
            int k = REmoteIpepsCbx.SelectedIndex;
            if(k==-1) REmoteIpepsCbx.SelectedIndex=0;
            string sendText = UDPSendTbx.Text;
            if (string.IsNullOrWhiteSpace(sendText)) {
                MessageBox.Show("请输入发送内容","提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            //开启发送线程
            ParameterizedThreadStart udpSend = new ParameterizedThreadStart(UDPSendThread);
            Thread thrSend = new Thread(udpSend);
            string[] msg=new string[3];
            msg[0] = UDPSendTbx.Text;
            msg[1]= REmoteIpepsCbx.SelectedIndex.ToString();
            if (UDPHexSendCbtn.IsChecked == true)
                msg[2] = "true";
            else
                msg[2] = "false";
            thrSend.Start(msg);
            if (ClientAutoClearTbx.IsChecked == true) ClientSendTbx.Clear();

            TextBoxScrollToEnd(UDPRichTbx);
        }
        /// <summary>
        /// UDP发送线程
        /// </summary>
        /// <param name="send"></param>
        private void UDPSendThread(object send){
            string[] message = send as string[];
            string msg = message[0];
            int index = Convert.ToInt32(message[1]);
            byte[] sendbytes = Encoding.Unicode.GetBytes(msg);
            if (message[2] == "true")
                sendbytes = TransformFactory.StrToHexArray(msg);
            
            IPEndPoint remoteIP = UDPRemoteIpeps[index];
            UDPScend.Send(sendbytes, sendbytes.Length, remoteIP);
            //UDPScend.Close();
            string text = remoteIP.ToString()+" "+getNowTime();
            TextBoxWrite(UDPRichTbx, sendNotifyColor,text);

            if (message[2] == "true")
                WriteTextBoxHexMode(UDPRichTbx, sendbytes, sendbytes.Length);
            else
                TextBoxWrite(UDPRichTbx, normalTextColor,msg);
            TextBoxScrollToEnd(UDPRichTbx);
        }
        
        private void UDPReceiveThread(object obj)
        {
            IPEndPoint remoteIpep = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    byte[] bytRecv = UDPScend.Receive(ref remoteIpep);
                    string message = remoteIpep.ToString();
                    TextBoxWrite(UDPRichTbx, recNotifyColor, "7777");
                    //message = Encoding.Unicode.GetString(
                    //    bytRecv, 0, bytRecv.Length);

                    //ShowMessage(txtRecvMssg,
                    //string.Format("{0}[{1}]", remoteIpep, message));
                    TextBoxScrollToEnd(UDPRichTbx);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    break;
                }
            }
        }
        private void UDPLocalIPTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbx = sender as TextBox;
            IPTextBoxCheck(tbx);
        }
        private void UDPTargetIPTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbx = sender as TextBox;
            IPTextBoxCheck(tbx);
        }
        private void UDPLocalPort_LostFocus(object sender, RoutedEventArgs e){
            TextBox tbx = sender as TextBox;
            IPPortTextBoxCheck(tbx);
        }
        private void UDPTargetIPPortTbx_LostFocus(object sender, RoutedEventArgs e){
            TextBox tbx = sender as TextBox;
            bool flag=IPPortTextBoxCheck(tbx);
            if (!flag) tbx.Text = "8501";
        }
        #endregion
        #endregion

        #region GPS
        private void MapInit(){  
            try{
                System.Net.IPHostEntry e = System.Net.Dns.GetHostEntry("ditu.google.cn");
            }
            catch{
                mapControl.Manager.Mode = AccessMode.CacheOnly;
                System.Windows.MessageBox.Show("No internet connection avaible, going to CacheOnly mode.", "GMap.NET Demo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            mapControl.MapProvider = GMapProviders.GoogleChinaTerrainMap;//.GoogleChinaMap; //google china 地图
            mapControl.MinZoom = 2;  //最小缩放
            mapControl.MaxZoom = 24; //最大缩放
            mapControl.Zoom = 15;     //当前缩放
            mapControl.ShowCenter = false; //不显示中心十字点
            mapControl.DragButton = MouseButton.Left; //左键拖拽地图
            mapControl.Position = new PointLatLng(31.89159480045, 120.56872782196048); //地图中心位置：南京
            //button1_Click();//获得当前经纬
            getGeoLocation();
        }

        private void button1_Click()
        {
            string address = "北京";
            string output = "csv";
            string key = "ABQIAAAAXDq__hWKi9eMCwnn7LrMCxT2yXp_ZAY8_ufC3CFXhHIE1NvwkxSnSVp_Xlsd4Ph5iyMua7PE5E0x_A";
            string url = string.Format("http://ditu.google.cn/maps/geo?q={0}&output={1}&key={2}", address, output, key);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string[] tmpArray = sr.ReadToEnd().Split(',');
            string latitude = tmpArray[2];
            string longitude = tmpArray[3];
            System.Windows.Forms.MessageBox.Show(string.Format("纬度: {0}, 经度: {1}", latitude, longitude), address);

        }
        private void getGeoLocation()
        {
            GeoCoordinateWatcher watcher = new GeoCoordinateWatcher();
            watcher.TryStart(false, TimeSpan.FromMilliseconds(5000));////超过5S则返回False;  
            GeoCoordinate coord = watcher.Position.Location;
            if (coord == null)
            {
                GPSRecTbx.Text = "地理未知";
                return;
            }
            if (coord.IsUnknown != true)
            {
                GPSRecTbx.Text = "东经:" + coord.Longitude.ToString() + "\t北纬" + coord.Latitude.ToString() + "\n";
            }
            else
            {
                GPSRecTbx.Text = "地理未知";
            }
        }
        #endregion

        #region 状态栏
        private void UpdateStatusBar(string status)
        {
            comName.Text = ComPort.PortName;
            
            switch (status)
            {
                case "OPEN": { comStatus.Foreground = new SolidColorBrush(Colors.Green); } break;
                case "CLOSED": { comStatus.Foreground = new SolidColorBrush(Colors.Red); } break;
                case "UNABLE": { comStatus.Foreground = new SolidColorBrush(Colors.Red); } break;
            }
            comStatus.Text = status;
        }
        
        #endregion

        #region TabControl
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void functionTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded == false) return;
            if (functionTab.IsInitialized == false) return;
            TABINDEX = functionTab.SelectedIndex;
            if (isFixFunctionTab == false)  //当串口处于打开状态时，先关闭串口，再切换TabControl
            {
                PortSwitch.IsChecked = false;
                PortSwitch_Click(new object(), new RoutedEventArgs());
            }
            isFixFunctionTab = true;        //切换完成后，设置TabControl为可直接切换状态

            PortSwitch.Visibility = Visibility.Visible;
            SettingExpander.Visibility = Visibility.Visible;
            switch (TABINDEX)
            {
                case 0: {
                        SetWindowWidth(750, 720, 800);
                        SetWindowHeight(600, 550, 600);
                        baudRate.SelectedValue = 9600;
                    } break;
                case 1: {
                        SetWindowWidth(1024,960, 1200);
                        SetWindowHeight(700, 640, 800);
                        baudRate.SelectedValue = 9600;
                    } break;
                case 2: {
                        SetWindowWidth(860, 800, 900);

                        SetWindowHeight(640, 600, 650);
                        baudRate.SelectedValue = 115200;
                    } break;
                case 3: { } break;
                case 4: {
                        SetWindowWidth(760, 640, 1100);
                        SetWindowHeight(580, 540, 640);
                        PortSwitch.Visibility = Visibility.Hidden;
                        SettingExpander.Visibility = Visibility.Hidden;
                    } break;
                case 5: {
                        SetWindowWidth(810, 720, 940);
                        SetWindowHeight(640, 560, 768);
                        MapInit();
                    } break;
            }
        }
        private void SetWindowWidth(int width,int minWidth,int maxWidth)
        {
            this.Width = width;
            this.MinWidth = minWidth;
            this.MaxWidth = maxWidth;
        }

        private void UDPRemoteClearBtn_Click(object sender, RoutedEventArgs e)
        {
            REmoteIpepsCbx.Items.Clear();
            UDPRemoteIpeps.Clear();
        }

        private void SetWindowHeight(int height, int minHeight, int maxHeight)
        {
            this.Height = height;
            this.MinHeight = minHeight;
            this.MaxHeight = maxHeight;
        }
        #endregion
    }
}
