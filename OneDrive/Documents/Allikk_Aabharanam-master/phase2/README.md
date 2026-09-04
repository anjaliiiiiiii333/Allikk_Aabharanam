# Digital Accessories — Desktop Prototype

**Digital Accessories** is a Windows desktop application that visually attaches decorative keychains to real files, folders, and application shortcuts on the Windows desktop without modifying the files in any way.

---

## 🌟 Features

- **Real Desktop Tracking**: Tracks real files, folders, and application shortcuts on the Windows desktop using native Windows Shell COM APIs.
- **Non-Invasive Visual Overlay**: The actual Windows file/folder remains completely untouched—no file modifications, no renaming, no icon alteration, no metadata changes.
- **Click-Through Transparency**: Mouse clicks pass through the overlay completely (`WS_EX_TRANSPARENT`), allowing you to click, double-click, and drag desktop items normally.
- **Real-Time Drag Following**: Continuously monitors the desktop icon position and automatically follows it when dragged across the screen.
- **Subtle Protective Layer**: Draws a delicate protective outline around the desktop icon with a corner eyelet ring connecting the keychain hook.
- **Transparent PNG Accessory**: Uses a high-quality transparent PNG asset with smooth alpha channels.
- **Extensible Architecture**: Structured so future accessories (bows, hats, moustaches) can be registered easily.

---

## 🚀 Quick Start

### Prerequisites

- Windows 10 or Windows 11
- Python 3.10 to 3.14
- Dependencies: `pywin32`, `pillow`

### 1. Installation

From the project root:

```powershell
pip install -r requirements.txt
```

### 2. Run the Application

Launch the desktop GUI:

```powershell
python app.py
```

Or run via the phase2 module:

```powershell
python -m phase2.run_demo
```

### 3. CLI Mode (Headless / Direct)

You can also run directly from the command line targeting a specific desktop item:

```powershell
# Track by desktop item name
python -m phase2.run_demo "Assignment.pdf"

# Track by COM index (0-based)
python -m phase2.run_demo --index 0
```

---

## 🔍 Technical Architecture & Windows APIs

### 1. How Desktop Icon Position Detection Works

Desktop items are retrieved through the official Windows Shell COM interfaces:

1. **`Shell.Application`**:
   The desktop window is queried via `FindWindowSW` with `SWC_DESKTOP` to obtain the top-level Shell browser dispatch.
2. **`IServiceProvider` & `IShellBrowser`**:
   Queries the active Shell view for the `IFolderView` interface (`{CDE725B0-CCC9-4519-917E-325D72FAB4CE}`).
3. **`IFolderView`**:
   - `ItemCount(SVGIO_ALLVIEW)` returns the total count of real items currently on the desktop.
   - `Item(index)` returns the item's PIDL (`ITEMIDLIST`), representing files, folders, and application shortcuts identically without distinction.
   - `GetItemPosition(pidl)` retrieves the relative coordinate `(x, y)` of the icon within the desktop view.
4. **`SHGetDesktopFolder().GetDisplayNameOf(...)`**:
   Retrieves the exact display name of the item shown under the desktop icon.
5. **`ClientToScreen(...)`**:
   Translates desktop ListView coordinates to absolute screen pixels, taking into account multi-monitor configurations and per-monitor DPI scaling.

### 2. How the Transparent Click-Through Overlay Works

1. **Layered Window Setup**:
   A borderless top-level window (`overrideredirect(True)`) is configured with `wm_attributes("-transparentcolor", key)` and `attributes("-topmost", True)`.
2. **Win32 Click-Through Styles**:
   Using `ctypes.windll.user32.SetWindowLongW` with `GWL_EXSTYLE (-20)`:
   - `WS_EX_TRANSPARENT (0x20)`: Mouse clicks pass through directly to windows underneath (the Windows desktop and icons).
   - `WS_EX_LAYERED (0x80000)`: Enables hardware-accelerated transparency keying.
   - `WS_EX_NOACTIVATE (0x08000000)`: Prevents the overlay from stealing focus from the desktop.
   - `WS_EX_TOOLWINDOW (0x80)`: Keeps the overlay off the Alt+Tab switcher and taskbar.
3. **Visual Composition**:
   - **Protective Layer**: A delicate dashed pastel sleeve outline drawn around the 70x70 icon area.
   - **Grommet Eyelet**: A small metallic ring drawn at the bottom-right corner of the sleeve `(attach_x, attach_y)`.
   - **Accessory Anchor**: The transparent PNG keychain's metal hook `(23, 0)` is positioned right on the eyelet so the beads and fish hang down and outside the icon.
4. **Position Tracking Loop**:
   Every 60ms, the position provider calls `IFolderView.GetItemPosition`. If the coordinates change (e.g. icon dragged), `geometry(width x height + X + Y)` is updated smoothly with virtually zero CPU overhead (< 0.5%).

---

## 📁 Project Structure

```
Allikk Aabharanam/
├── app.py                      # Root launcher for Digital Accessories
├── requirements.txt            # Python dependencies (pywin32, pillow)
├── assets/
│   ├── desktop_accessory_keychain.jpg  # Source reference artwork
│   └── keychain.png            # Master transparent PNG
├── tracker/                    # Phase 1: COM tracking code (UNMODIFIED)
│   ├── __init__.py
│   ├── com_method.py
│   └── desktop_finder.py
└── phase2/                     # Phase 2: Application, overlay & accessories
    ├── assets/
    │   └── keychain.png        # Transparent PNG keychain asset
    ├── accessory.py            # Accessory model & registry (extensible)
    ├── attachment.py           # Desktop item attachment model
    ├── tracker_bridge.py       # Wrapper & enumerator for Phase 1 tracker
    ├── overlay.py              # Click-through transparent overlay window
    ├── app.py                  # "Digital Accessories" desktop GUI surface
    ├── run_demo.py             # CLI runner / entry point
    └── README.md
```