"""Transparent click-through overlay window for Digital Accessories.

This module implements a transparent, click-through Windows overlay that displays
a subtle protective border around a desktop icon and visually attaches a keychain
accessory to its bottom-right corner. All mouse clicks pass directly through to
the underlying desktop icon.
"""

import ctypes
from ctypes import wintypes
import logging
import tkinter as tk
from pathlib import Path
from typing import Callable, Optional

from PIL import Image, ImageTk

from .accessory import Accessory, FISH_KEYCHAIN

# Extended window styles for Win32 API
GWL_EXSTYLE = -20
WS_EX_TRANSPARENT = 0x00000020   # Mouse clicks pass through
WS_EX_LAYERED = 0x00080000       # Supports transparency keying
WS_EX_TOOLWINDOW = 0x00000080    # Hides from Alt+Tab switcher
WS_EX_NOACTIVATE = 0x08000000    # Window cannot receive focus

# Color key used for transparency
TRANSPARENT_COLOR = "#000001"


def enable_dpi_awareness():
    """Ensure process coordinates match screen pixels 1:1 on high-DPI displays."""
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)  # Per-monitor DPI aware
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


class TransparentOverlay:
    """Click-through overlay window that follows a real Windows desktop icon."""

    def __init__(
        self,
        accessory: Accessory = FISH_KEYCHAIN,
        position_provider: Optional[Callable[[], Optional[tuple[int, int]]]] = None,
        poll_ms: int = 60,
        parent: Optional[tk.Tk] = None,
        on_position_changed: Optional[Callable[[Optional[tuple[int, int]]], None]] = None,
    ):
        """Initialize the transparent overlay window.
        
        Args:
            accessory: The accessory definition to display.
            position_provider: Callable returning the target icon's (x, y) screen coords.
            poll_ms: Polling interval in milliseconds (default 60ms for smooth 16fps tracking).
            parent: Optional parent Tk instance.
            on_position_changed: Optional callback when position changes.
        """
        enable_dpi_awareness()

        self.accessory = accessory
        self.position_provider = position_provider
        self.poll_ms = poll_ms
        self.on_position_changed = on_position_changed
        self.is_running = False
        self._last_pos: Optional[tuple[int, int]] = None
        self._is_external_root = parent is not None

        # Create window
        if self._is_external_root:
            self.root = tk.Toplevel(parent)
        else:
            self.root = tk.Tk()

        self.root.withdraw()
        self.root.overrideredirect(True)
        self.root.attributes("-topmost", True)
        self.root.wm_attributes("-transparentcolor", TRANSPARENT_COLOR)
        self.root.configure(bg=TRANSPARENT_COLOR)

        # Protective layer dimensions (surrounds standard 70x70 desktop icon area)
        self.icon_box_x = 4
        self.icon_box_y = 2
        self.icon_box_w = 70
        self.icon_box_h = 70

        # Load transparent PNG keychain asset
        self.image = self._load_accessory_image()
        self.photo = ImageTk.PhotoImage(self.image)

        # Attachment geometry:
        # Hook anchor is at (hook_anchor_x, hook_anchor_y) in the keychain image.
        # It attaches to the bottom-right corner of the protective box: (icon_box_x + icon_box_w, icon_box_y + icon_box_h)
        hook_x, hook_y = self.accessory.anchor
        self.attach_x = self.icon_box_x + self.icon_box_w
        self.attach_y = self.icon_box_y + self.icon_box_h

        # Position of keychain image relative to overlay window
        self.keychain_draw_x = self.attach_x - hook_x
        self.keychain_draw_y = self.attach_y - hook_y

        # Window dimensions
        self.win_width = max(self.icon_box_x + self.icon_box_w + 10, self.keychain_draw_x + self.image.width + 10)
        self.win_height = self.keychain_draw_y + self.image.height + 10

        # Canvas for drawing protective layer and keychain
        self.canvas = tk.Canvas(
            self.root,
            width=self.win_width,
            height=self.win_height,
            bg=TRANSPARENT_COLOR,
            highlightthickness=0,
        )
        self.canvas.pack(fill="both", expand=True)

        self._render_scene()
        self._make_click_through()

    def _load_accessory_image(self) -> Image.Image:
        """Load and prepare the transparent PNG accessory image."""
        img_path = Path(self.accessory.image_path)
        if not img_path.is_absolute():
            img_path = Path(__file__).parent / img_path

        if not img_path.exists():
            # Fallback to assets directory
            img_path = Path(__file__).parent / "assets" / "keychain.png"

        image = Image.open(img_path).convert("RGBA")
        if self.accessory.default_scale != 1.0:
            new_w = int(image.width * self.accessory.default_scale)
            new_h = int(image.height * self.accessory.default_scale)
            image = image.resize((new_w, new_h), Image.Resampling.LANCZOS)
        return image

    def _make_click_through(self):
        """Apply Win32 styles to make the overlay click-through and non-activating."""
        try:
            self.root.update_idletasks()
            hwnd = self.root.winfo_id()
            user32 = ctypes.windll.user32
            styles = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
            user32.SetWindowLongW(
                hwnd,
                GWL_EXSTYLE,
                styles | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            )
        except Exception as e:
            logging.warning("Could not apply Win32 click-through styles: %s", e)

    def _render_scene(self):
        """Draw the subtle protective sleeve and attach the keychain."""
        self.canvas.delete("all")

        # 1. Subtle protective layer around the icon
        # Thin rounded-like rectangle with subtle soft border
        bx1 = self.icon_box_x
        by1 = self.icon_box_y
        bx2 = bx1 + self.icon_box_w
        by2 = by1 + self.icon_box_h

        # Thin outer border
        self.canvas.create_rectangle(
            bx1, by1, bx2, by2,
            outline="#98d8c8",  # Soft pastel teal matching the keychain
            width=1,
            dash=(4, 3),        # Subtle dashed line for a delicate sleeve look
        )

        # 2. Small metallic eyelet / grommet ring at the bottom-right corner
        # This is where the keychain's hook visually clasps
        ring_r = 3
        self.canvas.create_oval(
            self.attach_x - ring_r, self.attach_y - ring_r,
            self.attach_x + ring_r, self.attach_y + ring_r,
            outline="#c0c8d0",
            fill="#506070",
            width=1.5,
        )

        # 3. Transparent PNG keychain accessory
        self.canvas.create_image(
            self.keychain_draw_x,
            self.keychain_draw_y,
            image=self.photo,
            anchor="nw",
        )

    def set_position_provider(self, provider: Callable[[], Optional[tuple[int, int]]]):
        """Change the active position provider dynamically."""
        self.position_provider = provider
        self._last_pos = None

    def update_position(self):
        """Poll the position provider and update overlay geometry."""
        if not self.is_running:
            return

        pos = None
        if self.position_provider:
            try:
                pos = self.position_provider()
            except Exception as e:
                logging.debug("Position provider error: %s", e)
                pos = None

        if pos != self._last_pos:
            self._last_pos = pos
            if self.on_position_changed:
                try:
                    self.on_position_changed(pos)
                except Exception:
                    pass

            if pos is not None:
                icon_x, icon_y = pos
                # Position overlay at the icon screen coordinates
                self.root.geometry(f"{self.win_width}x{self.win_height}+{icon_x}+{icon_y}")
                if self.root.state() == "withdrawn":
                    self.root.deiconify()
            else:
                # Target item not found or off-screen, hide overlay
                if self.root.state() != "withdrawn":
                    self.root.withdraw()

        if self.is_running:
            self.root.after(self.poll_ms, self.update_position)

    def start(self):
        """Start tracking and show the overlay."""
        if self.is_running:
            return
        self.is_running = True
        self.update_position()

    def stop(self):
        """Stop tracking and hide the overlay."""
        self.is_running = False
        try:
            self.root.withdraw()
        except Exception:
            pass

    def run(self):
        """Run standalone event loop (used for CLI mode)."""
        self.start()
        self.root.deiconify()
        self.root.mainloop()

    def close(self):
        """Cleanly destroy the overlay window."""
        self.is_running = False
        try:
            self.root.destroy()
        except Exception:
            pass