using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Configuration;

namespace Purifier
{
    /// <summary>
    /// KeyWordWindow.xaml 的交互逻辑
    /// </summary>
    public partial class KeyWordWindow : Window
    {
        String fileName = ConfigurationManager.AppSettings["CONFIG_PATH"] + "\\" + ConfigurationManager.AppSettings["KEYWORDS_FILE"];
        public KeyWordWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bt_Save_Click(object sender, RoutedEventArgs e)
        {
            String keyWordList = this.tb_Keys.Text;
            File.WriteAllText(System.IO.Path.GetFullPath(fileName), keyWordList);
            this.DialogResult = true;
            this.Close();
        }

        private void Bt_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if(File.Exists(fileName)==true)
            {
                this.tb_Keys.Text = File.ReadAllText(fileName);
            }
        }
    }
}
