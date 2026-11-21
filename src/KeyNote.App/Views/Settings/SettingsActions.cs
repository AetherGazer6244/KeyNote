using KeyNote.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KeyNote.App.Views;

/// 设置页动作:备份/VACUUM/CSV 分发。
public sealed partial class SettingsPage
{
    private async void RunAction(string action)
    {
        try
        {
            switch (action)
            {
                case "backup":
                    StatusText.Text = MaintenanceService.Backup(_cfg);
                    break;
                case "vacuum":
                    StatusText.Text = MaintenanceService.Vacuum();
                    break;
                case "csv":
                    StatusText.Text = MaintenanceService.ExportCsv(30);
                    break;
                case "plugin_disable_all":
                {
                    // 只翻控件:每台 Toggled 回调会把 _editors 一并置 false,点「保存设置」才落盘
                    foreach (var (key, toggle) in _switchControls)
                        toggle.IsOn = false;
                    ShowStatus($"已全关 {_switchControls.Count} 个探测插件,记得点「保存设置」", ok: true);
                    break;
                }
                case "open_logs":
                {
                    var dir = System.IO.Path.Combine(Core.Utils.AppPaths.DataDir(), "logs");
                    System.IO.Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
                    ShowStatus("已打开日志文件夹", ok: true);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Utils.CrashLog.Write("settings.action", ex);
            StatusText.Text = "操作失败";
        }
    }

}
