using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace baba
{
    /// <summary>
    /// 傻瓜式设置窗口：全中文大字、拖滑杆就能调，改完立刻生效，不用重启。
    /// 猴子图片的槽位跟着“猴子数量”走：设几只就有几个缩略图和换图按钮。
    /// 右上角齿轮按钮或 F1 打开。
    /// </summary>
    public sealed class SettingsForm : Form
    {
        /// <summary>本程序的开源地址（MIT 协议）。</summary>
        public const string RepoUrl = "https://github.com/Bade-Gusi/MonkeyPet";

        private readonly MainForm _pet;
        private readonly PetSettings _settings;

        private readonly Label _title = new Label();
        private readonly Label _sub = new Label();

        private readonly GroupBox _gMonkey = new GroupBox { Text = "猴子" };
        private readonly TrackBar _countBar = new TrackBar();
        private Label _countValue = new Label();

        private readonly GroupBox _gAction = new GroupBox { Text = "动作" };
        private readonly TrackBar _speedBar = new TrackBar();
        private Label _speedValue = new Label();
        private readonly TrackBar _bobBar = new TrackBar();
        private Label _bobValue = new Label();
        private readonly TrackBar _groupBar = new TrackBar();
        private Label _groupValue = new Label();

        private readonly GroupBox _gBehavior = new GroupBox { Text = "行为" };
        private CheckBox _topMostCheck = new CheckBox();
        private CheckBox _soundCheck = new CheckBox();
        private CheckBox _hintCheck = new CheckBox();

        private readonly GroupBox _gImage = new GroupBox { Text = "猴子图片（跟着数量走，实时缩略图）" };
        private readonly Label _imgHeader = new Label();
        private readonly Button _cutoutBtn = new Button();
        private readonly List<PictureBox> _thumbs = new List<PictureBox>();

        private readonly GroupBox _gOps = new GroupBox { Text = "操作" };
        private readonly LinkLabel _license = new LinkLabel();

        public SettingsForm(MainForm pet, PetSettings settings)
        {
            _pet = pet;
            _settings = settings;

            Text = "猴群宠物 · 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f);

            BuildUi();
            LoadValues();
            WireEvents();
            Relayout();
        }

        // ==================== 界面搭建 ====================

        private void BuildUi()
        {
            _title.Text = "🐵 猴群宠物 · 设置";
            _title.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            _title.AutoSize = true;

            _sub.Text = "想改哪里就改哪里，改完立刻生效，不用重启程序。";
            _sub.ForeColor = Color.Gray;
            _sub.AutoSize = true;

            // ---- 猴子 ----
            _gMonkey.Controls.Add(new Label { Text = "猴子数量", Location = new Point(20, 26), AutoSize = true });
            ConfigureBar(_countBar, 1, 6, 1, new Point(100, 18), new Size(250, 40));
            _gMonkey.Controls.Add(_countBar);
            _countValue = new Label { Text = "4 只", Location = new Point(356, 26), AutoSize = true };
            _gMonkey.Controls.Add(_countValue);

            // ---- 动作 ----
            _gAction.Controls.Add(new Label { Text = "移动速度", Location = new Point(20, 24), AutoSize = true });
            ConfigureBar(_speedBar, 20, 200, 20, new Point(100, 16), new Size(250, 40));
            _speedValue = new Label { Text = "100%", Location = new Point(356, 24), AutoSize = true };
            _gAction.Controls.Add(_speedValue);

            _gAction.Controls.Add(new Label { Text = "爬行幅度", Location = new Point(20, 72), AutoSize = true });
            ConfigureBar(_bobBar, 0, 200, 20, new Point(100, 64), new Size(250, 40));
            _bobValue = new Label { Text = "100%", Location = new Point(356, 72), AutoSize = true };
            _gAction.Controls.Add(_bobValue);

            _gAction.Controls.Add(new Label { Text = "群聚距离", Location = new Point(20, 120), AutoSize = true });
            ConfigureBar(_groupBar, 100, 1000, 100, new Point(100, 112), new Size(250, 40));
            _groupValue = new Label { Text = "500 px", Location = new Point(356, 120), AutoSize = true };
            _gAction.Controls.Add(_groupValue);

            // ---- 行为 ----
            _topMostCheck = new CheckBox { Text = "始终置顶（猴子一直在最上层）", Location = new Point(20, 26), AutoSize = true };
            _soundCheck = new CheckBox { Text = "启用叫声（右键点击猴子）", Location = new Point(20, 52), AutoSize = true };
            _hintCheck = new CheckBox { Text = "显示操作提示（左上角小字）", Location = new Point(20, 78), AutoSize = true };
            _gBehavior.Controls.AddRange(new Control[] { _topMostCheck, _soundCheck, _hintCheck });

            // ---- 猴子图片（动态槽位）----
            _imgHeader.Text = "每只猴子单独换图，缩略图实时预览：";
            _imgHeader.AutoSize = true;
            _cutoutBtn.Text = "🖼 抠图工具…";
            _cutoutBtn.Size = new Size(124, 32);
            _gImage.Controls.Add(_imgHeader);
            _gImage.Controls.Add(_cutoutBtn);

            // ---- 操作 ----
            var testBtn = new Button { Text = "🔊 试听叫声", Location = new Point(20, 30), Size = new Size(120, 40) };
            testBtn.Click += (s, e) => _pet.PlaySound();
            var resetBtn = new Button { Text = "♻ 恢复默认", Location = new Point(150, 30), Size = new Size(120, 40) };
            resetBtn.Click += (s, e) => ResetAll();
            var exitBtn = new Button { Text = "✖ 退出程序", Location = new Point(280, 30), Size = new Size(120, 40) };
            exitBtn.Click += (s, e) => Application.Exit();
            _gOps.Controls.AddRange(new Control[] { testBtn, resetBtn, exitBtn });

            // ---- 开源声明 ----
            _license.Text = "🔓 本程序已在 GitHub 开源（MIT 协议）：\r\n" + RepoUrl + "\r\n可以随便改、随便用、随便发给朋友～";
            _license.ForeColor = Color.Gray;
            _license.Links.Add(RepoUrl.IndexOf(RepoUrl, StringComparison.Ordinal), RepoUrl.Length, RepoUrl);

            Controls.Add(_title);
            Controls.Add(_sub);
            Controls.Add(_gMonkey);
            Controls.Add(_gAction);
            Controls.Add(_gBehavior);
            Controls.Add(_gImage);
            Controls.Add(_gOps);
            Controls.Add(_license);
        }

        private static void ConfigureBar(TrackBar bar, int min, int max, int tickFreq, Point location, Size size)
        {
            bar.Minimum = min;
            bar.Maximum = max;
            bar.TickFrequency = tickFreq;
            bar.SmallChange = Math.Max(1, (max - min) / 20);
            bar.LargeChange = Math.Max(1, (max - min) / 10);
            bar.TickStyle = TickStyle.None;
            bar.AutoSize = false;
            bar.Location = location;
            bar.Size = size;
            bar.Value = min;
        }

        // ==================== 动态布局（数量变了自动重排） ====================

        private void Relayout()
        {
            int y = 10;
            _title.Location = new Point(14, y); y += 34;
            _sub.Location = new Point(16, y); y += 26;

            _gMonkey.Location = new Point(12, y); _gMonkey.Size = new Size(436, 76); y += 84;
            _gAction.Location = new Point(12, y); _gAction.Size = new Size(436, 200); y += 208;
            _gBehavior.Location = new Point(12, y); _gBehavior.Size = new Size(436, 116); y += 124;

            RebuildImageSlots();
            int rows = (_countBar.Value + 1) / 2;
            int imgH = 50 + rows * 58 + 8;
            _gImage.Location = new Point(12, y); _gImage.Size = new Size(436, imgH); y += imgH + 8;

            _gOps.Location = new Point(12, y); _gOps.Size = new Size(436, 80); y += 88;

            _license.Location = new Point(12, y); _license.Size = new Size(436, 64); y += 72;

            int total = y + 12;
            int maxH = Math.Min((Screen.PrimaryScreen?.WorkingArea.Height ?? 760) - 40, 860);
            if (total > maxH)
            {
                AutoScroll = true;
                ClientSize = new Size(460, maxH);
            }
            else
            {
                AutoScroll = false;
                ClientSize = new Size(460, total);
            }
        }

        private void RebuildImageSlots()
        {
            foreach (var t in _thumbs)
            {
                t.Image?.Dispose();
                t.Dispose();
            }
            _thumbs.Clear();
            _gImage.Controls.Clear();

            _imgHeader.Location = new Point(16, 22);
            _gImage.Controls.Add(_imgHeader);

            _cutoutBtn.Location = new Point(300, 18);
            _gImage.Controls.Add(_cutoutBtn);

            int count = _countBar.Value;
            var sprites = _pet.Sprites;
            for (int i = 0; i < count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = 16 + col * 212;
                int yy = 58 + row * 58;

                var thumb = new PictureBox
                {
                    Location = new Point(x, yy),
                    Size = new Size(46, 46),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(240, 240, 240),
                    BorderStyle = BorderStyle.FixedSingle,
                };
                if (i < sprites.Count && sprites[i] != null)
                    thumb.Image = CloneSprite(sprites[i]);
                _thumbs.Add(thumb);
                _gImage.Controls.Add(thumb);

                int idx = i;
                var btn = new Button
                {
                    Text = (i + 1) + "号 换图",
                    Location = new Point(x + 54, yy + 6),
                    Size = new Size(146, 34),
                };
                btn.Click += (s, e) => PickImage(idx);
                _gImage.Controls.Add(btn);
            }
        }

        /// <summary>复制一份图片给缩略图用，避免主界面换图后缩略图引用到已释放的图。</summary>
        private static Image CloneSprite(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            return bmp;
        }

        // ==================== 取值 / 联动 ====================

        private void LoadValues()
        {
            _countBar.Value = Math.Clamp(_settings.MonkeyCount, _countBar.Minimum, _countBar.Maximum);
            _speedBar.Value = Math.Clamp(_settings.SpeedPercent, _speedBar.Minimum, _speedBar.Maximum);
            _bobBar.Value = Math.Clamp(_settings.BobAmount, _bobBar.Minimum, _bobBar.Maximum);
            _groupBar.Value = Math.Clamp(_settings.GroupDistance, _groupBar.Minimum, _groupBar.Maximum);
            _topMostCheck.Checked = _settings.TopMost;
            _soundCheck.Checked = _settings.SoundEnabled;
            _hintCheck.Checked = _settings.ShowHint;
            UpdateValueLabels();
        }

        private void WireEvents()
        {
            _countBar.Scroll += (s, e) => { SaveAndApply(); Relayout(); };
            _speedBar.Scroll += (s, e) => SaveAndApply();
            _bobBar.Scroll += (s, e) => SaveAndApply();
            _groupBar.Scroll += (s, e) => SaveAndApply();
            _topMostCheck.CheckedChanged += (s, e) => SaveAndApply();
            _soundCheck.CheckedChanged += (s, e) => SaveAndApply();
            _hintCheck.CheckedChanged += (s, e) => SaveAndApply();
            _cutoutBtn.Click += (s, e) => OpenCutout();
            _license.LinkClicked += (s, e) =>
            {
                try
                {
                    string url = e.Link?.LinkData?.ToString() ?? RepoUrl;
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { }
            };
        }

        private void SaveAndApply()
        {
            _settings.MonkeyCount = _countBar.Value;
            _settings.SpeedPercent = _speedBar.Value;
            _settings.BobAmount = _bobBar.Value;
            _settings.GroupDistance = _groupBar.Value;
            _settings.TopMost = _topMostCheck.Checked;
            _settings.SoundEnabled = _soundCheck.Checked;
            _settings.ShowHint = _hintCheck.Checked;

            UpdateValueLabels();
            _pet.SetMonkeyCount(_countBar.Value);
            _pet.ApplySettings();
            SettingsStore.Save(_settings);
        }

        private void UpdateValueLabels()
        {
            _countValue.Text = _countBar.Value + " 只";
            _speedValue.Text = _speedBar.Value + "%";
            _bobValue.Text = _bobBar.Value + "%";
            _groupValue.Text = _groupBar.Value + " px";
        }

        // ==================== 按钮动作 ====================

        private void OpenCutout()
        {
            using (var form = new CutoutForm(_pet))
            {
                form.ShowDialog(this);
            }
        }

        private void PickImage(int index)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择第 " + (index + 1) + " 号猴子的图片（建议透明 PNG）";
                dlg.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|PNG 图片|*.png|所有文件|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pet.SetMonkeyImage(index, dlg.FileName);
                    SettingsStore.Save(_settings);
                    Relayout(); // 刷新缩略图
                    MessageBox.Show(this,
                        "已给 " + (index + 1) + " 号猴子换上图片！",
                        "猴群宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ResetImages()
        {
            for (int i = 0; i < _countBar.Value; i++)
                _pet.SetMonkeyImage(i, null);
            SettingsStore.Save(_settings);
            Relayout();
            MessageBox.Show(this, "已恢复为默认卡通脸。", "猴群宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ResetAll()
        {
            _settings.ResetToDefaults();
            LoadValues();
            _pet.SetMonkeyCount(_settings.MonkeyCount);
            _pet.ApplySettings();
            for (int i = 0; i < _settings.MonkeyCount; i++)
                _pet.SetMonkeyImage(i, null);
            SettingsStore.Save(_settings);
            Relayout();
            MessageBox.Show(this, "已恢复全部默认设置！", "猴群宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close(); // 设置窗口里按 ESC 只关设置，不退出程序
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
