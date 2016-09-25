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

namespace ComDebugger
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private int TABINDEX=0;
        private MyPort ComPort = new MyPort();
        private string[] ports;
        IList<Customer> comList = new List<Customer>(); //可用串口集合  
        private bool isFixFunctionTab = false;          //是否可以直接切换TabControl
        private DispatcherTimer autoSendTimer = new DispatcherTimer();  //自动发送定时器

        List<byte[]> recBuffers = new List<byte[]>();   //串口缓冲区的数据，待CRC16循环冗余验证
        private DispatcherTimer WaveDataDeal = new DispatcherTimer();  //定时处理缓冲区数据
        private int DATADEALTimeSpan = 10; 
        List<byte[]> waveDatas = new List<byte[]>();    //已处理好的缓冲数据，等待Plotter展示
        private DispatcherTimer WaveShowTimer = new DispatcherTimer();  //定时处理缓冲区数据
        private int WAVESHOWTimeSpan = 1;
        ulong XAXIS = 0;            //XAXIS横坐标
        readonly int recMaxCount = 20;
        
        private ObservableDataSource<Point> dataSource1 = new ObservableDataSource<Point>();//CH1
        private ObservableDataSource<Point> dataSource2 = new ObservableDataSource<Point>();//CH2
        private ObservableDataSource<Point> dataSource3 = new ObservableDataSource<Point>();//CH3
        private ObservableDataSource<Point> dataSource4 = new ObservableDataSource<Point>();//Ch4 
        public MainWindow()
        {
            InitializeComponent();
        }
        #region 初始化区
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PortOptionInit();       //COM口选项卡初始化
            AddPortEventHandler();  //添加串口事件
            SendVariableInit();     //发送数据有关初始化
            WaveVariableInit();     //示波器有关初始化
            UIInit();               //UI初始化
            
        }
        private void WaveVariableInit()
        {
            WaveDataDeal.Interval = TimeSpan.FromMilliseconds(DATADEALTimeSpan);
            WaveDataDeal.Tick += new EventHandler(BufferDataDeal);
            WaveShowTimer.Interval = TimeSpan.FromMilliseconds(WAVESHOWTimeSpan);
            WaveShowTimer.Tick += new EventHandler(AnimatedPlot);

            dataSource1.AppendAsync(base.Dispatcher, new Point(-100, 0));
            dataSource1.AppendAsync(base.Dispatcher, new Point(0, 0));
            Plotter.AddLineGraph(dataSource1,Colors.Red,1,"CH1");

            dataSource2.AppendAsync(base.Dispatcher, new Point(-100, 0));
            dataSource2.AppendAsync(base.Dispatcher, new Point(0, 0));
            Plotter.AddLineGraph(dataSource2, Colors.Green, 1, "CH2");

            dataSource3.AppendAsync(base.Dispatcher, new Point(-100, 0));
            dataSource3.AppendAsync(base.Dispatcher, new Point(0, 0));
            Plotter.AddLineGraph(dataSource3, Colors.Blue, 1, "CH3");

            dataSource4.AppendAsync(base.Dispatcher, new Point(-100, 0));
            dataSource4.AppendAsync(base.Dispatcher, new Point(0, 0));
            Plotter.AddLineGraph(dataSource4, Colors.Purple, 1, "CH4");
            Plotter.Viewport.FitToView();
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
                MessageBox.Show("无可用串口");
            }

            //↓↓↓↓↓↓↓↓↓波特率下拉控件↓↓↓↓↓↓↓↓↓
            IList<Customer> rateList = new List<Customer>();//可用波特率集合
            rateList.Add(new Customer() { BaudRate = "9600" });
            rateList.Add(new Customer() { BaudRate = "19200" });
            rateList.Add(new Customer() { BaudRate = "38400" });
            rateList.Add(new Customer() { BaudRate = "57600" });
            rateList.Add(new Customer() { BaudRate = "115200" });
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
                        WaveShowTimer.Start();
                    } break;
                case "CameraTab": { } break;
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
                        WaveShowTimer.Stop();
                    } break;
                case "CameraTab": { } break;
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
        private void Port_NormalReceived(object sender, SerialDataReceivedEventArgs e)
        {
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
        private void Port_WaveShow(object sender, SerialDataReceivedEventArgs e)
        {
            if (TABINDEX != 1)
                return;
            Thread.Sleep(10);
            byte[] buffer=null;
            
            if (ComPort.IsOpen)
            {
                if (ComPort.BytesToRead < 100)
                    return;
                buffer = new byte[ComPort.BytesToRead];//接收数据缓存 
            }   
            else {
                return;
            } 
            int count=ComPort.Read(buffer, 0, buffer.Length);//读取数据 
            if(count >= 10)
                recBuffers.Add(buffer);
            if (recBuffers.Count >= recMaxCount)
                recBuffers.RemoveAt(0);
        }
        private void BufferDataDeal(object sender, EventArgs e)
        {
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
                    //PlottrDispatcherInvoke(subBuf);
                    waveDatas.Add(subBuf);
                    listbuff.RemoveRange(0, 10);
                    continue;
                }
                listbuff.RemoveAt(0);
            }
        }
        private void AnimatedPlot(object sender, EventArgs e)
        {
            if (waveDatas.Count <= 0)
                return;
            byte[] buffer = waveDatas[0];
            this.Plotter.Dispatcher.Invoke(
                new Action(
                        delegate {
                            Point[] CH = new Point[4];
                            for (int k = 0; k < 4; k++)
                            {
                                int temp = buffer[2 * k + 1];
                                temp <<= 8;
                                temp |= buffer[2 * k];
                                CH[k] = new Point(XAXIS, temp);
                            }
                            XAXIS++;
                            dataSource1.AppendAsync(Plotter.Dispatcher, CH[0]);
                            dataSource2.AppendAsync(Plotter.Dispatcher, CH[1]);
                            dataSource3.AppendAsync(Plotter.Dispatcher, CH[2]);
                            dataSource4.AppendAsync(Plotter.Dispatcher, CH[3]);
                        }
                    )
                );
            waveDatas.RemoveAt(0);
        }
        #endregion

        #region Port_CameraDebug
        private void Port_CameraDebug(object sender, SerialDataReceivedEventArgs e)
        {
            if (TABINDEX != 2)
                return;
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
            TABINDEX = functionTab.SelectedIndex;
            if (isFixFunctionTab == false)  //当串口处于打开状态时，先关闭串口，再切换TabControl
            {
                PortSwitch.IsChecked = false;
                PortSwitch_Click(new object(), new RoutedEventArgs());
            }

            isFixFunctionTab = true;        //切换完成后，设置TabControl为可直接切换状态
        }
        #endregion

        
    }
}
