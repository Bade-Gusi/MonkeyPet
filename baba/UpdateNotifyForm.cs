using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace baba
{
    /// <summary>发现新版本时弹出的非模态小窗，不打断操作。</summary>
    public sealed class UpdateNotifyForm : Form
    {
        public UpdateNotifyForm(string latest, string url)
        {
            Text = "发现新版本";
            ClientSize = new Size(360, 130);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f);

            var label = new Label
            {
                Text = "✨ 发现新版本 v" + latest + "\r\n当前版本 v" + UpdateChecker.CurrentVersion,
                Location = new Point(18, 14),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            };

            var dlBtn = new Button { Text = "去 GitHub 下载", Location = new Point(18, 72), Size = new Size(150, 40) };
            dlBtn.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                Close();
            };
            var okBtn = new Button { Text = "知道了", Location = new Point(184, 72), Size = new Size(150, 40) };
            okBtn.Click += (s, e) => Close();

            Controls.Add(label);
            Controls.Add(dlBtn);
            Controls.Add(okBtn);
        }
    }
}
