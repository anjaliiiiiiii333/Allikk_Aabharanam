# Windows API Reference - Desktop Keychain Accessory

## Overview

This document explains the specific Windows APIs used for desktop item detection and overlay management.

---

## 1. Desktop Item Detection APIs

### FindWindow (user32.dll)

**Purpose**: Locate the Progman (Program Manager) window that contains desktop icons.

```csharp
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

// Usage:
IntPtr progmanHandle = FindWindow("Progman", "Program Manager");
```

**Parameters**:
- `lpClassName`: Window class name ("Progman")
- `lpWindowName`: Window title ("Program Manager")

**Returns**: Handle to the Progman window

**Why**: Progman is the container for all desktop icons. All visible items are child elements of this window.

---

### UIAutomation (UIAutomationClient.dll)

**Purpose**: Programmatic access to UI elements and their properties.

#### AutomationElement.RootElement
```csharp
AutomationElement root = AutomationElement.RootElement;
```
- Entry point to the automation tree
- Represents the entire desktop

#### AutomationElement.FromHandle()
```csharp
AutomationElement desktopAutomation = AutomationElement.FromHandle(progmanHandle);
```
- Create automation element from window handle
- Allows UIAutomation to interact with the Progman window

#### TreeWalker.ControlViewWalker
```csharp
TreeWalker walker = TreeWalker.ControlViewWalker;
AutomationElement child = walker.GetFirstChild(desktopAutomation);
while (child != null) {
    // Process child element (desktop item)
    child = walker.GetNextSibling(child);
}
```

**Purpose**: Navigate the automation tree to enumerate all desktop items

**Methods**:
- `GetFirstChild(element)` — Get first child element
- `GetNextSibling(element)` — Get next sibling element
- `GetParent(element)` — Get parent element

#### AutomationElement Properties

```csharp
string name = element.Current.Name;              // Display name of item
Rect bounds = element.Current.BoundingRectangle; // Screen coordinates (Rect: X, Y, Width, Height)
```

**AutomationElement.Current.Name**:
- The display name of the desktop item
- Examples: "Document.txt", "Folder", "Chrome"

**AutomationElement.Current.BoundingRectangle**:
- Screen position and size of the item in logical coordinates
- Already DPI-aware (accounts for display scaling)
- Returns a Windows.Foundation.Rect struct with:
  - `X`: Left edge (screen pixels)
  - `Y`: Top edge (screen pixels)
  - `Width`: Icon width
  - `Height`: Icon height

**DPI Awareness**: Rect values are in logical screen coordinates, automatically scaled by Windows. No manual DPI conversion needed.

---

## 2. Transparent Overlay APIs

### CreateWindowEx (user32.dll)

**Purpose**: Create a transparent overlay window with specific styles.

```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern IntPtr CreateWindowEx(
    int dwExStyle,              // Extended window styles
    string lpClassName,         // Window class name
    string lpWindowName,        // Window title
    int dwStyle,                // Window styles
    int x, int y,               // Position
    int nWidth, int nHeight,    // Size
    IntPtr hWndParent,          // Parent window
    IntPtr hMenu,               // Menu handle
    IntPtr hInstance,           // Application instance
    IntPtr lpParam);            // Creation parameters

// Usage:
IntPtr overlayHandle = CreateWindowEx(
    WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
    "STATIC",
    "KeychainOverlay",
    WS_POPUP,
    0, 0, 64, 64,
    IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
```

**Extended Window Styles (dwExStyle)**:

| Style | Value | Purpose |
|-------|-------|---------|
| `WS_EX_LAYERED` | 0x80000 | Enable layered window (alpha blending, transparency) |
| `WS_EX_TRANSPARENT` | 0x20 | Make window click-through (mouse events pass through) |
| `WS_EX_TOPMOST` | 0x8 | Always on top of other windows |
| `WS_EX_NOACTIVATE` | 0x8000000 | Window doesn't receive focus when clicked |

**Window Styles (dwStyle)**:

| Style | Value | Purpose |
|-------|-------|---------|
| `WS_POPUP` | 0x80000000 | Borderless, parentless popup window |
| `WS_VISIBLE` | 0x10000000 | Window is initially visible |

---

### UpdateLayeredWindow (user32.dll)

**Purpose**: Update the content of a layered window with alpha blending.

```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern bool UpdateLayeredWindow(
    IntPtr hwnd,                // Window handle
    IntPtr hdcDst,              // Device context (destination)
    ref POINT pptDst,           // Position (destination)
    ref SIZE psize,             // Size (source and destination)
    IntPtr hdcSrc,              // Device context (source)
    ref POINT pptSrc,           // Position (source)
    uint crKey,                 // Color key (transparency)
    ref BLENDFUNCTION pblend,   // Blend function (alpha)
    uint dwFlags);              // Flags (LWA_ALPHA or LWA_COLORKEY)

// Usage:
var ptDst = new POINT { X = overlayX, Y = overlayY };
var ptSrc = new POINT { X = 0, Y = 0 };
var size = new SIZE { cx = 64, cy = 64 };
var blend = new BLENDFUNCTION(255); // Full opacity

UpdateLayeredWindow(
    overlayHandle, hdcDest, ref ptDst, ref size, hdcSrc, ref ptSrc,
    0, ref blend, LWA_ALPHA);
```

