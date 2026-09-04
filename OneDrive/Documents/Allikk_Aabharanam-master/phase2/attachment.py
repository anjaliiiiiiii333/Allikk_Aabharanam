"""Attachment model representing an association between a desktop item and an accessory."""

from dataclasses import dataclass, field
from typing import Optional


@dataclass
class Attachment:
    """Maps a desktop item (by name or index) to an accessory."""

    target_name: str
    accessory_id: str
    target_index: Optional[int] = None
    offset_x: int = 0
    offset_y: int = 0
    is_active: bool = True

    @property
    def display_name(self) -> str:
        return self.target_name