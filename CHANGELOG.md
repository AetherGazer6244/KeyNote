# 更新日志

本文件记录 KeyNote 的版本变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。

## [1.6.1] - 2026-11-21

全局快捷键行为变更。

### 变更

- **全局快捷键改为唤起/收起主窗口**:按下组合键唤起 KeyNote 主窗口(最小化时自动恢复),再按一次收起回托盘;此前唤起的是额度悬浮小组件,该入口移除(小组件仍可从托盘「🤖 AI 额度」子菜单和设置页开关打开)
  - 服务正名:`QuotaHotkeyService` → `HotkeyService`(注册/状态机/失败文案不变),窗口类名 AgentEyeHotkey_* → KeyNoteHotkey_*
  - 配置搬家:真源从 quota.json `ui.hotkey` 迁至 config.json `app_hotkey` / `app_hotkey_enabled`;旧键启动时一次性自动迁移(自定义组合不丢,空值视为主动清除;quota.json 旧值保留不删)
  - 设置页「全局快捷键 / 快捷键组合」两项从「AI 额度」组移到「启动与常规」组;空组合按停用处理,不再报「组合无效」

## [1.6.0] - 2026-11-21

运行日志:事件时间线,方便排查问题。

### 新增

- **运行日志**(`Core/Utils/AppLog.cs`):启动 / 钩子 / 额度 / 更新等关键事件与错误,按天落盘数据目录 `logs/app-yyyy-MM-dd.log`,与设备域日志(vigil 移植)共用同一文件与时间线,一个文件看全;写入 / 跨天换文件 / 保留清理(14 天)/ 失败兜底全部复用 Devices.Log 成熟实现
  - 埋点:启动序列与版本、DB 初始化、设备域/额度轮询/钩子启停、记录暂停/恢复、额度行失败(WARN,带 provider 名与原因)、每日与手动更新检查、正常退出
  - `CrashLog` 的异常同步进入时间线(崩溃记录 keynote.crash.log 仍独立保留,永不受开关影响)
- 设置页「启动与常规」新增「运行日志」开关(默认开,保存后立即生效);「数据」组新增「打开日志文件夹」动作
- 声明表 `FieldSpec` 新增 `SwitchDefault` 参数:开关的 UI 默认态与行为默认值同源(此前一律按 false 显示)

## [1.5.0] - 2026-11-21

设置页设备插件收纳 + 全新关于页。

### 修复

- **设备插件分组可收纳**:「设备插件」23 个品牌默认收起为一行组头(标题 +「N/23 已启用」计数 + chevron),点击组头展开/收起,状态记忆进 `config.json`(`settings_plugin_group_expanded`);搜索命中插件组时自动临时展开,清空搜索后恢复手动状态

### 新增

- **关于页**(`Views/AboutPage`):侧边栏底部新增「ⓘ 关于」入口(替换原「暂停记录」按钮,暂停/恢复记录改由托盘菜单唯一承担)
  - 应用卡:图标 + 名称 + 版本号 + 一句话定位 + 「检查更新」
  - 项目简介 / 功能一览:三域功能清单照 README 口径
  - 仓库与许可:GitHub 仓库、Releases 更新页链接(复用 github.com URL 白名单)+ MIT 许可声明
  - 关于作者:LoneStar(GitHub:AetherGazer6244)简介 + 主页链接
  - 「检查更新」自设置页迁入:检查结果提示行 + 「发现新版」下载提示 + 更新确认框全链路随迁,设置页「关于」分组移除

## [1.4.0] - 2026-11-21

稳健性 + 全局线程模型根治 + 蓝牙电量修复 + 界面体验打磨。

### 修复

