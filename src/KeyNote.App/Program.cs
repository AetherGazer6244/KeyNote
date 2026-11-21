using System;
using System.Runtime.InteropServices;
using System.Threading;
using KeyNote.Core.Utils;

namespace KeyNote.App;

/// 程序入口(DISABLE_XAML_GENERATED_MAIN):单实例互斥 → 启动 XAML。
public static class Program
{
    /// <summary>上次运行是否未正常退出（crash.log 末尾不是干净退出标记）。供启动后提示用。</summary>
    public static bool LastExitDirty { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // 先读标记再写本次 app.start：末尾不是 app.exit（且非首次运行）＝上次没走到干净退出
        LastExitDirty = CrashLog.LastMarker() is not (null or "app.exit");
        CrashLog.Write("app.start", message: $"launch args: {string.Join(' ', args)}");

        using var mutex = new Mutex(true, @"Local\KeyNoteSingleInstance", out var createdNew);
        if (!createdNew)
        {
            BringExistingToFront();
            return;
        }

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var app = new App();
            app.OnArgs(args);
        });
    }

    /// 已有实例在跑:把它的主窗口拉回前台
    private static void BringExistingToFront()
    {
        // 按标题找主窗口;托盘隐藏消息窗口的标题是 "KeyNote.Tray",不会误命中
        var hwnd = FindWindowW(null, "KeyNote");
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, 9 /* SW_RESTORE */);
        SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
