# Desktop Keychain Accessory - Proof of Concept

A complete, working Windows desktop application that visually attaches a digital keychain accessory to real desktop files, folders, and application shortcuts. The keychain appears as a transparent overlay that automatically follows the item when dragged across the desktop.

## Features

✅ **Non-invasive Design**: No modifications to original files, folders, or shortcuts—only visual overlays.  
✅ **Desktop Item Detection**: Uses Windows UIAutomation to reliably detect and track files, folders, and app shortcuts on the desktop.  
✅ **Transparent Overlay**: Keychain renders as a transparent PNG with click-through behavior—original items remain fully interactive.  
✅ **Real-time Tracking**: Monitors desktop item positions and automatically repositions the keychain overlay at 100ms intervals.  
✅ **Drag Support**: When you drag a desktop item, the keychain follows seamlessly.  
✅ **Error Handling**: Gracefully handles deleted items, missing items, or unavailable positions.  
✅ **Clean Shutdown**: Properly releases all resources and overlay windows on exit.

## Architecture

### Core Components

#### 1. **DesktopIconDetector.cs** - Desktop Item Detection
- **Purpose**: Detects and retrieves real desktop items (files, folders, app shortcuts).
- **Implementation**: Uses Windows UIAutomation to enumerate child elements of the Progman (Program Manager) window—the desktop's underlying container.
- **API Used**: `AutomationElement.RootElement`, `TreeWalker.ControlViewWalker`, and `AutomationElement.BoundingRectangle`.
- **Key Method**: `GetDesktopItemPosition()` retrieves the current screen position (Rect) of any desktop item.
- **Why UIAutomation**: Provides reliable, programmatic access to desktop items across Windows versions without requiring shell shortcuts or file system parsing.

#### 2. **KeychainOverlay.cs** - Transparent Overlay Window
- **Purpose**: Creates and manages a transparent, click-through overlay window displaying the keychain.
- **Implementation**: 
  - Uses layered windows (WS_EX_LAYERED) with Win32 API for efficiency.
  - WS_EX_TRANSPARENT flag makes the overlay click-through—mouse events pass through to the desktop item.
  - WS_EX_TOPMOST ensures the overlay always appears above the desktop.
  - UpdateLayeredWindow API efficiently updates the bitmap without redraw overhead.
- **Key Methods**:
  - `CreateOverlayWindow()`: Sets up the layered window with proper styles.
  - `SetPosition()`: Repositions the overlay relative to the desktop item (offset by +10px X and Y).

#### 3. **KeychainTracker.cs** - Position Tracking Loop
- **Purpose**: Continuously monitors the desktop item's position and updates the overlay.
- **Implementation**:
  - Runs on a background thread (Task.Run).
  - Checks the item's position every 100ms.
  - Retries up to 3 times if the position is temporarily unavailable (handles cases like items being briefly selected or moved).
  - Raises `ItemLost` event if the item disappears permanently.
- **Why Background Thread**: Prevents UI blocking while monitoring desktop positions.

#### 4. **AssetGenerator.cs** - Keychain Asset Generation
- **Purpose**: Generates a placeholder keychain PNG asset at runtime if it doesn't exist.
- **Asset Spec**: 64x64 transparent PNG with a gold keyring and key design.
- **Easy Replacement**: Code structure allows simple replacement of the PNG with custom artwork.

#### 5. **MainWindow.xaml / MainWindow.xaml.cs** - User Interface
- **Workflow**:
  1. User clicks "Refresh Desktop Items" → detects all desktop items and displays them in a ListBox.
  2. User selects an item → enables the "Attach Keychain" button.
  3. User clicks "Attach Keychain" → creates overlay and starts tracking.
  4. User drags the item on desktop → overlay follows in real-time.
  5. User clicks "Detach Keychain" → stops tracking and cleans up resources.

#### 6. **Win32Interop.cs** - Native API Bindings
- **Provides**: All P/Invoke declarations for layered windows, position updates, and device contexts.
- **Includes Structs**: `BLENDFUNCTION`, `SIZE`, `POINT`, `BITMAPINFO`.

## How It Works: Desktop Item Detection

### The Challenge
Reliably detecting and tracking files, folders, and app shortcuts on the actual Windows desktop (not within Explorer windows).

### The Solution: UIAutomation + Progman Window

1. **Find the Desktop Container**: `FindWindow("Progman", "Program Manager")`
   - Progman is the program manager—the window that hosts desktop icons.
   - All desktop items are child elements of this window.

