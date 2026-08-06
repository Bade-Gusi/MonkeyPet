using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace baba
{
    /*
     * ================================================================
     *  使用者操作指南：
     *   1. 双击 baba.sln 用 Visual Studio 2022 打开项目，按 F5 启动。
     *   2. 右键点击猴子 → 它定住 0.3 秒并大喊（播放 assets\dad.wav，没有就用系统“哔哔”两声）。
     *   3. 点右上角齿轮按钮（或按 F1）打开【设置】窗口，可以改猴子数量、速度、
     *      颠簸幅度、群聚距离、置顶、声音、换图片等，改完立刻生效，不用重启。
     *   4. 按 ESC 键退出程序。
     *   5. 把 4 张透明 PNG 改名为 p1.png~p4.png 放进 assets 文件夹也可自定义猴子。
     * ================================================================
     */
    public class MainForm : Form
    {
        // ---------------- Win32 API ----------------
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // ---------------- 常量 ----------------
        private const string AssetsDirName = "assets";
        private static readonly string[] ImageNames = { "p1.png", "p2.png", "p3.png", "p4.png" };
        private static readonly Color[] DefaultColors =
        {
            Color.FromArgb(255, 120, 60),   // 橙
            Color.FromArgb(70, 130, 255),   // 蓝
            Color.FromArgb(80, 200, 120),   // 绿
            Color.FromArgb(255, 200, 60),   // 金
        };

        // ---------------- 字段 ----------------
        private readonly Random _rng = new Random();
        private readonly List<MonkeyEntity> _monkeys = new List<MonkeyEntity>();
        private readonly List<Rectangle> _obstacles = new List<Rectangle>();
        private readonly List<Image> _sprites = new List<Image>();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly PetSettings _settings;

        private BufferedGraphicsContext _graphicsContext = null!;
        private System.Windows.Forms.Timer _timer = null!;
        private SoundPlayer? _soundPlayer;
        private Rectangle _screenBounds;
        private Rectangle _gearRect;
        private float _lastTime;
        private float _elapsed;
        private float _groupDistance = 500f;
        private float _bobScale = 1f;
        private bool _soundEnabled = true;
        private bool _showHint = true;
        private bool _missingImages;
        private bool _missingAudio;

        public MainForm(PetSettings settings)
        {
            _settings = settings;
            InitializeWindow();
            LoadSprites();
            EnsureSoundPlayer();
            RefreshObstacles();
            CreateMonkeys();
            SetupTimer();
            ApplySettings();
        }

        // ==================== 初始化 ====================

        private void InitializeWindow()
        {
            Text = "猴群宠物";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            BackColor = Color.Magenta;          // 透明底色
            TransparencyKey = Color.Magenta;    // 该颜色全透明，实现桌面穿透效果
            DoubleBuffered = true;              // 防闪烁
            KeyPreview = true;
            ShowInTaskbar = true;
            Cursor = Cursors.Arrow;
            StartPosition = FormStartPosition.Manual;
            Rectangle primary = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            Location = primary.Location;
            Size = primary.Size;
            _screenBounds = primary;

            _graphicsContext = BufferedGraphicsManager.Current;
            _graphicsContext.MaximumBuffer = new Size(_screenBounds.Width + 1, _screenBounds.Height + 1);
        }

        private void LoadSprites()
        {
            _sprites.Clear();
            int count = Math.Clamp(_settings.MonkeyCount, 1, 6);
            for (int i = 0; i < count; i++)
                _sprites.Add(LoadOneSprite(i));
        }

        /// <summary>加载第 index 只猴子的图片：自选路径 → assets\pN.png → 默认卡通脸。</summary>
        private Image LoadOneSprite(int index)
        {
            string assetsDir = Path.Combine(Application.StartupPath, AssetsDirName);

            string? custom = _settings.GetImagePath(index);
            if (!string.IsNullOrEmpty(custom))
            {
                Image? img = TryLoadImage(custom);
                if (img != null) return img;
            }

            if (index < ImageNames.Length)
            {
                Image? img = TryLoadImage(Path.Combine(assetsDir, ImageNames[index]));
                if (img != null) return img;
            }

            _missingImages = true;
            return CreateDefaultSprite(DefaultColors[index % DefaultColors.Length], index + 1);
        }

        /// <summary>让图片池数量跟着猴子数量走（加就补、减就删）。</summary>
        private void ResizeSprites(int count)
        {
            count = Math.Clamp(count, 1, 6);
            while (_sprites.Count < count)
                _sprites.Add(LoadOneSprite(_sprites.Count));
            while (_sprites.Count > count)
            {
                Image old = _sprites[_sprites.Count - 1];
                _sprites.RemoveAt(_sprites.Count - 1);
                old.Dispose();
            }
        }

        private void EnsureSoundPlayer()
        {
            string path = Path.Combine(Application.StartupPath, AssetsDirName, "dad.wav");
            if (!File.Exists(path))
            {
                _missingAudio = true;
                return;
            }
            try
            {
                _soundPlayer = new SoundPlayer(path);
                _soundPlayer.Load();
            }
            catch
            {
                _missingAudio = true;
                _soundPlayer = null;
            }
        }

        private void CreateMonkeys()
        {
            _monkeys.Clear();
            int count = Math.Clamp(_settings.MonkeyCount, 1, 6);
            for (int i = 0; i < count; i++)
            {
                var m = new MonkeyEntity(_rng, _sprites[i % _sprites.Count], _screenBounds);
                m.SetSpeedFactor(Math.Clamp(_settings.SpeedPercent, 20, 300) / 100f);
                // 确保出生点不在障碍物里
                for (int attempt = 0; attempt < 40 && IntersectsObstacle(GetCollisionBox(m, m.X, m.Y)); attempt++)
                {
                    m.X = _screenBounds.Left + _rng.Next(0, Math.Max(1, _screenBounds.Width - 80));
                    m.Y = _screenBounds.Top + _rng.Next(0, Math.Max(1, _screenBounds.Height - 80));
                }
                _monkeys.Add(m);
            }
        }

        // ==================== 设置面板调用接口 ====================

        /// <summary>当前设置对象（抠图工具等需要读取/保存）。</summary>
        public PetSettings Settings => _settings;

        /// <summary>当前每只猴子的图片（设置面板的缩略图用）。</summary>
        public IReadOnlyList<Image> Sprites => _sprites;

        /// <summary>把当前设置应用到正在运行中的状态（改完立刻生效）。</summary>
        public void ApplySettings()
        {
            TopMost = _settings.TopMost;
            _groupDistance = Math.Max(50f, _settings.GroupDistance);
            _bobScale = Math.Clamp(_settings.BobAmount, 0, 300) / 100f;
            _soundEnabled = _settings.SoundEnabled;
            _showHint = _settings.ShowHint;

            float factor = Math.Clamp(_settings.SpeedPercent, 20, 300) / 100f;
            foreach (var m in _monkeys)
                m.SetSpeedFactor(factor);

            Invalidate();
        }

        /// <summary>设置猴子数量（1~6），立刻重建。</summary>
        public void SetMonkeyCount(int count)
        {
            count = Math.Clamp(count, 1, 6);
            if (_monkeys.Count == count) return;
            _settings.MonkeyCount = count;
            ResizeSprites(count);
            CreateMonkeys();
            ApplySettings();
        }

        /// <summary>更换某只猴子的图片（index 0~3，path 传 null 表示恢复默认脸）。</summary>
        public void SetMonkeyImage(int index, string? path)
        {
            if (index < 0 || index >= _sprites.Count) return;

            _settings.SetImagePath(index, path);
            Image? img = TryLoadImage(path);
            if (img == null)
                img = CreateDefaultSprite(DefaultColors[index % DefaultColors.Length], index + 1);

            Image old = _sprites[index];
            _sprites[index] = img;
            old?.Dispose();

            foreach (var m in _monkeys)
            {
                if (m.Sprite == old) m.Sprite = img;
            }
            Invalidate();
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(this, _settings))
            {
                form.ShowDialog(this);
            }
        }

        private void SetupTimer()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 16 }; // 约 60 帧/秒
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // ==================== 障碍物（窗口）枚举 ====================

        private void RefreshObstacles()
        {
            _obstacles.Clear();
            EnumWindows(CollectWindows, IntPtr.Zero);
        }

        private bool CollectWindows(IntPtr hWnd, IntPtr lParam)
        {
            if (!IsWindowVisible(hWnd)) return true;

            // 排除本程序自身的窗口
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return true;

            // 排除桌面图标窗口（Progman / WorkerW）
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string cls = className.ToString();
            if (cls == "Progman" || cls == "WorkerW") return true;

            // 过滤无效 / 最小化的离屏窗口
            if (!GetWindowRect(hWnd, out RECT r)) return true;
            if (r.Right <= r.Left || r.Bottom <= r.Top) return true;
            if (r.Left < -30000 || r.Top < -30000) return true;

            _obstacles.Add(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }

        // ==================== 每帧更新 ====================

        private void Timer_Tick(object? sender, EventArgs e)
        {
            float now = (float)_stopwatch.Elapsed.TotalSeconds;
            float dt = Math.Min(0.05f, now - _lastTime); // 防卡顿跳变
            _lastTime = now;
            _elapsed = now;

            RefreshObstacles();
            UpdateMonkeys(dt);
            Invalidate();
        }

        private void UpdateMonkeys(float dt)
        {
            foreach (var m in _monkeys)
            {
                m.Tick(dt);
                if (m.IsPaused) continue; // 右键定格中，不动

                // 2~7 秒随机改向
                m.UpdateDirectionTimer(dt);

                // 群聚逻辑
                ApplyGrouping(m);

                // 偶尔“打滚玩耍”（约每 7 秒一次）
                if (_rng.NextDouble() < 0.0025)
                    m.TryStartTumble();

                // 水平方向独立试探：撞上就沿法线反弹，并加一点随机扰动
                float nx = m.X + m.SpeedX * dt;
                if (!IntersectsObstacle(GetCollisionBox(m, nx, m.Y)))
                    m.X = nx;
                else
                    m.SpeedX = -m.SpeedX + (float)(_rng.NextDouble() * 20 - 10);

                float ny = m.Y + m.SpeedY * dt;
                if (!IntersectsObstacle(GetCollisionBox(m, m.X, ny)))
                    m.Y = ny;
                else
                    m.SpeedY = -m.SpeedY + (float)(_rng.NextDouble() * 60 - 30);

                m.UpdateAngle();
            }
        }

        /// <summary>落单（最近同伴超过群聚距离）时有 20% 概率向群体中心靠拢。</summary>
        private void ApplyGrouping(MonkeyEntity m)
        {
            float nearest = float.MaxValue;
            float cx = 0f, cy = 0f, count = 0f;
            foreach (var o in _monkeys)
            {
                if (o == m) continue;
                float dx = o.X - m.X, dy = o.Y - m.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < nearest) nearest = d;
                cx += o.X;
                cy += o.Y;
                count++;
            }
            if (count == 0f) return;

            if (nearest > _groupDistance && _rng.NextDouble() < 0.20)
            {
                m.SteerToward(cx / count, cy / count);
            }
        }

        // ==================== 碰撞判定 ====================

        /// <summary>碰撞箱：图片宽高各缩小 40%，即 60% 尺寸，防止视觉擦边卡顿。</summary>
        private static Rectangle GetCollisionBox(MonkeyEntity m, float x, float y)
        {
            int w = Math.Max(8, (int)(m.Width * 0.6f));
            int h = Math.Max(8, (int)(m.Height * 0.6f));
            return new Rectangle((int)(x - w / 2f), (int)(y - h / 2f), w, h);
        }

        private bool IntersectsObstacle(Rectangle box)
        {
            // 屏幕四边 = 禁区
            if (box.Left < _screenBounds.Left || box.Top < _screenBounds.Top ||
                box.Right > _screenBounds.Right || box.Bottom > _screenBounds.Bottom)
                return true;

            foreach (var r in _obstacles)
            {
                if (Rectangle.Inflate(r, 2, 2).IntersectsWith(box)) return true;
            }
            return false;
        }

        // ==================== 绘制 ====================

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_graphicsContext == null) return;

            using (BufferedGraphics buffer = _graphicsContext.Allocate(e.Graphics, DisplayRectangle))
            {
                buffer.Graphics.Clear(Color.Magenta);
                buffer.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var m in _monkeys)
                    DrawMonkey(buffer.Graphics, m);

                DrawHint(buffer.Graphics);
                DrawGear(buffer.Graphics);

                buffer.Render(e.Graphics);
            }
        }

        private void DrawMonkey(Graphics g, MonkeyEntity m)
        {
            // 爬行动画：上下颠簸 + 左右摇摆（纯正弦波叠加，无需骨骼，幅度可调）
            float bobX = (float)Math.Sin(_elapsed * 6.0 + m.Phase) * (3f * _bobScale);
            float bobY = (float)Math.Sin(_elapsed * 8.0 + m.Phase) * (5f * _bobScale);
            float drawX = m.X + bobX;
            float drawY = m.Y + bobY;

            // 挤压拉伸：爬行时身体一鼓一鼓，像猴子用四肢爬
            float squash = (float)Math.Sin(_elapsed * 8.0 + m.Phase) * 0.07f;
            float scaleX = m.Scale * (1f + squash);
            float scaleY = m.Scale * (1f - squash);

            int w = (int)(m.Width * scaleX);
            int h = (int)(m.Height * scaleY);
            if (w < 4 || h < 4) return;

            // 朝向角 + 左右摇晃 + 偶尔打滚（ExtraAngle 转一圈）
            float rock = (float)Math.Sin(_elapsed * 5.0 + m.Phase) * 6f;
            float totalDeg = (m.Angle * 180f / (float)Math.PI) + rock + (m.ExtraAngle * 180f / (float)Math.PI);

            GraphicsState state = g.Save();
            g.TranslateTransform(drawX, drawY);
            g.RotateTransform(totalDeg);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(m.Sprite, -w / 2f, -h / 2f, w, h);
            g.Restore(state);
        }

        /// <summary>左上角操作提示，约 12 秒后淡出（可在设置里关掉）。</summary>
        private void DrawHint(Graphics g)
        {
            if (!_showHint) return;

            const float lifetime = 12f;
            const float fade = 2.5f;
            if (_elapsed > lifetime) return;

            int alpha = _elapsed > lifetime - fade
                ? (int)(255 * (lifetime - _elapsed) / fade)
                : 255;
            if (alpha <= 0) return;

            string text = "右键点猴子喊爸爸 ｜ 右上角齿轮或 F1 打开设置 ｜ ESC 退出";
            using (var font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(text, font);
                float x = 18f, y = 14f;
                using (var shadow = new SolidBrush(Color.FromArgb(alpha / 2, 0, 0, 0)))
                    g.DrawString(text, font, shadow, x + 2f, y + 2f);
                using (var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                    g.DrawString(text, font, brush, x, y);
            }
        }

        /// <summary>右上角的“设置”齿轮按钮（用 GDI+ 矢量绘制，保证任何字体都能显示）。</summary>
        private void DrawGear(Graphics g)
        {
            const int size = 48;
            const int margin = 12;
            _gearRect = new Rectangle(DisplayRectangle.Right - size - margin, margin, size, size);

            // 半透明背景圆
            using (var bg = new SolidBrush(Color.FromArgb(120, 20, 20, 20)))
                g.FillEllipse(bg, _gearRect);
            using (var border = new Pen(Color.FromArgb(210, 255, 255, 255), 1.5f))
                g.DrawEllipse(border, _gearRect);

            // 齿轮：8 齿多边形
            float cx = _gearRect.Left + _gearRect.Width / 2f;
            float cy = _gearRect.Top + _gearRect.Height / 2f;
            const float outerR = 15f;
            const float innerR = 10.5f;
            const int teeth = 8;
            var points = new PointF[teeth * 2];
            for (int i = 0; i < teeth * 2; i++)
            {
                double a = Math.PI / teeth * i;
                float r = (i % 2 == 0) ? outerR : innerR;
                points[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
            }

            using (var path = new GraphicsPath())
            using (var gearBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            {
                path.AddPolygon(points);
                g.FillPath(gearBrush, path);
            }

            // 中心孔
            using (var hole = new SolidBrush(Color.FromArgb(130, 20, 20, 20)))
                g.FillEllipse(hole, cx - 3.5f, cy - 3.5f, 7f, 7f);
        }

        // ==================== 交互 ====================

        protected override void OnMouseClick(MouseEventArgs e)
        {
            // 左键点右上角齿轮 → 打开设置
            if (e.Button == MouseButtons.Left && _gearRect.Contains(e.Location))
            {
                OpenSettings();
                return;
            }

            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Right) return;

            foreach (var m in _monkeys)
            {
                Rectangle hitBox = GetCollisionBox(m, m.X, m.Y);
                hitBox.Inflate(6, 6); // 放宽点击判定，更容易点中
                if (hitBox.Contains(e.Location))
                {
                    m.TriggerRoar(0.3f, 0.5f); // 定格 0.3 秒，吼叫放大 0.5 秒
                    PlaySound();
                    break;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Application.Exit();
                return;
            }
            if (e.KeyCode == Keys.F1)
            {
                OpenSettings();
                return;
            }
            base.OnKeyDown(e);
        }

        /// <summary>播放叫声（设置里关掉声音就不播）。</summary>
        internal void PlaySound()
        {
            if (!_soundEnabled) return;

            if (_soundPlayer != null)
            {
                try
                {
                    _soundPlayer.Play();
                    return;
                }
                catch
                {
                    // 播放失败走兜底
                }
            }
            // 没有 dad.wav：系统“哔哔”两声代替
            SystemSounds.Beep.Play();
            SystemSounds.Beep.Play();
        }

        // ==================== 窗体生命周期 ====================

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 以窗体实际占据的区域为准（最大化后可能不含任务栏）
            Rectangle primary = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            _screenBounds = Rectangle.Intersect(primary, this.Bounds);
            foreach (var m in _monkeys)
            {
                m.X = Math.Clamp(m.X, _screenBounds.Left + 15, _screenBounds.Right - 15);
                m.Y = Math.Clamp(m.Y, _screenBounds.Top + 15, _screenBounds.Bottom - 15);
            }

            Activate();
            Focus();

            // 一次性中文提示（之后不再打扰）
            var messages = new List<string>();
            if (_missingImages)
                messages.Add("没找到 assets 里的 p1.png~p4.png，已用默认卡通脸代替。\n把 4 张透明 PNG 改名后放进 assets 文件夹，重新 F5 即可。");
            if (_missingAudio)
                messages.Add("没找到 assets 里的 dad.wav，右键时用系统提示音代替。\n把 WAV 音频改名后放进 assets 文件夹，重新 F5 即可。");
            if (messages.Count > 0)
                MessageBox.Show(string.Join("\n\n", messages), "猴群宠物", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _soundPlayer?.Dispose();
            foreach (var img in _sprites)
                img?.Dispose();
            base.OnFormClosed(e);
        }

        // ==================== 默认图片生成 ====================

        /// <summary>没有 p1~p4.png 时，生成“圆形身体 + 滑稽表情”的默认卡通脸。</summary>
        private static Image CreateDefaultSprite(Color color, int index)
        {
            const int size = 96;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // 耳朵（两个小圆）
                using (var earBrush = new SolidBrush(color))
                {
                    g.FillEllipse(earBrush, 0, 18, 24, 24);
                    g.FillEllipse(earBrush, size - 24, 18, 24, 24);
                }
                using (var earPen = new Pen(Color.FromArgb(50, 50, 50), 2f))
                {
                    g.DrawEllipse(earPen, 0, 18, 24, 24);
                    g.DrawEllipse(earPen, size - 24, 18, 24, 24);
                }

                // 圆形身体
                using (var bodyBrush = new SolidBrush(color))
                    g.FillEllipse(bodyBrush, 6, 6, size - 12, size - 12);
                using (var bodyPen = new Pen(Color.FromArgb(50, 50, 50), 3f))
                    g.DrawEllipse(bodyPen, 6, 6, size - 12, size - 12);

                // 眼睛
                using (var eye = new SolidBrush(Color.FromArgb(40, 40, 40)))
                {
                    g.FillEllipse(eye, 30, 32, 11, 16);
                    g.FillEllipse(eye, 55, 32, 11, 16);
                }
                using (var highlight = new SolidBrush(Color.White))
                {
                    g.FillEllipse(highlight, 32, 34, 4, 6);
                    g.FillEllipse(highlight, 57, 34, 4, 6);
                }

                // 微笑嘴巴
                using (var mouth = new Pen(Color.FromArgb(40, 40, 40), 3f))
                {
                    g.DrawArc(mouth, 28, 50, 40, 24, 20, 140);
                }

                // 右上角数字标识（第几只猴子）
                using (var font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(60, 60, 60)))
                {
                    g.DrawString(index.ToString(), font, textBrush, size - 30f, size - 30f);
                }
            }
            return bmp;
        }

        /// <summary>安全加载图片（复制像素，避免 GDI+ 懒加载导致文件被占用）。</summary>
        private static Image? TryLoadImage(string? path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var original = Image.FromStream(fs);
                return new Bitmap(original);
            }
            catch
            {
                return null;
            }
        }
    }
}
