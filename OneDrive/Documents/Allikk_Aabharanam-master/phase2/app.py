"""Digital Accessories - Desktop Application Surface.

This application provides a clean, native graphical interface allowing users to:
1. Detect real desktop files, folders, and application shortcuts.
2. Select an item from the desktop.
3. Attach a decorative keychain overlay to the selected desktop icon.
4. Drag the desktop icon and watch the keychain smoothly follow it.
5. Detach the keychain cleanly at any time without modifying any desktop files.
"""

import sys
import tkinter as tk
from tkinter import ttk, messagebox
from pathlib import Path
from typing import Optional

from PIL import Image, ImageTk

from .accessory import FISH_KEYCHAIN, AVAILABLE_ACCESSORIES
from .attachment import Attachment
from .overlay import TransparentOverlay, enable_dpi_awareness
from .tracker_bridge import get_desktop_items, get_item_position, DesktopItem


class DigitalAccessoriesApp:
    """Main application surface for Digital Accessories."""

    def __init__(self, root: tk.Tk):
        self.root = root
        enable_dpi_awareness()

        self.root.title("Digital Accessories")
        self.root.geometry("460x580")
        self.root.minsize(440, 540)
        self.root.configure(bg="#f4f6f9")

        # Application state
        self.current_accessory = FISH_KEYCHAIN
        self.desktop_items: list[DesktopItem] = []
        self.active_attachment: Optional[Attachment] = None
        self.overlay: Optional[TransparentOverlay] = None

        self._setup_styles()
        self._build_ui()
        self._load_desktop_items()

        # Handle window close
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def _setup_styles(self):
        """Configure ttk styles for a modern, clean card-based UI."""
        self.style = ttk.Style()
        try:
            self.style.theme_use("clam")
        except Exception:
            pass

        self.style.configure(
            "TCombobox",
            fieldbackground="#ffffff",
            background="#e5e9f0",
            foreground="#2e3440",
            font=("Segoe UI", 10),
        )

    def _build_ui(self):
        """Build the clean application surface."""
        # Top banner / Header
        header_frame = tk.Frame(self.root, bg="#ffffff", padx=24, pady=18, highlightthickness=1, highlightbackground="#e2e8f0")
        header_frame.pack(fill="x")

        title_label = tk.Label(
            header_frame,
            text="🌸 Digital Accessories",
            font=("Segoe UI", 16, "bold"),
            bg="#ffffff",
            fg="#1e293b",
        )
        title_label.pack(anchor="w")

        subtitle_label = tk.Label(
            header_frame,
            text="Attach decorative keychains to real Windows desktop icons",
            font=("Segoe UI", 9),
            bg="#ffffff",
            fg="#64748b",
        )
        subtitle_label.pack(anchor="w", pady=(2, 0))

        # Main content area
        content = tk.Frame(self.root, bg="#f4f6f9", padx=20, pady=16)
        content.pack(fill="both", expand=True)

        # Card 1: Desktop Target Selector
        target_card = tk.LabelFrame(
            content,
            text=" 📁 Select Desktop Item ",
            font=("Segoe UI", 10, "bold"),
            bg="#ffffff",
            fg="#334155",
            padx=16,
            pady=14,
            relief="solid",
            bd=1,
        )
        target_card.pack(fill="x", pady=(0, 14))

        target_desc = tk.Label(
            target_card,
            text="Choose a real file, folder, or shortcut on your desktop:",
            font=("Segoe UI", 9),
            bg="#ffffff",
            fg="#64748b",
        )
        target_desc.pack(anchor="w", pady=(0, 8))

        select_row = tk.Frame(target_card, bg="#ffffff")
        select_row.pack(fill="x")

        self.item_combo_var = tk.StringVar()
        self.item_combo = ttk.Combobox(
            select_row,
            textvariable=self.item_combo_var,
            state="readonly",
            font=("Segoe UI", 10),
        )
        self.item_combo.pack(side="left", fill="x", expand=True)
        self.item_combo.bind("<<ComboboxSelected>>", self._on_item_selected)

        refresh_btn = tk.Button(
            select_row,
            text="🔄 Refresh",
            font=("Segoe UI", 9),
            bg="#e2e8f0",
            fg="#1e293b",
            activebackground="#cbd5e1",
            relief="flat",
            padx=10,
            pady=4,
            cursor="hand2",
            command=self._load_desktop_items,
        )
        refresh_btn.pack(side="right", padx=(8, 0))

        self.item_count_label = tk.Label(
            target_card,
            text="Scanning desktop items...",
            font=("Segoe UI", 8),
            bg="#ffffff",
            fg="#94a3b8",
        )
        self.item_count_label.pack(anchor="w", pady=(6, 0))

        # Card 2: Accessory Preview Card
        accessory_card = tk.LabelFrame(
            content,
            text=" 🎀 Selected Accessory ",
            font=("Segoe UI", 10, "bold"),
            bg="#ffffff",
            fg="#334155",
            padx=16,
            pady=14,
            relief="solid",
            bd=1,
        )
        accessory_card.pack(fill="x", pady=(0, 14))

        acc_inner = tk.Frame(accessory_card, bg="#ffffff")
        acc_inner.pack(fill="x")

        # Image preview
        self.preview_canvas = tk.Canvas(
            acc_inner,
            width=64,
            height=80,
            bg="#f8fafc",
            highlightthickness=1,
            highlightbackground="#e2e8f0",
        )
        self.preview_canvas.pack(side="left", padx=(0, 14))
        self._load_preview_image()

        # Accessory Details
        acc_info = tk.Frame(acc_inner, bg="#ffffff")
        acc_info.pack(side="left", fill="both", expand=True)

        acc_name = tk.Label(
            acc_info,
            text=self.current_accessory.name,
            font=("Segoe UI", 11, "bold"),
            bg="#ffffff",
            fg="#0f172a",
        )
        acc_name.pack(anchor="w")

        acc_desc = tk.Label(
            acc_info,
            text=self.current_accessory.description,
            font=("Segoe UI", 8),
            bg="#ffffff",
            fg="#64748b",
            wraplength=260,
            justify="left",
        )
        acc_desc.pack(anchor="w", pady=(2, 4))

        acc_hint = tk.Label(
            acc_info,
            text="✨ Extensible slot (bows, hats, moustaches ready)",
            font=("Segoe UI", 8, "italic"),
            bg="#ffffff",
            fg="#10b981",
        )
        acc_hint.pack(anchor="w")

        # Card 3: Live Status & Control
        status_card = tk.LabelFrame(
            content,
            text=" ⚡ Attachment & Tracking ",
            font=("Segoe UI", 10, "bold"),
            bg="#ffffff",
            fg="#334155",
            padx=16,
            pady=14,
            relief="solid",
            bd=1,
        )
        status_card.pack(fill="x", pady=(0, 14))

        # Status indicator
        status_row = tk.Frame(status_card, bg="#ffffff")
        status_row.pack(fill="x", pady=(0, 8))

        self.status_badge = tk.Label(
            status_row,
            text="⚪ Detached",
            font=("Segoe UI", 9, "bold"),
            bg="#f1f5f9",
            fg="#64748b",
            padx=8,
            pady=3,
        )
        self.status_badge.pack(side="left")

        self.pos_label = tk.Label(
            status_row,
            text="Coordinates: ( - , - )",
            font=("Segoe UI", 9),
            bg="#ffffff",
            fg="#64748b",
        )
        self.pos_label.pack(side="right")

        # Big Action Button
        self.action_btn = tk.Button(
            status_card,
            text="Attach Keychain",
            font=("Segoe UI", 11, "bold"),
            bg="#0284c7",
            fg="#ffffff",
            activebackground="#0369a1",
            activeforeground="#ffffff",
            relief="flat",
            pady=10,
            cursor="hand2",
            command=self.toggle_attachment,
        )
        self.action_btn.pack(fill="x", pady=(4, 0))

        # Footer note
        footer_label = tk.Label(
            self.root,
            text="🔒 Safe Prototype: Desktop files are 100% untouched and unmodified.",
            font=("Segoe UI", 8),
            bg="#f4f6f9",
            fg="#94a3b8",
        )
        footer_label.pack(side="bottom", pady=(0, 10))

    def _load_preview_image(self):
        """Load and display the accessory thumbnail in the UI preview box."""
        try:
            img_path = self.current_accessory.image_path
            if not img_path.is_absolute():
                img_path = Path(__file__).parent / img_path
            img = Image.open(img_path).convert("RGBA")
            img.thumbnail((56, 72), Image.Resampling.LANCZOS)
            self.preview_photo = ImageTk.PhotoImage(img)
            self.preview_canvas.delete("all")
            # Center in canvas
            cx = (64 - img.width) // 2
            cy = (80 - img.height) // 2
            self.preview_canvas.create_image(cx, cy, image=self.preview_photo, anchor="nw")
        except Exception as e:
            self.preview_canvas.create_text(32, 40, text="🐟", font=("Segoe UI", 20))

    def _load_desktop_items(self):
        """Query and populate active desktop files, folders, and shortcuts."""
        self.desktop_items = get_desktop_items()
        names = [item.name for item in self.desktop_items]
        self.item_combo["values"] = names

        if names:
            # If no selection or previous selection lost, select first file/folder
            current = self.item_combo_var.get()
            if not current or current not in names:
                self.item_combo.current(0)
            self.item_count_label.config(
                text=f"Detected {len(names)} desktop items ready for attachment."
            )
        else:
            self.item_count_label.config(text="No desktop items found.")

    def _on_item_selected(self, event=None):
        """Handle user changing the selected desktop item."""
        if self.active_attachment:
            # Re-attach to the new target
            selected_name = self.item_combo_var.get()
            if selected_name:
                self._attach_to_item(selected_name)

    def toggle_attachment(self):
        """Toggle attaching or detaching the keychain."""
        if self.active_attachment:
            self.detach_keychain()
        else:
            selected_name = self.item_combo_var.get()
            if not selected_name:
                messagebox.showwarning("No Item Selected", "Please select a desktop item first.")
                return
            self._attach_to_item(selected_name)

    def _attach_to_item(self, item_name: str):
        """Attach the transparent overlay to the chosen desktop item."""
        self.active_attachment = Attachment(
            target_name=item_name,
            accessory_id=self.current_accessory.id,
        )

        position_provider = lambda: get_item_position(item_name)

        if not self.overlay:
            self.overlay = TransparentOverlay(
                accessory=self.current_accessory,
                position_provider=position_provider,
                poll_ms=60,
                parent=self.root,
                on_position_changed=self._on_overlay_pos_update,
            )
        else:
            self.overlay.set_position_provider(position_provider)

        self.overlay.start()

        # Update UI states
        self.action_btn.config(
            text="Detach Keychain",
            bg="#ef4444",
            activebackground="#dc2626",
        )
        self.status_badge.config(
            text=f"🟢 Attached to '{item_name}'",
            bg="#dcfce7",
            fg="#15803d",
        )

    def detach_keychain(self):
        """Detach and hide the transparent overlay."""
        if self.overlay:
            self.overlay.stop()

        self.active_attachment = None

        # Reset UI states
        self.action_btn.config(
            text="Attach Keychain",
            bg="#0284c7",
            activebackground="#0369a1",
        )
        self.status_badge.config(
            text="⚪ Detached",
            bg="#f1f5f9",
            fg="#64748b",
        )
        self.pos_label.config(text="Coordinates: ( - , - )")

    def _on_overlay_pos_update(self, pos: Optional[tuple[int, int]]):
        """Callback from overlay when icon moves across desktop."""
        if pos:
            self.pos_label.config(text=f"Coordinates: (X: {pos[0]}, Y: {pos[1]})")
        else:
            self.pos_label.config(text="Coordinates: (Searching...)")

    def on_close(self):
        """Clean shutdown hook."""
        self.detach_keychain()
        if self.overlay:
            self.overlay.close()
        self.root.destroy()


def run_app():
    """Launch the Digital Accessories desktop application."""
    root = tk.Tk()
    app = DigitalAccessoriesApp(root)
    root.mainloop()


if __name__ == "__main__":
    run_app()
