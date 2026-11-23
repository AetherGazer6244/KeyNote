using KeyNote.App.Services;
using KeyNote.Core.Quota;
using KeyNote.Core.Quota.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace KeyNote.App.Views;

/// <summary>
/// 额度页:provider 行卡片化直排(AgentEye 总览页移植):
/// 等级色点 pill + 主值 + 4px 等级条 + 重置倒计时 chip;行右键菜单
/// 立即刷新 / 暂停 / 编辑 / 删除,点头部展开详情(手风琴,同时只开一张)。
/// 「管理」进入 ProviderManagerPane(列表 / 表单)。
/// </summary>
public sealed partial class QuotaPage : Page, IRefreshable
{
    private readonly QuotaService _app;

    /// <summary>手风琴:当前展开的 provider 名(同时只开一张)。</summary>
    private string? _expanded;

    public QuotaPage()
    {
        InitializeComponent();
        _app = QuotaService.Instance;
        SubtitleText.Text = "provider 用量、配额与重置倒计时";
        ManagerPane.BackRequested += () => ShowCards();
        Loaded += (_, _) =>
        {
            _app.ResultsChanged += OnResultsChanged;
            RefreshData();
        };
        Unloaded += (_, _) => _app.ResultsChanged -= OnResultsChanged;
    }

    /// <summary>自动刷新与切页回访:本地重建(远端数据由轮询循环异步更新)。</summary>
    public void RefreshData() => OnResultsChanged();

    private void Refresh_Click(object sender, RoutedEventArgs e)
        => _ = _app.Poller.RefreshNowAsync();

    private void Manage_Click(object sender, RoutedEventArgs e) => ShowManager(null);

    /// <summary>切到管理面板(可选直接进入某 provider 编辑表单)。</summary>
    private void ShowManager(string? editProvider)
    {
        ManagerPane.Open(editProvider);
        ManagerPane.Visibility = Visibility.Visible;
        CardsScroll.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        FooterGrid.Visibility = Visibility.Collapsed;
        ManageButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>回卡片视图。</summary>
    private void ShowCards()
    {
        ManagerPane.Visibility = Visibility.Collapsed;
        CardsScroll.Visibility = Visibility.Visible;
        FooterGrid.Visibility = Visibility.Visible;
        ManageButton.Visibility = Visibility.Visible;
        OnResultsChanged();
    }

    private void OnResultsChanged()
    {
        var rows = _app.Results;
        FooterUpdated.Text = _app.Poller.LastRefresh is { } t
            ? L.F("ov.lastRefresh", t.ToString("HH:mm:ss"))
            : L.T("ov.notRefreshed");
        FooterCount.Text = rows.Count == 0 ? "" : L.F("ov.count", rows.Count);

        // 展开中的 provider 被删了 → 收起
        if (_expanded is not null && !rows.Any(r => r.Name == _expanded))
            _expanded = null;

        RowsHost.Children.Clear();
        if (rows.Count == 0)
            EmptyState.Show(L.T("ov.empty"), "点右上「管理」添加 provider", "\uE774");
        else
            EmptyState.Hide();

        foreach (var row in rows)
            RowsHost.Children.Add(BuildRow(row));
    }

    private Brush LevelBrush(string level) => QuotaRes.Brush(this, level switch
    {
        Levels.Ok => "AeOkBrush",
        Levels.Warn => "AeWarnBrush",
        Levels.Critical => "AeCriticalBrush",
        Levels.Error => "AeErrorBrush",
        Levels.Unconfigured => "AePausedBrush",
        Levels.Paused => "AePausedBrush",
        _ => "AePausedBrush",
    });

    private Brush TextBrush() => QuotaRes.Brush(this, "AeTextPrimaryBrush");
    private Brush DimBrush() => QuotaRes.Brush(this, "AeTextSecondaryBrush");

    /// <summary>等级色的淡版,当进度条轨道用(0% 剩余时也能看出等级)。</summary>
    private static Brush LevelTrack(Brush level) => level is SolidColorBrush s
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(28, s.Color.R, s.Color.G, s.Color.B))
        : level;

