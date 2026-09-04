# Digital Accessories Dataset 🎀

A structured catalog of visual digital accessories for the **Allikk Aabharanam ("Digital Accessories")** desktop project.

---

## 📊 Dataset Summary

- **Total Items**: 30 accessories
- **Primary Categories**:
  - `keychain` (7 items): Draped and beaded charms with clasps and loops
  - `charm` (5 items): Individual pendants, amulets, and crystal icons
  - `hat` (8 items): Headwear that perches on the upper edge of desktop icons
  - `bow` (3 items): Delicate satin, velvet, and organza ribbons
  - `sticker` (4 items): Adhesive stamps, warning tape, and meme decals
  - `strap` (2 items): Beaded cords and cyberpunk wristlets
  - `bell` (1 item): Vintage acoustic jingling brass bell
- **Aesthetic Themes**: `pastel_kawaii`, `classic_gold`, `aquatic`, `retro_pixel`, `vintage_victorian`, `cyberpunk`, `gothic`, `meme`, `festive`, `cozy`
- **Formats Available**:
  - [`accessories.json`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/accessories/accessories.json): Complete nested object specification with physics & color palettes
  - [`accessories.csv`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/accessories/accessories.csv): Tabular flat dataset for data science, spreadsheets, and pandas
  - [`schema.json`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/accessories/schema.json): JSON Schema (Draft 2020-12) validation definition

---

## 📐 Field Dictionary

| Field | Type | Description | Example |
| :--- | :--- | :--- | :--- |
| `id` | string | Unique snake_case identifier | `pastel_pufferfish_keychain` |
| `name` | string | User-facing display title | `Pastel Pufferfish Keychain` |
| `category` | enum | Broad classification (`keychain`, `hat`, etc.) | `keychain` |
| `subcategory` | string | Specific mounting/form factor | `beaded_strand` |
| `theme` | string | Visual/aesthetic style family | `pastel_kawaii` |
| `description` | string | Creative and lore description | `Signature accessory with 3D pufferfish...` |
| `materials` | array[str] | Real-world material analogs | `["acrylic_resin", "glass_pearls"]` |
| `palette` | object | Primary, secondary, accent hex colors and finish | `{"primary": "#E0C3FC", ...}` |
| `dimensions` | object | Target pixel width, height, and ratio at 1.0x scale | `{"width_px": 54, "height_px": 136}` |
| `anchor` | object | Attachment type, coordinate offset, eyelet requirement | `{"type": "top_right_hook", "hole_required": true}` |
| `physics` | object | Weight (g), damping, stiffness, max swing angle (deg) | `{"weight_g": 18.5, "max_swing_deg": 42.0}` |
| `compatible_targets` | array[str] | Supported desktop objects (`file`, `folder`, etc.) | `["file", "pdf", "folder"]` |
| `rarity` | enum | Cosmetic tier (`common`, `uncommon`, `rare`, `epic`, `legendary`) | `rare` |
| `sound_mappings` | object | Audio IDs triggered on attach, move, interact, etc. | `{"on_attach": "sfx_clasp_lock", ...}` |
| `asset_status` | enum | Visual production state (`ready`, `in_design`, `planned`) | `ready` |
| `asset_path` | string | Relative path to transparent graphic asset | `assets/desktop_accessory_keychain.jpg` |

---

## 💻 Quick Python Usage

```python
import json
from pathlib import Path

DATASET_PATH = Path("datasets/accessories/accessories.json")
with open(DATASET_PATH, "r", encoding="utf-8") as f:
    catalog = json.load(f)

# List all keychains
keychains = [item for item in catalog["accessories"] if item["category"] == "keychain"]
print(f"Found {len(keychains)} keychains.")
```