2. **Enumerate All Items**: `TreeWalker.ControlViewWalker.GetFirstChild()` → `GetNextSibling()`
   - Walks the automation tree to discover all child elements (desktop icons).
   - Each element has a `Name` (the item's display name) and `BoundingRectangle` (screen coordinates).

3. **Retrieve Real-time Position**: `AutomationElement.Current.BoundingRectangle`
   - Returns the current screen position of the item.
   - Used every 100ms in the tracking loop to detect movement.

### Why This Approach Works
- **Non-invasive**: No file system access, no shell operations—pure visual detection.
- **Universal**: Works for files, folders, and shortcuts uniformly.
- **Reliable**: UIAutomation is a stable, officially supported Windows API.
- **DPI-Aware**: BoundingRectangle automatically accounts for DPI scaling.

## How It Works: Transparent Overlay

### The Challenge
Create a window that displays the keychain, doesn't intercept mouse events, and updates efficiently.

### The Solution: Layered Windows with Win32

1. **Create Layered Window** (WS_EX_LAYERED)
   - Enables per-pixel alpha blending.
   - Allows transparent regions in the window content.

2. **Make Click-Through** (WS_EX_TRANSPARENT)
   - Mouse events pass through the overlay to the desktop and desktop items.
   - Original items remain fully interactive (clickable, draggable, selectable).

3. **Update with UpdateLayeredWindow()**
   - Directly updates the bitmap content without WM_PAINT messages.
   - Efficient for frequent position updates.

4. **Position Relative to Item**
   - Every 100ms, the tracking loop calls `SetPosition(itemX, itemY, itemWidth, itemHeight)`.
   - The overlay is positioned at `(itemX + itemWidth + 10, itemY + itemHeight + 10)`—bottom-right corner of the item.

### DPI and Multi-Monitor Support
- `BoundingRectangle` from UIAutomation is already DPI-aware (returns logical screen coordinates that account for DPI).
- Works across multiple monitors because UIAutomation reports absolute screen positions.

## Directory Structure

```
DesktopKeychainApp.csproj          - Project file (.NET 8, WPF)
Win32Interop.cs                    - Native API bindings
DesktopIconDetector.cs             - Desktop item detection (UIAutomation)
KeychainOverlay.cs                 - Transparent overlay window
KeychainTracker.cs                 - Position tracking loop
AssetGenerator.cs                  - Keychain asset generation
MainWindow.xaml                    - UI layout
MainWindow.xaml.cs                 - UI logic
App.xaml                           - Application config
App.xaml.cs                        - Application entry point
Assets/keychain.png                - Keychain accessory image (generated at runtime)
```

## Build Instructions

### Prerequisites
- **Windows 10/11** (desktop detection via UIAutomation)
- **.NET 8 SDK** ([Download](https://dotnet.microsoft.com/download))
- **Visual Studio 2022** (recommended) or command line tools

### Build via Command Line

```bash
cd "path\to\DesktopKeychainApp"
dotnet build --configuration Release
```

### Build via Visual Studio

1. Open `DesktopKeychainApp.csproj` in Visual Studio 2022.
2. **Build** → **Build Solution** (Ctrl+Shift+B).
3. The application will compile to `bin\Release\net8.0-windows\`.

## Run Instructions

### From Command Line
```bash
cd "path\to\DesktopKeychainApp"
dotnet run
```

### From Visual Studio
1. Press **F5** to start debugging, or **Ctrl+F5** to run without debugging.

### From Executable
After building, run the compiled .exe:
```bash
bin\Release\net8.0-windows\DesktopKeychainApp.exe
```

## Usage

1. **Launch the application** — the main window appears.
2. **Click "Refresh Desktop Items"** — scans the desktop and populates the list.
3. **Select a desktop item** (file, folder, or shortcut) from the list.
4. **Click "Attach Keychain"** — the keychain overlay appears, positioned at the bottom-right of the item's icon.
5. **Drag the desktop item** around the desktop — watch the keychain follow in real-time!
6. **Click "Detach Keychain"** to stop tracking and remove the overlay.

## Technical Details

### Position Tracking Loop
- **Interval**: 100ms (10 updates per second).
- **Failure Tolerance**: Retries up to 3 times if position is unavailable (handles temporary unavailability).
- **Thread Safety**: Runs on background thread; UI updates marshalled to the main thread via Dispatcher.

### Overlay Performance
- **No FPS impact** on desktop or applications—overlay is a separate Win32 window with layered rendering.
- **CPU Usage**: Minimal; only reads position data and calls `SetWindowPos()`.

### Error Handling
- If a desktop item is deleted while tracking → `ItemLost` event fired; overlay automatically detached.
- If a desktop item moves off-screen → overlay follows and may be positioned off-screen (expected behavior).
- If the overlay window is closed externally → tracking stops and resources are cleaned up.

## Customization

### Replace the Keychain Asset
1. Create a new 64x64 transparent PNG (or any size).
2. Place it at `Assets\keychain.png`.
3. Restart the application (the asset generator will skip if the file exists).

To regenerate the default asset, delete `Assets\keychain.png` and restart.

### Adjust Overlay Position
In `MainWindow.xaml.cs`, change the offsets in `AttachButton_Click`:
```csharp
_currentOverlay = new KeychainOverlay(keychainBitmap, offsetX: 20, offsetY: 20);
```

### Change Tracking Interval
In `KeychainTracker.cs`, adjust `TRACKING_INTERVAL_MS`:
```csharp
private const int TRACKING_INTERVAL_MS = 50; // Faster updates
```

## Known Limitations & Future Improvements

### Current Scope (Proof of Concept)
- Single keychain accessory type only.
- No animation or customization system.
- No accessory marketplace or multiple accessories per item.

### Tested Scenarios
✅ Files and folders on desktop  
✅ Application shortcuts on desktop  
✅ Dragging items across the desktop  
✅ Moving items between monitors  
✅ Deleting a tracked item  
✅ Multi-monitor setups  

### Not Tested
- Windows 7 (UIAutomation availability may differ)
- Very large primary monitor (>4K) with extreme DPI scaling
- Rapid item movements (> 20 moves per second)

## Troubleshooting

**Desktop items not appearing in the list:**
- Ensure you have items on your actual desktop, not in Explorer windows.
- Right-click desktop → "View" → ensure "Auto arrange icons" or similar is enabled.
- Try clicking "Refresh Desktop Items" again.

**Overlay not appearing:**
- Confirm the `Assets\keychain.png` file was generated (check application directory).
- Ensure the selected desktop item is still visible on-screen.
- Check Windows taskbar isn't covering the overlay position.

**Overlay not following:**
- Verify the desktop item hasn't been deleted or moved to an Explorer window.
- Try selecting a different item and re-attaching.

**Application crashes on start:**
- Ensure .NET 8 SDK is installed: `dotnet --version`
- Check Windows version is Windows 10 or later (UIAutomation requirement).

## Code Comments

All source files include detailed comments explaining:
- **Desktop detection logic** (DesktopIconDetector.cs)
- **Overlay window creation and styling** (KeychainOverlay.cs, Win32Interop.cs)
- **Tracking loop and threading** (KeychainTracker.cs)
- **Event handling and UI updates** (MainWindow.xaml.cs)

## License

This is a proof-of-concept demonstration. Feel free to modify and extend as needed.

## Summary

This application successfully demonstrates:
1. ✅ **Non-invasive visual attachment** to real desktop items.
2. ✅ **Reliable desktop item detection** via UIAutomation.
3. ✅ **Transparent, click-through overlays** using Win32 layered windows.
4. ✅ **Real-time position tracking** with multi-monitor support.
5. ✅ **Clean resource management** and error handling.

The architecture is modular and easily extensible for future enhancements (animations, multiple accessories, customization systems, etc.).
![Screenshot1](Add screenshot 1 here with proper name)
*Add caption explaining what this shows*

![Screenshot2](Add screenshot 2 here with proper name)
*Add caption explaining what this shows*

![Screenshot3](Add screenshot 3 here with proper name)
*Add caption explaining what this shows*

# Diagrams
![Workflow](Add your workflow/architecture diagram here)
*Add caption explaining your workflow*

For Hardware:

# Schematic & Circuit
![Circuit](Add your circuit diagram here)
*Add caption explaining connections*

![Schematic](Add your schematic diagram here)
*Add caption explaining the schematic*

# Build Photos
![Components](Add photo of your components here)
*List out all components shown*

![Build](Add photos of build process here)
*Explain the build steps*

![Final](Add photo of final product here)
*Explain the final build*

### Project Demo
# Video
[Add your demo video link here]
*Explain what the video demonstrates*

# Additional Demos
[Add any extra demo materials/links]

## Team Contributions
- [Name 1]: [Specific contributions]
- [Name 2]: [Specific contributions]
- [Name 3]: [Specific contributions]

---
Made with ❤️ at TinkerHub Useless Projects 

![Static Badge](https://img.shields.io/badge/TinkerHub-24?color=%23000000&link=https%3A%2F%2Fwww.tinkerhub.org%2F)
![Static Badge](https://img.shields.io/badge/UselessProjects--26-26?link=https%3A%2F%2Ftinkerhub.org%2Fevents%2F1M8ORET9A1%2Fuseless-projects-3.0)



