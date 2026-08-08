using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace baba
{
    /// <summary>
    /// 傻瓜式抠图工具：打开一张合影/人物照片，点一下背景色，拖一下容差，
    /// 背景就变透明了，人物留下来。然后“保存为 1号/2号/3号/4号 物品”即可。
    /// 原理是纯图片像素处理（颜色抠像），完全不碰任何游戏进程。
    /// </summary>
    public sealed class CutoutForm : Form
    {
        private readonly MainForm _pet;

        private Bitmap? _source;      // 原图（已缩小到合适尺寸）
        private Bitmap? _cutout;      // 抠好的透明图
        private Color _keyColor;      // 选中的背景色
        private bool _hasColor;
        private bool _picking;

        private readonly PictureBox _pictureBox = new PictureBox();
        private readonly Button _btnOpen = new Button();
        private readonly Button _btnPick = new Button();
        private readonly Label _lblColor = new Label();
        private readonly TrackBar _tolBar = new TrackBar();
        private readonly TrackBar _featherBar = new TrackBar();
        private readonly CheckBox _previewCheck = new CheckBox { Text = "实时预览", Checked = true };

        public CutoutForm(MainForm pet)
        {
            _pet = pet;

            Text = "弹性桌面物品 · 抠图工具";
            ClientSize = new Size(740, 585);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f);

            BuildUi();
            WireEvents();
        }

        private void BuildUi()
        {
            // 图片预览区
            _pictureBox.Location = new Point(12, 12);
            _pictureBox.Size = new Size(460, 470);
            _pictureBox.BackColor = Color.FromArgb(235, 235, 235);
            _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            _pictureBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_pictureBox);

            int x = 488;

            _btnOpen.Text = "🖼 打开图片";
            _btnOpen.Location = new Point(x, 12);
            _btnOpen.Size = new Size(220, 36);
            Controls.Add(_btnOpen);

            _btnPick.Text = "🎯 选背景色";
            _btnPick.Location = new Point(x, 54);
            _btnPick.Size = new Size(220, 36);
            Controls.Add(_btnPick);

            _lblColor.Text = "背景色：未选择";
            _lblColor.Location = new Point(x, 94);
            _lblColor.Size = new Size(220, 22);
            _lblColor.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_lblColor);

            var lblTol = new Label { Text = "容差（越大去得越多）", Location = new Point(x, 120), AutoSize = true };
            Controls.Add(lblTol);
            ConfigureBar(_tolBar, 0, 200, 10, new Point(x, 140), new Size(220, 40));
            Controls.Add(_tolBar);

            var lblFeather = new Label { Text = "边缘柔化", Location = new Point(x, 184), AutoSize = true };
            Controls.Add(lblFeather);
            ConfigureBar(_featherBar, 0, 10, 1, new Point(x, 204), new Size(220, 40));
            Controls.Add(_featherBar);

            _previewCheck.Location = new Point(x, 248);
            _previewCheck.AutoSize = true;
            Controls.Add(_previewCheck);

            var lblSave = new Label { Text = "保存为物品：", Location = new Point(x, 278), AutoSize = true };
            Controls.Add(lblSave);

            // 保存按钮跟着当前物品数量走（设几只就显示几个）
            int monkeyCount = Math.Max(1, _pet.Settings.MonkeyCount);
            for (int i = 0; i < monkeyCount; i++)
            {
                int index = i;
                var btn = new Button
                {
                    Text = (i + 1) + "号",
                    Location = new Point(x + (i % 2) * 112, 300 + (i / 2) * 40),
                    Size = new Size(104, 34),
                };
                btn.Click += (s, e) => SaveAsMonkey(index);
                Controls.Add(btn);
            }
            int saveRows = (monkeyCount + 1) / 2;
            int saveBottom = 300 + saveRows * 40;

            var btnFile = new Button { Text = "💾 另存为PNG文件…", Location = new Point(x, saveBottom + 8), Size = new Size(220, 36) };
            btnFile.Click += (s, e) => SaveAsFile();
            Controls.Add(btnFile);

            var btnDone = new Button { Text = "✅ 完成", Location = new Point(x, saveBottom + 50), Size = new Size(220, 44) };
            btnDone.Click += (s, e) => Close();
            Controls.Add(btnDone);

            var help = new Label
            {
                Text = "怎么用：\r\n" +
                       "1) 点【打开图片】选你的合影照片\r\n" +
                       "2) 点【选背景色】，再在照片的空白背景上点一下\r\n" +
                       "3) 拖【容差】直到背景透明、人物保留\r\n" +
                       "4) 点【保存为 1号 / 2号 / 3号 / 4号】\r\n" +
                       "5) 回到设置窗口关掉，物品就换成这四个人了",
                Location = new Point(12, 490),
                Size = new Size(700, 90),
                ForeColor = Color.Gray,
            };
            Controls.Add(help);
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

        private void WireEvents()
        {
            _btnOpen.Click += (s, e) => OpenImage();
            _btnPick.Click += (s, e) => TogglePick();
            _pictureBox.MouseClick += PictureBox_MouseClick;
            _tolBar.Scroll += (s, e) => Regenerate();
            _featherBar.Scroll += (s, e) => Regenerate();
            _previewCheck.CheckedChanged += (s, e) => RefreshPreview();
        }

        // ==================== 打开图片 ====================

        private void OpenImage()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择照片（里面有四个人那张）";
                dlg.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    using (var loaded = new Bitmap(dlg.FileName))
                    {
                        _source?.Dispose();
                        _source = Downscale(loaded, 500);
                    }
                    _cutout?.Dispose();
                    _cutout = null;
                    _hasColor = false;
                    _picking = false;
                    _btnPick.Text = "🎯 选背景色";

                    // 自动把四角平均色当背景色，通常一下就抠出来了
                    _keyColor = AverageCornerColor(_source);
                    _hasColor = true;
                    _tolBar.Value = 40;
                    _featherBar.Value = 2;
                    UpdateColorLabel();
                    Regenerate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "打不开这张图片：\n" + ex.Message, "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void TogglePick()
        {
            if (_source == null)
            {
                MessageBox.Show(this, "请先点【打开图片】选一张照片。", "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _picking = !_picking;
            _pictureBox.Cursor = _picking ? Cursors.Cross : Cursors.Default;
            _btnPick.Text = _picking ? "在图片的背景上点一下…" : "🎯 选背景色";
            if (!_picking) RefreshPreview();
        }

        private void PictureBox_MouseClick(object? sender, MouseEventArgs e)
        {
            if (!_picking || _source == null) return;
            Point pt = ImagePointFromMouse(e.Location);
            if (pt.IsEmpty) return;

            _keyColor = _source.GetPixel(pt.X, pt.Y);
            _hasColor = true;
            _picking = false;
            _pictureBox.Cursor = Cursors.Default;
            _btnPick.Text = "🎯 选背景色";
            UpdateColorLabel();
            Regenerate();
        }

        // ==================== 抠图处理 ====================

        private void Regenerate()
        {
            if (_source == null || !_hasColor) return;

            Bitmap? old = _cutout;
            _cutout = CreateCutout(_source, _keyColor, _tolBar.Value, _featherBar.Value);
            old?.Dispose();

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_source == null) return;
            _pictureBox.Image = _previewCheck.Checked && _cutout != null ? _cutout : _source;
        }

        /// <summary>把鼠标在预览框的坐标换算成图片像素坐标（适配 Zoom 缩放）。</summary>
        private Point ImagePointFromMouse(Point e)
        {
            if (_source == null) return Point.Empty;
            var img = _source;
            float scale = Math.Min((float)_pictureBox.ClientSize.Width / img.Width,
                                   (float)_pictureBox.ClientSize.Height / img.Height);
            int dispW = (int)(img.Width * scale);
            int dispH = (int)(img.Height * scale);
            int offsetX = (_pictureBox.ClientSize.Width - dispW) / 2;
            int offsetY = (_pictureBox.ClientSize.Height - dispH) / 2;
            int ix = (int)((e.X - offsetX) / scale);
            int iy = (int)((e.Y - offsetY) / scale);
            ix = Math.Max(0, Math.Min(img.Width - 1, ix));
            iy = Math.Max(0, Math.Min(img.Height - 1, iy));
            return new Point(ix, iy);
        }

        /// <summary>颜色抠像：把接近背景色的像素变透明，人物保留。</summary>
        private static Bitmap CreateCutout(Image source, Color key, int tolerance, int feather)
        {
            var bmp = new Bitmap(source);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                var bytes = new byte[Math.Abs(stride) * bmp.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                int tol = tolerance;
                int featherPx = Math.Max(1, feather);
                int innerTol = tol - featherPx;

                for (int y = 0; y < bmp.Height; y++)
                {
                    int rowStart = y * stride;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int idx = rowStart + x * 4;
                        byte b = bytes[idx];
                        byte g = bytes[idx + 1];
                        byte r = bytes[idx + 2];
                        byte a = bytes[idx + 3];

                        int dr = r - key.R;
                        int dg = g - key.G;
                        int db = b - key.B;
                        double dist = Math.Sqrt(dr * dr + dg * dg + db * db);

                        if (dist < innerTol)
                        {
                            bytes[idx + 3] = 0;                          // 全透明
                        }
                        else if (dist < tol && featherPx > 0)
                        {
                            double t = (dist - innerTol) / featherPx;    // 0..1 边缘渐变
                            bytes[idx + 3] = (byte)(a * t);
                        }
                    }
                }

                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        /// <summary>把大图缩小到最长边 maxSide，让抠图又快又流畅。</summary>
        private static Bitmap Downscale(Image img, int maxSide)
        {
            if (img.Width <= maxSide && img.Height <= maxSide)
                return new Bitmap(img);

            float scale = Math.Min((float)maxSide / img.Width, (float)maxSide / img.Height);
            int w = Math.Max(1, (int)(img.Width * scale));
            int h = Math.Max(1, (int)(img.Height * scale));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, w, h);
            }
            return bmp;
        }

        /// <summary>取图片四角附近的平均色，自动当作背景色。</summary>
        private static Color AverageCornerColor(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int margin = Math.Max(1, Math.Min(w, h) / 30);
            var pts = new[]
            {
                new Point(margin, margin),
                new Point(w - 1 - margin, margin),
                new Point(margin, h - 1 - margin),
                new Point(w - 1 - margin, h - 1 - margin),
            };
            long r = 0, g = 0, b = 0, n = 0;
            foreach (var p in pts)
            {
                var c = bmp.GetPixel(p.X, p.Y);
                r += c.R; g += c.G; b += c.B; n++;
            }
            return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
        }

        // ==================== 保存 ====================

        private void SaveAsMonkey(int index)
        {
            if (_cutout == null)
            {
                MessageBox.Show(this, "还没有抠好的图，请先打开图片并选背景色。", "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MonkeyPet", "images");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "p" + (index + 1) + ".png");
                _cutout.Save(path, ImageFormat.Png);

                _pet.SetMonkeyImage(index, path);
                SettingsStore.Save(_pet.Settings);

                MessageBox.Show(this,
                    "已保存为 " + (index + 1) + " 号物品！\n\n" +
                    "回到设置窗口关掉它，回主界面就能看到效果了。",
                    "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：\n" + ex.Message, "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveAsFile()
        {
            if (_cutout == null)
            {
                MessageBox.Show(this, "还没有抠好的图。", "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "另存为透明 PNG";
                dlg.Filter = "PNG 图片|*.png";
                dlg.FileName = "monkey.png";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try { _cutout.Save(dlg.FileName, ImageFormat.Png); }
                    catch (Exception ex) { MessageBox.Show(this, "保存失败：\n" + ex.Message, "弹性桌面物品", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            }
        }

        private void UpdateColorLabel()
        {
            _lblColor.Text = _hasColor
                ? "背景色：RGB(" + _keyColor.R + "," + _keyColor.G + "," + _keyColor.B + ")"
                : "背景色：未选择";
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
