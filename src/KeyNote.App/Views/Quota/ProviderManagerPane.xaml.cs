using KeyNote.App.Services;
using KeyNote.Core.Quota;
using KeyNote.Core.Quota.Config;
using KeyNote.Core.Quota.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KeyNote.App.Views;

/// <summary>
/// 额度 provider 管理面板(AgentEye 设置页「API 管理」分节移植;
/// 其余分节归 KeyNote 设置页声明表):列表态 / 表单态切换。
///
/// 契约:
/// - 表单只暴露 url / key / model;名称自动取 URL 域名,kind 由 key/URL 形态
///   推断(新增时),其余高级字段走 quota.json。provider 名称唯一;
///   删除要输入名称确认;key 留空则沿用已存。
/// - BackRequested 由宿主页(QuotaPage)接线,回卡片视图。
/// </summary>
public sealed partial class ProviderManagerPane : UserControl
{
    private readonly QuotaService _app;
    private Dictionary<string, object?>? _editing; // 非 null = 编辑模式
    private bool _loading;                          // 回显期间不触发保存
    private bool _testing;                          // 「测试」进行中,防重入

    /// 返回额度页卡片视图(QuotaPage 接线)。
    public event Action? BackRequested;

    // 空串 = 自动识别(保存时走 Detect);具体 kind 由保存时定死
    private readonly List<KindOption> _kindOptions =
    [
        new(L.T("form.kindAuto"), ""),
        new(L.T("kind.deepseek"), "deepseek"),
        new(L.T("kind.zhipu"), "zhipu"),
        new(L.T("kind.minimax"), "minimax"),
        new(L.T("kind.opencode_go"), "opencode_go"),
        new(L.T("kind.siliconflow"), "siliconflow"),
        new(L.T("kind.openrouter"), "openrouter"),
        new(L.T("kind.generic_openai"), "generic_openai"),
    ];

    /// <summary>有官方默认端点的 kind(base_url 可留空,拉取层有 fallback)。</summary>
    private static readonly string[] OfficialKinds =
        ["deepseek", "zhipu", "minimax", "opencode_go", "siliconflow", "openrouter"];

    public ProviderManagerPane() : this(QuotaService.Instance) { }

    public ProviderManagerPane(QuotaService app)
    {
        InitializeComponent();
        _app = app;
        ApplyStrings();
        FKind.ItemsSource = _kindOptions;
        ShowListPane();
        app.ConfigSaved += ReloadList;
        Unloaded += (_, _) => app.ConfigSaved -= ReloadList;
    }

    /// <summary>打开面板:默认列表态,给 name 则直接进编辑表单。</summary>
    public void Open(string? editProvider = null)
    {
        ShowListPane();
        if (editProvider is not null) EditProvider(editProvider);
    }

    /// <summary>静态文案回填(API 管理分节;语言启动定型)。</summary>
    private void ApplyStrings()
    {
        PaneTitle.Text = L.T("sec.api");
        BtnAdd.Content = L.T("api.addKey");
        BtnEdit.Content = L.T("api.edit");
        BtnDelete.Content = L.T("api.delete");
        FKey.PlaceholderText = L.T("form.keyPlaceholder");
        FModel.PlaceholderText = L.T("form.modelPlaceholder");
        TxtKindLabel.Text = L.T("form.kindLabel");
        FBaseUrl.PlaceholderText = L.T("form.urlPlaceholder");
        BtnTest.Content = L.T("form.test");
        BtnSave.Content = L.T("form.save");
        BtnCancel.Content = L.T("form.cancel");
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    // ------------------------------------------------------------------
    // 列表
    // ------------------------------------------------------------------

    private void ReloadList()
    {
        var list = new List<string>();
        foreach (var p in _app.Providers().OfType<Dictionary<string, object?>>())
        {
            var name = p.TryGetValue("name", out var n) ? n : "?";
            var kind = p.TryGetValue("kind", out var k) ? k : "?";
            list.Add($"{name}    [{kind}]");
        }
        var selected = ProviderList.SelectedIndex;
        ProviderList.ItemsSource = list;
        if (selected >= 0 && selected < list.Count)
            ProviderList.SelectedIndex = selected;
    }

    private Dictionary<string, object?>? SelectedProvider()
    {
        var idx = ProviderList.SelectedIndex;
        if (idx < 0) return null;
        var arr = _app.Providers().OfType<Dictionary<string, object?>>().ToList();
        return idx < arr.Count ? arr[idx] : null;
    }

    private void ProviderList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var p = SelectedProvider();
        if (p is not null) EditProvider(TryGetName(p));
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        var p = SelectedProvider();
        if (p is null) return;
        EditProvider(TryGetName(p));
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var p = SelectedProvider();
        if (p is null) return;
        await ConfirmDeleteAsync(_app, this, TryGetName(p));
    }

