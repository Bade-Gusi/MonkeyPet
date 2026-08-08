using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace baba
{
    /// <summary>可持久化的宠物设置（保存在 AppData\MonkeyPet\settings.json，用户不用管它）。</summary>
    public sealed class PetSettings
    {
        public int MonkeyCount { get; set; } = 4;
        public int SpeedPercent { get; set; } = 100;      // 移动速度 %
        public int BobAmount { get; set; } = 100;         // 爬行颠簸幅度 %
        public int SizePercent { get; set; } = 100;       // 物品大小 %
        public int TumbleRate { get; set; } = 0;          // 打滚频率 %（0 = 不打滚，默认关）
        public int GroupDistance { get; set; } = 500;     // 群聚距离（像素）
        public bool TopMost { get; set; } = true;         // 始终置顶
        public bool SoundEnabled { get; set; } = true;    // 启用叫声
        public bool ShowHint { get; set; } = true;        // 显示操作提示
        public bool GroupingEnabled { get; set; } = true; // 群聚行为
        public bool ObstaclesEnabled { get; set; } = true;// 窗口障碍
        public bool HasSeenTutorial { get; set; }         // 是否看过新手教程
        public bool ApiEnabled { get; set; } = true;      // 启用本机控制 API
        public int ApiPort { get; set; } = 17580;         // 控制 API 端口
        public bool AutoStart { get; set; }               // 开机自启动
        public bool AutoUpdateCheck { get; set; } = true;  // 启动时自动检查新版本
        public int CollisionSizePercent { get; set; } = 60;   // 碰撞体积大小 %（相对物品图片）
        public bool ItemCollisionEnabled { get; set; } = true;// 物品之间互相碰撞
        public int BounceElasticity { get; set; } = 60;       // 弹性/弹力 %
        public List<string?> ImagePaths { get; set; } = new List<string?>(); // 每只物品的自选图片（跟着数量走）

        // ===== 自定义文字（全部可在设置里改） =====
        public List<string> BubbleTexts { get; set; } = new List<string> { "爸爸！", "叫爸爸！", "诶，爸爸！", "爸爸爸爸！" };
        public string PokeText { get; set; } = "咦！";
        public string TossText { get; set; } = "咻——！";
        public string DanceText { get; set; } = "🎵 蹦迪时间！";
        public string BananaText { get; set; } = "🍌 我抢到啦！";
        public string SleepText { get; set; } = "💤 困了…";
        public string HintText { get; set; } =
            "右键喊爸爸 ｜ F3 一起喊 ｜ 左键戳一下 / 拖起来扔\nF4 跳舞 ｜ F5 跟随 ｜ B 扔香蕉 ｜ F1 设置 ｜ ESC 退出";

        public string? GetImagePath(int index) =>
            index >= 0 && index < ImagePaths.Count ? ImagePaths[index] : null;

        public void SetImagePath(int index, string? path)
        {
            while (ImagePaths.Count <= index) ImagePaths.Add(null);
            ImagePaths[index] = path;
        }

        public void ResetToDefaults()
        {
            MonkeyCount = 4;
            SpeedPercent = 100;
            BobAmount = 100;
            SizePercent = 100;
            TumbleRate = 0;
            GroupDistance = 500;
            TopMost = true;
            SoundEnabled = true;
            ShowHint = true;
            GroupingEnabled = true;
            ObstaclesEnabled = true;
            ApiEnabled = true;
            ApiPort = 17580;
            AutoStart = false;
            AutoUpdateCheck = true;
            CollisionSizePercent = 60;
            ItemCollisionEnabled = true;
            BounceElasticity = 60;
            ImagePaths.Clear();
            BubbleTexts = new List<string> { "爸爸！", "叫爸爸！", "诶，爸爸！", "爸爸爸爸！" };
            PokeText = "咦！";
            TossText = "咻——！";
            DanceText = "🎵 蹦迪时间！";
            BananaText = "🍌 我抢到啦！";
            SleepText = "💤 困了…";
            HintText = "右键喊爸爸 ｜ F3 一起喊 ｜ 左键戳一下 / 拖起来扔\nF4 跳舞 ｜ F5 跟随 ｜ B 扔香蕉 ｜ F1 设置 ｜ ESC 退出";
        }
    }

    public static class SettingsStore
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MonkeyPet", "settings.json");

        public static PetSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<PetSettings>(json);
                    if (s != null) return s;
                }
            }
            catch
            {
                // 配置坏了就用默认，绝不让程序崩
            }
            return new PetSettings();
        }

        public static void Save(PetSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // 存不上也不影响运行
            }
        }
    }
}