**Parameters**:
- `hdcDst`: Screen device context
- `pptDst`: Position on screen (where overlay appears)
- `psize`: Size of overlay bitmap
- `hdcSrc`: Memory device context containing bitmap
- `pptSrc`: Source position within bitmap (usually 0,0)
- `crKey`: Color key for color-based transparency (unused with LWA_ALPHA)
- `pblend`: Blend function with alpha value
- `dwFlags`: `LWA_ALPHA` for per-pixel alpha blending

**BLENDFUNCTION Structure**:
```csharp
struct BLENDFUNCTION {
    byte BlendOp;               // 0 = AC_SRC_OVER (alpha blend)
    byte BlendFlags;            // 0 (unused)
    byte SourceConstantAlpha;   // 0-255 (0=fully transparent, 255=fully opaque)
    byte AlphaFormat;           // 1 = AC_SRC_ALPHA (use per-pixel alpha)
}
```

**Why UpdateLayeredWindow?**
- Direct bitmap update without WM_PAINT messages
- Efficient for frequent updates (100ms intervals)
- Built-in alpha blending (no extra processing needed)
- No flicker, no redraw overhead

---

### SetWindowPos (user32.dll)

**Purpose**: Change window position and size.

```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern bool SetWindowPos(
    IntPtr hWnd,                // Window handle
    IntPtr hWndInsertAfter,     // Insertion order
    int x, int y,               // New position
    int cx, int cy,             // New size
    uint uFlags);               // Positioning flags

// Usage:
SetWindowPos(
    overlayHandle,
    IntPtr.Zero,
    overlayX, overlayY,
    64, 64,
    SWP_NOACTIVATE | SWP_NOZORDER);
```

**Flags**:

| Flag | Value | Purpose |
|------|-------|---------|
| `SWP_NOACTIVATE` | 0x10 | Don't activate the window |
| `SWP_NOZORDER` | 0x4 | Don't change Z-order (keep current position in stack) |

**Why These Flags?**
- `SWP_NOACTIVATE`: Prevents stealing focus from the user's current window
- `SWP_NOZORDER`: Keeps the overlay always on top (WS_EX_TOPMOST) without re-ordering

---

### GetDC and CreateCompatibleDC (gdi32.dll)

**Purpose**: Create device contexts for drawing.

```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern IntPtr GetDC(IntPtr hWnd);

[DllImport("gdi32.dll", SetLastError = true)]
public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

// Usage:
IntPtr hdcDest = GetDC(overlayHandle);        // Get screen DC
IntPtr hdcSrc = CreateCompatibleDC(hdcDest);  // Create memory DC
```

**Device Contexts (DC)**:
- Screen DC: Represents the screen where we'll display the overlay
- Memory DC: Holds the bitmap in memory before transferring to screen

---

### CreateDIBSection (gdi32.dll)

**Purpose**: Create a device-independent bitmap (DIB) with 32-bit RGBA format.

```csharp
[DllImport("gdi32.dll", SetLastError = true)]
public static extern IntPtr CreateDIBSection(
    IntPtr hdc,                 // Device context
    ref BITMAPINFO pbmi,        // Bitmap info (format, size, etc.)
    uint usage,                 // Palette type (0 = RGB)
    out IntPtr ppvBits,         // Pointer to bitmap bits
    IntPtr hSection,            // Section object (NULL)
    uint offset);               // Offset (0)

// Usage:
var bmpInfo = new BITMAPINFO();
bmpInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
bmpInfo.bmiHeader.biWidth = 64;
bmpInfo.bmiHeader.biHeight = -64;  // Negative for top-down
bmpInfo.bmiHeader.biPlanes = 1;
bmpInfo.bmiHeader.biBitCount = 32;  // 32-bit RGBA

IntPtr ppvBits;
IntPtr hBitmap = CreateDIBSection(hdcDest, ref bmpInfo, 0, out ppvBits, IntPtr.Zero, 0);
```

**BITMAPINFOHEADER**:
- `biWidth`: Bitmap width (64 pixels)
- `biHeight`: Bitmap height (-64 for top-down, +64 for bottom-up)
- `biBitCount`: Bits per pixel (32 for RGBA)
- `biCompression`: 0 = BI_RGB (no compression)

---

### SelectObject and DeleteObject (gdi32.dll)

**Purpose**: Select bitmap into device context and clean up resources.

