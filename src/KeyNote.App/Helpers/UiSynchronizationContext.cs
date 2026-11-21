using Microsoft.UI.Dispatching;

namespace KeyNote.App.Helpers;

/// <summary>
/// 功 能： WinUI3 的 UI 线程默认没有 SynchronizationContext——Task 续体不会自动回 UI 线程，
///         后台完成链路（ConfigureAwait(false) 的下载/轮询等）的续体会直接在线程池上碰控件，
///         COMException 0x8001010E 后经 async void 逸出即进程终止（2026-11-21 实机崩溃根因）。
///         启动时装上本上下文后，UI 线程发起的所有 await 续体自动经 DispatcherQueue 回 UI 线程；
///         显式 DispatcherQueue.TryEnqueue 的既有编组保留，作为双保险不受影响。
/// 2026/11/21 xliu
/// </summary>
public sealed class UiSynchronizationContext : SynchronizationContext
{
    private readonly DispatcherQueue _dq;

    public UiSynchronizationContext(DispatcherQueue dq) => _dq = dq;

    /// <summary>异步派发：await 续体的标准路径。</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        // 队列已关闭（退出竞态）时续体丢弃——进程都要没了，无处可派发
        _dq.TryEnqueue(() =>
        {
            try { d(state); }
            catch (Exception ex) { Core.Utils.CrashLog.Write("ui.syncctx", ex); }
        });
    }

    /// <summary>同步派发：WinUI 生态几乎不用（ContentDialog 等都是异步 API），实现只为契约完整。
    /// UI 线程上直接执行；否则排队并阻塞等待——严禁在 UI 线程等待自身，否则死锁。</summary>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (_dq.HasThreadAccess)
        {
            d(state);
            return;
        }
        using var gate = new ManualResetEventSlim(false);
        Exception? err = null;
        if (!_dq.TryEnqueue(() =>
            {
                try { d(state); }
                catch (Exception ex) { err = ex; }
                finally { gate.Set(); }
            }))
        {
            throw new InvalidOperationException("DispatcherQueue 已关闭，无法同步派发。");
        }
        gate.Wait();
        if (err is not null) throw err;
    }

    /// <summary>上下文绑定固定的 DispatcherQueue，无可变状态，直接复用。</summary>
    public override SynchronizationContext CreateCopy() => this;
}
