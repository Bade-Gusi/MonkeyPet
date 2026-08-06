using System;
using System.Drawing;

namespace baba
{
    /// <summary>
    /// 猴子实体：只管自己的坐标、速度、朝向、颠簸相位和“吼叫”状态。
    /// 不含任何窗体/绘制逻辑，绘制与碰撞检测由 MainForm 完成。
    /// </summary>
    public sealed class MonkeyEntity
    {
        public float X, Y;                // 中心坐标（屏幕像素）
        public float SpeedX, SpeedY;      // 速度（像素/秒）
        public float Angle;               // 当前朝向角（弧度，0 = 朝右）
        public float ExtraAngle;          // 额外旋转角（打滚玩耍用，弧度）
        public readonly float Phase;      // 正弦波随机相位，让每只猴子的颠簸不同步
        public float Scale = 1.0f;        // 绘制缩放（吼叫时临时放大）
        public float SpeedFactor = 1f;    // 速度倍率（设置面板可调）
        public Image Sprite;              // 角色图片（始终非空，缺失时由 MainForm 生成默认脸）

        private readonly Random _rng;
        private double _nextDirectionChange;  // 距下次随机改向的剩余秒数
        private float _pauseTimer;            // 定格剩余秒数（右键触发）
        private float _scaleTimer;            // 吼叫放大剩余秒数
        private float _tumbleTime;            // 翻滚剩余秒数（玩耍动作）
        private float _tumbleDirection = 1f;  // 翻滚方向（正/反）
        private const float DefaultSpeed = 130f;
        private const float TumbleDuration = 0.9f;

        public bool IsPaused => _pauseTimer > 0f;
        public int Width => Sprite?.Width ?? 60;
        public int Height => Sprite?.Height ?? 60;

        public MonkeyEntity(Random rng, Image sprite, Rectangle screen)
        {
            _rng = rng;
            Sprite = sprite;
            Phase = (float)(rng.NextDouble() * Math.PI * 2.0);
            X = screen.Left + rng.Next(30, Math.Max(31, screen.Width - 60));
            Y = screen.Top + rng.Next(30, Math.Max(31, screen.Height - 60));
            RandomizeDirection();
            ScheduleDirectionChange();
        }

        /// <summary>随机设定一个新的行进方向。</summary>
        public void RandomizeDirection()
        {
            double a = _rng.NextDouble() * Math.PI * 2.0;
            float speed = (DefaultSpeed + (float)(_rng.NextDouble() * 70f)) * SpeedFactor;
            SpeedX = (float)Math.Cos(a) * speed;
            SpeedY = (float)Math.Sin(a) * speed;
        }

        /// <summary>调整速度倍率（按比例缩放当前速度，避免猴子突然跳变）。</summary>
        public void SetSpeedFactor(float factor)
        {
            if (factor <= 0f) factor = 0.01f;
            float ratio = factor / SpeedFactor;
            if (float.IsNaN(ratio) || ratio <= 0f) ratio = 1f;
            SpeedX *= ratio;
            SpeedY *= ratio;
            SpeedFactor = factor;
        }

        /// <summary>把速度朝某个目标点调整（群聚靠拢用）。</summary>
        public void SteerToward(float targetX, float targetY)
        {
            float dx = targetX - X;
            float dy = targetY - Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1f) return;

            float speed = (float)Math.Sqrt(SpeedX * SpeedX + SpeedY * SpeedY);
            if (speed < 60f) speed = DefaultSpeed;
            SpeedX = dx / dist * speed;
            SpeedY = dy / dist * speed;
        }

        public void UpdateDirectionTimer(float dt)
        {
            _nextDirectionChange -= dt;
            if (_nextDirectionChange <= 0.0)
            {
                RandomizeDirection();
                ScheduleDirectionChange();
            }
        }

        /// <summary>右键触发：速度归零、定格一下、吼叫放大。</summary>
        public void TriggerRoar(float pauseSeconds, float scaleSeconds)
        {
            _pauseTimer = pauseSeconds;
            _scaleTimer = scaleSeconds;
            SpeedX = 0f;
            SpeedY = 0f;
        }

        /// <summary>每帧更新定格/吼叫/翻滚计时状态。</summary>
        public void Tick(float dt)
        {
            if (_pauseTimer > 0f) _pauseTimer -= dt;
            if (_scaleTimer > 0f)
            {
                _scaleTimer -= dt;
                Scale = 1.1f;
            }
            else
            {
                Scale = 1.0f;
            }

            if (_tumbleTime > 0f)
            {
                _tumbleTime -= dt;
                float progress = 1f - (_tumbleTime / TumbleDuration); // 0→1
                ExtraAngle = progress * (float)(Math.PI * 2.0) * _tumbleDirection;
                if (_tumbleTime <= 0f)
                {
                    _tumbleTime = 0f;
                    ExtraAngle = 0f;
                }
            }
            else
            {
                ExtraAngle = 0f;
            }
        }

        /// <summary>随机触发一次“打滚玩耍”（转一圈），正在定格时不开打。</summary>
        public void TryStartTumble()
        {
            if (_tumbleTime > 0f || _pauseTimer > 0f) return;
            _tumbleTime = TumbleDuration;
            _tumbleDirection = (_rng.Next(2) == 0) ? 1f : -1f;
        }

        /// <summary>朝向角平滑转向速度方向（线性插值 + 最短角路径，避免瞬间掉头）。</summary>
        public void UpdateAngle()
        {
            float target = (float)Math.Atan2(SpeedY, SpeedX);
            float diff = target - Angle;
            while (diff > Math.PI) diff -= (float)(Math.PI * 2.0);
            while (diff < -Math.PI) diff += (float)(Math.PI * 2.0);
            Angle += diff * 0.08f;
        }

        private void ScheduleDirectionChange()
        {
            _nextDirectionChange = 2.0 + _rng.NextDouble() * 5.0; // 2~7 秒
        }
    }
}
