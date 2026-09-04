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
    public class KeychainTracker : IDisposable
    {
        private readonly DesktopIconDetector _detector;
        private readonly KeychainOverlay _overlay;
        private DesktopItem _trackedItem;
        private CancellationTokenSource _cancellationTokenSource;
        private Thread _trackingThread;
        private bool _disposed;
        private Rect? _lastPosition;
        private bool _wasLeftButtonDown;
        private bool _isDragging;
        private double _dragOffsetX;
        private double _dragOffsetY;
        private readonly int[] _trackedRuntimeId;
        private static readonly object LogLock = new object();
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "tracking.log");

        // Configuration
        private const int TRACKING_INTERVAL_MS = 100; // Update position every 100ms
        private const int MAX_RETRIES = 3; // Retry finding item if it's temporarily unavailable

        public event EventHandler<ItemLostEventArgs> ItemLost;
        public event EventHandler<ItemFoundEventArgs> ItemFound;

        public KeychainTracker(DesktopIconDetector detector, KeychainOverlay overlay, DesktopItem itemToTrack)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _trackedItem = itemToTrack ?? throw new ArgumentNullException(nameof(itemToTrack));
            _trackedRuntimeId = GetRuntimeId(_trackedItem.AutomationElement);
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

                    bool leftButtonDown = IsLeftButtonDown();
                    UpdateDragState(leftButtonDown);

                    // Windows does not update UI Automation bounds while a shell drag is active.
                    Rect? position = _isDragging
                        ? GetDraggedPosition()
                        : GetCurrentPosition();
                    _wasLeftButtonDown = leftButtonDown;

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

        private void UpdateDragState(bool leftButtonDown)
        {
            if (!leftButtonDown)
            {
                _isDragging = false;
                return;
            }

            if (_wasLeftButtonDown || _isDragging || !_lastPosition.HasValue)
                return;

            if (!Win32Interop.GetCursorPos(out Win32Interop.POINT cursorPosition))
                return;

            Rect lastPosition = _lastPosition.Value;
            if (cursorPosition.X < lastPosition.Left || cursorPosition.X > lastPosition.Right ||
                cursorPosition.Y < lastPosition.Top || cursorPosition.Y > lastPosition.Bottom)
            {
                return;
            }

            _dragOffsetX = lastPosition.Left - cursorPosition.X;
            _dragOffsetY = lastPosition.Top - cursorPosition.Y;
            _isDragging = true;
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

        private static bool IsLeftButtonDown()
        {
            return (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LBUTTON) & 0x8000) != 0;
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopTracking();
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