    /// <summary>kind → Segoe Fluent Icons 字形,未知 kind 落其它。</summary>
    private static string KindGlyph(string kind) => kind switch
    {
        "deepseek" => "\uE99A",
        "zhipu" => "\uE943",
        "minimax" => "\uE945",
        "opencode_go" => "\uE756",
        "yuanxuai" => "\uE825",
        "relay" => "\uE968",
        _ => "\uE774",
    };

    /// <summary>名称旁等级 pill 的配色与文案;暂停行显示"已暂停"。</summary>
    private (Brush Brush, string Text) LevelPillInfo(ProviderResult row)
    {
        if (row.Paused || row.Level == Levels.Paused)
            return (QuotaRes.Brush(this, "AePausedBrush"), L.T("ov.paused"));
        return row.Level switch
        {
            Levels.Ok => (QuotaRes.Brush(this, "AeOkBrush"), L.T("ov.level.ok")),
            Levels.Warn => (QuotaRes.Brush(this, "AeWarnBrush"), L.T("ov.level.warn")),
            Levels.Critical => (QuotaRes.Brush(this, "AeCriticalBrush"), L.T("ov.level.critical")),
            Levels.Error => (QuotaRes.Brush(this, "AeErrorBrush"), L.T("ov.error")),
            _ => (QuotaRes.Brush(this, "AePausedBrush"), L.T("ov.unconfigured")),
        };
    }

