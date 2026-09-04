using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Manages the continuous tracking of a desktop item and automatic repositioning of its keychain overlay.
    /// Monitors the desktop item's position and updates the overlay in real-time as the item is moved.
    /// Handles scenarios where the item is moved, deleted, or temporarily unavailable.
    /// </summary>
    public enum DesktopAudioAction
    {
        Open,
        Rename,
        Delete,
        Copy,
        Paste,
        Move,
        DragStart,
        Drop
    }

    public class AudioActionEventArgs : EventArgs
    {
        public AudioActionEventArgs(DesktopAudioAction action)
        {
            Action = action;
        }

        public DesktopAudioAction Action { get; }
    }

    public class KeychainTracker : IDisposable
    {
        private readonly DesktopIconDetector _detector;
        private readonly KeychainOverlay _overlay;
        private DesktopItem _trackedItem;
        private CancellationTokenSource _cancellationTokenSource;
        private Thread _trackingThread;
        private bool _disposed;
        private Rect? _lastPosition;
        private Rect? _dragStartPosition;
        private bool _isDragging;
        private bool _renameHooked;
        private string _lastObservedName;
        private double _dragOffsetX;
        private double _dragOffsetY;
        private readonly int[] _trackedRuntimeId;
        private IntPtr _mouseHookHandle;
        private IntPtr _keyboardHookHandle;
        private readonly Win32Interop.LowLevelMouseProc _mouseHookProc;
        private readonly Win32Interop.LowLevelKeyboardProc _keyboardHookProc;
        private bool _mouseButtonDown;
        private bool _dragCandidate;
        private Win32Interop.POINT _dragStartPoint;
        private Rect? _dragReleasePosition;
        private bool _pendingMoveCheck;
        private bool _deleteRequested;
        private bool _deleteTriggered;
        private static readonly object LogLock = new object();
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "tracking.log");

        // Configuration
        private const int TRACKING_INTERVAL_MS = 100; // Update position every 100ms
        private const int MAX_RETRIES = 3; // Retry finding item if it's temporarily unavailable

        public event EventHandler<ItemLostEventArgs> ItemLost;
        public event EventHandler<ItemFoundEventArgs> ItemFound;
        public event EventHandler<AudioActionEventArgs> ActionTriggered;

        public void RaiseAction(DesktopAudioAction action)
        {
            ActionTriggered?.Invoke(this, new AudioActionEventArgs(action));
        }

        public KeychainTracker(DesktopIconDetector detector, KeychainOverlay overlay, DesktopItem itemToTrack)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _trackedItem = itemToTrack ?? throw new ArgumentNullException(nameof(itemToTrack));
            _trackedRuntimeId = GetRuntimeId(_trackedItem.AutomationElement);
            _lastObservedName = _trackedItem.Name;
            _mouseHookProc = HandleMouseHook;
            _keyboardHookProc = HandleKeyboardHook;
            HookTrackedItemEvents();
            StartInputDetection();
            Log($"ATTACH name='{_trackedItem.Name}' originalRuntimeId={FormatRuntimeId(_trackedRuntimeId)} originalBounds={_trackedItem.BoundingRectangle}");
        }

        /// <summary>
        /// Starts continuous tracking of the desktop item.
        /// The tracking loop runs asynchronously and updates the overlay position at regular intervals.
        /// </summary>
        public void StartTracking()
        {
            if (_trackingThread != null)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;
            _trackingThread = new Thread(() => TrackingLoop(token))
            {
                IsBackground = true,
                Name = "DesktopKeychainItemTracker"
            };
            _trackingThread.SetApartmentState(ApartmentState.STA);
            _trackingThread.Start();
        }

        /// <summary>
        /// Stops the tracking loop and cleans up resources.
        /// </summary>
        public void StopTracking()
        {
            _cancellationTokenSource?.Cancel();
            try
            {
                if (_trackingThread != null && Thread.CurrentThread != _trackingThread)
                {
                    _trackingThread.Join(1000);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling the task
            }
            _trackingThread = null;
        }

        /// <summary>
        /// The main tracking loop that continuously updates the overlay position.
        /// Runs on a background thread and checks the item's position at regular intervals.
        /// </summary>
        private void TrackingLoop(CancellationToken cancellationToken)
        {
            int consecutiveFailures = 0;
            bool itemWasAvailable = false;
            Log($"TRACKING_STARTED thread={Thread.CurrentThread.ManagedThreadId} apartment={Thread.CurrentThread.GetApartmentState()}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if the overlay window is still valid
                    if (!_overlay.IsValid)
                    {
                        Debug.WriteLine("Overlay window became invalid");
                        ItemLost?.Invoke(this, new ItemLostEventArgs { Reason = "Overlay window closed" });
                        break;
                    }

                    // Windows does not update UI Automation bounds while a shell drag is active.
                    Rect? position = _isDragging
                        ? GetDraggedPosition()
                        : GetCurrentPosition();

                    if (position.HasValue)
                    {
                        Rect currentPosition = position.Value;
                        bool boundsChanged = !_lastPosition.HasValue || !_lastPosition.Value.Equals(currentPosition);
                        Log($"CYCLE name='{_trackedItem.Name}' oldBounds={FormatRect(_lastPosition)} newBounds={currentPosition} boundsChanged={boundsChanged}");
                        if (boundsChanged)
                        {
                            bool repositioned = _overlay.SetPosition(
                                currentPosition.Left,
                                currentPosition.Top,
                                currentPosition.Width,
                                currentPosition.Height);
                            Log($"REPOSITION name='{_trackedItem.Name}' calculatedOverlay=({_overlay.LastOverlayX},{_overlay.LastOverlayY}) overlayCall={repositioned} nativeResult={_overlay.LastRepositionSucceeded}");
                            if (repositioned)
                            {
                                _lastPosition = currentPosition;
                            }
                        }
                        if (_pendingMoveCheck && !_isDragging && _dragStartPosition.HasValue &&
                            !currentPosition.Equals(_dragStartPosition.Value))
                        {
                            ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Move));
                            _pendingMoveCheck = false;
                            _dragStartPosition = null;
                        }
                        else if (_pendingMoveCheck && !_isDragging && _dragStartPosition.HasValue &&
                            _dragReleasePosition.HasValue &&
                            _dragReleasePosition.Value.Equals(_dragStartPosition.Value))
                        {
                            _pendingMoveCheck = false;
                            _dragStartPosition = null;
                        }

                        consecutiveFailures = 0;

                        if (!itemWasAvailable)
                        {
                            itemWasAvailable = true;
                            ItemFound?.Invoke(this, new ItemFoundEventArgs { Item = _trackedItem });
                        }
                    }
                    else
                    {
                        // Item position could not be retrieved
                        consecutiveFailures++;

                        Log($"CURRENT_BOUNDS_FAILED name='{_trackedItem.Name}' attempt={consecutiveFailures}/{MAX_RETRIES}");

                        bool reacquired = ReacquireTrackedItem();

                        if (_deleteRequested && !reacquired && !_deleteTriggered)
                        {
                            _deleteTriggered = true;
                            ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Delete));
                        }

                        if (consecutiveFailures > MAX_RETRIES)
                        {
                            Debug.WriteLine($"Item '{_trackedItem.Name}' lost after {MAX_RETRIES} failed attempts");
                            ItemLost?.Invoke(this, new ItemLostEventArgs { Reason = "Item position unavailable" });
                            break;
                        }
                    }

                    cancellationToken.WaitHandle.WaitOne(TRACKING_INTERVAL_MS);
                }
                catch (Exception ex)
                {
                    Log($"TRACKING_EXCEPTION name='{_trackedItem.Name}' type={ex.GetType().FullName} message='{ex.Message}' stack='{ex.StackTrace}'");
                    cancellationToken.WaitHandle.WaitOne(TRACKING_INTERVAL_MS);
                }
            }

            Debug.WriteLine("Tracking loop exited");
        }

        private Rect? GetCurrentPosition()
        {
            // Keep tracking the original automation element when desktop enumeration is temporarily empty.
            Rect? directPosition = _detector.GetDesktopItemPosition(_trackedItem);
            if (directPosition.HasValue)
            {
                _trackedItem.BoundingRectangle = directPosition.Value;
                Log($"DIRECT_POSITION name='{_trackedItem.Name}' bounds={directPosition.Value}");
                return directPosition;
            }

            List<DesktopItem> currentItems = _detector.GetDesktopItems();
            DesktopItem currentItem = null;
            Log($"QUERY name='{_trackedItem.Name}' itemCount={currentItems.Count} originalRuntimeId={FormatRuntimeId(_trackedRuntimeId)}");

            if (_trackedRuntimeId != null)
            {
                currentItem = currentItems.FirstOrDefault(item =>
                    RuntimeIdsEqual(_trackedRuntimeId, GetRuntimeId(item.AutomationElement)));
                Log($"RUNTIME_MATCH name='{_trackedItem.Name}' matched={currentItem != null}");
            }

            if (currentItem == null)
            {
                List<DesktopItem> sameNameItems = currentItems
                    .Where(item => string.Equals(item.Name, _trackedItem.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (sameNameItems.Count == 1)
                {
                    currentItem = sameNameItems[0];
                    Log($"NAME_FALLBACK name='{_trackedItem.Name}' matchedRuntimeId={FormatRuntimeId(GetRuntimeId(currentItem.AutomationElement))}");
                }
            }

            if (currentItem == null)
            {
                Log($"ITEM_NOT_FOUND name='{_trackedItem.Name}'");
                return null;
            }

            _trackedItem.AutomationElement = currentItem.AutomationElement;
            Rect currentBounds = currentItem.BoundingRectangle;
            if (NativeMethods.TryGetDesktopItemPosition(
                currentItem.NativeListViewIndex,
                out NativeMethods.POINT nativePosition))
            {
                currentBounds = new Rect(
                    nativePosition.X,
                    nativePosition.Y,
                    currentBounds.Width,
                    currentBounds.Height);
                Log($"NATIVE_POSITION name='{_trackedItem.Name}' index={currentItem.NativeListViewIndex} screen=({nativePosition.X},{nativePosition.Y})");
            }
            else
            {
                Log($"NATIVE_POSITION_FAILED name='{_trackedItem.Name}' index={currentItem.NativeListViewIndex}; using UIA bounds");
            }

            _trackedItem.BoundingRectangle = currentBounds;
            Log($"RESOLVED name='{_trackedItem.Name}' runtimeId={FormatRuntimeId(GetRuntimeId(currentItem.AutomationElement))} currentBounds={currentBounds}");
            return currentBounds;
        }

        private bool ReacquireTrackedItem()
        {
            DesktopItem freshItem = _detector.FindFreshDesktopItemByName(_trackedItem.Name);
            if (freshItem == null)
            {
                Log($"FRESH_REACQUIRE_FAILED name='{_trackedItem.Name}'");
                return false;
            }

            _trackedItem.AutomationElement = freshItem.AutomationElement;
            _trackedItem.BoundingRectangle = freshItem.BoundingRectangle;
            _trackedItem.NativeListViewIndex = freshItem.NativeListViewIndex;
            Log($"FRESH_REACQUIRED name='{_trackedItem.Name}' bounds={freshItem.BoundingRectangle}");
            return true;
        }

        private Rect? GetDraggedPosition()
        {
            if (!Win32Interop.GetCursorPos(out Win32Interop.POINT cursorPosition) || !_lastPosition.HasValue)
                return _lastPosition;

            Rect lastPosition = _lastPosition.Value;
            return new Rect(
                cursorPosition.X + _dragOffsetX,
                cursorPosition.Y + _dragOffsetY,
                lastPosition.Width,
                lastPosition.Height);
        }

        private static string FormatRect(Rect? rect)
        {
            return rect.HasValue ? rect.Value.ToString() : "<none>";
        }

        private static string FormatRuntimeId(int[] runtimeId)
        {
            return runtimeId == null ? "<none>" : string.Join(",", runtimeId);
        }

        private static void Log(string message)
        {
            string line = $"{DateTime.Now:O} {message}{Environment.NewLine}";
            Debug.Write(line);
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tracking log write failed: {ex.Message}");
            }
        }

        private static int[] GetRuntimeId(AutomationElement element)
        {
            try
            {
                return element?.GetRuntimeId();
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static bool RuntimeIdsEqual(int[] first, int[] second)
        {
            return first != null && second != null && first.SequenceEqual(second);
        }

        public DesktopItem TrackedItem => _trackedItem;

        private void StartInputDetection()
        {
            if (_mouseHookHandle != IntPtr.Zero)
                return;

            _mouseHookHandle = Win32Interop.SetWindowsHookEx(
                Win32Interop.WH_MOUSE_LL,
                _mouseHookProc,
                IntPtr.Zero,
                0);

            if (_mouseHookHandle == IntPtr.Zero)
            {
                Log($"OPEN_HOOK_FAILED name='{_trackedItem.Name}' error={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            }

            _keyboardHookHandle = Win32Interop.SetWindowsHookEx(
                Win32Interop.WH_KEYBOARD_LL,
                _keyboardHookProc,
                IntPtr.Zero,
                0);
        }

        private IntPtr HandleMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (Win32Interop.MouseMessages)wParam == Win32Interop.MouseMessages.WM_LBUTTONDBLCLK)
            {
                var hookStruct = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32Interop.MSLLHOOKSTRUCT>(lParam);
                var message = (Win32Interop.MouseMessages)wParam;
                bool inside = IsPointInsideTrackedItem(hookStruct.pt.X, hookStruct.pt.Y);

                if (message == Win32Interop.MouseMessages.WM_LBUTTONDOWN && inside)
                {
                    _mouseButtonDown = true;
                    _dragCandidate = true;
                    _dragStartPoint = hookStruct.pt;
                }
                else if (message == Win32Interop.MouseMessages.WM_MOUSEMOVE && _mouseButtonDown && _dragCandidate &&
                    HasMovedEnough(hookStruct.pt, _dragStartPoint))
                {
                    _dragCandidate = false;
                    _isDragging = true;
                    _dragStartPosition = _lastPosition ?? _trackedItem.BoundingRectangle;
                    _dragOffsetX = _dragStartPosition.Value.Left - _dragStartPoint.X;
                    _dragOffsetY = _dragStartPosition.Value.Top - _dragStartPoint.Y;
                    ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.DragStart));
                }
                else if (message == Win32Interop.MouseMessages.WM_LBUTTONUP)
                {
                    if (_isDragging)
                    {
                        _dragReleasePosition = GetDraggedPosition();
                        _isDragging = false;
                        _pendingMoveCheck = true;
                        ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Drop));
                    }

                    _mouseButtonDown = false;
                    _dragCandidate = false;
                }
                else if (message == Win32Interop.MouseMessages.WM_LBUTTONDBLCLK && inside && !_isDragging)
                {
                    ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Open));
                }
            }

            return Win32Interop.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private bool IsPointInsideTrackedItem(int x, int y)
        {
            Rect bounds = _lastPosition ?? _trackedItem.BoundingRectangle;
            return bounds.Width > 0 && bounds.Height > 0 &&
                x >= bounds.Left && x <= bounds.Right && y >= bounds.Top && y <= bounds.Bottom;
        }

        private static bool HasMovedEnough(Win32Interop.POINT current, Win32Interop.POINT start)
        {
            return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private IntPtr HandleKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                ((Win32Interop.KeyboardMessages)wParam == Win32Interop.KeyboardMessages.WM_KEYDOWN ||
                 (Win32Interop.KeyboardMessages)wParam == Win32Interop.KeyboardMessages.WM_SYSKEYDOWN))
            {
                var key = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32Interop.KBDLLHOOKSTRUCT>(lParam).vkCode;
                bool selected = IsDesktopForeground() && IsTrackedItemSelected();
                bool controlDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_CONTROL) & 0x8000) != 0;

                if (selected && controlDown && key == Win32Interop.VK_C)
                    ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Copy));
                else if (selected && controlDown && key == Win32Interop.VK_V)
                    ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Paste));
                else if (selected && key == Win32Interop.VK_DELETE)
                {
                    _deleteRequested = true;
                    _deleteTriggered = false;
                }
            }

            return Win32Interop.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private bool IsTrackedItemSelected()
        {
            try
            {
                return (bool)_trackedItem.AutomationElement.GetCurrentPropertyValue(
                    SelectionItemPattern.IsSelectedProperty);
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
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

        private void HookTrackedItemEvents()
        {
            try
            {
                if (_trackedItem?.AutomationElement == null)
                    return;

                if (!_renameHooked)
                {
                    Automation.AddAutomationPropertyChangedEventHandler(
                        _trackedItem.AutomationElement,
                        TreeScope.Element,
                        (_, e) =>
                        {
                            if (e.Property == AutomationElement.NameProperty)
                            {
                                var newName = e.NewValue as string;
                                if (!string.IsNullOrWhiteSpace(newName) &&
                                    !string.Equals(newName, _lastObservedName, StringComparison.OrdinalIgnoreCase))
                                {
                                    _lastObservedName = newName;
                                    ActionTriggered?.Invoke(this, new AudioActionEventArgs(DesktopAudioAction.Rename));
                                }
                            }
                        },
                        AutomationElement.NameProperty);
                    _renameHooked = true;
                }
            }
            catch (Exception ex)
            {
                Log($"HOOKS_FAILED name='{_trackedItem?.Name}' message='{ex.Message}'");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopTracking();
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
            if (_trackedItem?.AutomationElement != null)
            {
                try
                {
                    if (_renameHooked)
                    {
                        Automation.RemoveAllEventHandlers();
                        _renameHooked = false;
                    }
                }
                catch
                {
                }
            }
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Event args for when a tracked item is lost.
    /// </summary>
    public class ItemLostEventArgs : EventArgs
    {
        public string Reason { get; set; }
    }

    /// <summary>
    /// Event args for when a tracked item is found.
    /// </summary>
    public class ItemFoundEventArgs : EventArgs
    {
        public DesktopItem Item { get; set; }
    }
}
