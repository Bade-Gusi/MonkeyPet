# 🐵 猴群宠物 (Monkey Pet)

一个全屏桌面宠物应用：一群猴子（也可以是你们几个人～）在桌面上像猴子一样爬行、
打滚、玩耍，把 **窗口和桌面边缘当成障碍物** 弹来弹去；右键点它，它会大喊「爸爸」。

纯 WinForms（.NET 8）编写，**不开任何外挂、不碰游戏进程、不注入、不钩子、不抓屏**，
就是一只普通的置顶透明窗口宠物，放心在游戏旁边挂着。

## ✨ 功能

- 🐒 猴子全屏随机爬行：上下颠簸、左右摇摆、身体挤压拉伸，偶尔打滚 360°
- 🧱 把桌面上的**窗口矩形和屏幕边缘**当障碍物，撞上就反弹
- 👨‍👩‍👧 右键点击猴子 → 定格 0.3 秒 + 放大 + 播放 `assets\dad.wav` 喊「爸爸」
  （没有音频就用系统「哔哔」两声代替）
- ⚙️ 右上角齿轮 / F1 打开**设置窗口**（分页签，全中文大字）：
  · 基本：猴子数量（1~6）、移动速度、爬行幅度、猴子大小、打滚频率、群聚距离
  · 行为：始终置顶、启用叫声、显示提示、群聚行为、窗口障碍（都可开关）
  · 全部改完立刻生效、自动保存
- 🖼 内置**抠图工具**：打开合影 → 点一下背景色 → 拖容差 → 保存为 1号~N号 猴子，
  不用 PS 就能把人从背景里抠出来
- 🧵 猴子图片槽位**跟着数量走**，每只猴子都能单独换图，带实时缩略图预览
- 📖 **新手教程**：第一次运行自动弹出，之后按 **F2** 或设置里「关于 → 新手教程」随时可看
- 🛡 **防误关**：宠物窗口只认 ESC / 设置里的退出按钮，外部程序误发的关闭消息一律拦下，
  不会莫名其妙自己关掉

## 🚀 运行

- 需要：Windows 10/11 + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 Visual Studio 2022）
- 用 Visual Studio 打开 `baba.sln`，按 **F5** 运行
- 或命令行：`dotnet run --project baba`

操作：
- 右键点猴子 → 喊爸爸
- 点右上角齿轮 或按 F1 → 设置
- 按 ESC → 退出

## 🎨 自定义素材（可选）

把文件放进 `baba\assets\`，重新运行：

| 文件 | 作用 |
| --- | --- |
| `p1.png` ~ `pN.png` | 透明背景 PNG，当第 1~N 只猴子（名字要小写） |
| `dad.wav` | 右键喊「爸爸」的音频（WAV 格式） |

不放也可以：程序自动生成彩色卡通脸，音频用系统提示音代替。
更简单的做法：在设置窗口里用「抠图工具」或「换图」按钮，全程不用手动放文件。

## 🔌 控制 API（每个猴子都是一个对象）

程序内置一个**只在本机监听**的 HTTP 接口（默认 `http://localhost:17580`），
每一只猴子都能按 ID 单独控制。设置里「关于 → 开发者 API」可开关、可看地址、
可一键在浏览器打开实时数据。

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/api/status` | 程序状态、猴子数量 |
| GET | `/api/monkeys` | 所有猴子的实时数据（数组，每个元素是一只猴子对象） |
| GET | `/api/monkeys/{id}` | 单只猴子的数据（x、y、角度、大小、是否定格等） |
| POST | `/api/monkeys/{id}/roar` | 让这只猴子喊爸爸 |
| POST | `/api/monkeys/{id}/move?x=100&y=200` | 把它移动到指定坐标 |
| POST | `/api/monkeys/{id}/speed?percent=150` | 单独调它的速度 |
| POST | `/api/monkeys/{id}/image?path=C:\xxx.png` | 单独换它的图片 |
| GET | `/api/settings` | 读取当前全部设置 |
| POST | `/api/settings` | 改设置（**支持局部更新**：body 里写了哪个字段就改哪个） |
| POST | `/api/exit` | 退出程序 |

例：让 3 号猴子喊爸爸

```bash
curl -X POST "http://localhost:17580/api/monkeys/3/roar"
```

例：只把速度改成 150%

```bash
curl -X POST "http://localhost:17580/api/settings" -H "Content-Type: application/json" -d '{"SpeedPercent":150}'
```

> 该接口只监听 `127.0.0.1`，别的电脑访问不到；不开外挂、不碰游戏进程。

## 🧑‍💻 技术栈

- C# / Windows Forms（.NET 8）
- GDI+ 双缓冲绘制（60 FPS，`Timer` 16ms）
- Win32 只读 API：`EnumWindows` / `GetWindowRect`（只用来拿窗口矩形当障碍物）
- 设置持久化到 `%AppData%\MonkeyPet\settings.json`

## 📄 License

[MIT](LICENSE) © 2026 Bade-Gusi