```csharp
[DllImport("gdi32.dll", SetLastError = true)]
public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

[DllImport("gdi32.dll", SetLastError = true)]
public static extern bool DeleteObject(IntPtr hObject);

// Usage:
IntPtr hOld = SelectObject(hdcSrc, hBitmap);  // Select bitmap into memory DC
// ... draw bitmap ...
SelectObject(hdcSrc, hOld);                   // Restore old bitmap
DeleteObject(hBitmap);                        // Delete bitmap
```

---

## 3. Supporting Structures

### POINT
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct POINT {
    public int X;
    public int Y;
}
```
Screen coordinates for overlay position.

### SIZE
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SIZE {
    public int cx;  // Width
    public int cy;  // Height
}
```
Size of overlay window.

### BLENDFUNCTION
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct BLENDFUNCTION {
    public byte BlendOp;             // AC_SRC_OVER (0)
    public byte BlendFlags;          // Reserved (0)
    public byte SourceConstantAlpha; // 0-255 (alpha value)
    public byte AlphaFormat;         // AC_SRC_ALPHA (1)
}
```
Alpha blending parameters for layered window.

### BITMAPINFOHEADER
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFOHEADER {
    public uint biSize;              // Size of this struct
    public int biWidth;              // Bitmap width
    public int biHeight;             // Bitmap height
    public ushort biPlanes;          // Color planes (1)
    public ushort biBitCount;        // Bits per pixel (32)
    public uint biCompression;       // Compression type (0)
    public uint biSizeImage;         // Image size
    public int biXPelsPerMeter;      // Horizontal resolution
    public int biYPelsPerMeter;      // Vertical resolution
    public uint biClrUsed;           // Colors used
    public uint biClrImportant;      // Important colors
}
```

---

## 4. Complete API Usage Flow

### Detection Flow
```
FindWindow("Progman", "Program Manager")
  ↓
AutomationElement.FromHandle(progmanHandle)
  ↓
TreeWalker.GetFirstChild()
  ↓ (Loop through siblings)
Read element.Current.Name and element.Current.BoundingRectangle
```

### Overlay Creation Flow
```
CreateWindowEx(
    WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
    ...)
  ↓
GetDC(overlayHandle) → GetScreen DC
CreateCompatibleDC() → CreateMemory DC
CreateDIBSection() → Create 32-bit RGBA bitmap
  ↓
SelectObject() → Select bitmap into memory DC
  ↓
UpdateLayeredWindow() → Transfer bitmap to screen with alpha blending
```

### Position Update Flow
```
GetDesktopItemPosition()  // Read from UIAutomation
  ↓
SetWindowPos() // Move overlay to new position
```

---

## 5. Key Design Decisions

### Why These APIs?

| Component | API | Reason |
|-----------|-----|--------|
| Detection | UIAutomation | Works for files, folders, shortcuts uniformly |
| Overlay | Layered Windows (WS_EX_LAYERED) | Efficient alpha blending, no redraw overhead |
| Click-through | WS_EX_TRANSPARENT | Ensures original item remains interactive |
| Position | SetWindowPos | Fast, minimal overhead |
| Image Update | UpdateLayeredWindow | Direct bitmap transfer, no painting |

### Performance Impact

- **Detection**: One-time scan when user clicks Refresh (~1-2 sec)
- **Tracking**: Reading position (≈1ms) + SetWindowPos (≈0.1ms) every 100ms
- **CPU Usage**: ≈1-2% per active overlay
- **Memory**: ≈2MB per overlay
- **No FPS impact**: Overlay is separate window, doesn't affect desktop rendering

---

## 6. Comparison: Why Not Alternative Approaches?

### Alternative 1: Shell Object Model
```csharp
// Not used because:
// - Complex COM interop
// - Slower enumeration
// - Requires path-based access
// - Doesn't work for all shortcut types
```

### Alternative 2: SetWindowsHookEx
```csharp
// Not used because:
// - Requires DLL injection
// - System-wide performance impact
// - Complex setup and cleanup
// - Overkill for simple overlay
```

### Alternative 3: Screen capture + image processing
```csharp
// Not used because:
// - Expensive CPU usage
// - Can't get exact icon positions
// - Brittle (doesn't handle DPI scaling well)
```

### Why UIAutomation + Win32 Layered Windows?
✅ Clean, official APIs  
✅ Efficient and performant  
✅ DPI-aware (automatic scaling)  
✅ Multi-monitor capable  
✅ No system-wide performance impact  
✅ Easy to debug and maintain  

---

## Summary

The Desktop Keychain Accessory uses a minimal set of well-established Windows APIs:

**For Detection**:
- `FindWindow()` → Locate Progman
- `UIAutomation` → Enumerate and track items

**For Overlay**:
- `CreateWindowEx()` → Create layered, transparent window
- `UpdateLayeredWindow()` → Efficient bitmap updates with alpha
- `SetWindowPos()` → Reposition overlay

**Result**: A lightweight, performant application that seamlessly attaches visual overlays to real desktop items without modification.
