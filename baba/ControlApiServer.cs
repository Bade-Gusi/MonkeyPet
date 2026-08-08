using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace baba
{
    /// <summary>单只物品的 API 快照（给控制接口用）。</summary>
    public sealed class MonkeyInfo
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Angle { get; set; }
        public float Scale { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Paused { get; set; }
        public float SpeedFactor { get; set; }
    }

    /// <summary>
    /// 本机控制 API：一个极简的 HTTP 服务，只监听 127.0.0.1。
    /// 每个物品都是一个"对象"，都能通过 /api/monkeys/&lt;id&gt;/... 单独控制。
    /// 不需要管理员权限（监听回环地址即可），不开外挂、不碰游戏进程。
    /// </summary>
    public sealed class ControlApiServer : IDisposable
    {
        private readonly MainForm _form;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread? _thread;

        public int Port { get; }
        public bool IsRunning { get; private set; }

        public ControlApiServer(MainForm form, int port)
        {
            _form = form;
            Port = port;
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start()
        {
            _listener.Start();
            IsRunning = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "MonkeyPetControlApi",
            };
            _thread.Start();
        }

        private void Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (_cts.IsCancellationRequested) break;
                }
            }
        }

        private void HandleClient(object? state)
        {
            using (var client = (TcpClient)state!)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    using (var stream = client.GetStream())
                    {
                        (string method, string path, string body) = ReadRequest(stream);
                        (int status, string json) = Route(method, path, body);
                        WriteResponse(stream, status, json, method);
                    }
                }
                catch
                {
                    // 请求解析失败就静默断开，不影响宠物本体
                }
            }
        }

        // ==================== 极简 HTTP 解析 ====================

        private static (string method, string path, string body) ReadRequest(Stream stream)
        {
            var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
            string requestLine = reader.ReadLine() ?? "";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? line;
            while ((line = reader.ReadLine()) != null && line.Length > 0)
            {
                int idx = line.IndexOf(':');
                if (idx > 0)
                    headers[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
            }

            string body = "";
            if (headers.TryGetValue("Content-Length", out var clStr) &&
                int.TryParse(clStr, out int cl) && cl > 0 && cl < 1024 * 1024)
            {
                var chars = new char[cl];
                int read = 0;
                while (read < cl)
                {
                    int n = reader.Read(chars, read, cl - read);
                    if (n <= 0) break;
                    read += n;
                }
                body = new string(chars, 0, read);
            }

            var parts = requestLine.Split(' ');
            string method = parts.Length > 0 ? parts[0] : "";
            string path = parts.Length > 1 ? parts[1] : "";
            return (method, path, body);
        }

        private static void WriteResponse(Stream stream, int status, string json, string method)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            string reason = status == 200 ? "OK" : status == 404 ? "Not Found" : status == 400 ? "Bad Request" : "Error";
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            sb.Append("Content-Type: application/json; charset=utf-8\r\n");
            sb.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type\r\n");
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("Connection: close\r\n\r\n");
                stream.Write(Encoding.ASCII.GetBytes(sb.ToString()));
            }
            else
            {
                sb.Append("Connection: close\r\n\r\n");
                stream.Write(Encoding.ASCII.GetBytes(sb.ToString()));
                stream.Write(payload);
            }
            stream.Flush();
        }

        // ==================== 路由 ====================

        private (int, string) Route(string method, string rawPath, string body)
        {
            string path = rawPath;
            string query = "";
            int q = rawPath.IndexOf('?');
            if (q >= 0)
            {
                path = rawPath.Substring(0, q);
                query = rawPath.Substring(q + 1);
            }

            var segs = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            // /api/...
            if (segs.Length < 2 || segs[0] != "api")
                return (404, Json(new { error = "unknown path" }));

            try
            {
                switch (segs[1])
                {
                    case "status":
                        return (200, Json(_form.ApiStatus()));

                    case "dance":
                        if (method == "POST")
                            return (200, Json(new { ok = _form.ApiDance() }));
                        return (400, Json(new { error = "use POST" }));

                    case "banana":
                        if (method == "POST")
                            return (200, Json(new { ok = _form.ApiThrowBanana() }));
                        return (400, Json(new { error = "use POST" }));

                    case "follow":
                        if (method == "POST")
                            return (200, Json(new { ok = _form.ApiToggleFollow() }));
                        return (400, Json(new { error = "use POST" }));

                    case "monkeys":
                    case "items": // 改名后的别名，旧路径 /api/monkeys 也继续可用
                        if (segs.Length == 2 && method == "GET")
                            return (200, Json(_form.ApiListMonkeys()));
                        if (segs.Length == 3 && segs[2] == "roar-all" && method == "POST")
                            return (200, Json(new { ok = _form.ApiRoarAll() }));
                        if (segs.Length == 3 && method == "GET")
                            return ApiGetMonkey(segs[2]);
                        if (segs.Length == 4)
                            return ApiMonkeyAction(method, segs[2], segs[3], query);
                        return (400, Json(new { error = "bad monkeys request" }));

                    case "settings":
                        if (segs.Length == 2 && method == "GET")
                            return (200, Json(_form.ApiGetSettings()));
                        if (segs.Length == 2 && (method == "POST" || method == "PUT"))
                            return ApiApplySettings(body);
                        return (400, Json(new { error = "bad settings request" }));

                    case "exit":
                        if (method == "POST")
                        {
                            _form.ApiExit();
                            return (200, Json(new { ok = true, message = "exiting" }));
                        }
                        return (400, Json(new { error = "use POST" }));

                    default:
                        return (404, Json(new { error = "unknown api: " + segs[1] }));
                }
            }
            catch (Exception ex)
            {
                return (500, Json(new { error = ex.Message }));
            }
        }

        private (int, string) ApiGetMonkey(string idStr)
        {
            if (!int.TryParse(idStr, out int id))
                return (400, Json(new { error = "bad id" }));
            var info = _form.ApiGetMonkey(id);
            return info == null
                ? (404, Json(new { error = "monkey not found", id }))
                : (200, Json(info));
        }

        private (int, string) ApiMonkeyAction(string method, string idStr, string action, string query)
        {
            if (!int.TryParse(idStr, out int id))
                return (400, Json(new { error = "bad id" }));
            if (method != "POST")
                return (400, Json(new { error = "use POST" }));

            var q = ParseQuery(query);
            switch (action)
            {
                case "roar":
                    return (200, Json(new { ok = _form.ApiRoar(id) }));
                case "poke":
                    return (200, Json(new { ok = _form.ApiPoke(id) }));
                case "toss":
                    if (!TryGetFloat(q, "vx", out float tvx) || !TryGetFloat(q, "vy", out float tvy))
                        return (400, Json(new { error = "need ?vx=&vy=" }));
                    return (200, Json(new { ok = _form.ApiToss(id, tvx, tvy) }));
                case "move":
                    if (!TryGetFloat(q, "x", out float x) || !TryGetFloat(q, "y", out float y))
                        return (400, Json(new { error = "need ?x=&y=" }));
                    return (200, Json(new { ok = _form.ApiMove(id, x, y) }));
                case "speed":
                    if (!TryGetFloat(q, "percent", out float pct))
                        return (400, Json(new { error = "need ?percent=" }));
                    return (200, Json(new { ok = _form.ApiSetSpeed(id, pct) }));
                case "image":
                    if (!q.TryGetValue("path", out string? path) || string.IsNullOrEmpty(path))
                        return (400, Json(new { error = "need ?path=" }));
                    return (200, Json(new { ok = _form.ApiSetImage(id, path) }));
                default:
                    return (404, Json(new { error = "unknown action: " + action }));
            }
        }

        private (int, string) ApiApplySettings(string body)
        {
            try
            {
                bool ok = _form.ApiApplySettings(body);
                return (200, Json(new { ok = ok }));
            }
            catch (Exception ex)
            {
                return (400, Json(new { error = ex.Message }));
            }
        }

        // ==================== 小工具 ====================

        private static string Json(object obj)
        {
            try
            {
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return "{}";
            }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return dict;
            foreach (var part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0)
                {
                    string k = Uri.UnescapeDataString(part.Substring(0, eq));
                    string v = Uri.UnescapeDataString(part.Substring(eq + 1));
                    dict[k] = v;
                }
            }
            return dict;
        }

        private static bool TryGetFloat(Dictionary<string, string> q, string key, out float value)
        {
            value = 0f;
            return q.TryGetValue(key, out var s) &&
                   float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            IsRunning = false;
        }
    }
}
