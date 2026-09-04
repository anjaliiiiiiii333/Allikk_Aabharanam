# Digital Accessories Datasets 📦

This directory houses the twin standalone datasets created for the **Allikk Aabharanam ("Digital Accessories")** project:

1. **[`accessories/`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/accessories)**: The complete catalog of visual accessories (keychains, charms, hats, bows, pins, stickers, straps, bells) with physical dimensions, anchor coordinates, physics models, and sound trigger mappings.
2. **[`audios/`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/audios)**: The complete sound effects and meme audio library cataloging all audio files in `Audios/` alongside tactile interaction SFX (clasps, bead rattles, jingle bells, bubble pops).

---

## 📁 Directory Structure

```
datasets/
├── README.md                      # This directory overview
├── __init__.py                    # Programmatic Python loader & query utilities
├── accessories/
│   ├── README.md                  # Detailed field dictionary & category documentation
│   ├── accessories.json           # Nested JSON catalog with full specs (30 items)
│   ├── accessories.csv            # Flat CSV for tabular analysis & data science
│   └── schema.json                # JSON Schema (Draft 2020-12) validation
└── audios/
    ├── README.md                  # Audio events & trigger documentation
    ├── audios.json                # Audio catalog with volume, cooldown & pitch rules (24 items)
    ├── audios.csv                 # Flat CSV for tabular processing
    └── schema.json                # JSON Schema (Draft 2020-12) validation
```

---

## ⚡ Quick Start in Python

```python
from datasets import (
    load_accessories,
    load_audios,
    get_accessory_by_id,
    get_sounds_for_accessory,
)

# 1. Load full datasets
accessories_data = load_accessories()
audios_data = load_audios()

print(f"Total Accessories: {accessories_data['total_items']}")
print(f"Total Audio Tracks: {audios_data['total_items']}")

# 2. Inspect an accessory and its mapped sounds
item = get_accessory_by_id("pastel_pufferfish_keychain")
sounds = get_sounds_for_accessory("pastel_pufferfish_keychain")

print(f"\nAccessory: {item['name']} ({item['category']})")
print(f"Palette: {item['palette']['primary']} / {item['palette']['secondary']}")
print(f"Attach Sound: {sounds['on_attach']['name']} -> {sounds['on_attach']['file_path']}")
print(f"Drag Sound:   {sounds['on_drag_move']['name']} -> {sounds['on_drag_move']['file_path']}")
```
