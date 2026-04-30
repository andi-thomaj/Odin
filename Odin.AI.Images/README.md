# Odin AI Images & Videos — Population Asset Generation

This folder contains the Python pipeline used to generate the **ancestral population icons** and their **looping videos** displayed in the Odin app. Use this README whenever a new qpAdm population is added so the new assets stay visually consistent with the existing 30+ populations.

> ⚠️ The `FAL_KEY` is currently hardcoded at the top of every script. Rotate it after large batches and never commit a new value publicly.

---

## 1. Models used

| Asset | fal.ai model | Endpoint kind |
|---|---|---|
| Icon (PNG) | `openai/gpt-image-2` | text-to-image |
| Video (MP4) | `bytedance/seedance-2.0/image-to-video` | image-to-video |

Both are submitted via `fal_client.submit(...)` (fire-and-forget). No local download — review and download manually from https://fal.ai/dashboard/requests.

---

## 2. Folder layout on disk

```
~/Pictures/Odin/AncientIcons/      <- downloaded icon PNGs (source for videos)
~/Pictures/Odin/AncientVideos/     <- manifest JSON files + downloaded MP4s
```

Filename convention for icons: **human-readable, spaces allowed**, e.g.
`Yamnaya.png`, `Caucasus Hunter Gatherer.png`, `Imperial Italy.png`, `Proto Albanian.png`.

The video script reads from `AncientIcons/` by exact filename, so always save the downloaded icon under the same name you reference in the script.

---

## 3. Concurrency policy

fal.ai will throttle/error on bursts. **Always insert a 10-second sleep between submissions** when generating multiple assets in one run. See `generate_new_icons.py` / `generate_new_videos.py` for the pattern.

---

## 4. Icon prompt template (text-to-image)

Every icon prompt has **two parts** concatenated with a blank line:

### 4.1 Scene block (population-specific) — REQUIRED structure

```
Foreground: a <CULTURE> <ROLE> with <SKIN TONE>, <BEARD/HAIR>, wearing
<CLOTHING / ARMOR / HEADGEAR>, with <CULTURAL ACCESSORY/PENDANT>.
Background: <SIGNATURE LANDSCAPE>, <ICONIC ARCHITECTURE>, <CULTURAL SYMBOLS>,
and <ENVIRONMENT DETAIL>.
```

Guidelines for the scene block:
- One sentence for **Foreground**, one sentence for **Background**.
- Foreground must describe a **single bust/portrait** (head & shoulders).
- Skin tone, beard, and hair should be **archaeologically/ethnographically grounded** for the population.
- Background should contain **2–4 distinct iconic elements** (landscape + architecture + a moving thing such as ships, soldiers, banners, animals).
- Avoid weapons aimed at the viewer, blood, modern items, or text.

### 4.2 Style block (shared, never edit)

```
Square 1:1 website icon. Retro 16-bit pixel art style, inspired by classic 90s
SNES and Amiga RPG games such as Heroes of Might and Magic II, Age of Empires,
and Final Fantasy VI. A centered bust/portrait of a single character (head and
shoulders), facing slightly forward. Behind the character, a softly blurred
scenic background showing iconic landscape, architecture, and cultural symbols.
Warm earthy limited color palette, visible dithering, crisp pixel outlines,
hand-painted shading. Subtle rounded-square decorative border framing the icon.
Same lighting direction, same framing, same pixel resolution, and same color
grading across all icons for visual consistency. No text, no logos, no
watermarks, no modern elements, no signatures.
```

### 4.3 fal.ai arguments (icon)

```python
{
    "prompt": "<scene>\n\n<style>",
    "image_size": "square_hd",   # MUST be set; default is landscape_4_3
    "quality": "high",
    "num_images": 1,
    "output_format": "png",
}
```

### 4.4 Example — Hittite/Phrygian

> Foreground: a Hittite/Phrygian noble with olive skin and a dark curled beard, wearing a tall conical Phrygian cap and an embroidered robe with geometric patterns, with a bronze pendant.
> Background: the Anatolian highlands with the lion gate of Hattusa, rock-cut reliefs, a chariot, and Mount Ida in the distance.

### 4.5 Content safety

Do **not** include hate symbols (e.g., swastika, Aryan symbols, Nazi or supremacist iconography). For ancient European cultures use neutral wording such as "geometric tribal knotwork", "Indo-European symbols", "fibula brooch", or culture-specific motifs.

---

## 5. Video prompt template (image-to-video, Seedance 2.0)

Every video prompt has **two parts** concatenated with a blank line:

### 5.1 Motion block (population-specific) — REQUIRED structure

```
The <CULTURE> <ROLE> blinks <slowly / once or twice> and <FACE/BEARD/HAIR
behavior — almost no movement>. <CLOTHING/ARMOR — minimal motion>.
Behind him, <BACKGROUND ELEMENT 1 actively moving>, <ELEMENT 2 actively
moving>, <ELEMENT 3 actively moving>.
```