    /// <summary>等级 pill:淡等级底 + 等级色字(与进度条轨道同一套淡版算法)。</summary>
    private FrameworkElement BuildLevelPill(Brush brush, string text)
    {
        var tint = brush is SolidColorBrush s
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(30, s.Color.R, s.Color.G, s.Color.B))
            : brush;
        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 1),
            Background = tint,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = brush },
        };
    }

    /// <summary>重置倒计时 chip 的文案;没有重置时间(或已过期)返回 null。</summary>
    private static string? ResetChipText(ProviderResult row)
    {
        if (row.ResetAtMs is not { } ms) return null;
        var left = ResetTime.FormatLeft(ms);
        return left is null ? null : L.F("ov.resetIn", left);
    }

    /// <summary>重置 chip:淡底小胶囊,挂在主值下面。</summary>
    private FrameworkElement BuildResetChip(string text)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 1, 5, 1),
            Background = QuotaRes.Brush(this, "AeSubtleFillBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = DimBrush(),
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }

    private FrameworkElement BuildRow(ProviderResult row)
    {
        // 卡片容器:实底 + 1px 描边 + 圆角 8,行与行靠卡片边界分隔(无分割线)
        var card = new Border
        {
            Background = QuotaRes.Brush(this, "AeCardBrush"),
            BorderBrush = QuotaRes.Brush(this, "AeCardStrokeBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
        };
        var stack = new StackPanel { Spacing = 8 };

        var expanded = _expanded == row.Name;
        var resetChip = ResetChipText(row);

        // 上半:28px 图标底板 + (名称行 / detail) + 主值 + 展开箭头
        var top = new Grid { ColumnSpacing = 10 };
        // 透明底 = 整行都参与命中测试(空白处也能点开)
        top.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pausedBrush = QuotaRes.Brush(this, "AePausedBrush");
        var iconChip = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = QuotaRes.Brush(this, "AeSubtleFillBrush"),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                Glyph = KindGlyph(row.Kind),
                Foreground = row.Paused ? pausedBrush : DimBrush(),
            },
        };
        Grid.SetColumn(iconChip, 0);
        top.Children.Add(iconChip);

        var mid = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        // 名称 + 等级 pill(暂停时 pill 显示"已暂停",替代旧的行内"已暂停"小字)
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new TextBlock
        {
            Text = row.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
            VerticalAlignment = VerticalAlignment.Center,
            // 列宽约束 + 裁剪;不设 MaxWidth,窄窗口靠 Grid 自适应
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var (pillBrush, pillText) = LevelPillInfo(row);
        nameRow.Children.Add(BuildLevelPill(pillBrush, pillText));
        mid.Children.Add(nameRow);

        // detail:允许两行折行(长文案不再被单行截断),超出再裁
        var detail = row.Detail;
        if (row.Error is not null) detail = row.Error;
        // 重置时间已单独用 chip 展示,detail 末尾那句剥掉(不重复)
        else if (resetChip is not null) detail = ResetTime.StripResetNote(detail);
        mid.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = DimBrush(),
        });
        Grid.SetColumn(mid, 1);
        top.Children.Add(mid);

        // 右列:主值 18 SemiBold;余额类在下方补一行 11 Secondary 单位
        var valueStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var mainValue = new TextBlock
        {
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        string? unitLine = null;
        if (row.Level == Levels.Unconfigured)
        {
            // 未配置:降级成次级文案,不抢主值的视觉位
            mainValue.FontSize = 12;
            mainValue.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            mainValue.Foreground = DimBrush();
            mainValue.Text = L.T("ov.unconfigured");
        }
        else if (row.Error is not null)
        {
            mainValue.Text = "!";
            mainValue.FontSize = 16;
            mainValue.Foreground = LevelBrush(row.Level);
        }
        else if (row.Pct is not null)
            mainValue.Text = $"{Http.F0(row.Pct.Value)}%";
        else if (row.Remaining is not null)
        {
            // 额度剩余简化:大数缩写(12345.67 → 12.3k),小数点后两位太占位
            mainValue.Text = Http.FCompact(row.Remaining.Value);
            unitLine = row.Unit;
        }
        else
            mainValue.Text = row.Level == Levels.Error ? L.T("ov.error") : "-";

        valueStack.Children.Add(mainValue);
        if (unitLine is not null)
            valueStack.Children.Add(new TextBlock
            {
                Text = unitLine,
                FontSize = 11,
                Foreground = DimBrush(),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
        if (resetChip is not null)
            valueStack.Children.Add(BuildResetChip(resetChip));
        Grid.SetColumn(valueStack, 2);
        top.Children.Add(valueStack);

        // 展开箭头(收起时朝下,展开时转 180°)
        var chevron = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
            Glyph = "\uE70D",
            Foreground = DimBrush(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (expanded)
            chevron.RenderTransform = new RotateTransform { Angle = 180 };
        ToolTipService.SetToolTip(chevron, L.T("ov.expandHint"));
        Grid.SetColumn(chevron, 3);
        top.Children.Add(chevron);

        stack.Children.Add(top);

        // 下半:4px 等级色条(收在卡片内,圆角 2;无值时空轨)
        var level = LevelBrush(row.Level);
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Foreground = level,
            // 轨道也带等级色:剩余 0% 时填充为空,不然等级完全看不出来
            Background = LevelTrack(level),
        };
        if (row.Pct is not null) bar.Value = Math.Clamp(row.Pct.Value, 0, 100);
        stack.Children.Add(bar);

        // 展开区(手风琴:同时只开一张)
        if (expanded)
            stack.Children.Add(BuildDetailPanel(row));

        card.Child = stack;

        // 头部点击 → 展开/收起详情(点卡片里的链接不会冒泡到这,链接在展开区里)
        top.Tapped += (_, _) => ToggleExpand(row.Name);

        // 行右键菜单:立即刷新(行级单刷)/ 暂停 / 编辑 / 删除
        var menu = new MenuFlyout();
        var miRefresh = new MenuFlyoutItem { Text = L.T("ov.ctxRefresh") };
        miRefresh.Click += (_, _) => _ = _app.Poller.RefreshProviderAsync(row.Name);
        menu.Items.Add(miRefresh);
        var miPause = new MenuFlyoutItem
        { Text = _app.Paused.Contains(row.Name) ? L.T("ov.resume") : L.T("ov.pause") };
        miPause.Click += (_, _) => TogglePause(row.Name);
        menu.Items.Add(miPause);
        var miEdit = new MenuFlyoutItem { Text = L.T("api.edit") };
        miEdit.Click += (_, _) => ShowManager(row.Name);
        menu.Items.Add(miEdit);
        var miDelete = new MenuFlyoutItem { Text = L.T("api.delete") };
        miDelete.Click += async (_, _) =>
        {
            try
            {
                if (await ProviderManagerPane.ConfirmDeleteAsync(_app, this, row.Name))
                    FooterUpdated.Text = L.F("delete.done", row.Name);
            }
            catch (Exception ex)
            {
                Core.Utils.CrashLog.Write("quota.providerDelete", ex);
            }
        };
        menu.Items.Add(miDelete);
        card.ContextFlyout = menu;

        return card;
    }

    /// <summary>手风琴展开:点同一张收起,点别的换一张。</summary>
    private void ToggleExpand(string name)
    {
        _expanded = _expanded == name ? null : name;
        OnResultsChanged();
    }

    /// <summary>展开详情区:官网 / 模型 / 用量 / 重置 / 配额接口 / 上次刷新。</summary>
    private FrameworkElement BuildDetailPanel(ProviderResult row)
    {
        var inner = new StackPanel { Spacing = 6 };
        var label = L.T("ov.detail.usage");

        // 官网(kind 映射;自定义中转站没有固定官网,退到接口地址)
        var official = ProviderInfo.OfficialUrl(row.Kind);
        if (official is not null)
            inner.Children.Add(BuildLinkRow(L.T("ov.detail.website"), official, HostOf(official)));

        // 接口地址(自配中转站看这个;与官网同域名就不重复列了)
        var baseUrl = BaseUrlOf(row.Name);
        if (!string.IsNullOrEmpty(baseUrl))
        {
            var host = HostOf(baseUrl!);
            if (official is null || !host.Equals(HostOf(official), StringComparison.OrdinalIgnoreCase))
                inner.Children.Add(BuildLinkRow(L.T("ov.detail.api"), baseUrl!, host));
        }

        // 可用模型
        if (row.Models is { Count: > 0 } models)
            inner.Children.Add(BuildModelsRow(models, row.ModelsCount ?? models.Count));
        else if (row.ModelsCount is { } mc && mc > 0)
            inner.Children.Add(BuildInfoRow(L.T("ov.detail.models"),
                L.F("ov.detail.modelsCount", mc)));

        // 用量明细
        var usage = new StackPanel { Spacing = 3 };
        var any = false;
        if (row.Used is { } used)
        {
            usage.Children.Add(BuildInfoRow(L.T("ov.detail.used"), Amount(used, row.Unit)));
            any = true;
        }
        if (row.Total is { } total)
        {
            usage.Children.Add(BuildInfoRow(L.T("ov.detail.total"), Amount(total, row.Unit)));
            any = true;
        }
        if (row.UsedToday is { } today)
        {
            usage.Children.Add(BuildInfoRow(L.T("ov.detail.today"), Amount(today, row.Unit)));
            any = true;
        }
        if (row.Pct is { } pct)
        {
            usage.Children.Add(BuildInfoRow(L.T("ov.detail.remaining"), $"{Http.F0(pct)}%"));
            any = true;
        }
        else if (row.Remaining is { } rem)
        {
            usage.Children.Add(BuildInfoRow(L.T("ov.detail.remaining"), Amount(rem, row.Unit)));
            any = true;
        }
        if (any)
        {
            inner.Children.Add(BuildSectionLabel(label));
            inner.Children.Add(usage);
            if (row.IsEstimate)
                inner.Children.Add(BuildNote(L.T("ov.detail.estimate")));
        }

        // 重置时间:绝对 + 相对
        if (row.ResetAtMs is { } resetMs)
        {
            var left = ResetTime.FormatLeft(resetMs);
            var text = left is null
                ? ResetTime.FormatAbsolute(resetMs)
                : $"{ResetTime.FormatAbsolute(resetMs)} · {L.F("ov.resetIn", left)}";
            inner.Children.Add(BuildInfoRow(L.T("ov.detail.reset"), text));
        }

        // 配额接口
        if (!string.IsNullOrEmpty(row.QuotaEndpoint))
            inner.Children.Add(BuildLinkRow(L.T("ov.detail.endpoint"),
                row.QuotaEndpoint!, PathOf(row.QuotaEndpoint!)));

        // 上次刷新
        if (row.UpdatedAt > 0)
            inner.Children.Add(BuildInfoRow(L.T("ov.detail.updated"),
                DateTimeOffset.FromUnixTimeSeconds((long)row.UpdatedAt)
                    .ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

        return new Border
        {
            Background = QuotaRes.Brush(this, "AeSubtleFillBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Child = inner,
        };
    }

    /// <summary>用量区块的小标题。</summary>
    private FrameworkElement BuildSectionLabel(string text) => new TextBlock
    {
        Text = text,
        FontSize = 11,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = DimBrush(),
        Margin = new Thickness(0, 4, 0, 0),
    };

    /// <summary>小字备注(估算标记)。</summary>
    private FrameworkElement BuildNote(string text) => new TextBlock
    {
        Text = text,
        FontSize = 11,
        Foreground = DimBrush(),
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>一行"标签 | 值"。</summary>
    private FrameworkElement BuildInfoRow(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = DimBrush(),
            VerticalAlignment = VerticalAlignment.Top,
        });
        var v = new TextBlock
        {
            Text = value,
            FontSize = 12,
            Foreground = TextBrush(),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
        return grid;
    }

    /// <summary>一行"标签 | 链接"。链接用默认浏览器打开(失败静默)。</summary>
    private FrameworkElement BuildLinkRow(string label, string url, string display)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = DimBrush(),
            VerticalAlignment = VerticalAlignment.Top,
        });
        var link = new HyperlinkButton
        {
            // 前景色交给 HyperlinkButton 默认(主题链接色),不写死
            Content = new TextBlock
            {
                Text = display,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        ToolTipService.SetToolTip(link, url);
        link.Click += async (_, _) =>
        {
            try { await Launcher.LaunchUriAsync(new Uri(url)); }
            catch { /* 增强面:打不开就算了,不许把主流程带崩 */ }
        };
        Grid.SetColumn(link, 1);
        grid.Children.Add(link);
        return grid;
    }

    /// <summary>模型行:数量 + 名单(名单可滚动,不让卡片撑爆)。</summary>
    private FrameworkElement BuildModelsRow(List<string> models, int count)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = L.T("ov.detail.models"),
            FontSize = 11,
            Foreground = DimBrush(),
            VerticalAlignment = VerticalAlignment.Top,
        });

        var col = new StackPanel { Spacing = 2 };
        col.Children.Add(new TextBlock
        {
            Text = L.F("ov.detail.modelsCount", count),
            FontSize = 12,
            Foreground = TextBrush(),
        });
        var names = new TextBlock
        {
            Text = string.Join(" · ", models),
            FontSize = 11,
            Foreground = DimBrush(),
            TextWrapping = TextWrapping.Wrap,
        };
        col.Children.Add(new ScrollViewer
        {
            MaxHeight = 76,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = names,
        });
        Grid.SetColumn(col, 1);
        grid.Children.Add(col);
        return grid;
    }

    /// <summary>金额/数量展示:紧凑数字 + 单位。</summary>
    private static string Amount(double v, string unit) =>
        unit.Length > 0 ? $"{Http.FCompact(v)} {unit}" : Http.FCompact(v);

    private static string HostOf(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }

    private static string PathOf(string url)
    {
        try
        {
            var u = new Uri(url);
            var path = u.AbsolutePath;
            return path.Length > 0 ? path : u.Host;
        }
        catch { return url; }
    }

    /// <summary>从 config 里取该 provider 的 base_url(按 name 匹配)。</summary>
    private string? BaseUrlOf(string name)
    {
        foreach (var p in _app.Providers().OfType<Dictionary<string, object?>>())
        {
            if (p.TryGetValue("name", out var n) && n is string ns && ns == name)
                return p.TryGetValue("base_url", out var b) && b is string bs ? bs : null;
        }
        return null;
    }

    private void TogglePause(string name)
    {
        if (!_app.Paused.Remove(name)) _app.Paused.Add(name);
        _ = _app.Poller.RefreshNowAsync();
    }
}
