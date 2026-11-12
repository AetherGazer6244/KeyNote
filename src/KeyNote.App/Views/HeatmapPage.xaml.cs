using KeyNote.App.Controls;
using KeyNote.Core.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyNote.App.Views;

/// 热力图页(Page 3):GitHub 风格年度热力图。
public sealed partial class HeatmapPage : Page, IRefreshable
{
    public HeatmapPage()
    {
        InitializeComponent();
        PageRefresh.Wire(this, Header, RefreshData);
    }

    public void RefreshData()
    {
        var days = App.Instance.Settings.HeatmapDays;

        var (daily, start, end) = KeyNoteDb.HeatmapYearly(days);
        Heatmap.SetData(daily, start, end);
        // 高度由页面决定(控件只给建议值)
        Heatmap.Height = Heatmap.DesiredHeight;
        YearTitle.Text = $"过去 {days} 天 · 每日贡献";
        YearSummary.Text = Heatmap.Summary;

        // 页头副标题写结论:活跃天数 + 最长连续
        var activeDays = 0;
        var longest = 0;
        var cur = 0;
        foreach (var v in daily)
        {
            if (v > 0)
            {
                activeDays++;
                cur++;
                if (cur > longest) longest = cur;
            }
            else
            {
                cur = 0;
            }
        }
        Header.Subtitle = activeDays > 0
            ? $"活跃 {activeDays} 天 · 最长连续 {longest} 天"
            : "暂无记录";
    }
}
