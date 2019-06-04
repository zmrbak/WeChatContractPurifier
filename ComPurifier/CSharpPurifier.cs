using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace ComPurifier
{
    [ComVisible(true)]
    [Guid("76A394CB-6EEC-476D-8026-7D8680AA853B")]
    public interface ICSharpPurifier
    {
        [DispId(1)]
        Boolean StartWeb();

        [DispId(2)]
        Boolean PostString(String postString);
    }

    [ComVisible(true)]
    [Guid("57C1317C-039F-45AC-A420-97C2E6298D29")]
    [ProgId("ComPurifier.CSharpPurifier")]
    public class CSharpPurifier : ICSharpPurifier
    {
        //被注入的DLL名字
        String InJectedDll = "WxPurifier.dll";
        String InJectedDllFullName = "";

        public Boolean StartWeb()
        {
            //寻找DLL
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
                            InJectedDllFullName = processModule.FileName;
                        }
                    }
                    break;
                }
            }

            if (InJectedDllFullName == "")
            {
                MessageBox.Show("读取DLL错误", "错误");
                return false;
            }

            //寻找配置文件
            String[] configFiles = Directory.GetFiles(Path.GetDirectoryName(InJectedDllFullName), "*.config");
            if (configFiles.Length != 1)
            {
                MessageBox.Show("配置文件数量有误，请只保留一个配置文件！", "错误");
                return false;
            }

            int port = 8422;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configFiles[0]);
                XmlNodeList nodes = doc.GetElementsByTagName("appSettings");
                for (int i = 0; i < nodes[0].ChildNodes.Count; i++)
                {
                    var node = nodes[0].ChildNodes[i];
                    if (node.Attributes["key"].Value.ToString().Equals("WX_PORT"))
                    {
                        port = int.Parse(node.Attributes["value"].Value);
                        break;
                    }
                }
            }
            catch
            {
                MessageBox.Show("读取配置文件错误", "错误");
                return false;
            }

            WxHttpServer wxHttpServer = new WxHttpServer();
            wxHttpServer.Port = port;
            wxHttpServer.OnDataReceived += WxHttpServer_OnDataReceived; ;
            if (wxHttpServer.Start() == true)
            {
                return true;
            }

            return false;
        }

        private void WxHttpServer_OnDataReceived(System.Net.HttpListenerRequest reqeust, System.Net.HttpListenerResponse response)
        {
            String dataString = "";
            string responseString = "";
            if (reqeust.HttpMethod == "POST")
            {
                Console.WriteLine("POST");
                Stream stream = reqeust.InputStream;
                BinaryReader binaryReader = new BinaryReader(stream);

                byte[] data = new byte[reqeust.ContentLength64];
                binaryReader.Read(data, 0, (int)reqeust.ContentLength64);
                dataString = Encoding.UTF8.GetString(data);

                JavaScriptSerializer js = new JavaScriptSerializer();
                ClientCmd clientCmd = js.Deserialize<ClientCmd>(dataString);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(clientCmd.Wxid);
                Boolean result = DeleteWxUser(stringBuilder);

                responseString = (
                    new
                    {
                        cmd = clientCmd.Cmd,
                        result = result.ToString()
                    }
                ).ToString();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        [DllImport(
            "WxPurifier.dll",
            EntryPoint = "DeleteWxUser",
            CharSet = CharSet.Unicode,
            CallingConvention = CallingConvention.StdCall
            )]
        extern static Boolean DeleteWxUser(StringBuilder contractWxId);

        public bool PostString(string postString)
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.Encoding = Encoding.UTF8;
                webClient.UploadString("http://127.0.0.1:8421/", postString);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }
    }
    public class ClientCmd
    {
        public string Cmd { get; set; }
        public string RobotWxid { get; set; }
        public string Wxid { get; set; }
    }

}