- **电量监视器属性键修复**:BLE / 经典蓝牙 DeviceWatcher 不再携带非法属性键 `System.Devices.AepBattery_Level`(实测四种写法全部抛 "Property key syntax error",此前 `_batteryProp` 恒为 null 属死代码);BLE 电量改为 AEP 嵌套属性按台查询(`CreateFromIdAsync`,5 分钟节流)+ GATT 0x180F/0x2A19 直读兜底,经典蓝牙维持 HFP 网关设备节点通道
- **每日更新检查静默降级**:`UpdateService.CheckAsync(silent: true)` 用于每日静默检查,网络不可达不再每次启动刷 crash.log(手动检查仍落盘)
- **API 错误文案人话化**:新增 `ApiErrorText.Humanize` 映射(智谱 Coding Plan 未开通 / Key 无效 / 余额不足 / 限流 / 超时等),智谱、MiniMax、SiliconFlow 三处透传点接入;匹配不到显示原文(80 字截断)

### 改进

- **全局 SynchronizationContext 根治**:新增 `Helpers/UiSynchronizationContext`(Post→DispatcherQueue.TryEnqueue、Send→阻塞转发、CreateCopy→自身),App 构造时安装到 UI 线程——所有 `await` 续体自动回 UI 线程,`0x8001010E` 同类隐患从根上消除;显式 `TryEnqueue` 编组保留作双保险
- **async void 全面兜底**:更新下载进度回调经 `DispatcherQueue.TryEnqueue` 编组回 UI 线程(应用内更新下载必崩点);设备页 3 处 async 事件处理器收敛到 `RunSafe` 统一捕获落盘;额度轮询 `Reschedule` 改回同步方法(`Application.UnhandledException` 拦不住 async void 逸出的异常)
- **上次异常退出提示**:crash.log 干净退出标记(`app.exit`) + 启动检测,异常退出后下次启动经托盘气泡提示一次;系统关机/注销在托盘窗口过程补写标记避免误报

### 界面

- 设置页「设备插件」23 个品牌按品类分组(键盘 / 鼠标 / 键鼠通用 / 手柄 / 耳机,小标题含品牌数),组头新增「一键全关探测插件」
- 设置页固定底栏新增「N/M 已启用」插件计数胶囊(开关拨动实时刷新)
- 设备页空态新增「立即扫描」按钮(与页头刷新同一路径)
- 设置搜索命中插件名与品类小标题(搜索框输「罗技」「插件」均可定位)

## [1.3.1] - 2026-11-21

### 修复

- 设备电量页:手动刷新与删除设备的 async 收尾在后台线程直碰控件(`WinUI3` UI 线程无 `SynchronizationContext`,Task 续体不会自动回 UI 线程),触发 `0x8001010E` 后经 `async void` 未捕获直接终止进程;刷新收尾与删除确认链路现显式 `DispatcherQueue.TryEnqueue` 编组回 UI 线程(实机验证时以 .NET Runtime 1026 事件定位)

## [1.3.0] - 2026-11-19

三合一整合:并入 VigilWatt 设备电量与 AgentEye AI 额度,KeyNote 升级为「键鼠统计 + 设备电量 + AI 额度」一体的桌面观测台。不改既有统计口径,v1~v6 表结构与数据一律不动(v7 起纯增量)。

### 新增

- **设备电量域**(`KeyNote.Core.Devices`,自 VigilWatt 移植,零上游残留):BLE GATT / 2.4G HID / 蓝牙经典 / 系统电池四路来源,23 个品牌硬件插件(会写帧的协议默认关);电量页(设备列表 + KPI 卡 + 单设备 24h 曲线)、桌面悬浮小组件、任务栏低电贴片、低电托盘气泡、续航估算
- **AI 额度域**(`KeyNote.Core.Quota`,自 AgentEye 移植):8 类 provider 轮询(DeepSeek/智谱/MiniMax/OpenCode Go/SiliconFlow/OpenRouter/通用 OpenAI/中转站)、API Key DPAPI 加密、额度页卡片(等级色点/进度条/重置倒计时/行内刷新)、provider 管理面板(自动识别/连通测试/模型拉取)、Acrylic 悬浮小组件、全局快捷键、跨阈值告警(冷却/免打扰/恢复通知)
- **首启自动导入**:`MigrationService` 把 VigilWatt `history.db` 电量历史并入 keynote.db、AgentEye `~/.agenteye` 配置与缓存搬到数据目录(幂等标记,旧目录只读不动)
- keynote.db v7 增量表 `device_battery_samples`(电量样本,保留期 30~730 天可设);写入队列/保留期清理独立分支
- 设置页新增「设备电量 / 设备插件 / AI 额度」三组声明式字段;`FieldKind.Hotkey` 组合键编辑器(按键录入 + 手打校验回滚)
- 托盘新增「🔋 设备电量」「🤖 AI 额度」子菜单(在线设备/额度一览 + 悬浮窗开关 + 页面直达)
- 侧边栏导航 5 → 7 页,分「键鼠 / 设备 / 额度」三组 header;设备页/额度页支持自动刷新与托盘直达

