using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public static readonly string WakeupMessageName = "LANCHAT_WAKEUP_INSTANCE_MSG_V2";
        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;

        [STAThread]
        static void Main()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDPIAware();
                }
            }
            catch { }

            // 单实例互斥检查：只允许运行一个 LanChat 客户端
            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, "Global\\LanChat_SingleInstance_Mutex_9F24FEA2", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    // 已有实例在运行，向系统广播自定义消息唤醒主实例并前置显示
                    try
                    {
                        int msgId = RegisterWindowMessage(WakeupMessageName);
                        PostMessage(HWND_BROADCAST, msgId, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                    return; // 立即退出当前多余实例
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    MessageBox.Show("发生未捕获异常: " + e.Exception.Message, "系统提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Exception ex = e.ExceptionObject as Exception;
                    MessageBox.Show("严重错误: " + (ex != null ? ex.Message : "未知错误"), "系统提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                Application.Run(new MainForm());
            }
        }
    }
}
