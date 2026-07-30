using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Iciclecreek.Terminal
{
    /// <summary>
    /// Coalesces terminal invalidations while keeping focused-terminal work ahead of background windows.
    /// </summary>
    public static class TerminalRenderThrottle
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);
        private static readonly HashSet<Control> ForegroundPending = new();
        private static readonly HashSet<Control> BackgroundPending = new();
        private static readonly object Sync = new();
        private static bool _foregroundScheduled;
        private static bool _backgroundScheduled;

        public static void RequestInvalidate(Control control, bool foreground)
        {
            if (control == null)
                return;

            bool schedule;
            lock (Sync)
            {
                if (foreground)
                {
                    BackgroundPending.Remove(control);
                    ForegroundPending.Add(control);
                    schedule = !_foregroundScheduled;
                    _foregroundScheduled = true;
                }
                else
                {
                    if (!ForegroundPending.Contains(control))
                        BackgroundPending.Add(control);
                    schedule = !_backgroundScheduled;
                    _backgroundScheduled = true;
                }
            }

            if (schedule)
            {
                if (foreground)
                    Dispatcher.UIThread.Post(FlushForeground, DispatcherPriority.Input);
                else
                    _ = ScheduleBackgroundFlushAsync();
            }
        }

        private static async Task ScheduleBackgroundFlushAsync()
        {
            await Task.Delay(FrameInterval).ConfigureAwait(false);
            Dispatcher.UIThread.Post(FlushBackground, DispatcherPriority.Background);
        }

        private static void FlushForeground()
        {
            List<Control> controls;
            lock (Sync)
            {
                controls = new List<Control>(ForegroundPending);
                ForegroundPending.Clear();
                _foregroundScheduled = false;
            }

            foreach (Control control in controls)
                control.InvalidateVisual();
        }

        private static void FlushBackground()
        {
            List<Control> controls;
            lock (Sync)
            {
                if (ForegroundPending.Count > 0 || _foregroundScheduled)
                {
                    _backgroundScheduled = false;
                    if (BackgroundPending.Count > 0)
                    {
                        _backgroundScheduled = true;
                        _ = ScheduleBackgroundFlushAsync();
                    }
                    return;
                }

                controls = new List<Control>(BackgroundPending);
                BackgroundPending.Clear();
                _backgroundScheduled = false;
            }

            foreach (Control control in controls)
                control.InvalidateVisual();
        }
    }
}
