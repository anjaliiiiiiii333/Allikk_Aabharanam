"""Run the Digital Accessories desktop prototype (GUI or CLI mode)."""

import argparse
import sys

from .accessory import FISH_KEYCHAIN
from .attachment import Attachment
from .overlay import TransparentOverlay
from .tracker_bridge import get_item_position


def main() -> None:
    parser = argparse.ArgumentParser(description="Digital Accessories: Attach decorative keychains to desktop icons")
    parser.add_argument(
        "display_name",
        nargs="?",
        help="The exact desktop file/folder name to follow. If omitted, launches the GUI application.",
    )
    parser.add_argument(
        "--index",
        type=int,
        help="COM desktop icon index (0-based) to track.",
    )
    parser.add_argument(
        "--gui",
        action="store_true",
        help="Explicitly launch the graphical desktop application.",
    )
    args = parser.parse_args()

    # Default to GUI application if no target specified or --gui passed
    if args.gui or (args.display_name is None and args.index is None):
        from .app import run_app
        run_app()
        return

    # CLI direct overlay mode
    target = args.index if args.index is not None else args.display_name
    print(f"Starting Digital Accessories overlay for: '{target}'")
    print("Press Ctrl+C to detach and exit.")

    position_provider = lambda: get_item_position(target)
    overlay = TransparentOverlay(FISH_KEYCHAIN, position_provider)
    try:
        overlay.run()
    except KeyboardInterrupt:
        print("\nDetaching accessory and closing cleanly...")
        overlay.close()
        sys.exit(0)


if __name__ == "__main__":
    main()