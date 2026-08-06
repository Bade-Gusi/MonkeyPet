using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace baba
{
    /// <summary>
    /// 开机自启动：优先写当前用户注册表 Run 键；如果被安全软件/策略锁住（写不进去），
    /// 自动退回“启动文件夹快捷方式”。两种都不需要管理员权限。
    /// </summary>
    internal static class AutoStartHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MonkeyPet";

        private static string StartupShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "猴群宠物.lnk");

        public static void Enable()
        {
            // 方式一：注册表 Run 键（最常见）
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                // 路径带引号，防止含空格时开机起不来
                key?.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                return; // 写成功就直接结束
            }
            catch
            {
                // 注册表被安全软件/策略锁住，走方式二
            }

            // 方式二：启动文件夹里放一个快捷方式
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(StartupShortcutPath);
                shortcut.TargetPath = Application.ExecutablePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) ?? "";
                shortcut.Description = "猴群宠物";
                shortcut.Save();
                Marshal.FinalReleaseComObject(shell);
            }
            catch
            {
                // 两个方式都不行就静默，不影响主程序
            }
        }

        public static void Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch
            {
            }

            try
            {
                if (File.Exists(StartupShortcutPath)) File.Delete(StartupShortcutPath);
            }
            catch
            {
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (key?.GetValue(ValueName) != null) return true;
            }
            catch
            {
            }

            try
            {
                return File.Exists(StartupShortcutPath);
            }
            catch
            {
                return false;
            }
        }
    }
}
