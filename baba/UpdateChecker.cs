using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace baba
{
    /// <summary>自动更新检测：把本地版本和 GitHub 最新 Release 对比。</summary>
    internal static class UpdateChecker
    {
        private const string Repo = "Bade-Gusi/MonkeyPet";
        private const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
        public const string ReleasesUrl = "https://github.com/" + Repo + "/releases/latest";

        public static Version CurrentVersion { get; } =
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0);

        /// <summary>查询 GitHub 最新发布版本号；失败/没发布返回 null。</summary>
        public static async Task<string?> GetLatestVersionAsync()
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ElasticDesktopItems-Updater/" + CurrentVersion);
            string json = await client.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                return tag.GetString()?.TrimStart('v', 'V');
            return null;
        }

        /// <summary>本地版本是否比某个版本旧。</summary>
        public static bool IsNewer(string latest) =>
            Version.TryParse(latest, out var lv) && lv > CurrentVersion;
    }
}
