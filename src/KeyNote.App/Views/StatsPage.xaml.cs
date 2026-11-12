using KeyNote.App.Controls;
using KeyNote.Core.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyNote.App.Views;

/// 统计页(Page 1):3 张 KPI 卡 + 按键趋势。
public sealed partial class StatsPage : Page, IRefreshable
{
    private const int RangeIdxToday = 0;
    private const int RangeIdxWeek = 1;
    private const int RangeIdxMonth = 2;

    private int _range = RangeIdxMonth;
    private string? _fp;

    public StatsPage()
    {
        InitializeComponent();
        PageRefresh.Wire(this, Header, RefreshData);
    }

    private void RangeBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selected = sender.SelectedItem;
        var idx = selected == RangeToday ? RangeIdxToday
            : selected == RangeWeek ? RangeIdxWeek
            : RangeIdxMonth;
        if (idx == _range) return;
        _range = idx;
        RefreshData();
    }

    public void RefreshData()
    {
        var days = RangeDays(_range);
        var rangeLabel = RangeLabel(_range);

        var keys = KeyNoteDb.CountKeysSince(days);
        var total = KeyNoteDb.CountTotal();
        var active = KeyNoteDb.DailyActiveDays(days);
        var streak = KeyNoteDb.StreakDays(365);

        // KPI 标签跟随时间范围,避免「选今日却显示累计」的名实不符
        KpiTotal.Label = $"{rangeLabel}按键";
        KpiTotal.Value = keys.ToString("N0");
        KpiTotal.Footnote = days == 1
            ? $"全时累计 {total:N0}"
            : $"日均 {keys / (double)days:N0} · 全时累计 {total:N0}";
        KpiMouse.Value = KeyNoteDb.MouseSince(days).ToString("N0");
        KpiMouse.Footnote = $"全时 {MouseFoot()}";
        KpiActive.Value = active.ToString("N0");
        KpiActive.Footnote = $"活跃率 {active * 100.0 / days:0}%";

        UpdateTrend();

        var avgText = days == 1 ? "" : $" · 日均 {keys / (double)days:N0}";
        Header.Subtitle = streak > 0
            ? $"已连续记录 {streak} 天{avgText}"
            : total > 0 ? "暂无连续记录" : "暂无历史记录";
    }

    /// 时间范围 → 天数
    private static int RangeDays(int range) => range switch
    {
        RangeIdxToday => 1,
        RangeIdxWeek => 7,
        _ => 30,
    };

    private static string RangeLabel(int range) => range switch
    {
        RangeIdxToday => "今日",
        RangeIdxWeek => "近 7 天",
        _ => "近 30 天",
    };

    private void UpdateTrend()
    {
        var key = _range switch
        {
            RangeIdxToday => "today",
            RangeIdxWeek => "week",
            _ => "month",
        };

        List<ChartPoint> points;
        string title;
        switch (_range)
        {
            case RangeIdxToday:
                points = KeyNoteDb.HourlyToday()
                    .Select(r => new ChartPoint(r.Count, $"{DateTimeOffset.FromUnixTimeMilliseconds(r.HourTs).LocalDateTime:HH:mm}"))
                    .ToList();
                title = "按键趋势 · 逐时";
                break;
            case RangeIdxWeek:
            {
                var rows = KeyNoteDb.DailyCounts(7);
                points = rows.Select(r => new ChartPoint(r.Count,
                    DateTimeOffset.FromUnixTimeMilliseconds(r.DayTs).LocalDateTime.ToString("MM-dd"))).ToList();
                title = "按键趋势";
                break;
            }
            default:
            {
                var rows = KeyNoteDb.DailyCounts(30);
                points = rows.Select(r => new ChartPoint(r.Count,
                    DateTimeOffset.FromUnixTimeMilliseconds(r.DayTs).LocalDateTime.ToString("MM-dd"))).ToList();
                title = "按键趋势";
                break;
            }
        }

        var fp = $"{key}|{points.Count}|{points.LastOrDefault()?.Value ?? 0}";
        if (fp == _fp) return;
        _fp = fp;
        TrendTitle.Text = title;
        TrendChart.SetData(points);
    }

    private static string MouseFoot()
    {
        var rows = KeyNoteDb.MouseByButton();
        var total = KeyNoteDb.MouseTotal();
        if (rows.Count == 0 || total <= 0) return "暂无数据";
        var name = rows[0].Button switch
        {
            "left" => "左键",
            "right" => "右键",
            "middle" => "中键",
            "x1" => "侧键 1",
            "x2" => "侧键 2",
            _ => rows[0].Button,
        };
        return $"{name} {Math.Round(rows[0].Count / (double)total * 100)}%";
    }
}
