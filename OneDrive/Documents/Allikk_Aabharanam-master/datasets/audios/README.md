# Digital Accessories Audios Dataset 🔊

A comprehensive sound effects and audio dataset designed for the **Allikk Aabharanam ("Digital Accessories")** desktop application.

---

## 📊 Dataset Summary

- **Total Audio Tracks**: 24 audio items
- **Categories**:
  - `meme_sfx` (3 items): Wheezing laugh, dramatic stings, comedic punches
  - `reaction_voice` (4 items): Crowd aww, OMG, Oh hell no, Spongebob narrator
  - `physical_sfx` (11 items): Lobster clasps, bead clinks, jingle bells, bubble pops, drop thuds, rubber squeaks, stamps, tape peels
  - `musical_jingle` (4 items): 8-bit power-up, magic sparkles, party horn, suspense violins
  - `ambient_motion` (2 items): Helicopter rotor loop, speed whoosh
  - `ambient_bgm` (1 item): Chiptune theme track
- **Active Files**: Includes the 9 existing audio clips in `Audios/` plus specifications for interactive tactile physical SFX.
- **Formats Available**:
  - [`audios.json`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/audios/audios.json): Complete nested specification with cooldowns and pitch jitter settings
  - [`audios.csv`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/audios/audios.csv): Flat CSV for tabular analysis and audio pipeline integration
  - [`schema.json`](file:///c:/Users/anjal/OneDrive/Documents/Allikk%20Aabharanam/datasets/audios/schema.json): Validation schema

---

## 🎧 Event Triggers & Audio Mapping

| Trigger Event | Purpose & Application Lifecycle | Example Sounds |
| :--- | :--- | :--- |
| `on_attach` | Fired when an accessory is snapped onto the icon casing | Metallic clasp snap, crowd aww, party horn, magic sparkle |
| `on_detach` | Fired when an accessory is removed | Metallic unhook, adhesive tape peel |
| `on_drag_move` | Fired continuously or stochastically while dragging | Gentle bead clinks, brass bell jingles, helicopter rotors, wind whoosh |
| `on_drag_release` | Fired when the mouse releases the icon onto the desktop | Soft surface drop thud |
| `on_interact` | Fired when the user clicks or jiggles the accessory | Wheezing cat laugh, bubble pop, rubber squeak, OMG voice |
| `on_idle` | Fired when an icon sits undisturbed for several minutes | "A Few Moments Later" French narrator |
| `on_error` | Fired when an attachment fails or is dropped out of bounds | Rejection error buzzer |

---

## 💻 Quick Python Usage

```python
import json
from pathlib import Path

AUDIOS_PATH = Path("datasets/audios/audios.json")
with open(AUDIOS_PATH, "r", encoding="utf-8") as f:
    catalog = json.load(f)

# Find all sounds triggered when dragging an icon
drag_sounds = [a for a in catalog["audios"] if a["trigger_event"] == "on_drag_move"]
for s in drag_sounds:
    print(f"- {s['name']} ({s['category']}) -> {s['file_path']}")
```
