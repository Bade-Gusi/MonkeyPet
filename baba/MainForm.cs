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
using System.Text.Json;
using System.Windows.Forms;

namespace baba
{
    /*
     * ================================================================
     *  使用者操作指南：
     *   1. 双击 baba.sln 用 Visual Studio 2022 打开项目，按 F5 启动。
     *   2. 右键点击物品 → 它定住 0.3 秒并大喊（播放 assets\dad.wav，没有就用系统“哔哔”两声）。
     *   3. 点右上角齿轮按钮（或按 F1）打开【设置】窗口，可以改物品数量、速度、
     *      颠簸幅度、群聚距离、置顶、声音、换图片等，改完立刻生效，不用重启。
     *   4. 按 ESC 键退出程序。
     *   5. 把 4 张透明 PNG 改名为 p1.png~p4.png 放进 assets 文件夹也可自定义物品。
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
        private ControlApiServer? _apiServer;
        private Rectangle _screenBounds;
        private Rectangle _gearRect;
        private float _lastTime;
        private float _elapsed;
        private float _groupDistance = 500f;
        private float _bobScale = 1f;
        private float _sizeScale = 1f;
        private bool _groupingEnabled = true;
        private bool _obstaclesEnabled = true;
        private bool _soundEnabled = true;
        private bool _showHint = true;
        private bool _missingImages;
        private bool _missingAudio;

        // 喊“爸爸”时弹出的模拟消息框（说话气泡，非阻塞；文字全部可自定义）
        private readonly List<SpeechBubble> _bubbles = new List<SpeechBubble>();

        // 趣味玩法状态
        private readonly List<Banana> _bananas = new List<Banana>();
        private int _bananaScore;
        private int _dragId = -1;          // 正在拖的第几只（1 起，-1 = 没拖）
        private bool _mouseDown;
        private Point _dragStart;
        private Point _lastMouse;
        private bool _suppressNextLeftClick;
        private bool _followMode;          // F5：物品跟着鼠标走
        private float _danceEndTime;       // 跳舞结束时间
        private float _idleTime;           // 用户多久没操作

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
            Text = "弹性桌面物品";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal; // 手动铺满虚拟屏（多显示器/任意分辨率）
            TopMost = true;
            BackColor = Color.Magenta;          // 透明底色
            TransparencyKey = Color.Magenta;    // 该颜色全透明，实现桌面穿透效果
            DoubleBuffered = true;              // 防闪烁
            KeyPreview = true;
            ShowInTaskbar = true;
            Cursor = Cursors.Arrow;
            StartPosition = FormStartPosition.Manual;
            ApplyScreenBounds();

            _graphicsContext = BufferedGraphicsManager.Current;
            _graphicsContext.MaximumBuffer = new Size(_screenBounds.Width + 1, _screenBounds.Height + 1);
        }

        /// <summary>把窗口铺满所有显示器的“虚拟屏幕”，并把物品上限夹回屏幕内。</summary>
        private void ApplyScreenBounds()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            if (vs.Width <= 0 || vs.Height <= 0)
                vs = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

            Location = vs.Location;
            Size = vs.Size;
            _screenBounds = vs;

            foreach (var m in _monkeys)
            {
                m.X = Math.Clamp(m.X, _screenBounds.Left + 15, _screenBounds.Right - 15);
                m.Y = Math.Clamp(m.Y, _screenBounds.Top + 15, _screenBounds.Bottom - 15);
            }

            if (_graphicsContext != null)
                _graphicsContext.MaximumBuffer = new Size(_screenBounds.Width + 1, _screenBounds.Height + 1);
        }

        private void LoadSprites()
        {
            _sprites.Clear();
            int count = Math.Clamp(_settings.MonkeyCount, 1, 6);
            for (int i = 0; i < count; i++)
                _sprites.Add(LoadOneSprite(i));
        }

        /// <summary>加载第 index 只物品的图片：自选路径 → assets\pN.png → 默认卡通脸。</summary>
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

        /// <summary>让图片池数量跟着物品数量走（加就补、减就删）。</summary>
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

        /// <summary>当前每只物品的图片（设置面板的缩略图用）。</summary>
        public IReadOnlyList<Image> Sprites => _sprites;

        /// <summary>把当前设置应用到正在运行中的状态（改完立刻生效）。</summary>
        public void ApplySettings()
        {
            TopMost = _settings.TopMost;
            _groupDistance = Math.Max(50f, _settings.GroupDistance);
            _bobScale = Math.Clamp(_settings.BobAmount, 0, 300) / 100f;
            _sizeScale = Math.Clamp(_settings.SizePercent, 20, 300) / 100f;
            _groupingEnabled = _settings.GroupingEnabled;
            _obstaclesEnabled = _settings.ObstaclesEnabled;
            _soundEnabled = _settings.SoundEnabled;
            _showHint = _settings.ShowHint;

            float factor = Math.Clamp(_settings.SpeedPercent, 20, 300) / 100f;
            foreach (var m in _monkeys)
                m.SetSpeedFactor(factor);

            Invalidate();
        }

        /// <summary>设置物品数量（1~6），立刻重建。</summary>
        public void SetMonkeyCount(int count)
        {
            count = Math.Clamp(count, 1, 6);
            if (_monkeys.Count == count) return;
            _settings.MonkeyCount = count;
            ResizeSprites(count);
            CreateMonkeys();
            ApplySettings();
        }

        /// <summary>更换某只物品的图片（index 0~3，path 传 null 表示恢复默认脸）。</summary>
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

            _idleTime += dt; // 用户多久没动鼠标/键盘
            RefreshObstacles();
            UpdateMonkeys(dt);
            Invalidate();
        }

        private void UpdateMonkeys(float dt)
        {
            // 跳舞计时结束
            if (_danceEndTime > 0f && _elapsed >= _danceEndTime)
            {
                _danceEndTime = 0f;
                foreach (var m in _monkeys) m.IsDancing = false;
            }

            // 太久没人碰 → 全体睡觉
            if (_idleTime > 45f)
            {
                foreach (var m in _monkeys)
                {
                    if (!m.IsSleeping && !m.IsDancing)
                    {
                        m.IsSleeping = true;
                        AddBubble(GetMonkeyId(m), _settings.SleepText);
                    }
                }
            }

            foreach (var m in _monkeys)
            {
                m.Tick(dt);
                if (m.IsPaused) continue;      // 右键定格中，不动
                if (m.IsHeld) continue;        // 被鼠标拖着，位置鼠标说了算
                if (m.IsSleeping) continue;    // 睡觉不动

                if (m.IsDancing)
                {
                    m.SpeedX = 0f;
                    m.SpeedY = 0f;
                    m.UpdateAngle();
                    continue;                  // 跳舞不走路
                }

                // 戳一下跳起来的重力
                if (m.AirTime > 0f)
                {
                    m.SpeedY += 1400f * dt;
                    if (m.AirTime <= 0f)
                    {
                        m.AirTime = 0f;
                        m.SpeedY = 0f; // 落地停住
                    }
                }

                // 2~7 秒随机改向
                m.UpdateDirectionTimer(dt);

                // 群聚逻辑
                ApplyGrouping(m);

                // 打滚频率由设置控制（0 = 不打滚，100 ≈ 每 7 秒一次）
                float tumbleProb = 0.0025f * Math.Clamp(_settings.TumbleRate, 0, 200) / 100f;
                if (_rng.NextDouble() < tumbleProb)
                    m.TryStartTumble();

                // 跟随鼠标 / 抢香蕉
                if (_followMode)
                {
                    Point pt = PointToClient(Cursor.Position);
                    m.SteerToward(pt.X, pt.Y);
                }
                else if (_bananas.Count > 0)
                {
                    var nb = NearestBanana(m);
                    if (nb != null) m.SteerToward(nb.X, nb.Y);
                }

                // 被扔出去后滑行摩擦
                if (m.ThrowTimer > 0f)
                {
                    m.SpeedX *= 0.985f;
                    m.SpeedY *= 0.985f;
                }

                // 卡在窗口里就先找最近的空路跑出去，别被钉死在原地
                bool stuck = IntersectsObstacle(GetCollisionBox(m, m.X, m.Y));
                if (stuck) Unstick(m);

                // 水平方向独立试探：撞上就沿法线反弹，并加一点随机扰动
                float nx = m.X + m.SpeedX * dt;
                if (stuck || !IntersectsObstacle(GetCollisionBox(m, nx, m.Y)))
                    m.X = nx;
                else
                    m.SpeedX = -m.SpeedX + (float)(_rng.NextDouble() * 20 - 10);

                float ny = m.Y + m.SpeedY * dt;
                if (stuck || !IntersectsObstacle(GetCollisionBox(m, m.X, ny)))
                    m.Y = ny;
                else
                    m.SpeedY = -m.SpeedY + (float)(_rng.NextDouble() * 60 - 30);

                m.UpdateAngle();
            }

            UpdateBananas(dt);
        }

        /// <summary>落单（最近同伴超过群聚距离）时有 20% 概率向群体中心靠拢。</summary>
        private void ApplyGrouping(MonkeyEntity m)
        {
            if (!_groupingEnabled) return;

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
            // 屏幕四边 = 禁区（永远生效，防止跑丢）
            if (box.Left < _screenBounds.Left || box.Top < _screenBounds.Top ||
                box.Right > _screenBounds.Right || box.Bottom > _screenBounds.Bottom)
                return true;

            // 关掉“窗口障碍”后就不躲窗口了
            if (!_obstaclesEnabled) return false;

            foreach (var r in _obstacles)
            {
                if (Rectangle.Inflate(r, 2, 2).IntersectsWith(box)) return true;
            }
            return false;
        }

        /// <summary>物品卡在窗口里时，向外一圈圈找最近的空路，朝那边跑。</summary>
        private void Unstick(MonkeyEntity m)
        {
            for (int radius = 30; radius <= 300; radius += 30)
            {
                for (int angle = 0; angle < 360; angle += 20)
                {
                    float rad = angle * (float)Math.PI / 180f;
                    float tx = m.X + (float)Math.Cos(rad) * radius;
                    float ty = m.Y + (float)Math.Sin(rad) * radius;
                    if (_screenBounds.Contains((int)tx, (int)ty) &&
                        !IntersectsObstacle(GetCollisionBox(m, tx, ty)))
                    {
                        float speed = 220f * Math.Max(1f, m.SpeedFactor);
                        m.SpeedX = (float)Math.Cos(rad) * speed;
                        m.SpeedY = (float)Math.Sin(rad) * speed;
                        return;
                    }
                }
            }
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

                DrawBananas(buffer.Graphics);
                DrawScore(buffer.Graphics);
                DrawBubbles(buffer.Graphics);
                DrawHint(buffer.Graphics);
                DrawGear(buffer.Graphics);

                buffer.Render(e.Graphics);
            }
        }

        private void DrawMonkey(Graphics g, MonkeyEntity m)
        {
            // 温和的移动：只有一点点上下浮动，不搞夸张效果（幅度仍受“爬行幅度”设置控制）
            float bobX = (float)Math.Sin(_elapsed * 2.0 + m.Phase) * (2f * _bobScale);
            float bobY = (float)Math.Sin(_elapsed * 3.0 + m.Phase) * (3f * _bobScale);
            float drawX = m.X + bobX;
            float drawY = m.Y + bobY;

            float scaleX = m.Scale * _sizeScale * m.ScaleBoost;
            float scaleY = m.Scale * _sizeScale * m.ScaleBoost;
            if (m.IsSleeping) scaleY *= 0.85f; // 睡觉趴下一点

            int w = (int)(m.Width * scaleX);
            int h = (int)(m.Height * scaleY);
            if (w < 4 || h < 4) return;

            // 默认保持正立；往左走时水平翻个面（脸不朝下）。打滚/跳舞时才加额外旋转
            float totalDeg = m.ExtraAngle * 180f / (float)Math.PI;
            if (m.IsSleeping) totalDeg = 0f;
            if (m.IsDancing)
            {
                drawY -= (float)Math.Abs(Math.Sin(_elapsed * 10.0 + m.Phase)) * 14f;
                totalDeg += (float)Math.Sin(_elapsed * 12.0 + m.Phase) * 8f;
            }
            bool facingLeft = m.SpeedX < 0f && !m.IsSleeping;

            GraphicsState state = g.Save();
            g.TranslateTransform(drawX, drawY);
            g.RotateTransform(totalDeg);
            if (facingLeft) g.ScaleTransform(-1f, 1f); // 水平镜像
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

            string hint = _settings.HintText;
            if (string.IsNullOrWhiteSpace(hint)) return;
            string[] lines = hint.Replace("\r\n", "\n").Split('\n');
            using (var font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold))
            using (var shadow = new SolidBrush(Color.FromArgb(alpha / 2, 0, 0, 0)))
            using (var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            {
                float x = 18f, y = 14f;
                foreach (var line in lines)
                {
                    g.DrawString(line, font, shadow, x + 2f, y + 2f);
                    g.DrawString(line, font, brush, x, y);
                    y += 26f;
                }
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

        /// <summary>画所有“爸爸”说话气泡（模拟消息框），到时间自动消失。</summary>
        private void DrawBubbles(Graphics g)
        {
            if (_screenBounds.Width <= 0) return;

            // 清理过期的气泡
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                if (_elapsed - _bubbles[i].StartTime > _bubbles[i].Duration)
                    _bubbles.RemoveAt(i);
            }

            foreach (var b in _bubbles)
            {
                var m = GetMonkeyById(b.MonkeyId);
                if (m == null) continue;

                float age = _elapsed - b.StartTime;
                float alpha = Math.Clamp((b.Duration - age) / 0.35f, 0f, 1f) * 255f;
                if (alpha <= 0f) continue;
                int a = (int)alpha;

                using (var font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(b.Text, font);
                    float bw = sz.Width + 24f;
                    float bh = sz.Height + 14f;
                    float bx = m.X;
                    float by = m.Y - m.Height * _sizeScale * 0.5f - 46f;

                    var rect = new RectangleF(bx - bw / 2f, by - bh, bw, bh);
                    rect.X = Math.Clamp(rect.X, 4f, _screenBounds.Right - rect.Width - 4f);
                    rect.Y = Math.Clamp(rect.Y, 4f, _screenBounds.Bottom - rect.Height - 4f);

                    var tail = new PointF[]
                    {
                        new PointF(bx, rect.Bottom),
                        new PointF(bx - 9f, rect.Bottom + 9f),
                        new PointF(bx + 9f, rect.Bottom + 9f),
                    };

                    using (var bg = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                    using (var border = new Pen(Color.FromArgb(a, 60, 60, 60), 2f))
                    using (var textBrush = new SolidBrush(Color.FromArgb(a, 40, 40, 40)))
                    using (var path = RoundedRect(rect, 10f))
                    {
                        g.FillPath(bg, path);
                        g.DrawPath(border, path);
                        g.FillPolygon(bg, tail);
                        g.DrawString(b.Text, font, textBrush, rect.X + 12f, rect.Y + 6f);
                    }
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawBananas(Graphics g)
        {
            if (_bananas.Count == 0) return;
            using var font = new Font("Segoe UI Emoji", 22f);
            using var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            foreach (var b in _bananas)
            {
                g.DrawString("🍌", font, shadow, b.X - 11f, b.Y - 10f);
                g.DrawString("🍌", font, Brushes.Black, b.X - 12f, b.Y - 11f);
            }
        }

        private void DrawScore(Graphics g)
        {
            if (_bananaScore <= 0) return;
            string text = "🍌 抢到 " + _bananaScore + " 个";
            using var font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            using var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            using var brush = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
            g.DrawString(text, font, shadow, 20f, 68f);
            g.DrawString(text, font, brush, 18f, 66f);
        }

        // ==================== 交互 ====================

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ResetIdle();
            if (e.Button != MouseButtons.Left) return;

            int id = HitTestMonkey(e.Location);
            if (id > 0)
            {
                _dragId = id;
                _mouseDown = true;
                _dragStart = e.Location;
                _lastMouse = e.Location;
                var m = _monkeys[id - 1];
                m.IsHeld = true;
                m.IsSleeping = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ResetIdle();
            _lastMouse = e.Location;

            if (_dragId > 0 && _mouseDown)
            {
                var m = GetMonkeyById(_dragId);
                if (m != null)
                {
                    m.X = e.Location.X;
                    m.Y = e.Location.Y;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            ResetIdle();
            if (e.Button != MouseButtons.Left || _dragId <= 0) return;

            int id = _dragId;
            var m = GetMonkeyById(id);
            _dragId = -1;
            _mouseDown = false;
            if (m == null) return;
            m.IsHeld = false;
            _suppressNextLeftClick = true; // 这次点击已处理，别让它去点齿轮

            int dist = Math.Abs(e.Location.X - _dragStart.X) + Math.Abs(e.Location.Y - _dragStart.Y);
            if (dist < 8)
            {
                // 没拖动 = 戳一下
                PokeMonkey(id);
            }
            else
            {
                // 拖动 = 扔出去（速度按最后一下鼠标移动算）
                float vx = (e.Location.X - _lastMouse.X) * 8f;
                float vy = (e.Location.Y - _lastMouse.Y) * 8f;
                m.SpeedX = Math.Clamp(vx, -1500f, 1500f);
                m.SpeedY = Math.Clamp(vy, -1500f, 1500f);
                m.ThrowTimer = 1.5f;
                AddBubble(id, _settings.TossText);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            // 左键点右上角齿轮 → 打开设置（拖物品之后这一次点击不再触发）
            if (e.Button == MouseButtons.Left && _suppressNextLeftClick)
            {
                _suppressNextLeftClick = false;
                return;
            }
            if (e.Button == MouseButtons.Left && _gearRect.Contains(e.Location))
            {
                OpenSettings();
                return;
            }

            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Right) return;

            for (int i = 0; i < _monkeys.Count; i++)
            {
                var m = _monkeys[i];
                Rectangle hitBox = GetCollisionBox(m, m.X, m.Y);
                hitBox.Inflate(6, 6); // 放宽点击判定，更容易点中
                if (hitBox.Contains(e.Location))
                {
                    RoarMonkey(i + 1);
                    break;
                }
            }
        }

        /// <summary>让某只物品喊“爸爸”：定格、放大、弹气泡、播声音。UI 线程调用。</summary>
        private void RoarMonkey(int id)
        {
            var m = GetMonkeyById(id);
            if (m == null) return;

            m.TriggerRoar(0.3f, 0.5f); // 定格 0.3 秒，吼叫放大 0.5 秒
            AddBubble(id, RandomBubbleText());
            PlaySound();
        }

        // ==================== 趣味玩法 ====================

        private void AddBubble(int id, string text)
        {
            _bubbles.Add(new SpeechBubble { MonkeyId = id, StartTime = _elapsed, Text = text });
        }

        /// <summary>从自定义的“喊爸爸的话”里随机挑一句。</summary>
        private string RandomBubbleText()
        {
            var valid = new List<string>();
            foreach (var t in _settings.BubbleTexts)
                if (!string.IsNullOrWhiteSpace(t)) valid.Add(t);
            return valid.Count == 0 ? "爸爸！" : valid[_rng.Next(valid.Count)];
        }

        /// <summary>返回鼠标点中的物品 id（1 起），没点中返回 -1。</summary>
        private int HitTestMonkey(Point p)
        {
            for (int i = 0; i < _monkeys.Count; i++)
            {
                Rectangle box = GetCollisionBox(_monkeys[i], _monkeys[i].X, _monkeys[i].Y);
                box.Inflate(6, 6);
                if (box.Contains(p)) return i + 1;
            }
            return -1;
        }

        /// <summary>戳一下：跳起来 + 气泡。</summary>
        private void PokeMonkey(int id)
        {
            var m = GetMonkeyById(id);
            if (m == null) return;
            m.AirTime = 0.6f;      // 滞空 0.6 秒
            m.SpeedY = -500f;      // 往上一蹦
            m.SpeedX *= 0.2f;      // 别滑太远
            AddBubble(id, _settings.PokeText);
        }

        private void ResetIdle()
        {
            _idleTime = 0f;
            bool anySleeping = false;
            foreach (var m in _monkeys)
                if (m.IsSleeping) { anySleeping = true; break; }
            if (anySleeping) WakeAll();
        }

        private void WakeAll()
        {
            foreach (var m in _monkeys)
            {
                if (m.IsSleeping)
                {
                    m.IsSleeping = false;
                    m.RandomizeDirection();
                }
            }
        }

        /// <summary>F4 / API：所有物品一起跳舞 8 秒。</summary>
        public void StartDance()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(StartDance));
                return;
            }
            _danceEndTime = _elapsed + 8f;
            foreach (var m in _monkeys)
            {
                m.IsSleeping = false;
                m.IsDancing = true;
            }
            if (_monkeys.Count > 0) AddBubble(1, _settings.DanceText);
        }

        /// <summary>F5 / API：切换“跟着鼠标走”。</summary>
        public bool ToggleFollow()
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(ToggleFollow));
            _followMode = !_followMode;
            foreach (var m in _monkeys)
            {
                m.IsSleeping = false;
                if (_followMode) m.RandomizeDirection();
            }
            return _followMode;
        }

        /// <summary>B / API：从屏幕顶部扔一根香蕉，物品们抢着吃。</summary>
        public void ThrowBanana()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ThrowBanana));
                return;
            }
            if (_screenBounds.Width <= 0) return;
            float x = _rng.Next(40, Math.Max(41, _screenBounds.Width - 40));
            _bananas.Add(new Banana
            {
                X = x,
                Y = -24f,
                Vx = (float)(_rng.NextDouble() * 80 - 40),
                Vy = 60f,
            });
            WakeAll();
        }

        private Banana? NearestBanana(MonkeyEntity m)
        {
            Banana? best = null;
            float bestDist = float.MaxValue;
            foreach (var b in _bananas)
            {
                float d = (b.X - m.X) * (b.X - m.X) + (b.Y - m.Y) * (b.Y - m.Y);
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        private void UpdateBananas(float dt)
        {
            for (int i = _bananas.Count - 1; i >= 0; i--)
            {
                var b = _bananas[i];
                b.Vy += 520f * dt;       // 重力下落
                b.X += b.Vx * dt;
                b.Y += b.Vy * dt;

                if (b.Y > _screenBounds.Bottom - 16f || b.Y < -300f)
                {
                    _bananas.RemoveAt(i);
                    continue;
                }

                // 谁先抢到谁吃
                bool eaten = false;
                for (int j = 0; j < _monkeys.Count && !eaten; j++)
                {
                    var m = _monkeys[j];
                    if (m.IsSleeping) continue;
                    Rectangle box = GetCollisionBox(m, m.X, m.Y);
                    if (box.Contains((int)b.X, (int)b.Y))
                    {
                        _bananas.RemoveAt(i);
                        _bananaScore++;
                        m.ScaleBoost = 1.6f;
                        AddBubble(j + 1, _settings.BananaText);
                        PlaySound();
                        eaten = true;
                    }
                }
            }
        }

        /// <summary>让所有物品一起喊“爸爸”（F3 / 设置按钮 / API 都走这里）。</summary>
        public void RoarAll()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RoarAll));
                return;
            }
            for (int i = 0; i < _monkeys.Count; i++)
                RoarMonkey(i + 1);
        }

        private bool _allowExit;

        /// <summary>设置窗口的“退出程序”按钮调用：放行后退出整个程序。</summary>
        public void RequestExit()
        {
            _allowExit = true;
            Application.Exit();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            ResetIdle(); // 按了键就算“有人操作”，不睡觉

            if (e.KeyCode == Keys.Escape)
            {
                RequestExit();
                return;
            }
            if (e.KeyCode == Keys.F1)
            {
                OpenSettings();
                return;
            }
            if (e.KeyCode == Keys.F2)
            {
                OpenHelp();
                return;
            }
            if (e.KeyCode == Keys.F3)
            {
                RoarAll(); // 所有物品一起喊爸爸
                return;
            }
            if (e.KeyCode == Keys.F4)
            {
                StartDance(); // 一起跳舞
                return;
            }
            if (e.KeyCode == Keys.F5)
            {
                ToggleFollow(); // 跟着鼠标走
                return;
            }
            if (e.KeyCode == Keys.B)
            {
                ThrowBanana(); // 扔根香蕉
                return;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// 防误关：本程序是无边框全屏宠物，正常只能按 ESC 或设置里的“退出”关闭。
        /// 如果收到外部程序误发的 WM_CLOSE（CloseReason.UserClosing 且未放行），直接拦下，
        /// 保证宠物不会莫名其妙自己关掉。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_allowExit)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        /// <summary>打开新手教程。</summary>
        private void OpenHelp()
        {
            using (var form = new HelpForm())
            {
                form.ShowDialog(this);
            }
        }

        // ==================== 本机控制 API ====================

        /// <summary>当前 API 地址（没启动就是空字符串）。</summary>
        public string ApiUrl => _apiServer != null ? "http://localhost:" + _apiServer.Port : "";

        /// <summary>启动/停用 API（设置里勾选控制）。</summary>
        public void SetApiEnabled(bool enabled)
        {
            _settings.ApiEnabled = enabled;
            SettingsStore.Save(_settings);
            if (enabled) StartControlApi();
            else StopControlApi();
        }

        /// <summary>开机自启动开关（写/删当前用户注册表 Run 键）。</summary>
        public void SetAutoStart(bool enabled)
        {
            _settings.AutoStart = enabled;
            if (enabled) AutoStartHelper.Enable();
            else AutoStartHelper.Disable();
            SettingsStore.Save(_settings);
        }

        private void StartControlApi()
        {
            StopControlApi();
            if (!_settings.ApiEnabled) return;

            int port = Math.Clamp(_settings.ApiPort, 1024, 65535);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var server = new ControlApiServer(this, port + attempt);
                    server.Start();
                    _apiServer = server;
                    return;
                }
                catch
                {
                    _apiServer = null; // 端口被占就试下一个
                }
            }
        }

        private void StopControlApi()
        {
            _apiServer?.Dispose();
            _apiServer = null;
        }

        public object ApiStatus() => new
        {
            app = "弹性桌面物品",
            version = UpdateChecker.CurrentVersion.ToString(3),
            running = true,
            monkeyCount = _monkeys.Count,
            apiUrl = ApiUrl,
        };

        private MonkeyEntity? GetMonkeyById(int id) =>
            id >= 1 && id <= _monkeys.Count ? _monkeys[id - 1] : null;

        private int GetMonkeyId(MonkeyEntity m)
        {
            for (int i = 0; i < _monkeys.Count; i++)
                if (_monkeys[i] == m) return i + 1;
            return 1;
        }

        private static MonkeyInfo ToMonkeyInfo(int id, MonkeyEntity m) => new MonkeyInfo
        {
            Id = id,
            X = m.X,
            Y = m.Y,
            Angle = m.Angle,
            Scale = m.Scale,
            Width = m.Width,
            Height = m.Height,
            Paused = m.IsPaused,
            SpeedFactor = m.SpeedFactor,
        };

        // 下面的 Api* 方法会被后台 API 线程调用，统一切回 UI 线程再改状态，避免竞态。

        public MonkeyInfo[] ApiListMonkeys()
        {
            if (InvokeRequired)
                return (MonkeyInfo[])Invoke(new Func<MonkeyInfo[]>(ApiListMonkeys));

            var list = new List<MonkeyInfo>();
            for (int i = 0; i < _monkeys.Count; i++)
                list.Add(ToMonkeyInfo(i + 1, _monkeys[i]));
            return list.ToArray();
        }

        public MonkeyInfo? ApiGetMonkey(int id)
        {
            if (InvokeRequired)
                return (MonkeyInfo?)Invoke(new Func<MonkeyInfo?>(() => ApiGetMonkey(id)));

            var m = GetMonkeyById(id);
            return m == null ? null : ToMonkeyInfo(id, m);
        }

        public bool ApiRoar(int id)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiRoar(id)));

            if (GetMonkeyById(id) == null) return false;
            RoarMonkey(id);
            return true;
        }

        /// <summary>API：所有物品一起喊“爸爸”。</summary>
        public bool ApiRoarAll()
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(ApiRoarAll));
            RoarAll();
            return true;
        }

        /// <summary>API：一起跳舞。</summary>
        public bool ApiDance()
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(ApiDance));
            StartDance();
            return true;
        }

        /// <summary>API：扔一根香蕉。</summary>
        public bool ApiThrowBanana()
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(ApiThrowBanana));
            ThrowBanana();
            return true;
        }

        /// <summary>API：切换“跟随鼠标”。</summary>
        public bool ApiToggleFollow()
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(ApiToggleFollow));
            return ToggleFollow();
        }

        /// <summary>API：戳一下某只物品。</summary>
        public bool ApiPoke(int id)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiPoke(id)));
            if (GetMonkeyById(id) == null) return false;
            PokeMonkey(id);
            return true;
        }

        /// <summary>API：把某只物品扔出去（?vx=&vy=）。</summary>
        public bool ApiToss(int id, float vx, float vy)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiToss(id, vx, vy)));
            var m = GetMonkeyById(id);
            if (m == null) return false;
            m.SpeedX = Math.Clamp(vx, -1500f, 1500f);
            m.SpeedY = Math.Clamp(vy, -1500f, 1500f);
            m.ThrowTimer = 1.5f;
            AddBubble(id, "咻——！");
            return true;
        }

        public bool ApiMove(int id, float x, float y)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiMove(id, x, y)));

            var m = GetMonkeyById(id);
            if (m == null) return false;
            m.X = Math.Clamp(x, _screenBounds.Left, _screenBounds.Right);
            m.Y = Math.Clamp(y, _screenBounds.Top, _screenBounds.Bottom);
            return true;
        }

        public bool ApiSetSpeed(int id, float percent)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiSetSpeed(id, percent)));

            var m = GetMonkeyById(id);
            if (m == null) return false;
            m.SetSpeedFactor(Math.Clamp(percent, 10, 500) / 100f);
            return true;
        }

        public bool ApiSetImage(int id, string path)
        {
            if (InvokeRequired) return (bool)Invoke(new Func<bool>(() => ApiSetImage(id, path)));

            if (id < 1 || id > _sprites.Count) return false;
            SetMonkeyImage(id - 1, path);
            return true;
        }

        public PetSettings ApiGetSettings() => _settings;

        /// <summary>支持局部更新：POST 里写了哪些字段就改哪些，没写的保持原样。</summary>
        public bool ApiApplySettings(string jsonBody)
        {
            if (InvokeRequired)
                return (bool)Invoke(new Func<bool>(() => ApiApplySettings(jsonBody)));

            try
            {
                using var doc = JsonDocument.Parse(jsonBody);
                var root = doc.RootElement;

                ApplyInt(root, "MonkeyCount", v => _settings.MonkeyCount = Math.Clamp(v, 1, 6));
                ApplyInt(root, "SpeedPercent", v => _settings.SpeedPercent = Math.Clamp(v, 20, 300));
                ApplyInt(root, "BobAmount", v => _settings.BobAmount = Math.Clamp(v, 0, 300));
                ApplyInt(root, "SizePercent", v => _settings.SizePercent = Math.Clamp(v, 20, 300));
                ApplyInt(root, "TumbleRate", v => _settings.TumbleRate = Math.Clamp(v, 0, 200));
                ApplyInt(root, "GroupDistance", v => _settings.GroupDistance = Math.Clamp(v, 50, 2000));
                ApplyInt(root, "ApiPort", v => _settings.ApiPort = Math.Clamp(v, 1024, 65535));
                ApplyBool(root, "TopMost", v => _settings.TopMost = v);
                ApplyBool(root, "SoundEnabled", v => _settings.SoundEnabled = v);
                ApplyBool(root, "ShowHint", v => _settings.ShowHint = v);
                ApplyBool(root, "GroupingEnabled", v => _settings.GroupingEnabled = v);
                ApplyBool(root, "ObstaclesEnabled", v => _settings.ObstaclesEnabled = v);
                ApplyBool(root, "ApiEnabled", v => _settings.ApiEnabled = v);

                // 自定义文字（可局部更新）
                ApplyString(root, "PokeText", v => _settings.PokeText = v);
                ApplyString(root, "TossText", v => _settings.TossText = v);
                ApplyString(root, "DanceText", v => _settings.DanceText = v);
                ApplyString(root, "BananaText", v => _settings.BananaText = v);
                ApplyString(root, "SleepText", v => _settings.SleepText = v);
                ApplyString(root, "HintText", v => _settings.HintText = v);
                if (root.TryGetProperty("BubbleTexts", out var bt) && bt.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var el in bt.EnumerateArray())
                        if (el.ValueKind == JsonValueKind.String)
                            list.Add(el.GetString() ?? "");
                    _settings.BubbleTexts = list;
                }

                SetMonkeyCount(_settings.MonkeyCount);
                ApplySettings();

                if (root.TryGetProperty("ImagePaths", out var ip) && ip.ValueKind == JsonValueKind.Array)
                {
                    for (int i = 0; i < Math.Min(ip.GetArrayLength(), _sprites.Count); i++)
                    {
                        if (ip[i].ValueKind == JsonValueKind.String)
                            SetMonkeyImage(i, ip[i].GetString());
                        else if (ip[i].ValueKind == JsonValueKind.Null)
                            SetMonkeyImage(i, null);
                    }
                }

                if (root.TryGetProperty("ApiEnabled", out _))
                {
                    if (_settings.ApiEnabled) StartControlApi();
                    else StopControlApi();
                }

                SettingsStore.Save(_settings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyInt(JsonElement root, string name, Action<int> set)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int v))
                set(v);
        }

        private static void ApplyBool(JsonElement root, string name, Action<bool> set)
        {
            if (root.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
                set(el.GetBoolean());
        }

        private static void ApplyString(JsonElement root, string name, Action<string> set)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                set(el.GetString() ?? "");
        }

        public void ApiExit()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApiExit));
                return;
            }
            RequestExit();
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

            // 窗体实际铺满的区域就是活动范围
            _screenBounds = this.Bounds;
            foreach (var m in _monkeys)
            {
                m.X = Math.Clamp(m.X, _screenBounds.Left + 15, _screenBounds.Right - 15);
                m.Y = Math.Clamp(m.Y, _screenBounds.Top + 15, _screenBounds.Bottom - 15);
            }

            // 开机自启动与设置同步（用户手动删了注册表也能自动补回来）
            if (_settings.AutoStart) AutoStartHelper.Enable();
            else AutoStartHelper.Disable();

            Activate();
            Focus();

            // 一次性中文提示（之后不再打扰）
            var messages = new List<string>();
            if (_missingImages)
                messages.Add("没找到物品的图片，已用默认卡通脸代替。\n点设置窗口『物品图片』页的【打开素材文件夹】按钮，\n把 p1~p4.png 丢进去，重启程序即可。");
            if (_missingAudio)
                messages.Add("没找到叫声音频，右键时用系统提示音代替。\n点设置窗口『物品图片』页的【打开素材文件夹】按钮，\n把 dad.wav 丢进去，重启程序即可。");
            if (messages.Count > 0)
                MessageBox.Show(string.Join("\n\n", messages), "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 第一次运行：弹出新手教程（之后按 F2 或设置里的按钮可再看）
            if (!_settings.HasSeenTutorial)
            {
                _settings.HasSeenTutorial = true;
                SettingsStore.Save(_settings);
                OpenHelp();
            }

            // 启动本机控制 API（如果设置里开着）
            StartControlApi();

            // 自动检查 GitHub 新版本（异步，不影响启动）
            if (_settings.AutoUpdateCheck)
                _ = CheckForUpdatesAsync();
        }

        /// <summary>异步检查 GitHub 最新 Release，有新版本就弹个小窗。</summary>
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                string? latest = await UpdateChecker.GetLatestVersionAsync();
                if (string.IsNullOrEmpty(latest) || !UpdateChecker.IsNewer(latest)) return;

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var f = new UpdateNotifyForm(latest, UpdateChecker.ReleasesUrl);
                        f.Show();
                    }
                    catch { }
                }));
            }
            catch
            {
                // 查不到就不打扰（离线/被墙/没发布都算）
            }
        }

        /// <summary>分辨率/显示器数量变化时，把窗口重新铺满虚拟屏并把物品夹回屏幕内。</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_screenBounds == Bounds) return;

            Rectangle vs = SystemInformation.VirtualScreen;
            _screenBounds = vs.Width > 0 ? vs : Bounds;
            foreach (var m in _monkeys)
            {
                m.X = Math.Clamp(m.X, _screenBounds.Left + 15, _screenBounds.Right - 15);
                m.Y = Math.Clamp(m.Y, _screenBounds.Top + 15, _screenBounds.Bottom - 15);
            }
            if (_graphicsContext != null)
                _graphicsContext.MaximumBuffer = new Size(_screenBounds.Width + 1, _screenBounds.Height + 1);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DISPLAYCHANGE = 0x007E;
            if (m.Msg == WM_DISPLAYCHANGE)
            {
                // 换分辨率/插拔显示器：回到 UI 线程重新铺满
                try { BeginInvoke(new Action(ApplyScreenBounds)); } catch { }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopControlApi();
            _timer?.Stop();
            _timer?.Dispose();
            _soundPlayer?.Dispose();
            foreach (var img in _sprites)
                img?.Dispose();
            base.OnFormClosed(e);
        }

        /// <summary>喊“爸爸”时的一条说话气泡。</summary>
        private sealed class SpeechBubble
        {
            public int MonkeyId;
            public float StartTime;
            public string Text = "爸爸！";
            public float Duration = 1.6f;
        }

        /// <summary>一根香蕉（B 键扔出来，物品们抢着吃）。</summary>
        private sealed class Banana
        {
            public float X, Y, Vx, Vy;
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

                // 右上角数字标识（第几只物品）
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