    private static string TryGetName(Dictionary<string, object?> p) =>
        p.TryGetValue("name", out var n) && n is string ns ? ns : "";

    /// <summary>危险删除:输入 provider 名称确认(对齐 Python confirm_delete)。</summary>
    public static async Task ConfirmDeleteAsync(
        QuotaService app, FrameworkElement host, string name)
    {
        var input = new TextBox { PlaceholderText = L.F("delete.confirmInput", name) };
        var dialog = new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = L.F("delete.title", name),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = L.T("delete.body"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    input,
                },
            },
            PrimaryButtonText = L.T("api.delete"),
            CloseButtonText = L.T("form.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        if (input.Text.Trim() != name) return;
        var providers = ConfigStore.GetList(app.Cfg, "providers");
        var target = providers.OfType<Dictionary<string, object?>>()
            .FirstOrDefault(p => TryGetName(p) == name);
        if (target is null) return;
        providers.Remove(target);
        app.Paused.Remove(name);
        if (!app.TrySaveCfg())
            return; // 写盘失败:列表不动,让用户看到配置没存下来
        try
        {
            var baseUrl = target.TryGetValue("base_url", out var b) && b is string bs ? bs : "";
            app.Cache.RemoveProviderEntries(baseUrl, PlainKeyOf(target));
            app.Cache.LogProviderDeleted(name, baseUrl);
        }
        catch { /* 缓存清理失败不拦删除主流程 */ }
        app.SetResults(app.Results.Where(r => r.Name != name).ToList());
        _ = app.Poller.RefreshNowAsync();
    }

    private static string PlainKeyOf(Dictionary<string, object?> p) => ConfigStore.PlainKey(p);

    // ------------------------------------------------------------------
    // 表单(添加 / 编辑)
    // ------------------------------------------------------------------

    public void OpenAddForm()
    {
        _editing = null;
        ClearForm();
        _loading = true;
        try
        {
            FKind.IsEnabled = true;
            FKind.SelectedIndex = 0;
        }
        finally
        {
            _loading = false;
        }
        FormDetect.Visibility = Visibility.Collapsed;
        ShowFormPane();
    }

    public void EditProvider(string name)
    {
        var p = _app.FindProviderByName(name);
        if (p is null) return;
        _editing = p;
        LoadForm(p);
        ShowFormPane();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e) => OpenAddForm();

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => ShowListPane();

    private void ShowFormPane()
    {
        ListCard.Visibility = Visibility.Collapsed;
        FormCard.Visibility = Visibility.Visible;
        FormError.Visibility = Visibility.Collapsed;
        FormResult.Visibility = Visibility.Collapsed;
    }

    private void ShowListPane()
    {
        _editing = null;
        FormCard.Visibility = Visibility.Collapsed;
        ListCard.Visibility = Visibility.Visible;
        ReloadList();
    }

    private void ClearForm()
    {
        FBaseUrl.Text = "";
        FKey.Text = "";
        FModel.Text = "";
        FModel.ItemsSource = null;
    }

    private void LoadForm(Dictionary<string, object?> p)
    {
        _loading = true;
        try
        {
            ClearForm();
            FBaseUrl.Text = p.TryGetValue("base_url", out var b) && b is string bs ? bs : "";
            // key 留空 = 沿用已存(明文/密文都行,保存时解好放内存)
            FKey.Text = QuotaService.PlainKey(p);
            FModel.Text = p.TryGetValue("model", out var m) && m is string ms ? ms : "";
            // 编辑态 kind 定死(改 kind 会破坏 extra 语义):下拉禁用只回显
            var kind = p.TryGetValue("kind", out var k) && k is string ks ? ks : "";
            FKind.IsEnabled = false;
            FKind.SelectedIndex = Math.Max(0,
                _kindOptions.FindIndex(o => o.Value == kind));
        }
        finally
        {
            _loading = false;
        }
        FormDetect.Visibility = Visibility.Collapsed;
        FormResult.Visibility = Visibility.Collapsed;
    }