### 变更

- 数据目录新增 `quota.json`(AgentEye 原格式,兼容 Python 版合并语义)/ `quota-cache/` / `device_state.json`
- 依赖新增 `System.Security.Cryptography.ProtectedData`(API Key DPAPI 加密)

## [1.2.0] - 2026-11-12

界面视觉打磨,不改数据库 schema 与统计口径。

### 变更

- 统计页:KPI 卡改 1 大 2 小主次层级(主卡数值 32px),标签与脚注跟随时间范围,修「选今日却显示累计按键」的名实不符;活跃天数脚注改活跃率
- 趋势图:补底部日期刻度(≤5 个等分点,不重叠不越界),悬停气泡配色统一走 `UiColors`
- 页头副标题改动态结论:键鼠页「近 7 天最活跃 周X HH:00」、年度热力图页「活跃 X 天 · 最长连续 Y 天」、常用按键页「全部时间 · 按使用频次排序」;「活跃 X/168 桶」改「有记录 X/168 时段」
- 侧边栏导航文案精简为两字:后台→键鼠、热力图→年度、常用按键→排行;页内标题保持完整
- 主窗口:标题栏加 18px 品牌环(与 icon.ico 同构),状态点记录中时呼吸;侧栏底部新增「暂停/继续记录」按钮与版本号,与托盘菜单共用同一暂停切换
- 设置页:保存/恢复默认反馈改图标 + 语义色(成功绿勾 / 失败红叉),保存按钮加图标
- 全局字体钉死 Segoe UI(Win10 没有 Segoe UI Variable,默认字体会宽 5~8% 导致排版走样)

## [1.1.5] - 2026-11-07

安装位置改为可选。

### 变更

- 安装器:「选择安装位置」页改为**始终显示** —— 首装默认 `%LOCALAPPDATA%\KeyNote`,升级预填原目录,均可自选(此前 `auto` 模式检测到旧安装会直接跳过该页,用户无法改位置)
- 安装器:目录页对所选位置做可写性前置校验,选到 `C:\Program Files` 等需管理员权限的目录给中文提示(免 UAC 路线不变);升级时换目录会确认「旧目录不自动清理」
- 应用内静默更新:显式传 `/DIR` 锁定当前安装目录,防止注册表 `InstallLocation` 缺失时新版装回默认目录产生第二份副本

## [1.1.4] - 2026-11-06

修复更新检查仓库地址,补齐文档计数与版本示例。

### 修复

- 应用内更新检查:仓库地址由不存在的 `AetherGazer6244/Key_Note` 修正为 `AetherGazer6244/KeyNote`(原地址返回 404,更新检查永远显示「当前已是最新」)
- README:项目结构「9 控件 / 8 服务」修正为「9 控件 / 9 服务」;发布流程示例版本号更新为 `v1.1.4`

## [1.1.3] - 2026-10-31

设置页精简(含 1.1.1 ~ 1.1.3 累计变更)。

### 变更

- 设置页:移除「退出 KeyNote」动作行,退出统一走托盘菜单
- 设置页:三处分组「高级设置」折叠区合并为页面底部统一折叠卡,数据组(备份 / CSV / 保留策略 / 备份目录 / VACUUM)整体归入
- 设置页:移除六个低频选项(启动时启动监听 / 记录鼠标点击 / 闲置自动暂停 / 内容透明度 / 热力图天数 / 自动刷新间隔);功能保留,行为按 config.json 现值运行,恢复默认不再重置这些项
- README「配置」一节同步:移除项归入「仅可手编 config.json」清单

