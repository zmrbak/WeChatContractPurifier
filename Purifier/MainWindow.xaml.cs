using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

namespace Purifier
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //被注入的DLL名字
        String InJectedDll = "WxPurifier.dll";
        //关键词列表
        List<string> keyWordList = new List<string>();
        Object locker = new object();
        Object logLocker = new object();

        public MainWindow()
        {
            InitializeComponent();
        }

        //启动微信
        private void Bt_WxStart(object sender, RoutedEventArgs e)
        {
            if (WxStart() == false)
            {
                return;
            }

            //1) 遍历系统中的进程，找到微信进程（CreateToolhelp32Snapshot、Process32Next）
            Process[] processes = Process.GetProcesses();
            Process WxProcess = null;
            foreach (Process process in processes)
            {
                if (process.ProcessName.ToLower() == "WeChat".ToLower())
                {
                    WxProcess = process;
                    foreach (ProcessModule processModule in WxProcess.Modules)
                    {
                        if (processModule.ModuleName == InJectedDll)
                        {
                            MessageBox.Show("已经在监控中", "警告", MessageBoxButton.OK, MessageBoxImage.Stop);
                            return;
                        }
                    }
                    break;
                }
            }

            if (WxProcess == null)
            {
                MessageBox.Show("注入前请先启动微信！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //2) 打开微信进程，获得HANDLE（OpenProcess）。
            //3) 在微信进程中为DLL文件路径字符串申请内存空间（VirtualAllocEx）。
            String DLlPath = System.IO.Path.GetFullPath(InJectedDll); //\0
            if (File.Exists(DLlPath) == false)
            {
                MessageBox.Show("被注入的DLL文件(" + DLlPath + ")不存在！\n请把被注入的DLL文件放在本程序所在目录下。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;

            }

            int DllPathSize = DLlPath.Length * 2 + 1;
            int MEM_COMMIT = 0x00001000;
            int PAGE_READWRITE = 0x04;
            int DllAddress = VirtualAllocEx((int)WxProcess.Handle, 0, DllPathSize, MEM_COMMIT, PAGE_READWRITE);
            if (DllAddress == 0)
            {
                MessageBox.Show("内存分配失败！");
                return;
            }

            //4) 把DLL文件路径字符串写入到申请的内存中（WriteProcessMemory）
            if (WriteProcessMemory((int)WxProcess.Handle, DllAddress, DLlPath, DllPathSize, 0) == false)
            {
                MessageBox.Show("内存写入失败！");
                return;
            };


            //5) 从Kernel32.dll中获取LoadLibraryA的函数地址（GetModuleHandle、GetProcAddress）
            int module = GetModuleHandleA("Kernel32.dll");
            int LoadLibraryAddress = GetProcAddress(module, "LoadLibraryA");
            if (LoadLibraryAddress == 0)
            {
                MessageBox.Show("查找LoadLibraryA地址失败！");
                return;
            }

            //6) 在微信中启动内存中指定了文件名路径的DLL（CreateRemoteThread）。
            if (CreateRemoteThread((int)WxProcess.Handle, 0, 0, LoadLibraryAddress, DllAddress, 0, 0) == 0)
            {
                MessageBox.Show("执行远程线程失败！");
                return;
            }

        }

        /// <summary>
        /// 启动微信
        /// </summary>
        /// <returns></returns>
        private Boolean WxStart()
        {
            //如果当前系统中，微信在运行，则重启微信
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                if (process.ProcessName.ToLower() == "WeChat".ToLower())
                {
                    return true;
                }
            }

            String WxPath = "";
            //启动微信
            //在注册表中查找微信
            //计算机\HKEY_CURRENT_USER\Software\Tencent\WeChat
            //InstallPath
            try
            {
                RegistryKey registryKey = Registry.CurrentUser;
                RegistryKey software = registryKey.OpenSubKey("Software\\Tencent\\WeChat");
                object InstallPath = software.GetValue("InstallPath");
                WxPath = InstallPath.ToString() + "\\WeChat.exe";
                registryKey.Close();
            }
            catch
            {
                WxPath = "";
            }

            if (WxPath != "")
            {
                Process.Start(WxPath);
                Thread.Sleep(2000);
                return true;
            }
            else
            {
                MessageBox.Show("在系统中未找到微信，请手动启动微信", "错误", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                return false;
            }
        }


        #region  WinApi
        [DllImport("Kernel32.dll")]
        //LPVOID VirtualAllocEx(
        //  HANDLE hProcess,
        //  LPVOID lpAddress,
        //  SIZE_T dwSize,
        //  DWORD flAllocationType,
        //  DWORD flProtect
        //);
        public static extern int VirtualAllocEx(int hProcess, int lpAddress, int dwSize, int flAllocationType, int flProtect);

        [DllImport("Kernel32.dll")]
        //BOOL WriteProcessMemory(
        //  HANDLE hProcess,
        //  LPVOID lpBaseAddress,
        //  LPCVOID lpBuffer,
        //  SIZE_T nSize,
        //  SIZE_T* lpNumberOfBytesWritten
        //);
        public static extern Boolean WriteProcessMemory(int hProcess, int lpBaseAddress, String lpBuffer, int nSize, int lpNumberOfBytesWritten);

        [DllImport("Kernel32.dll")]
        //HMODULE GetModuleHandleA(
        //  LPCSTR lpModuleName
        //);
        public static extern int GetModuleHandleA(String lpModuleName);

        [DllImport("Kernel32.dll")]
        //FARPROC GetProcAddress(
        //  HMODULE hModule,
        //  LPCSTR lpProcName
        //);
        public static extern int GetProcAddress(int hModule, String lpProcName);

        [DllImport("Kernel32.dll")]
        //HANDLE CreateRemoteThread(
        //  HANDLE hProcess,
        //  LPSECURITY_ATTRIBUTES lpThreadAttributes,
        //  SIZE_T dwStackSize,
        //  LPTHREAD_START_ROUTINE lpStartAddress,
        //  LPVOID lpParameter,
        //  DWORD dwCreationFlags,
        //  LPDWORD lpThreadId
        //);
        public static extern int CreateRemoteThread(int hProcess, int lpThreadAttributes, int dwStackSize, int lpStartAddress, int lpParameter, int dwCreationFlags, int lpThreadId);


        [DllImport("Kernel32.dll")]
        //BOOL VirtualFreeEx(
        //  HANDLE hProcess,
        //  LPVOID lpAddress,
        //  SIZE_T dwSize,
        //  DWORD dwFreeType
        //);
        public static extern Boolean VirtualFreeEx(int hProcess, int lpAddress, int dwSize, int dwFreeType);
        #endregion

        private void Bt_KeyWord_Click(object sender, RoutedEventArgs e)
        {
            KeyWordWindow keyWord = new KeyWordWindow();
            if (keyWord.ShowDialog() == true)
            {
                KeyWordRefresh();
            }
        }

        private void KeyWordRefresh()
        {
            lock (locker)
            {
                keyWordList.Clear();
                String fileName = ConfigurationManager.AppSettings["CONFIG_PATH"] +"\\"+ ConfigurationManager.AppSettings["KEYWORDS_FILE"];
                if (File.Exists(fileName) == false)
                {
                    return;
                }

                var keyword = File.ReadLines(fileName);
                foreach (String item in keyword)
                {
                    keyWordList.Add(item);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckConfig();
            KeyWordRefresh();
            StartWeb();
        }

        /// <summary>
        /// 检查本地配置
        /// </summary>
        private void CheckConfig()
        {
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            Boolean connfIsOK = true;

            int clientPort = 8421;
            int wxPort = 8422;

            var CLIENT_PORT = cfa.AppSettings.Settings["CLIENT_PORT"];
            var WX_PORT = cfa.AppSettings.Settings["WX_PORT"];
            var KEYWORDS_FILE = cfa.AppSettings.Settings["KEYWORDS_FILE"];
            var LOG_PATH = cfa.AppSettings.Settings["LOG_PATH"];
            var CONFIG_PATH = cfa.AppSettings.Settings["CONFIG_PATH"];

            if (CLIENT_PORT == null)
            {
                cfa.AppSettings.Settings.Add("CLIENT_PORT", clientPort.ToString());
                connfIsOK = false;
            }
            else if (CLIENT_PORT.Value == "")
            {
                cfa.AppSettings.Settings["CLIENT_PORT"].Value = clientPort.ToString();
                connfIsOK = false;
            }

            if (WX_PORT == null)
            {
                cfa.AppSettings.Settings.Add("WX_PORT", wxPort.ToString());
                connfIsOK = false;
            }
            else if (WX_PORT.Value == "")
            {
                cfa.AppSettings.Settings["WX_PORT"].Value = wxPort.ToString();
                connfIsOK = false;
            }

            if (KEYWORDS_FILE == null)
            {
                cfa.AppSettings.Settings.Add("KEYWORDS_FILE", "BanedKeywordList.txt");
                connfIsOK = false;
            }
            else if (KEYWORDS_FILE.Value == "")
            {
                cfa.AppSettings.Settings["KEYWORDS_FILE"].Value = "BanedKeywordList.txt";
                connfIsOK = false;
            }

            if (LOG_PATH == null)
            {
                cfa.AppSettings.Settings.Add("LOG_PATH", "logs");
                connfIsOK = false;
            }
            else if (LOG_PATH.Value == "")
            {
                cfa.AppSettings.Settings["LOG_PATH"].Value = "logs";
                connfIsOK = false;
            }

            if (Directory.Exists(cfa.AppSettings.Settings["LOG_PATH"].Value) == false)
            {
                Directory.CreateDirectory(cfa.AppSettings.Settings["LOG_PATH"].Value);
            }

            if (CONFIG_PATH == null)
            {
                cfa.AppSettings.Settings.Add("CONFIG_PATH", "config");
                connfIsOK = false;
            }
            else if (CONFIG_PATH.Value == "")
            {
                cfa.AppSettings.Settings["CONFIG_PATH"].Value = "config";
                connfIsOK = false;
            }
            if (Directory.Exists(cfa.AppSettings.Settings["CONFIG_PATH"].Value) == false)
            {
                Directory.CreateDirectory(cfa.AppSettings.Settings["CONFIG_PATH"].Value);
            }

            if (connfIsOK == false)
            {
                cfa.Save();
                ConfigurationManager.RefreshSection("appSettings");
            }

            ComReg comReg = new ComReg();
            comReg.ComRegister();

        }
        /// <summary>
        /// 启动本地端Web服务器
        /// </summary>
        private Boolean StartWeb()
        {
            int port = 8421;
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (int.TryParse(ConfigurationManager.AppSettings["CLIENT_PORT"], out port) == true)
            {
                WxHttpServer wxHttpServer = new WxHttpServer();
                wxHttpServer.Port = port;
                wxHttpServer.OnDataReceived += WxHttpServer_OnDataReceived;
                if (wxHttpServer.Start() == true)
                {
                    return true;
                }
            }

            MessageBox.Show("配置文件中“CLIENT_PORT”的值有错，请重新配置！", "警告", MessageBoxButton.OK, MessageBoxImage.Stop);
            Process.Start("notepad.exe", cfa.FilePath);
            return false;
        }

        private void WxHttpServer_OnDataReceived(System.Net.HttpListenerRequest reqeust, System.Net.HttpListenerResponse response)
        {
            String dataString = "";
            if (reqeust.HttpMethod == "POST")
            {
                Console.WriteLine("POST");
                Stream stream = reqeust.InputStream;
                BinaryReader binaryReader = new BinaryReader(stream);

                byte[] data = new byte[reqeust.ContentLength64];
                binaryReader.Read(data, 0, (int)reqeust.ContentLength64);

                dataString = Encoding.UTF8.GetString(data);
                lock (locker)
                {
                    foreach (String keyWords in keyWordList)
                    {
                        if (dataString == "") return;
                        if (dataString.IndexOf(keyWords) > -1)
                        {
                            DeleteUser(dataString, keyWords);
                            break;
                        }
                    }
                }
            }

            string responseString = "";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

           
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="dataString"></param>
        /// <param name="keyWords"></param>
        private void DeleteUser(String dataString, String keyWords)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(WxTextMsg));
            byte[] byteArry = Encoding.UTF8.GetBytes(dataString);
            System.IO.Stream stream = new System.IO.MemoryStream(byteArry);
            WxTextMsg wxTextMsg = (WxTextMsg)xmlSerializer.Deserialize(stream);

            StringBuilder message = new StringBuilder();
            message.Append("机器人微信ID:" + wxTextMsg.robot_wxid + Environment.NewLine);
            message.Append("机器人微信昵称:" + wxTextMsg.robot_nickname + Environment.NewLine);
            message.Append("来源微信ID:" + wxTextMsg.from_wxid + Environment.NewLine);
            message.Append("来源微信昵称:" + wxTextMsg.from_nickname + Environment.NewLine);
            message.Append("文本消息内容:" + wxTextMsg.msg + Environment.NewLine);
            message.Append("包含关键词:" + keyWords + Environment.NewLine);

            Boolean isUserDeleteChecked = false;
            this.Dispatcher.Invoke(new Action(() =>
            {
                if(cb_UserDelete.IsChecked==true)
                {
                    isUserDeleteChecked = true;
                }
            }));

            if (isUserDeleteChecked == true)
            {
                message.Append("操作:删除" + Environment.NewLine);

                var postObject =new
                {
                    Cmd = "ContractDelete",
                    RobotWxid= wxTextMsg.robot_wxid,
                    Wxid = wxTextMsg.from_wxid
                };

                JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                String postString = javaScriptSerializer.Serialize(postObject);

                ParameterizedThreadStart s = new ParameterizedThreadStart(SendWxMsg);
                (new Thread(s)).Start(postString);
            }
            else
            {
                message.Append("操作:标记" + Environment.NewLine);
            }

            message.Append(Environment.NewLine);
            this.Dispatcher.Invoke(new Action(() =>
            {
                this.tb_LogMsg.AppendText(message.ToString());
            }));

            String logFileName = ConfigurationManager.AppSettings["LOG_PATH"] + "\\" + DateTime.Now.ToString("yyyy-MM-dd")+ ".txt";
            lock (logLocker)
            {
                JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                javaScriptSerializer.Serialize(wxTextMsg);
                File.AppendAllText(logFileName, javaScriptSerializer.Serialize(wxTextMsg));
            }
        }

        /// <summary>
        /// 发送删除用户的消息
        /// </summary>
        /// <param name="wxid"></param>
        private void SendWxMsg(Object postString)
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.UploadString("http://127.0.0.1:" + ConfigurationManager.AppSettings["WX_PORT"] + "/", postString.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Bt_ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", ConfigurationManager.AppSettings["LOG_PATH"]);
        }

        private void Bt_GitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/zmrbak/PcWeChatHooK");
        }

        private void Bt_163Class_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://t.cn/EXUbebQ");
        }
    }

    // 注意: 生成的代码可能至少需要 .NET Framework 4.5 或 .NET Core/Standard 2.0。
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class WxTextMsg
    {
        private string robot_wxidField;
        private string robot_nicknameField;
        private string from_wxidField;
        private string from_nicknameField;
        private string msgField;

        /// <remarks/>
        public string robot_wxid
        {
            get
            {
                return this.robot_wxidField;
            }
            set
            {
                this.robot_wxidField = value;
            }
        }

        /// <remarks/>
        public string robot_nickname
        {
            get
            {
                return this.robot_nicknameField;
            }
            set
            {
                this.robot_nicknameField = value;
            }
        }

        /// <remarks/>
        public string from_wxid
        {
            get
            {
                return this.from_wxidField;
            }
            set
            {
                this.from_wxidField = value;
            }
        }

        /// <remarks/>
        public string from_nickname
        {
            get
            {
                return this.from_nicknameField;
            }
            set
            {
                this.from_nicknameField = value;
            }
        }

        /// <remarks/>
        public string msg
        {
            get
            {
                return this.msgField;
            }
            set
            {
                this.msgField = value;
            }
        }
    }
}
