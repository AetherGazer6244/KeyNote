using KeyNote.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KeyNote.App.Views;

/// 关于页:应用标识 + 版本/检查更新 + 项目简介 + 功能一览 + 仓库与许可 + 作者。
/// 页面无侧边栏导航项,经 MainWindow 底部「关于」按钮进入(NavigateTo("about"))。
public sealed partial class AboutPage : Page
{
    /// 仓库与作者主页(打开走 UpdateService.OpenReleasePage 的 github.com 白名单)。
    private const string RepoUrl = "https://github.com/AetherGazer6244/KeyNote";
    private const string ReleasesUrl = "https://github.com/AetherGazer6244/KeyNote/releases";
    private const string AuthorUrl = "https://github.com/AetherGazer6244";

    private bool _checking;

    public AboutPage()
    {
        InitializeComponent();
        Header.ShowRefresh = false;
        VersionText.Text = $"v{UpdateService.CurrentVersionText}";
        UpdateFoundText.Foreground = new SolidColorBrush(Theme.UiColors.Warning);

        if (UpdateService.LastResult is { } last) ApplyUpdateResult(last);
        UpdateService.Completed += OnUpdateCompleted;
        Unloaded += (_, _) => UpdateService.Completed -= OnUpdateCompleted;
    }

    // ──── 检查更新 ────
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_checking) return;
        _checking = true;
        try
        {
            UpdateStatusText.Text = "正在检查更新…";
            UpdateStatusText.Visibility = Visibility.Visible;
            var info = await UpdateService.CheckAsync();
            ApplyUpdateResult(info);
            if (!info.HasUpdate) return;
            // 发现新版:弹确认框(下载安装 / 稍后)
            if (XamlRoot is not null)
            {
                var accepted = await UpdatePrompt.ShowAsync(info, XamlRoot, "取消");
                if (accepted)
                {
                    await UpdateServiceApp.InstallAsync(info, XamlRoot);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "检查失败";
            Core.Utils.CrashLog.Write("about.update", ex);
        }
        finally
        {
            _checking = false;
        }
    }

    /// 检查完成广播(后台线程触发 → 切回 UI 线程)。
    private void OnUpdateCompleted(UpdateService.UpdateInfo info)
    {
        DispatcherQueue.TryEnqueue(() => ApplyUpdateResult(info));
    }

    /// 检查结果落地:状态文案 + 新版提示行显隐。
    private void ApplyUpdateResult(UpdateService.UpdateInfo info)
    {
        UpdateStatusText.Text = info.Message;
        UpdateStatusText.Visibility = Visibility.Visible;
        UpdateFoundRow.Visibility = info.HasUpdate && info.Latest is not null
            ? Visibility.Visible : Visibility.Collapsed;
        if (info.HasUpdate && info.Latest is not null)
        {
            UpdateFoundText.Text = $"发现新版本 v{info.Latest.ToString(3)},点右侧「打开下载页」可下载安装";
        }
    }

    // ──── 链接 ────
    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateService.LastResult is not { } info) return;
        UpdateStatusText.Text = UpdateService.OpenReleasePage(info.ReleaseUrl) ? "已打开下载页" : "打开浏览器失败";
    }

    private void OpenRepo_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenReleasePage(RepoUrl);
    }

    private void OpenReleasePage_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenReleasePage(ReleasesUrl);
    }

    private void OpenAuthor_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenReleasePage(AuthorUrl);
    }
}
