"""Datasets module for Digital Accessories (Allikk Aabharanam).

Provides high-level programmatic loaders and query utilities for both
the Accessories dataset and the Audios dataset.
"""

import json
from pathlib import Path
from typing import Any, Optional, Union

DATASETS_DIR = Path(__file__).parent
ACCESSORIES_JSON = DATASETS_DIR / "accessories" / "accessories.json"
ACCESSORIES_CSV = DATASETS_DIR / "accessories" / "accessories.csv"
AUDIOS_JSON = DATASETS_DIR / "audios" / "audios.json"
AUDIOS_CSV = DATASETS_DIR / "audios" / "audios.csv"


def load_accessories(as_dataframe: bool = False) -> Union[dict[str, Any], Any]:
    """Load the full accessories dataset.
    
    Args:
        as_dataframe: If True, returns a pandas DataFrame (requires pandas).
                      If False (default), returns the raw parsed JSON dict.
    """
    if as_dataframe:
        import pandas as pd
        return pd.read_csv(ACCESSORIES_CSV)

    with open(ACCESSORIES_JSON, "r", encoding="utf-8") as f:
        return json.load(f)


def load_audios(as_dataframe: bool = False) -> Union[dict[str, Any], Any]:
    """Load the full audios dataset.
    
    Args:
        as_dataframe: If True, returns a pandas DataFrame (requires pandas).
                      If False (default), returns the raw parsed JSON dict.
    """
    if as_dataframe:
        import pandas as pd
        return pd.read_csv(AUDIOS_CSV)

    with open(AUDIOS_JSON, "r", encoding="utf-8") as f:
        return json.load(f)


def get_accessory_by_id(accessory_id: str) -> Optional[dict[str, Any]]:
    """Retrieve an accessory record by its unique ID."""
    data = load_accessories(as_dataframe=False)
    for acc in data.get("accessories", []):
        if acc["id"] == accessory_id:
            return acc
    return None


def get_audio_by_id(audio_id: str) -> Optional[dict[str, Any]]:
    """Retrieve an audio record by its unique ID."""
    data = load_audios(as_dataframe=False)
    for aud in data.get("audios", []):
        if aud["id"] == audio_id:
            return aud
    return None


def get_audios_by_trigger(trigger_event: str) -> list[dict[str, Any]]:
    """Find all audio items associated with a given trigger event (e.g. 'on_attach', 'on_drag_move')."""
    data = load_audios(as_dataframe=False)
    return [aud for aud in data.get("audios", []) if aud["trigger_event"] == trigger_event]


def get_accessories_by_category(category: str) -> list[dict[str, Any]]:
    """Filter accessories by category (e.g. 'keychain', 'hat', 'charm', 'bow')."""
    data = load_accessories(as_dataframe=False)
    return [acc for acc in data.get("accessories", []) if acc["category"] == category]


def get_sounds_for_accessory(accessory_id: str) -> dict[str, Optional[dict[str, Any]]]:
    """Resolve all associated audio records for a given accessory across all event triggers."""
    acc = get_accessory_by_id(accessory_id)
    if not acc or "sound_mappings" not in acc:
        return {}
    
    result = {}
    for event, audio_id in acc["sound_mappings"].items():
        result[event] = get_audio_by_id(audio_id)
    return result


__all__ = [
    "load_accessories",
    "load_audios",
    "get_accessory_by_id",
    "get_audio_by_id",
    "get_audios_by_trigger",
    "get_accessories_by_category",
    "get_sounds_for_accessory",
    "ACCESSORIES_JSON",
    "ACCESSORIES_CSV",
    "AUDIOS_JSON",
    "AUDIOS_CSV",
]
