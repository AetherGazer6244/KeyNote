using KeyNote.App.Theme;
using KeyNote.Core.Quota.Hotkeys;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace KeyNote.App.Views;

/// 设置项编辑器工厂:按 FieldKind 生成右侧控件。
public sealed partial class SettingsPage
{
    // Win32 修饰键虚拟键码(取修饰键状态用;WinUI 的 KeyRoutedEventArgs 不带修饰键)
    private const int VkShiftHk = 0x10, VkControlHk = 0x11, VkMenuHk = 0x12,
                      VkLWinHk = 0x5B, VkRWinHk = 0x5C;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private static bool IsKeyDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

    /// <summary>
    /// 快捷键编辑器:点输入框直接按组合键录入(修饰键经 Win32 采样);
    /// 也可手打 "Ctrl+Shift+A"(Enter 提交 / Esc 还原 / 失焦校验回滚),空 = 清除。
    /// </summary>
    private FrameworkElement BuildHotkey(FieldSpec spec)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var current = _cfg.GetString(spec.Key);
        var box = new TextBox { Text = current, Width = 150 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, spec.Title);

        void Commit()
        {
            var text = box.Text.Trim();
            if (text.Length == 0)
            {
                current = "";
            }
            else if (HotkeySpec.TryParse(text, out var k, out _) && k is not null)
            {
                current = k.ToText();
            }
            box.Text = current;
            _editors[spec.Key] = current;
        }

        box.KeyDown += (sender, e) =>
        {
            if (e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; return; }
            if (e.Key == VirtualKey.Escape) { box.Text = current; e.Handled = true; return; }
            // 只按了修饰键:等用户按主键,不处理
            if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu
                or VirtualKey.LeftWindows or VirtualKey.RightWindows)
                return;
            // 输入框已被手动改过 → 这是在打字,别抢
            if (!string.Equals(box.Text.Trim(), current, StringComparison.OrdinalIgnoreCase)) return;

            var mods = 0;
            if (IsKeyDown(VkControlHk)) mods |= HotkeySpec.ModControl;
            if (IsKeyDown(VkMenuHk)) mods |= HotkeySpec.ModAlt;
            if (IsKeyDown(VkShiftHk)) mods |= HotkeySpec.ModShift;
            if (IsKeyDown(VkLWinHk) || IsKeyDown(VkRWinHk)) mods |= HotkeySpec.ModWin;
            if (mods == 0) return; // 普通字符,走输入
            if (!HotkeySpec.TryFromVirtualKey((int)e.Key, mods, out var spec2, out _)
                || spec2 is null) return;
            current = spec2.ToText();
            box.Text = current;
            _editors[spec.Key] = current;
            e.Handled = true;
        };
        box.LostFocus += (_, _) => Commit();

        var clear = new Button { Content = "清除" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(clear, $"{spec.Title} 清除");
        clear.Click += (_, _) =>
        {
            current = "";
            box.Text = "";
            _editors[spec.Key] = "";
        };

        panel.Children.Add(box);
        panel.Children.Add(clear);
        _editors[spec.Key] = current;
        return panel;
    }

    private FrameworkElement BuildSwitch(FieldSpec spec)
    {
        var toggle = new ToggleSwitch
        {
            IsOn = _cfg.GetBool(spec.Key),
            OnContent = "",
            OffContent = "",
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, spec.Title);
        toggle.Toggled += (_, _) => _editors[spec.Key] = toggle.IsOn;
        _editors[spec.Key] = toggle.IsOn;
        // 插件开关登记:一键全关与底栏「N/M 已启用」直接操作/读取控件本身
        if (spec.Key.StartsWith("device_plugin_"))
        {
            _switchControls[spec.Key] = toggle;
            toggle.Toggled += (_, _) => UpdatePluginCount();
        }
        return toggle;
    }

    private FrameworkElement BuildSlider(FieldSpec spec)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var value = new TextBlock
        {
            Text = _cfg.GetDouble(spec.Key, 1.0).ToString("0.00"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Theme.UiColors.FgSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 36,
            TextAlignment = TextAlignment.Right,
        };
        var slider = new Slider
        {
            Minimum = spec.Lo,
            Maximum = spec.Hi,
            StepFrequency = spec.Step,
            Value = _cfg.GetDouble(spec.Key, spec.Hi),
            Width = 180,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider, spec.Title);
        slider.ValueChanged += (_, _) =>
        {
            value.Text = slider.Value.ToString("0.00");
            _editors[spec.Key] = slider.Value;
        };
        _editors[spec.Key] = slider.Value;
        panel.Children.Add(slider);
        panel.Children.Add(value);
        return panel;
    }

    private FrameworkElement BuildCombo(FieldSpec spec)
    {
        var combo = new ComboBox
        {
            MinWidth = 120,
            ItemsSource = spec.Options,
        };
        var current = _cfg.GetInt(spec.Key, 365);
        var values = spec.Values ?? RetentionValues;
        var idx = Array.IndexOf(values, current);
        // 未命中时回退:主保留期表回 365(下标3),设备样本表回 90(下标1)
        combo.SelectedIndex = idx >= 0 ? idx : (spec.Values is null ? 3 : 1);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, spec.Title);
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < values.Length)
            {
                _editors[spec.Key] = values[combo.SelectedIndex];
            }
        };
        _editors[spec.Key] = values[Math.Max(0, combo.SelectedIndex)];
        return combo;
    }

    private FrameworkElement BuildNumber(FieldSpec spec)
    {
        var box = new NumberBox
        {
            Minimum = spec.Lo,
            Maximum = spec.Hi,
            SmallChange = spec.Step,
            LargeChange = spec.Step * 10,
            Value = _cfg.GetInt(spec.Key, (int)spec.Lo),
            Width = 110,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, spec.Title);
        box.ValueChanged += (_, _) =>
        {
            if (!double.IsNaN(box.Value)) _editors[spec.Key] = (int)box.Value;
        };
        _editors[spec.Key] = (int)box.Value;
        return box;
    }

    private FrameworkElement BuildPath(FieldSpec spec)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var text = new TextBox
        {
            Text = _cfg.GetString(spec.Key),
            PlaceholderText = "默认:数据目录/backups",
            Width = 220,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(text, spec.Title);
        text.TextChanged += (_, _) => _editors[spec.Key] = text.Text;
        var browse = new Button { Content = "浏览…" };
        browse.Click += (_, _) =>
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.FileTypeFilter.Add("*");
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(WindowService.Current!);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var picked = picker.PickSingleFolderAsync();
                picked.AsTask().ContinueWith(t =>
                {
                    if (t.Result is { } folder)
                    {
                        DispatcherQueue.TryEnqueue(() => text.Text = folder.Path);
                    }
                });
            }
            catch (Exception ex)
            {
                Core.Utils.CrashLog.Write("settings.browse", ex);
            }
        };
        panel.Children.Add(text);
        panel.Children.Add(browse);
        _editors[spec.Key] = text.Text;
        return panel;
    }

    private FrameworkElement BuildAction(FieldSpec spec)
    {
        var button = new Button { Content = spec.Title };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, spec.Title);
        button.Click += (_, _) => RunAction(spec.ActionKey ?? string.Empty);
        return button;
    }

    private FrameworkElement BuildInfo(FieldSpec spec)
    {
        return new TextBlock
        {
            Text = spec.Value ?? string.Empty,
            FontSize = 13,
            Foreground = new SolidColorBrush(Theme.UiColors.FgSecondary),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// 「发现新版」提示行:提示文案 + 打开 Release 页按钮(默认隐藏)。
}
