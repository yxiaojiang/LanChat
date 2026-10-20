using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    static class UninstallProgram
    {
        private const string PRODUCT_CODE = "{7E993262-EAA5-4A37-975D-39F24FEA21D2}";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length >= 2 && args[0] == "--do-uninstall")
            {
                // Running from %TEMP%
                string targetDir = args[1];
                PerformUninstall(targetDir);
                return;
            }

            // Running directly from install directory
            DialogResult dr = MessageBox.Show(
                "您确定要从这台计算机彻底卸载 LanChat 及其所有组件吗？\n\n卸载过程将自动关闭运行中的程序并清理全部相关快捷方式。",
                "LanChat 卸载向导",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (dr != DialogResult.Yes)
            {
                return;
            }

            // 1. Close any running LanChat instances
            KillRunningLanChat();

            // 2. Copy self to %TEMP% to prevent file locking
            string tempExe = Path.Combine(Path.GetTempPath(), "LanChat_Uninstall_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
            string currentExe = Application.ExecutablePath;
            string installDir = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                File.Copy(currentExe, tempExe, true);
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = string.Format("--do-uninstall \"{0}\"", installDir),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动卸载程序失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void PerformUninstall(string targetDir)
        {
            Thread.Sleep(600); // Wait for original uninstall.exe to exit

            KillRunningLanChat();

            // Run Windows Installer uninstall
            try
            {
                Process msiProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = string.Format("/x {0} /qb", PRODUCT_CODE),
                    UseShellExecute = true
                });

                if (msiProc != null)
                {
                    msiProc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("调用 Windows Installer 卸载失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Cleanup target directory residual files
            try
            {
                if (Directory.Exists(targetDir))
                {
                    foreach (string f in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(f, FileAttributes.Normal);
                            File.Delete(f);
                        }
                        catch { }
                    }

                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            Directory.Delete(targetDir, true);
                            break;
                        }
                        catch
                        {
                            Thread.Sleep(300);
                        }
                    }
                }
            }
            catch { }

            // Cleanup residual shortcuts if any remain
            CleanupShortcuts();

            MessageBox.Show(
                "LanChat 已成功从您的计算机卸载完毕，未残留任何文件。",
                "卸载完成",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Self-delete temp exe
            try
            {
                string selfPath = Application.ExecutablePath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c ping 127.0.0.1 -n 2 >nul & del /f /q \"{0}\"", selfPath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }
            catch { }
        }

        private static void KillRunningLanChat()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("LanChat"))
                {
                    try
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(800))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void CleanupShortcuts()
        {
            string[] paths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LanChat.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "LanChat.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "LanChat.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "LanChat.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "卸载 LanChat.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "卸载 LanChat.lnk")
            };

            foreach (string p in paths)
            {
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }
        }
    }
}
