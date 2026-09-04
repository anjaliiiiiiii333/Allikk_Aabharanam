"""Bridge to the existing Phase 1 Windows desktop tracker.

This module reuses Phase 1's IFolderView COM coordinate tracking mechanism
without modifying Phase 1 files, adding robust item enumeration and name resolution
compatible with all Python versions (including Python 3.14).
"""

from dataclasses import dataclass
from typing import Optional
import logging

try:
    from tracker.com_method import (
        _get_desktop_folder_view,
        find_desktop_listview,
        listview_to_screen,
        get_icon_position_by_index,
        shellcon,
        shell,
    )
except ImportError as err:
    logging.warning("Could not import tracker modules: %s", err)


@dataclass
class DesktopItem:
    """Represents a real desktop item (file, folder, or application shortcut)."""
    index: int
    name: str
    position: tuple[int, int]


def get_desktop_items() -> list[DesktopItem]:
    """Enumerate all real files, folders, and shortcuts currently on the Windows desktop.
    
    Returns:
        List of DesktopItem objects with index, display name, and screen coordinates.
    """
    items: list[DesktopItem] = []
    try:
        folder_view = _get_desktop_folder_view()
        desktop_folder = shell.SHGetDesktopFolder()
        listview_hwnd = find_desktop_listview()
        count = folder_view.ItemCount(shellcon.SVGIO_ALLVIEW)

        for i in range(count):
            try:
                pidl = folder_view.Item(i)
                # Pass PIDL in a list to support pywin32 on Python 3.12, 3.13, and 3.14
                try:
                    name = desktop_folder.GetDisplayNameOf([pidl], shellcon.SHGDN_NORMAL)
                except Exception:
                    name = desktop_folder.GetDisplayNameOf(pidl, shellcon.SHGDN_NORMAL)
                
                x, y = folder_view.GetItemPosition(pidl)
                if listview_hwnd:
                    x, y = listview_to_screen(listview_hwnd, x, y)
                items.append(DesktopItem(index=i, name=name, position=(x, y)))
            except Exception:
                continue
    except Exception as e:
        logging.error("Failed to enumerate desktop items: %s", e)

    return items


def get_item_position(target: str | int) -> Optional[tuple[int, int]]:
    """Retrieve screen coordinates for a desktop item by its name or index.
    
    Args:
        target: Exact display name (case-insensitive) or COM item index.
        
    Returns:
        (x, y) screen coordinates or None if not found / off-screen.
    """
    if isinstance(target, int):
        try:
            return get_icon_position_by_index(target)
        except Exception:
            return None

    # Target is display name
    target_str = str(target).strip().lower()
    try:
        folder_view = _get_desktop_folder_view()
        desktop_folder = shell.SHGetDesktopFolder()
        listview_hwnd = find_desktop_listview()
        count = folder_view.ItemCount(shellcon.SVGIO_ALLVIEW)

        for i in range(count):
            try:
                pidl = folder_view.Item(i)
                try:
                    name = desktop_folder.GetDisplayNameOf([pidl], shellcon.SHGDN_NORMAL)
                except Exception:
                    name = desktop_folder.GetDisplayNameOf(pidl, shellcon.SHGDN_NORMAL)

                if name.strip().lower() == target_str:
                    x, y = folder_view.GetItemPosition(pidl)
                    if listview_hwnd:
                        x, y = listview_to_screen(listview_hwnd, x, y)
                    return (x, y)
            except Exception:
                continue
    except Exception as e:
        logging.debug("Error querying item position: %s", e)

    return None


def get_active_selection_item() -> Optional[DesktopItem]:
    """Retrieve the currently focused or selected item on the desktop, if any."""
    try:
        folder_view = _get_desktop_folder_view()
        focused_idx = folder_view.GetFocusedItem()
        if focused_idx >= 0 and focused_idx < folder_view.ItemCount(shellcon.SVGIO_ALLVIEW):
            desktop_folder = shell.SHGetDesktopFolder()
            listview_hwnd = find_desktop_listview()
            pidl = folder_view.Item(focused_idx)
            try:
                name = desktop_folder.GetDisplayNameOf([pidl], shellcon.SHGDN_NORMAL)
            except Exception:
                name = desktop_folder.GetDisplayNameOf(pidl, shellcon.SHGDN_NORMAL)
            x, y = folder_view.GetItemPosition(pidl)
            if listview_hwnd:
                x, y = listview_to_screen(listview_hwnd, x, y)
            return DesktopItem(index=focused_idx, name=name, position=(x, y))
    except Exception:
        pass
    return None
