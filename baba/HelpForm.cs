using System;
using System.Drawing;
using System.Windows.Forms;

namespace baba
{
    /// <summary>
    /// 新手教程：第一次运行自动弹出，之后按 F2 或在设置里点「新手教程」可以再看。
    /// </summary>
    public sealed class HelpForm : Form
    {
        private const string TutorialText =
            "欢迎使用 猴群宠物！🐵\r\n" +
            "\r\n" +
            "跟着下面 4 步就能玩起来：\r\n" +
            "\r\n" +
            "1️⃣ 看它们玩\r\n" +
            "    打开后，一群猴子（或你们几个人）会全屏爬来爬去、\r\n" +
            "    打滚玩耍，撞到窗口和屏幕边缘会被弹开。\r\n" +
            "\r\n" +
            "2️⃣ 让它喊爸爸\r\n" +
            "    右键点任意一只猴子 → 它定住 0.3 秒并大喊。\r\n" +
            "    想换声音？把 WAV 文件改名 dad.wav 放进 assets 文件夹。\r\n" +
            "\r\n" +
            "3️⃣ 调整它们\r\n" +
            "    点右上角齿轮（或按 F1）打开设置：\r\n" +
            "    · 猴子数量：想几只就几只（1~6）\r\n" +
            "    · 速度 / 大小 / 爬行幅度 / 打滚频率：随便拖\r\n" +
            "    · 换人：设置里「猴子图片」→ 每只猴子「换图」，\r\n" +
            "      或用「抠图工具」把合影里的人物抠出来当猴子\r\n" +
            "\r\n" +
            "4️⃣ 退出\r\n" +
            "    按 ESC 退出程序。\r\n" +
            "\r\n" +
            "小提示：\r\n" +
            "· 所有设置改完立刻生效，自动保存，不用重启。\r\n" +
            "· 按 F2 可以随时再看这个教程。\r\n" +
            "· 本程序已在 GitHub 开源（MIT 协议），链接在设置窗口里。\r\n";

        public HelpForm()
        {
            Text = "猴群宠物 · 新手教程";
            ClientSize = new Size(560, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f);

            var rtb = new RichTextBox
            {
                ReadOnly = true,
                Location = new Point(14, 14),
                Size = new Size(532, 440),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 11f),
                BackColor = Color.White,
                Text = TutorialText,
            };

            var btn = new Button
            {
                Text = "我明白了，开始玩！",
                Location = new Point(14, 462),
                Size = new Size(532, 48),
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 200, 60),
                FlatStyle = FlatStyle.Flat,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(255, 180, 40);
            btn.Click += (s, e) => Close();

            Controls.Add(rtb);
            Controls.Add(btn);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