    private void ShowFormError(string? msg)
    {
        if (msg is null)
        {
            FormError.Visibility = Visibility.Collapsed;
            return;
        }
        FormError.Text = msg;
        FormError.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------------
    // 类型下拉 + 识别预览(纯本地正则,无网络)
    // ------------------------------------------------------------------

    private string SelectedKind =>
        FKind.SelectedIndex >= 0 ? _kindOptions[FKind.SelectedIndex].Value : "";

    private string KindLabel(string kind) => L.T("kind." + kind);

    private void FKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        // 选官方类型时把默认端点填进 URL:仅在"空 or 还是别的官方默认"时,
        // 用户手敲的自定义 URL 不动
        var kind = SelectedKind;
        if (OfficialKinds.Contains(kind))
        {
            var def = ConfigStore.DefaultBaseUrl(kind);
            var cur = FBaseUrl.Text.Trim();
            if (def.Length > 0 &&
                (cur.Length == 0 || OfficialKinds.Any(k => ConfigStore.DefaultBaseUrl(k) == cur)))
                FBaseUrl.Text = def;
        }
        UpdateDetectPreview();
        BlurFocus(FKind);
    }

    private void FBaseUrl_LostFocus(object sender, RoutedEventArgs e) => UpdateDetectPreview();

    private void FKey_LostFocus(object sender, RoutedEventArgs e) => UpdateDetectPreview();

    /// <summary>自动识别态且填了内容时,实时展示 Detect 结论(JWT 撞形等提示不再丢失)。</summary>
    private void UpdateDetectPreview()
    {
        if (_editing is not null || SelectedKind.Length > 0)
        {
            FormDetect.Visibility = Visibility.Collapsed;
            return;
        }
        var baseUrl = FBaseUrl.Text.Trim();
        var key = FKey.Text.Trim();
        if (baseUrl.Length == 0 && key.Length == 0)
        {
            FormDetect.Visibility = Visibility.Collapsed;
            return;
        }
        var d = Detect.Run(key: key, hintUrl: baseUrl);
        FormDetect.Text = L.F("form.detect", KindLabel(d.Kind), d.Notes);
        FormDetect.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 把焦点从下拉框挪回页面:WinUI 里带焦点的 ComboBox 会被滚轮改选项,
    /// 焦点交还页面后,滚轮恢复滚动页面。
    /// </summary>
    private void BlurFocus(ComboBox combo)
    {
        try
        {
            combo.IsDropDownOpen = false;
            Focus(FocusState.Programmatic);
        }
        catch { /* 焦点挪不动也无碍:只是滚轮可能还会改下拉 */ }
    }

    // ------------------------------------------------------------------
    // 测试 Key:表单当前值跑一次真实拉取(不落盘),顺带填充模型下拉
    // ------------------------------------------------------------------

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (_testing) return;
        var baseUrl = FBaseUrl.Text.Trim();
        var key = FKey.Text.Trim();
        var model = FModel.Text.Trim();
        var kind = SelectedKind.Length > 0 ? SelectedKind
            : Detect.Run(key: key, hintUrl: baseUrl).Kind;
        if (baseUrl.Length == 0 && !OfficialKinds.Contains(kind))
        {
            ShowFormError(L.T("form.errEmptyUrl"));
            return;
        }

        _testing = true;
        BtnTest.IsEnabled = false;
        BtnSave.IsEnabled = false;
        ShowFormResult(null, L.T("form.testing"));
        try
        {
            var outcome = await ProviderTest.RunAsync(kind, baseUrl, key, model);
            var line = L.F(outcome.Ok ? "form.testOk" : "form.testFail", outcome.Message);
            if (outcome.Models.Count > 0)
            {
                FillModelCombo(outcome.Models);
                line += " · " + L.F("form.modelsLoaded", outcome.Models.Count);
            }
            ShowFormResult(outcome.Ok, line);
        }
        finally
        {
            _testing = false;
            BtnTest.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }
    }

