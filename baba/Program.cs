using System;
using System.Windows.Forms;

namespace baba
{
    /*
     * ================================================================
     *  关于“游戏反作弊”的说明（请放心）：
     *  本程序是 100% 普通的标准 Windows 窗体程序（WinForms），只做这些事：
     *   - 不注入任何 DLL
     *   - 不安装任何全局键盘/鼠标钩子（SetWindowsHookEx）
     *   - 不读取/写入任何进程的内存（不用 OpenProcess / ReadProcessMemory 等）
     *   - 不抓取屏幕或游戏画面
     *   - 不修改任何游戏文件、不做网络封包
     *   - 唯一的系统级调用是只读的 EnumWindows：列出窗口矩形当障碍物，
     *     这是系统工具、窗口管理器等成千上万正常程序都在用的 API。
     *  它本质上就是一个置顶的透明桌面宠物窗口，和普通桌面宠物一样，
     *  不应也不会被反作弊系统当作作弊工具。
     * ================================================================
     */
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        ///  任何异常都只会弹中文提示框，绝对不允许闪退。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (sender, e) =>
            {
                Log("ThreadException: " + e.Exception);
                SafeError("出错了，但程序不会崩：" + Environment.NewLine + e.Exception.Message);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log("UnhandledException: " + (ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "未知"));
                SafeError("出错了，但程序不会崩：" + Environment.NewLine + (ex?.Message ?? e.ExceptionObject?.ToString() ?? "未知错误"));
            };

            ApplicationConfiguration.Initialize();

            try
            {
                PetSettings settings = SettingsStore.Load();
                Application.Run(new MainForm(settings));
                SettingsStore.Save(settings); // 退出前再存一次，防止漏存
            }
            catch (Exception ex)
            {
                Log("Main catch: " + ex);
                SafeError("出错了，但程序不会崩：" + Environment.NewLine + ex.Message);
            }
        }

        internal static void Log(string message)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonkeyPet");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "babapet_log.txt");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        private static void SafeError(string message)
        {
            try
            {
                MessageBox.Show(message, "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
                // 连弹窗都失败时什么也不做，绝不抛回导致崩溃
            }
        }
    }
}
