"""Small Windows-only helpers for converting desktop ListView coordinates."""

import ctypes
from ctypes import wintypes


def find_desktop_listview() -> int | None:
    """Return the desktop ListView handle, or None when Explorer is unavailable."""
    user32 = ctypes.windll.user32
    progman = user32.FindWindowW("Progman", None)
    worker = user32.FindWindowExW(progman, 0, "SHELLDLL_DefView", None)
    if not worker:
        worker = user32.FindWindowExW(0, 0, "SHELLDLL_DefView", None)
    if not worker:
        return None
    return user32.FindWindowExW(worker, 0, "SysListView32", None) or None


def listview_to_screen(hwnd: int, x: int, y: int) -> tuple[int, int]:
    point = wintypes.POINT(x, y)
    if not ctypes.windll.user32.ClientToScreen(hwnd, ctypes.byref(point)):
        return x, y
    return point.x, point.y