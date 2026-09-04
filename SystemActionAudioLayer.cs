using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace DesktopKeychainApp
{
    public sealed class SystemActionAudioLayer : IDisposable
    {
        private readonly DesktopIconDetector _detector;
        private readonly FileSystemWatcher _desktopWatcher;
        private readonly Dictionary<string, DateTime> _lastActionTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Win32Interop.LowLevelKeyboardProc _keyboardHookProc;
        private readonly Win32Interop.LowLevelMouseProc _mouseHookProc;
        private readonly object _syncRoot = new object();
        private WinEventDelegate _winEventProc;
        private IntPtr _winEventHook;
        private IntPtr _keyboardHookHandle;
        private IntPtr _mouseHookHandle;
        private string _pendingRenameItem;
        private DateTime _pendingRenameTime;
        private bool _pendingPaste;
        private DateTime _pendingPasteTime;
        private string _draggedItem;
        private Win32Interop.POINT _dragStartPoint;
        private bool _dragCandidate;
        private bool _dragging;
        private bool _started;
        private bool _disposed;

        private const int VK_F2 = 0x71;
        private const int ACTION_DEBOUNCE_MS = 300;
        private const int PENDING_ACTION_TIMEOUT_MS = 2000;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const int OBJID_WINDOW = 0;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public event EventHandler<AudioActionEventArgs> ActionDetected;

        public SystemActionAudioLayer(DesktopIconDetector detector)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _keyboardHookProc = HandleKeyboardHook;
            _mouseHookProc = HandleMouseHook;
            _desktopWatcher = new FileSystemWatcher(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = false
            };
            _desktopWatcher.Created += DesktopWatcher_Created;
            _desktopWatcher.Deleted += DesktopWatcher_Deleted;
            _desktopWatcher.Renamed += DesktopWatcher_Renamed;
        }

        public void Start()
        {
            if (_started || _disposed)
                return;

            _started = true;
            _desktopWatcher.EnableRaisingEvents = true;
            _winEventProc = HandleWinEvent;
            _winEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_OBJECT_NAMECHANGE,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
            _mouseHookHandle = Win32Interop.SetWindowsHookEx(
                Win32Interop.WH_MOUSE_LL,
                _mouseHookProc,
                IntPtr.Zero,
                0);
            _keyboardHookHandle = Win32Interop.SetWindowsHookEx(
                Win32Interop.WH_KEYBOARD_LL,
                _keyboardHookProc,
                IntPtr.Zero,
                0);
            Debug.WriteLine($"System action audio layer started. mouseHook={_mouseHookHandle} keyboardHook={_keyboardHookHandle}");
        }

        public void Stop()
        {
            if (!_started)
                return;

            _started = false;
            _desktopWatcher.EnableRaisingEvents = false;
            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _winEventProc = null;
            if (_mouseHookHandle != IntPtr.Zero)
            {
                Win32Interop.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                Win32Interop.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private void HandleWinEvent(
            IntPtr hook,
            uint eventType,
            IntPtr windowHandle,
            int objectId,
            int childId,
            uint eventThreadId,
            uint eventTime)
        {
            if (windowHandle == IntPtr.Zero ||
                (eventType != EVENT_SYSTEM_FOREGROUND && eventType != EVENT_OBJECT_NAMECHANGE) ||
                (eventType == EVENT_OBJECT_NAMECHANGE && objectId != OBJID_WINDOW))
                return;

            try
            {
                string openedPath = ResolveOpenedDesktopPath(windowHandle);
                if (!string.IsNullOrWhiteSpace(openedPath))
                    RaiseAction(DesktopAudioAction.Open, Path.GetFileName(openedPath), openedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"System open detection failed: {ex.Message}");
            }
        }

        private string ResolveOpenedDesktopPath(IntPtr windowHandle)
        {
            if (windowHandle == Process.GetCurrentProcess().MainWindowHandle)
                return null;

            string windowTitle = GetWindowTitle(windowHandle);
            if (string.IsNullOrWhiteSpace(windowTitle))
                return null;

            foreach (DesktopItem item in _detector.GetDesktopItems())
            {
                string desktopPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    item.Name);
                if (!File.Exists(desktopPath) && !Directory.Exists(desktopPath))
                    continue;

                string fileName = Path.GetFileName(desktopPath);
                if (windowTitle.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Path.GetFullPath(desktopPath);
                }
            }

            return null;
        }

        private static string GetWindowTitle(IntPtr windowHandle)
        {
            int length = GetWindowTextLength(windowHandle);
            if (length <= 0)
                return null;

            var title = new System.Text.StringBuilder(length + 1);
            GetWindowText(windowHandle, title, title.Capacity);
            return title.ToString();
        }

        private void DesktopWatcher_Created(object sender, FileSystemEventArgs e)
        {
            string itemName = Path.GetFileName(e.FullPath);
            bool isPaste = _pendingPaste && DateTime.UtcNow - _pendingPasteTime <= TimeSpan.FromMilliseconds(PENDING_ACTION_TIMEOUT_MS);
            _pendingPaste = false;
            if (isPaste)
                RaiseAction(DesktopAudioAction.Paste, itemName);
        }

        private void DesktopWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            RaiseAction(DesktopAudioAction.Delete, Path.GetFileName(e.FullPath));
        }

        private void DesktopWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            string itemName = Path.GetFileName(e.FullPath);
            bool isRename = string.Equals(_pendingRenameItem, Path.GetFileName(e.OldFullPath), StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - _pendingRenameTime <= TimeSpan.FromMilliseconds(PENDING_ACTION_TIMEOUT_MS);
            _pendingRenameItem = null;
            RaiseAction(isRename ? DesktopAudioAction.Rename : DesktopAudioAction.Move, itemName);
        }

        private IntPtr HandleKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                ((Win32Interop.KeyboardMessages)wParam == Win32Interop.KeyboardMessages.WM_KEYDOWN ||
                 (Win32Interop.KeyboardMessages)wParam == Win32Interop.KeyboardMessages.WM_SYSKEYDOWN))
            {
                uint key = Marshal.PtrToStructure<Win32Interop.KBDLLHOOKSTRUCT>(lParam).vkCode;
                if (!IsDesktopForeground())
                    return Win32Interop.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);

                string selectedItem = FindSelectedDesktopItemName();
                bool controlDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_CONTROL) & 0x8000) != 0;
                if (string.IsNullOrWhiteSpace(selectedItem))
                    return Win32Interop.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);

                if (controlDown && key == Win32Interop.VK_C)
                    RaiseAction(DesktopAudioAction.Copy, selectedItem);
                else if (controlDown && key == Win32Interop.VK_V)
                {
                    _pendingPaste = true;
                    _pendingPasteTime = DateTime.UtcNow;
                }
                else if (key == VK_F2)
                {
                    _pendingRenameItem = selectedItem;
                    _pendingRenameTime = DateTime.UtcNow;
                }
            }

            return Win32Interop.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private IntPtr HandleMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var message = (Win32Interop.MouseMessages)wParam;
                var hookStruct = Marshal.PtrToStructure<Win32Interop.MSLLHOOKSTRUCT>(lParam);
                if (message == Win32Interop.MouseMessages.WM_LBUTTONDOWN)
                {
                    _draggedItem = FindDesktopItemAtPoint(hookStruct.pt);
                    _dragCandidate = !string.IsNullOrWhiteSpace(_draggedItem);
                    _dragStartPoint = hookStruct.pt;
                }
                else if (message == Win32Interop.MouseMessages.WM_MOUSEMOVE &&
                    _dragCandidate && HasMovedEnough(hookStruct.pt, _dragStartPoint))
                {
                    _dragCandidate = false;
                    _dragging = true;
                    RaiseAction(DesktopAudioAction.DragStart, _draggedItem);
                }
                else if (message == Win32Interop.MouseMessages.WM_LBUTTONUP)
                {
                    if (_dragging)
                        RaiseAction(DesktopAudioAction.Drop, _draggedItem);

                    _draggedItem = null;
                    _dragCandidate = false;
                    _dragging = false;
                }
            }

            return Win32Interop.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private void RaiseAction(DesktopAudioAction action, string itemName, string itemPath = null)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return;

            lock (_syncRoot)
            {
                string key = $"{action}:{itemPath ?? itemName}";
                DateTime now = DateTime.UtcNow;
                if (_lastActionTimes.TryGetValue(key, out DateTime last) &&
                    now - last < TimeSpan.FromMilliseconds(ACTION_DEBOUNCE_MS))
                    return;

                _lastActionTimes[key] = now;
            }

            ActionDetected?.Invoke(this, new AudioActionEventArgs(action, itemName));
        }

        private string FindSelectedDesktopItemName()
        {
            try
            {
                return _detector.GetDesktopItems()
                    .FirstOrDefault(item =>
                    {
                        try
                        {
                            return (bool)item.AutomationElement.GetCurrentPropertyValue(
                                SelectionItemPattern.IsSelectedProperty);
                        }
                        catch
                        {
                            return false;
                        }
                    })?.Name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Selected desktop item lookup failed: {ex.Message}");
                return null;
            }
        }

        private string FindDesktopItemAtPoint(Win32Interop.POINT point)
        {
            try
            {
                return _detector.GetDesktopItems()
                    .FirstOrDefault(item =>
                        point.X >= item.BoundingRectangle.Left &&
                        point.X <= item.BoundingRectangle.Right &&
                        point.Y >= item.BoundingRectangle.Top &&
                        point.Y <= item.BoundingRectangle.Bottom)?.Name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Desktop drag item lookup failed: {ex.Message}");
                return null;
            }
        }

        private static bool HasMovedEnough(Win32Interop.POINT current, Win32Interop.POINT start)
        {
            return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private static bool IsDesktopForeground()
        {
            IntPtr foregroundWindow = Win32Interop.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            var className = new System.Text.StringBuilder(256);
            Win32Interop.GetClassName(foregroundWindow, className, className.Capacity);
            return string.Equals(className.ToString(), "Progman", StringComparison.Ordinal) ||
                string.Equals(className.ToString(), "WorkerW", StringComparison.Ordinal);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            _desktopWatcher.Dispose();
            _lastActionTimes.Clear();
        }

        private delegate void WinEventDelegate(
            IntPtr hook,
            uint eventType,
            IntPtr windowHandle,
            int objectId,
            int childId,
            uint eventThreadId,
            uint eventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr moduleHandle,
            WinEventDelegate callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(
            IntPtr windowHandle,
            System.Text.StringBuilder windowText,
            int maxCount);
    }
}
