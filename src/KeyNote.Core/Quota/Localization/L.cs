namespace KeyNote.Core.Quota;

/// 代码侧界面文案查表(KeyNote 中文锁定版):原 AgentEye zh/en 双字典精简为仅 zh,
/// L.T / L.F 签名与键名不变,移植代码零改动。
public static class L
{
    /// <summary>当前语言:KeyNote 恒为 zh-Hans(保留 setter 形状以兼容原调用)。</summary>
    public static string Lang { get; private set; } = "zh-Hans";

    public static void Init(string lang) => Lang = "zh-Hans";

    /// <summary>取文案:缺键回退键名自身(好排查)。</summary>
    public static string T(string key)
    {
        return Zh.TryGetValue(key, out var v) ? v : key;
    }

    /// 插值的格式化:fmt("{0} 已暂停", name)
    public static string F(string key, params object?[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, string> Zh = new()
    {
        // —— 设置页:分节 ——
        ["sec.appearance"] = "外观",
        ["sec.behavior"] = "行为",
        ["sec.network"] = "网络",
        ["sec.alert"] = "告警",
        ["sec.api"] = "API 管理",
        ["sec.about"] = "关于软件",

        // —— 设置页:外观 ——
        ["theme.title"] = "外观模式",
        ["theme.subtitle"] = "更改软件的深浅色模式",
        ["theme.auto"] = "跟随系统",
        ["theme.light"] = "浅色",
        ["theme.dark"] = "深色",

        // —— 设置页:开机自启动 ——
        ["autostart.title"] = "开机自启动",
        ["autostart.subtitle"] = "开机自动在后台运行(静默进托盘,不弹主面板)",
        ["autostart.on"] = "已启用",
        ["autostart.off"] = "已关闭",

        // —— 设置页:关窗行为 ——
        ["close.tray.title"] = "关闭主面板时",
        ["close.tray.subtitle"] = "点关闭按钮隐藏到托盘,后台继续运行;可用托盘或快捷键唤回",
        ["close.tray.on"] = "隐藏到托盘",
        ["close.tray.off"] = "直接退出",

        // —— 设置页:语言 ——
        ["lang.title"] = "语言",
        ["lang.subtitle"] = "界面语言(切换后重启生效)",

        // —— 设置页:网络 + Core:Http 选路(1.5.2) ——
        ["net.proxyModeTitle"] = "代理模式",
        ["net.proxyModeSub"] = "默认直连,连不上才回退系统代理",
        ["net.proxyMode.direct"] = "直连优先(默认)",
        ["net.proxyMode.auto"] = "跟随系统代理(代理优先)",
        ["net.proxyMode.system"] = "严格跟随系统代理",
        ["net.proxyDead"] = "系统代理 {0} 无响应({1})",
        ["net.directFailed"] = "直连失败:{0}",
        ["net.reason.refused"] = "连接被拒绝",
        ["net.reason.dns"] = "域名解析失败",
        ["net.reason.unreachable"] = "网络不可达",

        // —— 设置页:快捷键 ——
        ["hotkey.title"] = "唤醒快捷键",
        ["hotkey.subtitle"] = "设置全局唤醒和隐藏主面板的快捷键",
        ["hotkey.clear"] = "清除",

        // —— 设置页:告警 ——
        ["alert.title"] = "告警",
        ["alert.subtitle"] = "额度低于阈值时弹系统通知",
        ["alert.on"] = "告警开",
        ["alert.off"] = "告警关",
        ["alert.warnPct"] = "剩余百分比 ≤",
        ["alert.warnPctSuffix"] = "% 记为黄",
        ["alert.critAmount"] = "剩余额度 ≤",
        ["alert.critSuffix"] = "元 记为红",

        // —— 设置页:API 管理 ——
        ["api.keysLabel"] = "已接入的 Key",
        ["api.addKey"] = "添加 Key",
        ["api.edit"] = "编辑",
        ["api.delete"] = "删除",
        ["form.kindLabel"] = "类型",
        ["form.kindAuto"] = "自动识别",
        ["form.urlPlaceholder"] = "官方端点可留空;中转站填完整 URL",
        ["form.detect"] = "识别:{0} — {1}",
        ["form.keyPlaceholder"] = "粘贴 key;留空则沿用已存 key",
        ["form.modelPlaceholder"] = "如 deepseek-chat;额度端点不可用时用真实对话探测",
        ["form.save"] = "保存",
        ["form.cancel"] = "取消",
        ["form.test"] = "测试",
        ["form.testing"] = "测试中…",
        ["form.testOk"] = "测试通过:{0}",
        ["form.testFail"] = "测试失败:{0}",
        ["form.modelsLoaded"] = "已拉到 {0} 个模型",
        ["pt.ok"] = "key 有效",
        ["pt.err"] = "测试异常:{0}",

        // —— provider 类型名 ——
        ["kind.deepseek"] = "DeepSeek",
        ["kind.zhipu"] = "智谱 GLM",
        ["kind.minimax"] = "MiniMax",
        ["kind.opencode_go"] = "OpenCode Go",
        ["kind.siliconflow"] = "SiliconFlow(硅基流动)",
        ["kind.openrouter"] = "OpenRouter",
        ["kind.generic_openai"] = "通用 OpenAI 兼容",

        // —— 设置页:高级 ——
        ["adv.sizeTitle"] = "主窗口面板大小",
        ["adv.sizeSubtitle"] = "主面板初始尺寸(320×400 ~ 800×1200 像素)",
        ["adv.width"] = "宽",
        ["adv.height"] = "高",
        ["adv.resetSize"] = "恢复默认 420×560",
        ["adv.adaptive.title"] = "自适应轮询",
        ["adv.adaptive.subtitle"] = "有紧张行时加密轮询(最快 30 秒),持续失败时退避",
        ["adv.adaptive.on"] = "已启用",
        ["adv.adaptive.off"] = "已关闭",

        // —— 设置页:关于软件 ——
        ["about.title"] = "关于软件",
        ["about.subtitle"] = "查看软件信息、作者与项目详情",
        ["about.details"] = "查看详情",

        // —— 设置页:更新 ——
        ["update.check"] = "检查更新",
        ["update.download"] = "下载并更新",
        ["update.ignore"] = "忽略此版本",

        // —— 主窗口 ——
        ["win.backTooltip"] = "返回总览",
        ["win.settingsTooltip"] = "设置",
        ["win.titleSettings"] = "AgentEye — 设置",

        // —— 总览页 ——
        ["ov.title"] = "总览",
        ["ov.empty"] = "尚未配置 provider",
        ["ov.openSettings"] = "打开设置",
        ["ov.notRefreshed"] = "尚未刷新",
        ["ov.lastRefresh"] = "上次刷新 {0}",
        ["ov.count"] = "{0} 个 provider",
        ["ov.paused"] = "已暂停",
        ["ov.level.ok"] = "正常",
        ["ov.level.warn"] = "注意",
        ["ov.level.critical"] = "紧张",
        ["ov.unconfigured"] = "未配置",
        ["ov.error"] = "错误",
        ["ov.resume"] = "恢复轮询",
        ["ov.pause"] = "暂停此 provider",
        ["ov.ctxRefresh"] = "立即刷新",
        ["ov.resetIn"] = "{0}后重置",
        ["ov.expandHint"] = "点击卡片查看详情",

        // —— 卡片展开详情 ——
        ["ov.detail.website"] = "官网",
        ["ov.detail.models"] = "可用模型",
        ["ov.detail.modelsCount"] = "{0} 个模型",
        ["ov.detail.usage"] = "用量明细",
        ["ov.detail.used"] = "已用",
        ["ov.detail.total"] = "总量",
        ["ov.detail.today"] = "今日已用",
        ["ov.detail.remaining"] = "剩余",
        ["ov.detail.reset"] = "重置时间",
        ["ov.detail.endpoint"] = "配额接口",
        ["ov.detail.updated"] = "上次刷新",
        ["ov.detail.api"] = "接口地址",
        ["ov.detail.estimate"] = "估算值(按公开档位换算)",

        // —— 倒计时文案(fmt.resetWord 是 detail 里"重置"二字的标记,用于剥离) ——
        ["fmt.leftDays"] = "{0}天{1}时",
        ["fmt.leftHours"] = "{0}时{1}分",
        ["fmt.leftMinutes"] = "{0}分",
        ["fmt.resetWord"] = "重置",

        // —— 热键状态(设置页回显/弹窗) ——
        ["hotkey.svcDown"] = "热键服务不可用",
        ["hotkey.applyFailed"] = "热键设置失败",
        ["hotkey.notEffective"] = "快捷键未生效",
        ["hotkey.invalidCombo"] = "组合无效",
        ["hotkey.invalidDefault"] = "请按字母/数字/功能键",
        ["hotkey.invalidExample"] = "{0}(例如 Ctrl+Shift+A)",
        ["hotkey.stDisabled"] = "未启用",
        ["hotkey.stSvcClosed"] = "热键服务已关闭",
        ["hotkey.stNoResponse"] = "热键服务无响应",
        ["hotkey.stTimeout"] = "热键设置超时",
        ["hotkey.stOff"] = "已关闭",
        ["hotkey.stInvalid"] = "组合无效:{0}",
        ["hotkey.stInUse"] = "注册失败(该组合已被其它程序占用,可换个组合)",
        ["hotkey.stRegError"] = "注册失败(错误 {0})",
        ["hotkey.stActive"] = "已生效:{0}",
        ["hotkey.startupWarn"] = "{0}。可在设置页更换组合。",

        // —— 告警通知 ——
        ["notify.titleOne"] = "{0} 额度告警",
        ["notify.titleMany"] = "{0} 个 provider 额度告警",
        ["notify.body"] = "剩 {0}% · {1}",
        ["notify.recoverTitleOne"] = "「{0}」额度已恢复",
        ["notify.recoverTitleMany"] = "{0} 个 provider 额度已恢复",
        ["notify.errorTitleOne"] = "{0} 连续拉取失败",
        ["notify.errorTitleMany"] = "{0} 个 provider 连续拉取失败",

        // —— 设置页:告警免打扰(1.6.0) ——
        ["alert.quiet.title"] = "免打扰时段",
        ["alert.quiet.subtitle"] = "该时段内只记录不弹通知(跨午夜也可)",
        ["alert.quiet.on"] = "已启用",
        ["alert.quiet.off"] = "已关闭",
        ["alert.quiet.startLabel"] = "开始",
        ["alert.quiet.endLabel"] = "结束",

        // —— 设置页弹窗/错误 ——
        ["autostart.errorTitle"] = "无法设置开机自启动",
        ["autostart.errorDefault"] = "写入启动项失败",
        ["startup.errOpen"] = "无法打开启动项注册表",
        ["startup.errDenied"] = "写入启动项被拒绝(权限不足?)",
        ["startup.errWrite"] = "写入启动项失败:{0}",
        ["form.errEmptyUrl"] = "Base URL 不能为空",
        ["form.errSave"] = "保存失败:{0}",

        // —— 删除确认 ——
        ["delete.title"] = "删除 {0}",
        ["delete.confirmInput"] = "输入 {0} 以确认",
        ["delete.body"] = "此操作会从配置里移除该 provider 与其 key。",
        ["delete.mismatch"] = "名称不匹配，请确认输入「{0}」。",
        ["delete.notSelected"] = "请先选中要删除的 provider。",
        ["delete.notFound"] = "配置中已找不到这个 provider，请刷新列表后重试。",
        ["delete.errorTitle"] = "删除失败",
        ["delete.errSave"] = "配置写盘失败，已恢复原 provider：{0}",
        ["delete.errUnexpected"] = "删除时发生错误：{0}",
        ["delete.done"] = "已删除 {0}",

        // —— 更新 ——
        ["update.checking"] = "正在检查更新…",
        ["update.downloading"] = "正在下载并更新… {0}%",
        ["update.versionLine"] = "当前版本 v{0}(更新源 github.com/{1}/{2})",
        ["update.checkFailed"] = "检查更新失败:{0}",
        ["update.ignoredSuffix"] = "{0}(已忽略此版本)",
        ["update.toastTitle"] = "发现新版本",
        ["update.noneFound"] = "尚未检查到新版本",
        ["update.noAsset"] = "release 里没有可用的 AgentEye_Setup.exe 安装包",
        ["update.dirReadOnly"] = "安装目录不可写({0}),请重新运行安装器手动修复",
        ["update.downloadingAsset"] = "正在下载 {0}…",
        ["update.badChecksum"] = "安装包校验失败(sha256 不匹配),已放弃更新",
        ["update.launching"] = "正在启动安装器,应用将被关闭并自动重启…",
        ["update.cancelled"] = "已取消更新",
        ["update.failed"] = "更新失败:{0}",

        // —— 托盘 ——
        ["tray.open"] = "打开主面板",
        ["tray.widget"] = "切换小组件",
        ["tray.settings"] = "设置",
        ["tray.exit"] = "退出",
        ["tray.tooltipEmpty"] = "AgentEye — 没有已接入的 provider",
        ["tray.tooltipWorst"] = "AgentEye — {0} 已用 {1}%",
        ["tray.tooltipLevel"] = "AgentEye — {0}({1})",

        // —— 小组件 ——
        ["widget.empty"] = "未接入 provider",

        // —— Core:ConfigStore ——
        ["cfg.plaintextPrefix"] = "拒绝写盘:以下 provider 的 key 未能加密(DPAPI 不可用?)——",
        ["cfg.providersNotList"] = "配置里 providers 不是列表(实际 {0}),已置空",
        ["cfg.providersBadEntries"] = "配置里 providers 有 {0} 个非对象条目,已丢弃(provider 必须是 {{...}} 对象)",
        ["cfg.migrateDpapi"] = "DPAPI 不可用,已中止 v1 迁移:备份会写出明文密钥",
        ["cfg.migrateBackupEnc"] = "v1 备份加密失败,已中止迁移:不愿写出明文密钥",
        ["cfg.migrateBackupWrite"] = "无法写入 v1 备份({0}),已中止迁移以免密钥丢失",
        ["cfg.noBackup"] = "没有备份文件:{0}",
        ["cfg.notOurBackup"] = "{0} 不是本程序写的加密备份",
        ["cfg.backupDecrypt"] = "{0} 解密失败:DPAPI 绑定当前用户,换账户/换机器无法恢复",
        ["cfg.backupNotJson"] = "{0} 解密后的内容不是合法 JSON",
        ["cfg.saveLocked"] = "配置保存失败(文件被占用?):{0}",
        ["cfg.readLocked"] = "无法读取配置文件 {0}({1}: {2})。请关闭正在占用它的程序(记事本/编辑器/另一个 AgentEye 实例)后重试;为避免覆盖你的配置,本次不做任何写入。",
        ["cfg.badUtf8"] = "字节流不是合法 UTF-8({0})",
        ["cfg.badJson"] = "根节点不是对象或 JSON 解析失败",
        ["cfg.versionTooNew"] = "配置版本 schema_version={0} 高于本程序支持的 {1}。请升级 AgentEye,或手动改回 2。",
        ["cfg.versionBadType"] = "schema_version 类型异常: {0}",
        ["cfg.renameFail"] = "(重命名失败,本次不会覆盖原文件)",

        // —— Core:HotkeySpec ——
        ["hk.empty"] = "快捷键为空",
        ["hk.unknownKey"] = "无法识别的按键:{0}",
        ["hk.multiMain"] = "只能有一个主键(如 A / F2 / Space)",
        ["hk.noMain"] = "缺少主键(如 A / F2 / Space)",
        ["hk.noMod"] = "至少需要一个修饰键(Ctrl / Alt / Shift / Win)",
        ["hk.invalidKey"] = "无效按键",
        ["hk.modOnly"] = "请按字母/数字/功能键,不能只按修饰键",
        ["hk.unsupported"] = "暂不支持的按键(0x{0})",

        // —— Core:provider 通用 ——
        ["pr.noKey"] = "未配置 api_key",
        ["pr.netErr"] = "网络错误: 网络异常",
        ["pr.netErrWhy"] = "网络错误: {0}",
        ["pr.netTimeoutWhy"] = "请求超时",
        ["pr.keyInvalid"] = "key 无效 (HTTP {0})",
        ["pr.notJson"] = "响应非 JSON",
        ["pr.apiError"] = "接口报错: {0}",
        ["pr.structUnknown"] = "响应结构未识别",
        ["pr.detailGranted"] = "赠金 {0}{1} · 充值 {2}{3}",
        ["pr.detailRemainUsd"] = "剩余 ${0} · 已用 ${1}",
        ["pr.detailBalance"] = "余额 ¥{0}(总资产 ¥{1})",
        ["pr.reset"] = " · 重置 {0}",

        // —— Core:SiliconFlow / OpenRouter ——
        ["sf.noBalance"] = "可用余额为 0(免费账号或未充值)",
        ["or.noBalance"] = "无额度记录(免费账号或未充值)",

        // —— Core:DeepSeek ——
        ["ds.noBalanceInfos"] = "响应无 balance_infos",
        ["ds.noBalance"] = " · 余额不可用",

        // —— Core:GenericOpenAI ——
        ["gen.noBaseUrl"] = "缺少 base_url",
        ["gen.noKey"] = "缺少 key",
        ["gen.labelNet"] = "{0}: 网络错误",
        ["gen.labelNotJson"] = "{0}: 非JSON",
        ["gen.labelStruct"] = "{0}: 结构未识别",
        ["gen.probeNet"] = "模型探测: 网络错误",
        ["gen.probeOk"] = "key+model 可用 (模型探测通过)",
        ["gen.probe404"] = "模型探测: 端点或模型不存在 (HTTP 404)",
        ["gen.probeHttp"] = "模型探测: HTTP {0}",
        ["gen.noModels"] = "该 provider 无 /v1/models 端点",
        ["gen.detailUsedTotal"] = "已用 ${0} · 总额 ${1}",
        ["gen.detailUsedLimit"] = "已用 ${0} · 额度 ${1}",

        // —— Core:MiniMax ——
        ["mm.noQuota"] = "套餐未启用或无可用额度项",
        ["mm.noPct"] = "无百分比字段",
        ["mm.timesLeft"] = " {0}/{1} 次",
        ["mm.weekly"] = " · 周 {0}%",
        ["mm.moreModels"] = "…共{0}个模型",
        ["mm.disabled"] = "({0}项未启用)",

        // —— Core:OpenCodeGo ——
        ["oc.5h"] = "5h",
        ["oc.week"] = "周",
        ["oc.month"] = "月",
        ["oc.seg"] = "{0} 剩≈${1}/${2}",
        ["oc.noWindows"] = "响应无窗口数据",
        ["oc.resetDone"] = "已重置",

        // —— Core:Detect ——
        ["det.minimaxCp"] = "key 以 'sk-cp-' 开头 → MiniMax Coding Plan",
        ["det.urlDeepseek"] = "URL 含 'deepseek.com' → DeepSeek 官方",
        ["det.urlZhipu"] = "URL 含 'bigmodel.cn' → 智谱 GLM",
        ["det.urlMinimax"] = "URL 含 'minimaxi.com' → MiniMax",
        ["det.urlMinimaxCn"] = "URL 含 'minimax.cn' → MiniMax",
        ["det.urlOc"] = "URL 含 'opencode.ai' → OpenCode Go",
        ["det.urlSiliconflow"] = "URL 含 'siliconflow' → SiliconFlow(硅基流动)",
        ["det.urlOpenrouter"] = "URL 含 'openrouter.ai' → OpenRouter",
        ["det.urlV1"] = "URL 含 '/v1' → 通用 OpenAI 兼容",
        ["det.fallbackGeneric"] = "未识别 URL,按通用 OpenAI 兼容处理",
        ["det.needMore"] = "未提供 key 与 URL,需要更多信息",
        ["det.jwtAmbiguous"] = "key 是 JWT 形态,可能是 OpenCode Go,也可能是 MiniMax 的 token_plan key;填 URL 提示可纠正,新增后请核对",
        ["det.skNeedsUrl"] = "key 以 'sk-' 开头,疑似 OpenAI 兼容,需要 base_url",
        ["det.unknownKeyShape"] = "无法识别 key 形态 (长度 {0}),按通用处理,请提供 base_url",

        // —— Core:ProviderRegistry ——
        ["reg.paused"] = "已暂停轮询",
        ["reg.skippedRound"] = "已退出,本轮未拉取",
        ["reg.keyDecrypt"] = "key_enc 解密失败,请在设置里重新填写 key",
        ["reg.noKey"] = "未配置 key",
        ["reg.unknownKind"] = "未知 provider kind: {0}",
        ["reg.noResult"] = "拉取任务未返回结果",

        // —— Core:Relay ——
        ["rl.net"] = "网络异常",
        ["rl.noToken"] = "响应无 access_token",
        ["rl.noCfg"] = "未配置 base_url/token",
        ["rl.loginFailed"] = "登录失败: {0}",
        ["rl.pathNet"] = "{0}: 网络异常",
        ["rl.keyInvalidFlag"] = "key 无效 (isValid=false)",
        ["rl.unitFallback"] = "额度",
        ["rl.today"] = "今日 {0}{1}",
        ["rl.reqs"] = "{0} 次",
        ["rl.paidTotal"] = "累计实付 {0}{1}",
        ["rl.sumRemainUsd"] = "剩余 ${0}",
        ["rl.sumRemainRaw"] = "剩余 {0}{1}",
        ["rl.keyCount"] = " · {0} 个 key",
        ["rl.sumTotalUsd"] = " · 总量 ${0}",
        ["rl.sumTotalRaw"] = " · 总量 {0}",

        // —— Core:Zhipu ——
        ["zp.week"] = "周",
        ["zp.noWindows"] = "无 token 额度窗口",
        ["zp.seg"] = "{0} 剩{1}%",
        ["zp.segReset"] = "(重置{0})",
        ["zp.plan"] = " · {0}套餐",

        // —— Core:UpdateChecker ——
        ["upd.net"] = "连不上 GitHub,检查更新失败(网络问题)",
        ["upd.noReleases"] = "仓库还没有发布版本,暂时无法检查更新",
        ["upd.ratelimit"] = "GitHub 接口限流(未登录每小时 60 次),稍后再试",
        ["upd.http"] = "GitHub 返回 HTTP {0}",
        ["upd.badFormat"] = "GitHub 返回的内容不是预期的 release 格式",
        ["upd.verCompare"] = "版本号无法比较(当前 {0},远端 {1})",
        ["upd.upToDate"] = "已是最新版本 v{0}",
        ["upd.noAsset"] = "发现新版本 v{0},但 release 里没有 {1} 安装包",
        ["upd.found"] = "发现新版本 v{0}(当前 v{1})",

        // —— Core:UpdateDownloader ——
        ["dl.http"] = "下载失败 HTTP {0}",
        ["dl.incomplete"] = "下载不完整({0}/{1} 字节)",

        // —— 语言切换 ——
        ["lang.saved"] = "语言设置已保存",
        ["lang.restart.msg"] = "重启应用后生效。要现在重启吗?",
        ["lang.restart.now"] = "立即重启",
        ["common.close"] = "好",
        ["common.error"] = "提示",
        ["app.restartFailed"] = "自动重启失败,请手动重启应用",
    };
}
