# 弹性桌面物品 — Design System

> 由 DESIGN.md 技能生成：记录本项目在生成 UI/素材时的统一设计规范，
> 保证后续由 AI 生成的内容风格一致。

## Brand
- Name: 弹性桌面物品 (Monkey Pet)
- Tone: playful / silly / warm（可爱、胡闹、暖色）

## Colors
- 透明色 Key: Magenta `#FF00FF`（窗体底色，会被置为全透明，实现桌面穿透）
- 物品 1 (p1): Orange `#FF783C`
- 物品 2 (p2): Blue `#4682FF`
- 物品 3 (p3): Green `#50C878`
- 物品 4 (p4): Gold `#FFC83C`
- 描边 / 眼睛 / 线条: Ink `#323232`
- 提示文字: White `#FFFFFF`，投影 `#00000080`

## Typography
- 提示字幕: Microsoft YaHei UI, Bold, 13px
- 默认脸数字角标: Microsoft YaHei UI, Bold, 12px
- 回退字体: 系统默认（SystemFont）

## Motion
- 帧率: 60 FPS（`Timer` 间隔 16ms，双缓冲 `BufferedGraphics`）
- 爬行颠簸: 垂直 `sin(t*8 + phase)*5px`，水平 `sin(t*6 + phase)*3px`
- 朝向插值: 每次 `0.08`，沿最短角度路径（不瞬移掉头）
- 右键吼叫: 缩放 `1.0 → 1.1`（持续 0.5s），定格 0.3s
- 随机改向: 每 2~7 秒
- 群聚: 最近同伴 > 500px 时，每帧 20% 概率向群体中心靠拢

## Components / Interaction
- 碰撞箱: 图片宽高的 60%（防止视觉擦边卡顿）
- 点击判定: 碰撞箱外扩 6px（更容易点中）
- 禁区: 屏幕四边 + 所有可见顶层窗口（排除自身进程、Progman、WorkerW）
- 退出: ESC
- 素材兜底: 缺图 → 自动生成彩色卡通脸；缺音频 → 系统“哔哔”两声；均只提示一次

## Settings Window（设置面板）
- 打开方式: 主界面右上角齿轮按钮 或 F1；点 ESC 只关设置不退出
- 布局: TabControl 三分页（基本设置 / 物品图片 / 关于），470x600，FixedDialog，居中，TopMost
- 控件字体: Microsoft YaHei UI 9pt；标题 16pt Bold
- 基本设置:
  - 物品数量 1~6（默认 4）
  - 移动速度 20~200%（默认 100）
  - 爬行幅度 0~200%（默认 100）
  - 物品大小 50~300%（默认 100）
  - 打滚频率 0~200%（默认 100；0 = 不打滚）
  - 群聚距离 100~1000px（默认 500）
- 行为勾选项: 始终置顶 / 启用叫声 / 显示操作提示 / 群聚行为 / 窗口障碍
- 图片槽位: **跟着物品数量走**（1~6），每只一个缩略图 + 「N号 换图」按钮，
  换图后立即刷新缩略图；图片路径存在 settings.json 的 `ImagePaths` 列表里
- 抠图工具: 颜色抠像（纯图片像素处理，不碰游戏进程）——选背景色 + 容差 + 边缘柔化，
  预览后保存为 1~N 号物品（保存按钮也跟随数量）；自动取四角平均色当默认背景色
- 关于页: 新手教程 / 试听叫声 / 恢复默认 / 退出程序 按钮 + 开源链接
- 新手教程: 第一次运行自动弹出，F2 随时再看（HelpForm）
- 防误关: 主窗体 OnFormClosing 拦截外部误发的 UserClosing，仅放行 ESC/退出按钮
- 屏幕适配: 主窗体铺满 SystemInformation.VirtualScreen（所有显示器），
  OnResize + WM_DISPLAYCHANGE 重新铺满并把物品夹回屏内；缓冲区按虚拟屏尺寸重建
- 版本与更新: `<Version>2.0.0</Version>`；UpdateChecker 异步对比 GitHub Releases/latest，
  有新版本弹 UpdateNotifyForm（非模态）；设置「行为」可关自动检查，「关于」有手动检查按钮；
  API 的 `/api/items` 是 `/api/monkeys` 的别名
- 开机自启动: 优先注册表 HKCU Run 键，被锁则回退到启动文件夹 .lnk 快捷方式；
  设置「行为」勾选即生效，启动时按设置同步注册表
- 自定义文字: 设置「自定义文字」页签可编辑——BubbleTexts(每行一条随机挑)、
  PokeText/TossText/DanceText/BananaText/SleepText/HintText(可换行)；
  API 支持局部更新这些字段
- 控制 API: 内置极简 HTTP 服务（`TcpListener` 只绑 `127.0.0.1`，默认端口 17580），
  每只物品按 ID 可控（`/api/monkeys/{id}/roar|move|speed|image`），设置支持局部更新，
  `/api/exit` 退出；设置「关于」页可开关/看地址
- 持久化: `%AppData%\MonkeyPet\settings.json`，改动即存，启动时加载
- 图标语义: 按钮使用 Emoji 图标 + 中文文字（🔊试听 ♻恢复 ✖退出 📖教程 🐵N号）
- 开源: MIT 协议，设置窗口「关于」页有 GitHub 链接（点击跳转浏览器）

## Monkey-Crawl Animation（移动动画，默认温和）
- 有节奏浮动: 垂直 `sin(t*5+phase)*6`，水平 `sin(t*4+phase)*3`（幅度由「爬行幅度」设置 0~200% 控制）
- 保持正立: 不随移动方向旋转，往左走时水平镜像（脸不朝下），观感正常
- 无默认挤压/摇晃/打滚；打滚（360°）默认关闭，用户可在设置里调大「打滚频率」开启
- 跳舞（F4 触发）才临时加 8° 扭 + 14px 蹦

## Obstacle Anti-Stick（防卡死）
- 物品若卡在窗口矩形内（出生点/窗口盖上来），每帧向外一圈圈找最近的空路，
  朝那儿跑（Unstick），且卡住期间允许移动，避免被永久钉死

## Fun Interactions（趣味玩法）
- 左键戳: 空气跳（AirTime 0.6s + vy -500，重力 1400），气泡「咦！」
- 拖拽投掷: MouseDown 拾取 → MouseMove 跟随 → MouseUp 按最后速度*8 抛出，
  ThrowTimer 1.5s 内摩擦 0.985，撞窗反弹
- 一起跳舞 F4: `_danceEndTime = now+8s`，原地蹦 `|sin(t*10+phase)|*16` + 扭 `sin(t*12+phase)*10°`
- 跟随鼠标 F5: 每帧 SteerToward(光标)
- 扔香蕉 B: 顶部掉落，重力 520，物品抢最近一根，抢到 +1 分 + ScaleBoost 1.6
- 发呆睡觉: 45s 无操作 → IsSleeping 趴下(scaleY*0.85)+💤；任意鼠标/键盘唤醒
- 气泡系统: SpeechBubble（白圆角+尾巴+文字），约 1.6s 淡出，全局复用

## Robustness
- 全局异常捕获：UI 线程 + 非 UI 线程都只弹中文提示框，绝不允许闪退
- 图片加载失败、音频加载失败、配置读写失败一律静默回退到默认值
