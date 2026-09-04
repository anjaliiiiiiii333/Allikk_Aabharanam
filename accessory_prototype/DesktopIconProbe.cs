using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace AccessoryPrototype
{
    public sealed class DesktopIconInfo
    {
        public string Name { get; init; }
        public Rect Bounds { get; init; }

        public override string ToString() => Name;
    }

    public static class DesktopIconProbe
    {
        public static IReadOnlyList<DesktopIconInfo> ReadIcons()
        {
            IntPtr desktopView = FindDesktopView();
            if (desktopView == IntPtr.Zero)
                return Array.Empty<DesktopIconInfo>();

            AutomationElement view = AutomationElement.FromHandle(desktopView);
            if (view == null)
                return Array.Empty<DesktopIconInfo>();

            var result = new List<DesktopIconInfo>();
            var condition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.ListItem);
            foreach (AutomationElement item in view.FindAll(TreeScope.Descendants, condition))
            {
                try
                {
                    string name = item.Current.Name;
                    Rect bounds = item.Current.BoundingRectangle;
                    if (!string.IsNullOrWhiteSpace(name) && bounds.Width > 0 && bounds.Height > 0)
                    {
                        result.Add(new DesktopIconInfo { Name = name, Bounds = bounds });
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // The desktop can recreate an icon while it is being moved.
                }
            }

            return result;
        }

        public static DesktopIconInfo FindIcon(string name)
        {
            return ReadIcons().FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static IntPtr FindDesktopView()
        {
            IntPtr progman = FindWindow("Progman", "Program Manager");
            IntPtr view = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero)
                return view;

            IntPtr found = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                found = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
                return found == IntPtr.Zero;
            }, IntPtr.Zero);
            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(
            IntPtr parent,
            IntPtr childAfter,
            string className,
            string windowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    }
}
