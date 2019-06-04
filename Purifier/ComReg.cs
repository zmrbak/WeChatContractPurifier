using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Purifier
{
    public class ComReg
    {
        String comDllPathName = "";

        public string ComDllPathName { get => comDllPathName; set => comDllPathName = value; }

        public ComReg()
        {
            this.comDllPathName = Path.GetFullPath("ComPurifier.dll");
        }

        public Boolean ComRegister()
        {
            //获取.NET安装位置

            if (IsComRegisted() == false)
            {
                try
                {
                    string comDllPathName = Path.GetFullPath("ComPurifier.dll");
                    String pathName = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
                    String fileGacutil = "gacutil.exe";
                    String fileRegasm = pathName + "regasm.exe";
                    if (File.Exists(fileGacutil) == false)
                    {
                        throw (new Exception("文件缺失："+fileGacutil+"，程序无法继续执行..."));
                    }

                    if (File.Exists(fileRegasm) == false)
                    {
                        throw (new Exception("文件缺失：" + fileRegasm + "，程序无法继续执行..."));
                    }


                    Process p = Process.Start(fileGacutil, "/i " + comDllPathName);
                    while (p.HasExited == false)
                    {
                        Thread.Sleep(100);
                    }

                    Process pRegasm = Process.Start(fileRegasm, comDllPathName);
                    while (pRegasm.HasExited == false)
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    throw (ex);
                }
            }
            return true;
        }

        private Boolean IsComRegisted()
        {
            String clsid = "";

            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(comDllPathName));
            Type[] types = assembly.GetTypes();
            foreach (Type item in types)
            {
                if (item.FullName == "ComPurifier.ICSharpPurifier")
                {
                    clsid = item.GUID.ToString();
                    break;
                }
            }

            if (clsid == "")
            {
                throw (new Exception("文件未找到:" + comDllPathName));
            }

            RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry32);
            string cld = String.Format("\\Interface\\{0}{1}{2}", "{", clsid.ToUpper(), "}");
            RegistryKey comKey = root.OpenSubKey(cld);
            if (comKey == null)
            {
                return false;
            }

            return true;
        }
    }
}
