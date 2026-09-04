# Quick Start Guide - Desktop Keychain Accessory

## Prerequisites Check

Before building, ensure you have:

```powershell
# Check .NET 8 SDK is installed
dotnet --version
# Should output: 8.x.x or later
```

If .NET 8 is not installed, download from: https://dotnet.microsoft.com/download/dotnet/8.0

---

## Build Instructions

### Option 1: Command Line (Recommended)

```powershell
# Navigate to project directory
cd "path\to\DesktopKeychainApp"

# Build Release version
dotnet build --configuration Release

# Output location:
# bin\Release\net8.0-windows\DesktopKeychainApp.exe
```

### Option 2: Visual Studio

1. Open `DesktopKeychainApp.csproj` in Visual Studio 2022
2. Press **Ctrl+Shift+B** (Build Solution)
3. Executable: `bin\Release\net8.0-windows\DesktopKeychainApp.exe`

### Option 3: Run Directly (Without Building)

```powershell
cd "path\to\DesktopKeychainApp"
dotnet run
```

---

## First Run

1. **Minimize or move all windows** so you can see the desktop clearly

2. **Place some files/folders on your desktop** (if you don't have any)
   - Copy a document to desktop
   - Create a new folder on desktop
   - Add an application shortcut to desktop

3. **Run the application**
   ```powershell
   # Option A: Run from compiled executable
   bin\Release\net8.0-windows\DesktopKeychainApp.exe
   
   # Option B: Run from command line
   dotnet run
   ```

4. **The application window appears**
   - "Refresh Desktop Items" button on the left
   - Empty ListBox in the center
   - Status message at the bottom

---

## Usage: Attaching a Keychain

### Step 1: Detect Desktop Items
Click the **"Refresh Desktop Items"** button
- Application scans your desktop (takes ~1-2 seconds)
- List populates with detected files, folders, shortcuts
- You should see your desktop items by name

### Step 2: Select an Item
Click any item in the list
- Item is highlighted
- **"Attach Keychain"** button becomes enabled
- Try selecting different items to see them highlighted

### Step 3: Attach the Keychain
Click the **"Attach Keychain"** button
- Keychain overlay appears on your desktop
- Positioned at the bottom-right corner of the selected item's icon
- Overlay is transparent (golden keychain with key design)
- Status shows: "Keychain attached to [item name]. Drag it around..."

### Step 4: Drag the Item
Click and drag the selected item across your desktop
- Keychain follows the item smoothly
- Works best when dragging slowly at first
- Try moving the item to different corners of the desktop
- Watch the overlay update in real-time (100ms refresh)

### Step 5: Detach the Keychain
Click the **"Detach Keychain"** button
- Overlay disappears
- Item remains unchanged
- Application ready to attach to another item

---

## What You Should See

### The Keychain Accessory

- **Appearance**: Golden keychain with a ring and key design
- **Size**: 64x64 pixels
- **Position**: Bottom-right corner of the desktop item's icon (offset by 10px)
- **Transparency**: Semi-transparent; you can see the desktop behind it
- **Interaction**: It doesn't respond to clicks (you can click through it to interact with the item)

### The Status Message

Initial state:
```
Ready. Click 'Refresh Desktop Items' to begin.
```

After refresh:
```
Found 5 desktop items.
```

When attached:
```
Keychain attached to 'Document.txt'. Drag it around to see the accessory follow!
```

When dragging:
```
Tracking 'Document.txt' - keychain is following!
```

---

## Troubleshooting

### "Found 0 desktop items"

**Problem**: Desktop items not detected

**Solutions**:
1. Ensure you have files/folders/shortcuts **on the desktop itself** (not in Explorer windows)
2. Right-click desktop → View → Check "Auto arrange icons" is enabled
3. Click "Refresh Desktop Items" again
4. If still empty, temporarily create a file on desktop:
   - Open Notepad
   - Save as `Test.txt` on desktop
   - Click "Refresh" again

### Overlay not appearing after clicking "Attach"

**Problem**: Keychain doesn't show

**Solutions**:
1. Check if the application is in focus; if not, click on the selected item in the list
2. Ensure the desktop item is visible on-screen
3. The taskbar might be covering the overlay area—try attaching to an item away from the taskbar
4. Check if `Assets/keychain.png` was generated:
   - Look for `Assets` folder next to `.exe`
   - It should contain `keychain.png`

### Overlay isn't moving when I drag the item

**Problem**: Keychain not following

**Solutions**:
1. Drag slowly at first; let the tracking loop catch up
2. Don't drag too rapidly (the 100ms tracking interval has limits)
3. If still not moving, click "Detach" and try a different item
4. Restart the application if tracking becomes stuck

### Application crashes on startup

**Problem**: "CLR error" or immediate crash

**Solutions**:
1. Verify .NET 8 is installed:
   ```powershell
   dotnet --version
   ```
2. If not installed, install from: https://dotnet.microsoft.com/download/dotnet/8.0
3. Reinstall .NET if already installed:
   ```powershell
   dotnet workload restore
   ```

### Item disappears from list after "Attach"

**Problem**: Item removed while tracking

**Explanation**: If you drag a desktop item into a folder or delete it while tracking, the application will detect it's missing and automatically detach the keychain.

**Solution**: Click "Refresh Desktop Items" to see current items and attach to another one.

---

## Advanced Usage

### Customize the Overlay Position

To change where the keychain appears relative to the item:

1. Open `MainWindow.xaml.cs`
2. Find line (in `AttachButton_Click`):
   ```csharp
   _currentOverlay = new KeychainOverlay(keychainBitmap, offsetX: 10, offsetY: 10);
   ```
3. Change `10` values:
   - `offsetX: 0` — overlay at left edge of item
   - `offsetY: 0` — overlay at top edge of item
   - `offsetX: 20, offsetY: 20` — further away from item
4. Rebuild and run

### Replace the Keychain Asset

To use a custom keychain icon:

1. Create a 64x64 transparent PNG image (or any size)
2. Save it as `Assets/keychain.png` in the application directory
3. Restart the application (the generator skips existing files)
4. Attach keychain—your custom asset will appear!

To reset to the default asset:
1. Delete `Assets/keychain.png`
2. Restart the application (generator will recreate it)

### Speed Up/Slow Down Tracking

To change how often the overlay updates:

1. Open `KeychainTracker.cs`
2. Find:
   ```csharp
   private const int TRACKING_INTERVAL_MS = 100; // Update every 100ms
   ```
3. Change to your desired interval:
   - `50` — faster (5 updates/second, more CPU usage)
   - `200` — slower (2 updates/second, less CPU usage)
4. Rebuild and run

---

## Performance Tips

### If you notice high CPU usage:
- Increase `TRACKING_INTERVAL_MS` (e.g., 200ms)
- Detach keychains you're not actively using
- Reduce number of active overlays

### If overlay seems to lag behind:
- Decrease `TRACKING_INTERVAL_MS` (e.g., 50ms)
- Be sure nothing else is consuming CPU
- Try moving item more slowly

### If many overlays are active:
- Each overlay is a separate window (~2% CPU per 100ms interval)
- Detach inactive overlays to free resources
- Don't attach more than 3-4 keychains at once

---

## Examples

### Example 1: Organizing Documents
1. Create several `.txt` files on your desktop
2. Attach keychains to important documents
3. Drag them to mark their organization
4. The keychains help visually distinguish them

### Example 2: App Shortcuts
1. Create shortcuts to your favorite apps on the desktop
2. Attach a keychain to your most-used app
3. The keychain acts as a visual indicator

### Example 3: Multi-Monitor Setup
1. Place items on different monitors
2. Attach keychains—they work across monitors
3. Move items between monitors—keychains follow

---

## Getting Help

### Check the Documentation
- `README.md` — Complete usage and architecture
- `IMPLEMENTATION_SUMMARY.md` — Technical deep-dive
- `PROJECT_MANIFEST.md` — Project structure
- Inline code comments — Implementation details

### Common Questions

**Q: Does the application modify my files?**
A: No. It only reads the position of items and displays an overlay. Your files are completely unchanged.

**Q: Can I attach multiple keychains?**
A: The current proof-of-concept supports one keychain at a time. You can detach and attach to a different item, but only one overlay runs per session. Future versions could support multiple simultaneous keychains.

**Q: Does it work with networked/cloud files?**
A: Yes, as long as the file is visible on your desktop, it can be tracked.

**Q: Can I use it with the taskbar or start menu?**
A: This prototype works only with items on the actual desktop, not the taskbar or start menu.

**Q: What if I move my monitor around?**
A: The application uses absolute screen coordinates, so it should handle monitor changes. Try refreshing if you switch monitors with an active keychain attached.

---

## Next Steps

### Try the Application
1. Build: `dotnet build --configuration Release`
2. Run: `bin\Release\net8.0-windows\DesktopKeychainApp.exe`
3. Attach a keychain to a desktop item
4. Drag it around and watch the keychain follow!

### Explore the Code
- Start with [DesktopIconDetector.cs](DesktopIconDetector.cs) to understand detection
- Read [KeychainOverlay.cs](KeychainOverlay.cs) to see overlay creation
- Check [KeychainTracker.cs](KeychainTracker.cs) for tracking logic
- Review [MainWindow.xaml.cs](MainWindow.xaml.cs) for UI flow

### Customize
- Replace the keychain PNG with your own design
- Adjust overlay position offsets
- Modify tracking interval for different speeds
- Add your own event handlers for tracking events

---

## Success!

If you can:
1. ✅ Launch the application
2. ✅ See a list of desktop items
3. ✅ Select an item
4. ✅ Click "Attach Keychain"
5. ✅ See a keychain overlay on your desktop
6. ✅ Drag the item and watch the keychain follow

**You have successfully set up and are using the Desktop Keychain Accessory!**

---

## Questions or Issues?

Refer to the troubleshooting section above, or review the detailed documentation in:
- `README.md` (comprehensive guide)
- `IMPLEMENTATION_SUMMARY.md` (technical details)

**Happy keychaining!** 🔑
