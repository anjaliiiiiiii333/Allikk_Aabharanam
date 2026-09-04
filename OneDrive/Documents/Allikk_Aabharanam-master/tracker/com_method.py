"""Phase 1 IFolderView coordinate lookup used by the Phase 2 overlay."""

import pythoncom
import win32com.client as wcomcli
from win32com.shell import shell, shellcon

from .desktop_finder import find_desktop_listview, listview_to_screen

SWC_DESKTOP = 0x08
SWFO_NEEDDISPATCH = 0x01
IID_IFolderView = "{CDE725B0-CCC9-4519-917E-325D72FAB4CE}"


def _get_desktop_folder_view():
    shell_windows = wcomcli.Dispatch("{9BA05972-F6A8-11CF-A442-00A0C90A8F39}")
    dispatch = shell_windows.FindWindowSW(
        wcomcli.VARIANT(pythoncom.VT_I4, shellcon.CSIDL_DESKTOP),
        wcomcli.VARIANT(pythoncom.VT_EMPTY, None),
        SWC_DESKTOP,
        0,
        SWFO_NEEDDISPATCH,
    )
    service_provider = dispatch._oleobj_.QueryInterface(pythoncom.IID_IServiceProvider)
    browser = service_provider.QueryService(shell.SID_STopLevelBrowser, shell.IID_IShellBrowser)
    return browser.QueryActiveShellView().QueryInterface(IID_IFolderView)


def get_all_icon_positions() -> dict[str, tuple[int, int]]:
    """Return desktop display names mapped to absolute screen coordinates."""
    results: dict[str, tuple[int, int]] = {}
    folder_view = _get_desktop_folder_view()
    desktop_folder = shell.SHGetDesktopFolder()
    listview_hwnd = find_desktop_listview()

    for index in range(folder_view.ItemCount(shellcon.SVGIO_ALLVIEW)):
        try:
            pidl = folder_view.Item(index)
            name = desktop_folder.GetDisplayNameOf(pidl, shellcon.SHGDN_NORMAL)
            x, y = folder_view.GetItemPosition(pidl)
            if listview_hwnd:
                x, y = listview_to_screen(listview_hwnd, x, y)
            results[name] = (x, y)
            results[name.lower()] = (x, y)
        except Exception:
            continue
    return results


def get_icon_position(display_name: str) -> tuple[int, int] | None:
    positions = get_all_icon_positions()
    return positions.get(display_name) or positions.get(display_name.lower())


def get_icon_position_by_index(index: int) -> tuple[int, int] | None:
    """Get a desktop icon position without shell name conversion."""
    folder_view = _get_desktop_folder_view()
    if index < 0 or index >= folder_view.ItemCount(shellcon.SVGIO_ALLVIEW):
        return None
    pidl = folder_view.Item(index)
    x, y = folder_view.GetItemPosition(pidl)
    listview_hwnd = find_desktop_listview()
    return listview_to_screen(listview_hwnd, x, y) if listview_hwnd else (x, y)