    /// <summary>结果行:null=中性(进行中),true=绿,false=红。</summary>
    private void ShowFormResult(bool? ok, string msg)
    {
        FormResult.Foreground = ok is { } flagged
            ? QuotaRes.Brush(this, flagged ? "AeOkBrush" : "AeErrorBrush")
            : QuotaRes.Brush(this, "AeTextSecondaryBrush");
        FormResult.Text = msg;
        FormResult.Visibility = msg.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>模型下拉填充:保留用户已输入/已选的值。</summary>
    private void FillModelCombo(IReadOnlyList<string> models)
    {
        var current = FModel.Text;
        // 填充是程序动作,不算用户选择:别触发 BlurFocus 抢走正在输入的焦点
        _loading = true;
        try { FModel.ItemsSource = models; }
        finally { _loading = false; }
        FModel.Text = current;
    }

    private void FModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        BlurFocus(FModel);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var baseUrl = FBaseUrl.Text.Trim();
        var key = FKey.Text.Trim();
        var model = FModel.Text.Trim();

        // 新增:下拉选了具体 kind 就不跑 Detect;编辑:kind 保持不变
        var kind = _editing is not null
            ? _editing.TryGetValue("kind", out var k0) && k0 is string ks0 ? ks0 : ""
            : SelectedKind.Length > 0 ? SelectedKind
            : Detect.Run(key: key, hintUrl: baseUrl).Kind;

        // 官方 kind 拉取层对空 URL 有 fallback,允许留空;通用兼容必须给
        if (baseUrl.Length == 0 && !OfficialKinds.Contains(kind))
        {
            ShowFormError(L.T("form.errEmptyUrl"));
            return;
        }

        // 名称自动取 URL 域名(留空时用类型名),重名追加 -2/-3;编辑时保持原名不变
        // (改名会让暂停集合与托盘最紧张行的索引错位)
        var name = _editing is not null ? TryGetName(_editing)
            : UniqueName(baseUrl.Length > 0 ? NameFromUrl(baseUrl) : KindLabel(kind));

        Dictionary<string, object?> entry;
        if (_editing is not null)
        {
            entry = _editing;
        }
        else
        {
            entry = new Dictionary<string, object?>
            {
                ["id"] = Guid.NewGuid().ToString("N")[..12],
                ["extra"] = new Dictionary<string, object?>(),
            };
            ConfigStore.GetList(_app.Cfg, "providers").Add(entry);
        }

        // key 处理:留空则沿用内存里已有的;env 覆盖在 ApplyEnvV2。
        var existingKey = QuotaService.PlainKey(entry);
        var newKey = key.Length > 0 ? key
            : existingKey.Length > 0 ? existingKey
            : "";
        if (_editing is null)
        {
            // kind 只在新增时定死;已有条目改 kind 会破坏 extra 语义,走 config
            entry["kind"] = kind;
        }
        entry["name"] = name;
        SetOrRemove(entry, "base_url", baseUrl);
        if (newKey.Length > 0)
            entry["key"] = newKey;
        SetOrRemove(entry, "model", model);
        entry.Remove("_key_from_env");

        var err = _app.SaveCfgError();
        if (err is not null)
        {
            // 写盘失败:把刚加的新条目回滚,别让内存态和磁盘态分叉
            if (_editing is null) ConfigStore.GetList(_app.Cfg, "providers").Remove(entry);
            ShowFormError(L.F("form.errSave", err));
            return;
        }
        ShowListPane();
        _ = _app.Poller.RefreshNowAsync();
        await Task.CompletedTask;
    }

    /// <summary>URL → 展示名:优先域名,畸形输入退化为最后一段。</summary>
    private static string NameFromUrl(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) && u.Host.Length > 0)
            return u.Host;
        var tail = baseUrl.Trim().TrimEnd('/').Split('/').LastOrDefault() ?? "";
        return tail.Length > 0 ? tail : "provider";
    }

    /// <summary>域名撞车时追加 -2/-3,保证 provider 名称唯一。</summary>
    private string UniqueName(string baseName)
    {
        var candidate = baseName;
        for (var n = 2; _app.FindProviderByName(candidate) is not null; n++)
            candidate = $"{baseName}-{n}";
        return candidate;
    }

    private static void SetOrRemove(Dictionary<string, object?> d, string key, string value)
    {
        if (value.Length > 0) d[key] = value;
        else d.Remove(key);
    }

    private sealed record KindOption(string Text, string Value);
}
