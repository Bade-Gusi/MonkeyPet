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
    /// 傻瓜式设置窗口：分三个页签（基本设置 / 猴子图片 / 关于），全中文大字，
    /// 改完立刻生效、自动保存。猴子图片槽位跟着数量走，带实时缩略图。
    /// 右上角齿轮按钮或 F1 打开。
    /// </summary>
    public sealed class SettingsForm : Form
    {
        /// <summary>本程序的开源地址（MIT 协议）。</summary>
        public const string RepoUrl = "https://github.com/Bade-Gusi/MonkeyPet";

        private readonly MainForm _pet;
        private readonly PetSettings _settings;

        private readonly TabControl _tabs = new TabControl();
        private readonly TabPage _tabMain = new TabPage("基本设置");
        private readonly TabPage _tabImages = new TabPage("猴子图片");
        private readonly TabPage _tabAbout = new TabPage("关于");

        // 基本：猴子数量
        private readonly GroupBox _gMonkey = new GroupBox { Text = "猴子" };
        private readonly TrackBar _countBar = new TrackBar();
        private Label _countValue = new Label();

        // 基本：动作
        private readonly GroupBox _gAction = new GroupBox { Text = "动作" };
        private readonly TrackBar _speedBar = new TrackBar();
        private Label _speedValue = new Label();
        private readonly TrackBar _bobBar = new TrackBar();
        private Label _bobValue = new Label();
        private readonly TrackBar _sizeBar = new TrackBar();
        private Label _sizeValue = new Label();
        private readonly TrackBar _tumbleBar = new TrackBar();
        private Label _tumbleValue = new Label();
        private readonly TrackBar _groupBar = new TrackBar();
        private Label _groupValue = new Label();

        // 基本：行为
        private readonly GroupBox _gBehavior = new GroupBox { Text = "行为" };
        private CheckBox _topMostCheck = new CheckBox();
        private CheckBox _soundCheck = new CheckBox();
        private CheckBox _hintCheck = new CheckBox();
        private CheckBox _groupCheck = new CheckBox();
        private CheckBox _obstacleCheck = new CheckBox();

        // 猴子图片
        private readonly Label _imgHeader = new Label();
        private readonly Button _cutoutBtn = new Button();
        private readonly List<PictureBox> _thumbs = new List<PictureBox>();

        // 关于
        private readonly LinkLabel _license = new LinkLabel();
        private CheckBox _apiCheck = new CheckBox();
        private LinkLabel _apiLink = new LinkLabel();

        public SettingsForm(MainForm pet, PetSettings settings)
        {
            _pet = pet;
            _settings = settings;

            Text = "猴群宠物 · 设置";
            ClientSize = new Size(470, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f);

            BuildTabs();
            LoadValues();
            WireEvents();
            RelayoutImages();
        }

        // ==================== 界面搭建 ====================

        private void BuildTabs()
        {
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(_tabMain);
            _tabs.TabPages.Add(_tabImages);
            _tabs.TabPages.Add(_tabAbout);

            // ---------- 基本设置 ----------
            _tabMain.AutoScroll = true;

            _gMonkey.Location = new Point(12, 12);
            _gMonkey.Size = new Size(430, 70);
            _gMonkey.Controls.Add(new Label { Text = "猴子数量", Location = new Point(20, 26), AutoSize = true });
            ConfigureBar(_countBar, 1, 6, 1, new Point(100, 18), new Size(232, 36));
            _gMonkey.Controls.Add(_countBar);
            _countValue = new Label { Text = "4 只", Location = new Point(348, 26), AutoSize = true };
            _gMonkey.Controls.Add(_countValue);

            _gAction.Location = new Point(12, 90);
            _gAction.Size = new Size(430, 252);
            AddSlider(_gAction, "移动速度", _speedBar, _speedValue, 14, 20, 200, 20);
            AddSlider(_gAction, "爬行幅度", _bobBar, _bobValue, 60, 0, 200, 20);
            AddSlider(_gAction, "猴子大小", _sizeBar, _sizeValue, 106, 50, 300, 25);
            AddSlider(_gAction, "打滚频率", _tumbleBar, _tumbleValue, 152, 0, 200, 20);
            AddSlider(_gAction, "群聚距离", _groupBar, _groupValue, 198, 100, 1000, 100);

            _gBehavior.Location = new Point(12, 350);
            _gBehavior.Size = new Size(430, 116);
            _topMostCheck = new CheckBox { Text = "始终置顶", Location = new Point(20, 26), AutoSize = true };
            _soundCheck = new CheckBox { Text = "启用叫声", Location = new Point(220, 26), AutoSize = true };
            _hintCheck = new CheckBox { Text = "显示操作提示", Location = new Point(20, 52), AutoSize = true };
            _groupCheck = new CheckBox { Text = "群聚行为", Location = new Point(220, 52), AutoSize = true };
            _obstacleCheck = new CheckBox { Text = "窗口障碍", Location = new Point(20, 78), AutoSize = true };
            _gBehavior.Controls.AddRange(new Control[] { _topMostCheck, _soundCheck, _hintCheck, _groupCheck, _obstacleCheck });

            _tabMain.Controls.Add(_gMonkey);
            _tabMain.Controls.Add(_gAction);
            _tabMain.Controls.Add(_gBehavior);

            // ---------- 猴子图片 ----------
            _tabImages.AutoScroll = true;
            _imgHeader.Text = "每只猴子单独换图，缩略图实时预览：";
            _imgHeader.AutoSize = true;
            _cutoutBtn.Text = "🖼 抠图工具…";
            _cutoutBtn.Size = new Size(124, 32);

            // ---------- 关于 ----------
            _tabAbout.AutoScroll = true;
            var gAbout = new GroupBox { Text = "本程序", Location = new Point(12, 12), Size = new Size(430, 320) };
            gAbout.Controls.Add(new Label
            {
                Text = "🐵 猴群宠物 · 开源桌面宠物",
                Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
                Location = new Point(20, 26),
                AutoSize = true,
            });
            gAbout.Controls.Add(new Label
            {
                Text = "MIT 开源协议 · 纯 WinForms（.NET 8）\r\n不开外挂、不碰游戏进程，放心在游戏旁边挂。",
                Location = new Point(20, 56),
                AutoSize = true,
            });

            var helpBtn = new Button { Text = "📖 新手教程", Location = new Point(20, 120), Size = new Size(190, 44) };
            helpBtn.Click += (s, e) => OpenHelp();
            var soundBtn = new Button { Text = "🔊 试听叫声", Location = new Point(220, 120), Size = new Size(190, 44) };
            soundBtn.Click += (s, e) => _pet.PlaySound();
            var resetBtn = new Button { Text = "♻ 恢复默认", Location = new Point(20, 176), Size = new Size(190, 44) };
            resetBtn.Click += (s, e) => ResetAll();
            var exitBtn = new Button { Text = "✖ 退出程序", Location = new Point(220, 176), Size = new Size(190, 44) };
            exitBtn.Click += (s, e) => _pet.RequestExit();
            gAbout.Controls.AddRange(new Control[] { helpBtn, soundBtn, resetBtn, exitBtn });

            _license.Text = "🔓 本程序已在 GitHub 开源（MIT 协议）：\r\n" + RepoUrl + "\r\n可以随便改、随便用、随便发给朋友～";
            _license.Location = new Point(20, 238);
            _license.Size = new Size(390, 70);
            _license.ForeColor = Color.Gray;
            _license.Links.Add(RepoUrl.IndexOf(RepoUrl, StringComparison.Ordinal), RepoUrl.Length, RepoUrl);
            gAbout.Controls.Add(_license);

            _tabAbout.Controls.Add(gAbout);

            // ---- 开发者 API ----
            var gApi = new GroupBox { Text = "开发者 API（本机控制接口）", Location = new Point(12, 340), Size = new Size(430, 130) };
            _apiCheck = new CheckBox { Text = "启用 API 控制（只在本机监听）", Location = new Point(20, 26), AutoSize = true };
            gApi.Controls.Add(_apiCheck);

            gApi.Controls.Add(new Label
            {
                Text = "每个猴子都是一个对象，都能用 ID 单独控制：\r\n" +
                       "GET /api/monkeys  ·  POST /api/monkeys/<id>/roar 等",
                Location = new Point(20, 54),
                AutoSize = true,
                ForeColor = Color.Gray,
            });

            _apiLink.Text = "API 未启动";
            _apiLink.Location = new Point(20, 102);
            _apiLink.AutoSize = true;
            gApi.Controls.Add(_apiLink);

            _tabAbout.Controls.Add(gApi);

            Controls.Add(_tabs);
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

        private void AddSlider(Control parent, string label, TrackBar bar, Label valueLabel, int rowY, int min, int max, int tick)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(20, rowY + 5), AutoSize = true });
            ConfigureBar(bar, min, max, tick, new Point(100, rowY), new Size(232, 36));
            parent.Controls.Add(bar);
            valueLabel.Location = new Point(348, rowY + 5);
            valueLabel.AutoSize = true;
            parent.Controls.Add(valueLabel);
        }

        // ==================== 猴子图片动态槽位 ====================

        private void RelayoutImages()
        {
            foreach (var t in _thumbs)
            {
                t.Image?.Dispose();
                t.Dispose();
            }
            _thumbs.Clear();
            _tabImages.Controls.Clear();

            _imgHeader.Location = new Point(16, 16);
            _tabImages.Controls.Add(_imgHeader);

            _cutoutBtn.Location = new Point(320, 12);
            _tabImages.Controls.Add(_cutoutBtn);

            int count = _countBar.Value;
            var sprites = _pet.Sprites;
            for (int i = 0; i < count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = 16 + col * 214;
                int yy = 56 + row * 58;

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
                _tabImages.Controls.Add(thumb);

                int idx = i;
                var btn = new Button
                {
                    Text = (i + 1) + "号 换图",
                    Location = new Point(x + 54, yy + 6),
                    Size = new Size(148, 34),
                };
                btn.Click += (s, e) => PickImage(idx);
                _tabImages.Controls.Add(btn);
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
            _sizeBar.Value = Math.Clamp(_settings.SizePercent, _sizeBar.Minimum, _sizeBar.Maximum);
            _tumbleBar.Value = Math.Clamp(_settings.TumbleRate, _tumbleBar.Minimum, _tumbleBar.Maximum);
            _groupBar.Value = Math.Clamp(_settings.GroupDistance, _groupBar.Minimum, _groupBar.Maximum);
            _topMostCheck.Checked = _settings.TopMost;
            _soundCheck.Checked = _settings.SoundEnabled;
            _hintCheck.Checked = _settings.ShowHint;
            _groupCheck.Checked = _settings.GroupingEnabled;
            _obstacleCheck.Checked = _settings.ObstaclesEnabled;
            _apiCheck.Checked = _settings.ApiEnabled;
            UpdateApiLabel();
            UpdateValueLabels();
        }

        private void WireEvents()
        {
            _countBar.Scroll += (s, e) => { SaveAndApply(); RelayoutImages(); };
            _speedBar.Scroll += (s, e) => SaveAndApply();
            _bobBar.Scroll += (s, e) => SaveAndApply();
            _sizeBar.Scroll += (s, e) => SaveAndApply();
            _tumbleBar.Scroll += (s, e) => SaveAndApply();
            _groupBar.Scroll += (s, e) => SaveAndApply();
            _topMostCheck.CheckedChanged += (s, e) => SaveAndApply();
            _soundCheck.CheckedChanged += (s, e) => SaveAndApply();
            _hintCheck.CheckedChanged += (s, e) => SaveAndApply();
            _groupCheck.CheckedChanged += (s, e) => SaveAndApply();
            _obstacleCheck.CheckedChanged += (s, e) => SaveAndApply();
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
            _apiCheck.CheckedChanged += (s, e) =>
            {
                _pet.SetApiEnabled(_apiCheck.Checked);
                UpdateApiLabel();
            };
            _apiLink.LinkClicked += (s, e) =>
            {
                try
                {
                    string url = _pet.ApiUrl;
                    if (!string.IsNullOrEmpty(url))
                        Process.Start(new ProcessStartInfo(url + "/status") { UseShellExecute = true });
                }
                catch { }
            };
        }

        private void SaveAndApply()
        {
            _settings.MonkeyCount = _countBar.Value;
            _settings.SpeedPercent = _speedBar.Value;
            _settings.BobAmount = _bobBar.Value;
            _settings.SizePercent = _sizeBar.Value;
            _settings.TumbleRate = _tumbleBar.Value;
            _settings.GroupDistance = _groupBar.Value;
            _settings.TopMost = _topMostCheck.Checked;
            _settings.SoundEnabled = _soundCheck.Checked;
            _settings.ShowHint = _hintCheck.Checked;
            _settings.GroupingEnabled = _groupCheck.Checked;
            _settings.ObstaclesEnabled = _obstacleCheck.Checked;

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
            _sizeValue.Text = _sizeBar.Value + "%";
            _tumbleValue.Text = _tumbleBar.Value <= 0 ? "关" : _tumbleBar.Value + "%";
            _groupValue.Text = _groupBar.Value + " px";
        }

        private void UpdateApiLabel()
        {
            string url = _pet.ApiUrl;
            _apiLink.Links.Clear();
            if (string.IsNullOrEmpty(url))
            {
                _apiLink.Text = "API 未启动（勾选上面的开关即可）";
                return;
            }
            _apiLink.Text = "在浏览器打开： " + url + "/status";
            int idx = _apiLink.Text.IndexOf(url, StringComparison.Ordinal);
            if (idx >= 0)
                _apiLink.Links.Add(idx, url.Length, url + "/status");
        }

        // ==================== 按钮动作 ====================

        private void OpenHelp()
        {
            using (var form = new HelpForm())
            {
                form.ShowDialog(this);
            }
        }

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
                    RelayoutImages(); // 刷新缩略图
                    MessageBox.Show(this,
                        "已给 " + (index + 1) + " 号猴子换上图片！",
                        "猴群宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
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
            RelayoutImages();
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
