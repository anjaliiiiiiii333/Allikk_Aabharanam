"""Accessory definitions and data models for Digital Accessories."""

from dataclasses import dataclass
from pathlib import Path
from typing import Optional


@dataclass(frozen=True)
class Accessory:
    """A visual accessory definition, independent of desktop tracking.
    
    Attributes:
        id: Unique identifier for the accessory.
        name: Human-readable display name.
        category: Category type (e.g. 'keychain', 'bow', 'hat', 'moustache').
        image_path: Path to the transparent PNG asset.
        anchor: (x, y) coordinate within the accessory image where the hook connects.
        description: Brief description of the accessory.
        default_scale: float = 1.0
    """

    id: str
    name: str
    category: str
    image_path: Path
    anchor: tuple[int, int] = (23, 0)
    description: str = ""
    default_scale: float = 1.0


# Base directory for Phase 2 assets
ASSETS_DIR = Path(__file__).parent / "assets"

# Pre-defined single prototype accessory: Pastel Fish Keychain
FISH_KEYCHAIN = Accessory(
    id="pastel_fish_keychain",
    name="Pastel Fish Keychain",
    category="keychain",
    image_path=ASSETS_DIR / "keychain.png",
    anchor=(23, 0),
    description="A delicate pastel fish charm hanging from pastel beads and a metallic ring.",
    default_scale=1.0,
)

# Registry of available accessories - structured for easy future extension
# (e.g. bows, hats, moustaches can be added here without modifying tracking logic)
AVAILABLE_ACCESSORIES: dict[str, Accessory] = {
    FISH_KEYCHAIN.id: FISH_KEYCHAIN,
}


def get_accessory(accessory_id: str) -> Optional[Accessory]:
    """Retrieve an accessory by its ID."""
    return AVAILABLE_ACCESSORIES.get(accessory_id)