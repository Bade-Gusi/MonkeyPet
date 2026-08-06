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
        public int SizePercent { get; set; } = 100;       // 猴子大小 %
        public int TumbleRate { get; set; } = 100;        // 打滚频率 %（0 = 不打滚）
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
        public List<string?> ImagePaths { get; set; } = new List<string?>(); // 每只猴子的自选图片（跟着数量走）

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
            TumbleRate = 100;
            GroupDistance = 500;
            TopMost = true;
            SoundEnabled = true;
            ShowHint = true;
            GroupingEnabled = true;
            ObstaclesEnabled = true;
            ApiEnabled = true;
            ApiPort = 17580;
            AutoStart = false;
            ImagePaths.Clear();
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