Golden rules for motion:
- **Avatar must be near-static.** Allowed: blinks, very faint hair/beard wind, tiny chainmail glints. Forbidden: mouth opening, talking, head turning, leaning.
- If the population has braids or thick beards the user often dislikes movement — explicitly write *"the beard doesn't move at all"* and *"his mouth doesn't move"*.
- **Background must be alive and active.** Use 2–4 visibly moving elements: drifting clouds, marching soldiers, sailing ships, snapping banners, flowing rivers, drifting snow, swaying branches, smoke curling.
- Camera is locked. No zoom, no pan, no cuts. Loop-friendly.

### 5.2 Style block (shared, never edit)

```
Preserve the source 16-bit pixel art style, palette, dithering, crisp pixel
outlines and rounded-square decorative border. Keep the centered bust framing
static; add only subtle ambient motion. Camera is locked, no zoom, no pan,
no cuts. Loop-friendly. No text, no watermarks, no modern elements.
```

### 5.3 fal.ai arguments (video)

```python
{
    "prompt": "<motion>\n\n<style>",
    "image_url": fal_client.upload_file("<path to icon PNG>"),
    "resolution": "480p",
    "duration": "4",
    "aspect_ratio": "1:1",
    "generate_audio": False,
}
```

### 5.4 Example — Medieval Slavic

> The medieval Slavic warrior blinks and his brown braided beard doesn't move at all and also his mouth doesn't move. Chainmail rings catch tiny glints of light. The conical iron nasal helmet stays still. Behind him, light snow drifts past the wooden palisade gord, birch branches sway, smoke curls from the wooden onion-domed church, and the snowy river flows slowly.

---

## 6. End-to-end workflow for a NEW population

1. **Decide the scene block** (foreground + background) following §4.1, grounded in the population's archaeology.
2. **Add an entry** to `generate_new_icons.py` (`ICON_JOBS` list) and run:
   ```powershell
   .\.venv\Scripts\python.exe .\generate_new_icons.py
   ```
   The script submits with a 10s delay between jobs and writes a manifest to `~/Pictures/Odin/AncientVideos/new_icons_<timestamp>.json`.
3. **Review** at https://fal.ai/dashboard/requests. If a population looks wrong, tweak its scene block and re-submit just that one (you can copy `generate_single_video.py` as a one-icon template, or call the model directly).
4. **Download** the approved icon and save it as `~/Pictures/Odin/AncientIcons/<Population Name>.png` (use the exact filename you'll reference in the video script).
5. **Decide the motion block** following §5.1 — emphasise active background, near-static avatar.
6. **Add an entry** to `generate_new_videos.py` (`VIDEO_JOBS` list, 3-tuple `(name, motion_prompt, source_filename)`) and run:
   ```powershell
   .\.venv\Scripts\python.exe .\generate_new_videos.py
   ```
7. **Review** the MP4 on the dashboard. If the avatar moves too much, rewrite the motion block to forbid the specific movement (e.g. "the beard doesn't move at all", "his mouth doesn't move") and re-submit using `generate_single_video.py`.
8. **Download** the MP4 to `~/Pictures/Odin/AncientVideos/<Population Name>.mp4`.

### Optional: convert MP4 → GIF

```powershell
ffmpeg -i "<Population>.mp4" -vf "fps=12,scale=512:-1:flags=lanczos" -loop 0 "<Population>.gif"
```

---

## 7. Scripts in this folder

| Script | Purpose |
|---|---|
| `generate_icons.py` | Original full batch (all 30 founding populations) — text-to-image |
| `generate_videos.py` | Original full batch — image-to-video |
| `generate_new_icons.py` | Template for a small batch of NEW icons (10s delays) |
| `generate_new_videos.py` | Template for a small batch of NEW videos (10s delays) |
| `generate_single_video.py` | One-off re-render of a single video |
| `regenerate_selected.py` | Selectively re-submit a single icon + a few videos |
| `check_status.py` | Polls the fal.ai queue API for in-flight request IDs |

When adding a new population, the **easiest workflow** is to edit the `ICON_JOBS` and `VIDEO_JOBS` lists at the top of `generate_new_icons.py` and `generate_new_videos.py` (clear them out, drop in just your new entries), then run them.

---

## 8. Troubleshooting

- **"image not found"** — the filename in `VIDEO_JOBS` doesn't match the file on disk under `~/Pictures/Odin/AncientIcons/`. Spaces matter.
- **Concurrency / rate-limit errors** — keep the 10-second delay; reduce batch size.
- **Avatar talks or moves head in video** — strengthen the negative wording in the motion block (`"his mouth doesn't move"`, `"his head stays still"`, `"the beard doesn't move at all"`).
- **Icon background looks empty** — add 2–4 explicit iconic background elements with concrete nouns (ships, banners, mountains, soldiers, animals) rather than abstract adjectives.
- **Wrong skin tone** — explicitly specify it in the foreground sentence (e.g. `"fair pale skin, light flushed cheeks"`, `"sun-weathered olive skin"`, `"fair European skin (not tanned)"`).
