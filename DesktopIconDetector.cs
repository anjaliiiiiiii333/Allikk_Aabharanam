using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Detects and tracks desktop icons for files, folders, and application shortcuts.
    /// Uses UIAutomation to enumerate desktop items and retrieve their screen positions.
    /// Works exclusively with desktop items shown on the desktop, not in Explorer windows.
    /// </summary>
    public class DesktopIconDetector
    {
        private readonly AutomationElement _desktopElement;
        private Rect _desktopBounds;

        public DesktopIconDetector()
        {
            _desktopElement = AutomationElement.RootElement;
            _desktopBounds = _desktopElement.Current.BoundingRectangle;
        }

        /// <summary>
        /// Retrieves all desktop icons currently visible on the desktop.
        /// Returns a list of desktop items with their names and bounding rectangles.
        /// </summary>
        public List<DesktopItem> GetDesktopItems()
        {
            var items = new List<DesktopItem>();

            try
            {
                IntPtr desktopViewHandle = NativeMethods.FindDesktopView();
                if (desktopViewHandle == IntPtr.Zero)
                {
                    Debug.WriteLine("Desktop view window not found");
                    return items;
                }

                AutomationElement desktopView = AutomationElement.FromHandle(desktopViewHandle);
                if (desktopView == null)
                {
                    Debug.WriteLine("Could not get automation element for desktop view");
                    return items;
                }

                System.Windows.Automation.Condition itemCondition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ControlType.ListItem);
                AutomationElementCollection desktopItems = desktopView.FindAll(
                    TreeScope.Descendants,
                    itemCondition);

                for (int itemIndex = 0; itemIndex < desktopItems.Count; itemIndex++)
                {
                    AutomationElement desktopItem = desktopItems[itemIndex];
                    try
                    {
                        string name = desktopItem.Current.Name;
                        Rect bounds = desktopItem.Current.BoundingRectangle;

                        // Only include items that have valid names and positions
                        if (!string.IsNullOrEmpty(name) && bounds.Width > 0 && bounds.Height > 0)
                        {
                            items.Add(new DesktopItem
                            {
                                Name = name,
                                BoundingRectangle = bounds,
                                AutomationElement = desktopItem,
                                NativeListViewIndex = itemIndex
                            });
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // Element was removed or became unavailable; skip it
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating desktop items: {ex.Message}");
            }

            return items;
        }

        /// <summary>
        /// Finds a desktop item by name (case-insensitive).
        /// Returns null if the item is not found.
        /// </summary>
        public DesktopItem FindDesktopItemByName(string name)
        {
            try
            {
                var items = GetDesktopItems();
                return items.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding desktop item: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current screen position (bounding rectangle) of a desktop item.
        /// Returns null if the item cannot be found or its position cannot be retrieved.
        /// </summary>
        public Rect? GetDesktopItemPosition(DesktopItem item)
        {
            try
            {
                if (item.AutomationElement == null)
                {
                    // Try to find it again
                    var foundItem = FindDesktopItemByName(item.Name);
                    if (foundItem == null)
                        return null;

                    item.AutomationElement = foundItem.AutomationElement;
                }

                Rect bounds = item.AutomationElement.Current.BoundingRectangle;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return bounds;
                }
            }
            catch (ElementNotAvailableException)
            {
                // Element no longer available
                Debug.WriteLine($"Desktop item '{item.Name}' is no longer available");
                item.AutomationElement = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting position for desktop item '{item.Name}': {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Represents a desktop item (file, folder, or app shortcut) with its current position.
    /// </summary>
    public class DesktopItem
    {
        public string Name { get; set; }
        public Rect BoundingRectangle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        internal int NativeListViewIndex { get; set; }
    }

    /// <summary>
    /// Native Win32 method wrappers for desktop detection.
    /// </summary>
    public static class NativeMethods
    {
        private const string DesktopViewClassName = "SHELLDLL_DefView";
        private const string DesktopListViewClassName = "SysListView32";

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);
        private delegate bool EnumChildWindowsProc(IntPtr windowHandle, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr FindWindowEx(
            IntPtr parentHandle,
            IntPtr childAfterHandle,
            string className,
            string windowName);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(
            IntPtr parentHandle,
            EnumChildWindowsProc callback,
            IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr windowHandle, ref POINT point);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wParam,
            ref POINT lParam);

        private const uint LVM_GETITEMPOSITION = 0x1010;

        public static IntPtr FindDesktopView()
        {
            IntPtr progmanHandle = FindWindow("Progman", "Program Manager");
            IntPtr desktopViewHandle = FindWindowEx(
                progmanHandle,
                IntPtr.Zero,
                DesktopViewClassName,
                null);

            if (desktopViewHandle != IntPtr.Zero)
            {
                return desktopViewHandle;
            }

            IntPtr workerDesktopView = IntPtr.Zero;
            EnumWindows((windowHandle, _) =>
            {
                workerDesktopView = FindWindowEx(
                    windowHandle,
                    IntPtr.Zero,
                    DesktopViewClassName,
                    null);
                return workerDesktopView == IntPtr.Zero;
            }, IntPtr.Zero);

            return workerDesktopView;
        }

        public static bool TryGetDesktopItemPosition(int itemIndex, out POINT screenPosition)
        {
            screenPosition = new POINT();
            IntPtr desktopView = FindDesktopView();
            if (desktopView == IntPtr.Zero)
                return false;

            IntPtr listView = FindWindowEx(desktopView, IntPtr.Zero, DesktopListViewClassName, null);
            if (listView == IntPtr.Zero)
            {
                IntPtr found = IntPtr.Zero;
                EnumChildWindows(desktopView, (windowHandle, _) =>
                {
                    IntPtr candidate = FindWindowEx(
                        windowHandle,
                        IntPtr.Zero,
                        DesktopListViewClassName,
                        null);
                    if (candidate != IntPtr.Zero)
                    {
                        found = candidate;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
                listView = found;
            }

            if (listView == IntPtr.Zero)
                return false;

            SendMessage(listView, LVM_GETITEMPOSITION, new IntPtr(itemIndex), ref screenPosition);
            return ClientToScreen(listView, ref screenPosition);
        }
    }
}