## [1.1.0] - 2026-10-30

界面精简优化。

### 变更

- 统计页:移除键型分布胶囊行(字母 / 数字 / 符号 / 功能键 占比),页面收敛为 KPI 卡 + 趋势图;连带清理 `PillFactory` 与 `KeyNoteDb.KeyTypeDistribution()` 死代码
- 主窗口:移除 Alt+1~5 切页快捷键,消除悬停内容区时系统自动弹出的加速键气泡(「Alt+1」ToolTip)

## [1.0.0] - 2026-10-22

首个 WinUI3(C#)重写版。数据库 schema 与配置格式与历史 Python 版完全兼容,覆盖安装即可迁移。

### 重写

- Python → WinUI3(.NET 8 + Windows App SDK 1.7)全量重写,拆双工程:`src/KeyNote.App`(UI)/ `src/KeyNote.Core`(无 UI)
- 5 页侧边栏界面(统计 / 后台 / 热力图 / 常用按键 / 设置),Win11 Mica 直透整窗,永久暗色 Fluent 主题
- 纯 Win32 托盘(隐藏消息窗口 + Shell_NotifyIconW),图标按 accent 30% tint 渲染
- 图表全部改为 XAML 绘制:趋势图 / 7×24 / QWERTY / GitHub 年度热力图 / Top 10 条形图

### 新增

- 应用内更新检查:GitHub Releases 列表端点 + 组件式版本比较 + 更新说明 + 状态广播
- 应用内静默升级:流式下载 + 大小校验 → 拉起 Inno Setup 静默安装器(自动替换并重启)
- 设置页「高级设置」折叠区(10 项)、搜索过滤、无障碍名称
- 托盘实时统计 / 最近活动 / 保留策略快捷切换
- 备份 / VACUUM / CSV 导出抽为 `MaintenanceService`,托盘与设置页共用
- `Theme/UiColors.cs` 统一色板 + `App.xaml` 字号令牌;公共控件 `ChartTip` / `EmptyStateView` / `PageRefresh`

### 修复

- 钩子:线程内自维护键盘状态(Shift / CapsLock / AltGr 还原);鼠标结构体补 POINT、消息常量改按下语义、侧键按高字判定
- 钩子:安装状态可见 + 监听开关即时生效(修「显示记录中但实际没录」)
- 统计:KPI 跟随时间筛选、年度热力图不再截断、键盘热力图 / Top 10 口径统一
- 数据库:写路径改多行 INSERT、查询避开全表扫;CSV 转义、配置覆盖、迁移事务、批次丢弃、读连接 NRE
- UI:过滤残留、窗口可缩放、图表溢出、无障碍标注、日志 tag;控件不再自改自身高度、切页去双刷新、页头固定
- 托盘:`AddIcon`/`RemoveIcon` 同步 `_visible` 状态,修重复 NIM_ADD 与 hIcon 泄漏
- 发布链:安装包夹带用户数据、唯一备份被删、ISCC 静默失败三处隐患;桌面快捷方式改 `{userdesktop}`、安装包加应用图标
- 更新失败不再静默(给明确文案,不写每日时间戳)

### 统计口径变更

见 README「统计口径变更」一节:长按只计一次、大小写正确还原、鼠标点击精确计数、KPI 跟随时间筛选、两页口径统一、年度热力图按设置天数展开、非 ASCII 归入字母类、修饰键不计入统计。升级后历史数字变小属正常。

### 已知取舍

- `window_alpha`:WinUI3 不支持整窗 alpha,退化为内容区不透明度
- 便携版运行副本无法自动替换自己,更新时改为提示并打开下载页
- 发布体积约 95MB,其中约 88MB 为 Windows App SDK 运行时(瘦身见路线图)
