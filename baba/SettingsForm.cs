using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace baba
{
    /// <summary>
    /// 傻瓜式设置窗口：分三个页签（基本设置 / 物品图片 / 关于），全中文大字，
    /// 改完立刻生效、自动保存。物品图片槽位跟着数量走，带实时缩略图。
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
        private readonly TabPage _tabImages = new TabPage("物品图片");
        private readonly TabPage _tabText = new TabPage("自定义文字");
        private readonly TabPage _tabPhysics = new TabPage("碰撞物理");
        private readonly TabPage _tabAbout = new TabPage("关于");

        // 碰撞物理
        private readonly TrackBar _collisionBar = new TrackBar();
        private Label _collisionValue = new Label();
        private CheckBox _itemCollisionCheck = new CheckBox();
        private readonly TrackBar _bounceBar = new TrackBar();
        private Label _bounceValue = new Label();

        // 自定义文字
        private readonly TextBox _roarTextsBox = new TextBox { Multiline = true };
        private readonly TextBox _pokeBox = new TextBox();
        private readonly TextBox _tossBox = new TextBox();
        private readonly TextBox _danceBox = new TextBox();
        private readonly TextBox _bananaBox = new TextBox();
        private readonly TextBox _sleepBox = new TextBox();
        private readonly TextBox _hintBox = new TextBox { Multiline = true };

        // 基本：物品数量
        private readonly GroupBox _gMonkey = new GroupBox { Text = "物品" };
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
        private CheckBox _autoStartCheck = new CheckBox();
        private CheckBox _autoUpdateCheck = new CheckBox();

        // 物品图片
        private readonly Label _imgHeader = new Label();
        private readonly Button _cutoutBtn = new Button();
        private readonly Button _openAssetsBtn = new Button();
        private readonly List<PictureBox> _thumbs = new List<PictureBox>();

        // 关于
        private readonly LinkLabel _license = new LinkLabel();
        private CheckBox _apiCheck = new CheckBox();
        private LinkLabel _apiLink = new LinkLabel();
        private Button _followBtn = new Button();

        public SettingsForm(MainForm pet, PetSettings settings)
        {
            _pet = pet;
            _settings = settings;

            Text = "弹性桌面物品 · 设置";
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
            _tabs.TabPages.Add(_tabText);
            _tabs.TabPages.Add(_tabPhysics);
            _tabs.TabPages.Add(_tabAbout);

            // ---------- 基本设置 ----------
            _tabMain.AutoScroll = true;

            _gMonkey.Location = new Point(12, 12);
            _gMonkey.Size = new Size(430, 70);
            _gMonkey.Controls.Add(new Label { Text = "物品数量", Location = new Point(20, 26), AutoSize = true });
            ConfigureBar(_countBar, 1, 6, 1, new Point(100, 18), new Size(232, 36));
            _gMonkey.Controls.Add(_countBar);
            _countValue = new Label { Text = "4 只", Location = new Point(348, 26), AutoSize = true };
            _gMonkey.Controls.Add(_countValue);

            _gAction.Location = new Point(12, 90);
            _gAction.Size = new Size(430, 252);
            AddSlider(_gAction, "移动速度", _speedBar, _speedValue, 14, 20, 200, 20);
            AddSlider(_gAction, "爬行幅度", _bobBar, _bobValue, 60, 0, 200, 20);
            AddSlider(_gAction, "物品大小", _sizeBar, _sizeValue, 106, 50, 300, 25);
            AddSlider(_gAction, "打滚频率", _tumbleBar, _tumbleValue, 152, 0, 200, 20);
            AddSlider(_gAction, "群聚距离", _groupBar, _groupValue, 198, 100, 1000, 100);

            _gBehavior.Location = new Point(12, 350);
            _gBehavior.Size = new Size(430, 132);
            _topMostCheck = new CheckBox { Text = "始终置顶", Location = new Point(20, 26), AutoSize = true };
            _soundCheck = new CheckBox { Text = "启用叫声", Location = new Point(220, 26), AutoSize = true };
            _hintCheck = new CheckBox { Text = "显示操作提示", Location = new Point(20, 52), AutoSize = true };
            _groupCheck = new CheckBox { Text = "群聚行为", Location = new Point(220, 52), AutoSize = true };
            _obstacleCheck = new CheckBox { Text = "窗口障碍", Location = new Point(20, 78), AutoSize = true };
            _autoStartCheck = new CheckBox { Text = "开机自启动", Location = new Point(220, 78), AutoSize = true };
            _autoUpdateCheck = new CheckBox { Text = "自动检查更新", Location = new Point(20, 104), AutoSize = true };
            _gBehavior.Controls.AddRange(new Control[] { _topMostCheck, _soundCheck, _hintCheck, _groupCheck, _obstacleCheck, _autoStartCheck, _autoUpdateCheck });

            _tabMain.Controls.Add(_gMonkey);
            _tabMain.Controls.Add(_gAction);
            _tabMain.Controls.Add(_gBehavior);

            // ---------- 物品图片 ----------
            _tabImages.AutoScroll = true;
            _imgHeader.Text = "每只物品单独换图，缩略图实时预览：";
            _imgHeader.AutoSize = true;
            _cutoutBtn.Text = "🖼 抠图工具…";
            _cutoutBtn.Size = new Size(124, 32);
            _openAssetsBtn.Text = "📂 打开素材文件夹（把 p1~p4.png 和 dad.wav 丢进去就行）";
            _openAssetsBtn.Size = new Size(428, 36);

            // ---------- 自定义文字 ----------
            _tabText.AutoScroll = true;
            var gText = new GroupBox { Text = "所有文字都能改，改完立刻生效", Location = new Point(12, 12), Size = new Size(430, 520) };
            int ty = 26;
            AddTextField(gText, "喊爸爸时弹的话（一行一句，随机挑一句）：", _roarTextsBox, 72, ref ty);
            AddTextField(gText, "被戳一下时：", _pokeBox, 26, ref ty);
            AddTextField(gText, "被扔出去时：", _tossBox, 26, ref ty);
            AddTextField(gText, "跳舞时：", _danceBox, 26, ref ty);
            AddTextField(gText, "抢到香蕉时：", _bananaBox, 26, ref ty);
            AddTextField(gText, "睡觉时：", _sleepBox, 26, ref ty);
            AddTextField(gText, "左上角操作提示（可换行）：", _hintBox, 64, ref ty);
            _tabText.Controls.Add(gText);

            // ---------- 碰撞物理 ----------
            _tabPhysics.AutoScroll = true;
            var gPhysics = new GroupBox { Text = "碰撞 & 物理", Location = new Point(12, 12), Size = new Size(430, 200) };
            AddSlider(gPhysics, "碰撞体积", _collisionBar, _collisionValue, 14, 40, 120, 10);
            _itemCollisionCheck = new CheckBox { Text = "物品之间会互相碰撞（弹开）", Location = new Point(20, 62), AutoSize = true };
            gPhysics.Controls.Add(_itemCollisionCheck);
            AddSlider(gPhysics, "弹性/弹力", _bounceBar, _bounceValue, 92, 0, 100, 10);
            gPhysics.Controls.Add(new Label
            {
                Text = "碰撞体积越大越容易撞到别人/窗口；弹力越大，\r\n撞上之后弹得越开（0 = 撞上就停）。",
                Location = new Point(20, 150),
                AutoSize = true,
                ForeColor = Color.Gray,
            });
            _tabPhysics.Controls.Add(gPhysics);

            // ---------- 关于 ----------
            _tabAbout.AutoScroll = true;
            var gAbout = new GroupBox { Text = "本程序", Location = new Point(12, 12), Size = new Size(430, 360) };
            gAbout.Controls.Add(new Label
            {
                Text = "🧸 弹性桌面物品 · 开源桌面小物",
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
            var togetherBtn = new Button { Text = "🗣 一起喊爸爸", Location = new Point(20, 176), Size = new Size(190, 44) };
            togetherBtn.Click += (s, e) => _pet.RoarAll();
            var resetBtn = new Button { Text = "♻ 恢复默认", Location = new Point(220, 176), Size = new Size(190, 44) };
            resetBtn.Click += (s, e) => ResetAll();
            var updateBtn = new Button { Text = "🔍 检查更新", Location = new Point(220, 232), Size = new Size(190, 40) };
            updateBtn.Click += async (s, e) => await CheckForUpdatesAsync(updateBtn);
            var exitBtn = new Button { Text = "✖ 退出程序", Location = new Point(20, 232), Size = new Size(190, 40) };
            exitBtn.Click += (s, e) => _pet.RequestExit();
            gAbout.Controls.AddRange(new Control[] { helpBtn, soundBtn, togetherBtn, resetBtn, updateBtn, exitBtn });

            _license.Text = "🔓 本程序已在 GitHub 开源（MIT 协议）：\r\n" + RepoUrl + "\r\n可以随便改、随便用、随便发给朋友～";
            _license.Location = new Point(20, 284);
            _license.Size = new Size(390, 70);
            _license.ForeColor = Color.Gray;
            _license.Links.Add(RepoUrl.IndexOf(RepoUrl, StringComparison.Ordinal), RepoUrl.Length, RepoUrl);
            gAbout.Controls.Add(_license);

            _tabAbout.Controls.Add(gAbout);

            // ---- 开发者 API ----
            var gApi = new GroupBox { Text = "开发者 API（本机控制接口）", Location = new Point(12, 380), Size = new Size(430, 130) };
            _apiCheck = new CheckBox { Text = "启用 API 控制（只在本机监听）", Location = new Point(20, 26), AutoSize = true };
            gApi.Controls.Add(_apiCheck);

            gApi.Controls.Add(new Label
            {
                Text = "每个物品都是一个对象，都能用 ID 单独控制：\r\n" +
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

            // ---- 玩法 ----
            var gPlay = new GroupBox { Text = "玩法（也可以按快捷键）", Location = new Point(12, 518), Size = new Size(430, 120) };
            var danceBtn = new Button { Text = "💃 一起跳舞（F4）", Location = new Point(20, 30), Size = new Size(190, 40) };
            danceBtn.Click += (s, e) => _pet.ApiDance();
            var bananaBtn = new Button { Text = "🍌 扔根香蕉（B）", Location = new Point(220, 30), Size = new Size(190, 40) };
            bananaBtn.Click += (s, e) => _pet.ApiThrowBanana();
            _followBtn = new Button { Text = "🐒 跟随鼠标（F5）", Location = new Point(20, 76), Size = new Size(190, 40) };
            _followBtn.Click += (s, e) => UpdateFollowBtn();
            gPlay.Controls.AddRange(new Control[] { danceBtn, bananaBtn, _followBtn });
            gPlay.Controls.Add(new Label
            {
                Text = "在桌面上：左键戳一下 / 拖起来扔，也很好玩～",
                Location = new Point(220, 84),
                AutoSize = true,
                ForeColor = Color.Gray,
            });
            _tabAbout.Controls.Add(gPlay);

            Controls.Add(_tabs);
        }

        private static void ConfigureBar(TrackBar bar, int min, int max, int tickFreq, Point location, Size size)
        {
            bar.Minimum = min;
            bar.Maximum = max;
            bar.TickFrequency = tickFreq;
            bar.SmallChange = Math.Max(1, (max - min) / 20);
            bar.LargeChange = Math.Max(1, (max - min) / 10);
            bar.TickStyle = TickStyle.BottomRight; // 带刻度，看着就像能拖
            bar.AutoSize = true;                   // 自然高度，滑块拇指看得见、好抓
            bar.Location = location;
            bar.Width = size.Width;
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

        private void AddTextField(Control parent, string label, TextBox box, int height, ref int y)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(16, y), AutoSize = true });
            y += 22;
            box.Location = new Point(16, y);
            box.Size = new Size(398, height);
            if (box.Multiline) box.ScrollBars = ScrollBars.Vertical;
            parent.Controls.Add(box);
            y += height + 14;
        }

        // ==================== 物品图片动态槽位 ====================

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

            _openAssetsBtn.Location = new Point(16, 50);
            _tabImages.Controls.Add(_openAssetsBtn);

            int count = _countBar.Value;
            var sprites = _pet.Sprites;
            for (int i = 0; i < count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = 16 + col * 214;
                int yy = 96 + row * 58;

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
            _autoStartCheck.Checked = _settings.AutoStart;
            _autoUpdateCheck.Checked = _settings.AutoUpdateCheck;
            _collisionBar.Value = Math.Clamp(_settings.CollisionSizePercent, _collisionBar.Minimum, _collisionBar.Maximum);
            _bounceBar.Value = Math.Clamp(_settings.BounceElasticity, _bounceBar.Minimum, _bounceBar.Maximum);
            _itemCollisionCheck.Checked = _settings.ItemCollisionEnabled;
            _apiCheck.Checked = _settings.ApiEnabled;
            _roarTextsBox.Lines = _settings.BubbleTexts.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            _pokeBox.Text = _settings.PokeText;
            _tossBox.Text = _settings.TossText;
            _danceBox.Text = _settings.DanceText;
            _bananaBox.Text = _settings.BananaText;
            _sleepBox.Text = _settings.SleepText;
            _hintBox.Text = _settings.HintText;
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
            _autoStartCheck.CheckedChanged += (s, e) => _pet.SetAutoStart(_autoStartCheck.Checked);
            _autoUpdateCheck.CheckedChanged += (s, e) => SaveAndApply();
            _collisionBar.Scroll += (s, e) => SaveAndApply();
            _bounceBar.Scroll += (s, e) => SaveAndApply();
            _itemCollisionCheck.CheckedChanged += (s, e) => SaveAndApply();
            _roarTextsBox.TextChanged += (s, e) => SaveAndApply();
            _pokeBox.TextChanged += (s, e) => SaveAndApply();
            _tossBox.TextChanged += (s, e) => SaveAndApply();
            _danceBox.TextChanged += (s, e) => SaveAndApply();
            _bananaBox.TextChanged += (s, e) => SaveAndApply();
            _sleepBox.TextChanged += (s, e) => SaveAndApply();
            _hintBox.TextChanged += (s, e) => SaveAndApply();
            _cutoutBtn.Click += (s, e) => OpenCutout();
            _openAssetsBtn.Click += (s, e) => OpenAssetsFolder();
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

        private int _lastSaveTick;

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
            _settings.AutoUpdateCheck = _autoUpdateCheck.Checked;
            _settings.CollisionSizePercent = _collisionBar.Value;
            _settings.BounceElasticity = _bounceBar.Value;
            _settings.ItemCollisionEnabled = _itemCollisionCheck.Checked;
            _settings.BubbleTexts = _roarTextsBox.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            _settings.PokeText = _pokeBox.Text;
            _settings.TossText = _tossBox.Text;
            _settings.DanceText = _danceBox.Text;
            _settings.BananaText = _bananaBox.Text;
            _settings.SleepText = _sleepBox.Text;
            _settings.HintText = _hintBox.Text;

            UpdateValueLabels();
            _pet.SetMonkeyCount(_countBar.Value);
            _pet.ApplySettings();

            // 拖滑块时别每帧都写盘，防止卡顿；关窗时再存一次
            int now = Environment.TickCount;
            if (now - _lastSaveTick > 400)
            {
                _lastSaveTick = now;
                SettingsStore.Save(_settings);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SettingsStore.Save(_settings); // 关设置窗口时确保全部保存
            base.OnFormClosed(e);
        }

        private void UpdateValueLabels()
        {
            _countValue.Text = _countBar.Value + " 只";
            _speedValue.Text = _speedBar.Value + "%";
            _bobValue.Text = _bobBar.Value + "%";
            _sizeValue.Text = _sizeBar.Value + "%";
            _tumbleValue.Text = _tumbleBar.Value <= 0 ? "关" : _tumbleBar.Value + "%";
            _groupValue.Text = _groupBar.Value + " px";
            _collisionValue.Text = _collisionBar.Value + "%";
            _bounceValue.Text = _bounceBar.Value + "%";
        }

        private void UpdateFollowBtn()
        {
            bool on = _pet.ApiToggleFollow();
            _followBtn.Text = on ? "🐒 跟随鼠标：开（再点关）" : "🐒 跟随鼠标（F5）";
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync(Button btn)
        {
            btn.Enabled = false;
            btn.Text = "检查中…";
            try
            {
                string? latest = await UpdateChecker.GetLatestVersionAsync();
                if (string.IsNullOrEmpty(latest))
                {
                    MessageBox.Show(this, "暂时查不到更新，可能是网络问题或还没发布新版本。",
                        "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (UpdateChecker.IsNewer(latest))
                {
                    new UpdateNotifyForm(latest, UpdateChecker.ReleasesUrl).Show();
                }
                else
                {
                    MessageBox.Show(this, "已经是最新版 v" + UpdateChecker.CurrentVersion.ToString(3) + "！",
                        "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "检查更新失败：" + ex.Message,
                    "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btn.Enabled = true;
                btn.Text = "🔍 检查更新";
            }
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

        /// <summary>一键弹出素材文件夹，用户把 p1~p4.png / dad.wav 丢进去就行。</summary>
        private void OpenAssetsFolder()
        {
            try
            {
                string dir = Path.Combine(Application.StartupPath, "assets");
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打不开素材文件夹：\n" + ex.Message, "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                dlg.Title = "选择第 " + (index + 1) + " 号物品的图片（建议透明 PNG）";
                dlg.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|PNG 图片|*.png|所有文件|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pet.SetMonkeyImage(index, dlg.FileName);
                    SettingsStore.Save(_settings);
                    RelayoutImages(); // 刷新缩略图
                    MessageBox.Show(this,
                        "已给 " + (index + 1) + " 号物品换上图片！",
                        "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ResetAll()
        {
            _settings.ResetToDefaults();
            LoadValues();
            _pet.SetMonkeyCount(_settings.MonkeyCount);
            _pet.ApplySettings();
            _pet.SetAutoStart(_settings.AutoStart); // 默认关 → 同时清掉注册表自启动
            for (int i = 0; i < _settings.MonkeyCount; i++)
                _pet.SetMonkeyImage(i, null);
            SettingsStore.Save(_settings);
            RelayoutImages();
            MessageBox.Show(this, "已恢复全部默认设置！", "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
