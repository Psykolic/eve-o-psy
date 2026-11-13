# Spotlight mode (OnTopReplica-style enlarged previews)

Spotlight mode lets you pick one or more EVE clients from the **Spotlight** tab and show each of them in its own large, independent, always-on-top preview window. This is useful when you want to keep one main alt (or a small set of alts) visible at a readable size while the remaining clients stay as small thumbnails.

## Using it

1. Open the **Spotlight** tab.
2. In the **Spotlight clients** list, check one or more clients.
3. Each checked client gets its own enlarged preview window.
4. Select a client in the list to see its current crop region. The **Clear crop** button removes the crop and restores the full-window aspect ratio.
5. Drag with the right mouse button to move a spotlight window. Drag any edge or corner to resize it; the cursor changes when you are over a resize zone. The bottom-right grip is a visual hint for the same resize area. By default the resize preserves the aspect ratio of the current crop; hold **Shift** while dragging to resize freely.
6. Press the spotlight toggle hotkey (`Control+F12` by default) to show/hide all spotlight windows at once.
7. Hold **Shift** and drag with the **right mouse button** over a spotlight to crop it to a region. Shift+right-click without dragging resets to the full window.

The client's normal thumbnail and its spotlight are independent: hiding the small thumbnail, hiding the active client's thumbnail, or checking a thumbnail in the "force hide" list does not hide the spotlight.

Spotlight windows do not relay input to the EVE client. As with normal thumbnails, click a spotlight to bring the corresponding EVE client to the foreground.

## Smart region cropping

Region cropping keeps only the part of the client that matters (minimap, chat panel, timer, cargo, etc.). It applies to spotlight windows.

- **Shift + right-drag** on a spotlight selects the region.
- **Shift + right-click** (without dragging) clears the crop and returns to the full client.
- The **Spotlight** tab shows the current crop region for the selected client and provides a **Clear crop** button.
- The spotlight window adjusts its aspect ratio to the selected region, so a square minimap gets a square preview instead of a stretched one.
- Each client remembers its own crop region.
- The crop is stored in `PerClientSpotlightRegion` and survives restarts.

A selection made on an already cropped spotlight is relative to the currently visible crop, so you can zoom in progressively. Clear the crop first to select from the full window again.

## Options

| Option | Where | Default | Description |
| --- | --- | --- | --- |
| Spotlight clients | Spotlight tab | none | Clients shown in spotlight windows. Multiple selection is supported. |
| Spotlight opacity | Spotlight tab | `1.0` | Independent opacity for spotlight windows (20% to 100%). |
| Clear crop | Spotlight tab | — | Removes the crop of the selected client and restores the full-window aspect ratio. |
| Toggle hotkey | `EVE-O-Preview.json` → `ToggleSpotlightHotkeys` | `["Control+F12"]` | Hotkeys that show/hide all spotlight windows. |

## Per-client size and position

Each spotlight remembers its own size and position. They are persisted under `PerClientSpotlightSize` and `SpotlightLayout` in `EVE-O-Preview.json`. If a client has no saved position yet, spotlight windows are tiled using the primary screen's working area.

The default size for a new spotlight is `SpotlightSize` (`640x360` by default). The minimum spotlight size is `160x90`.

## Configuration example

```json
"SpotlightClients": [
  "EVE - Main Pilot",
  "EVE - Scout"
],
"ToggleSpotlightHotkeys": [
  "Control+F12"
],
"SpotlightSize": "640, 360",
"SpotlightOpacity": 1.0,
"SpotlightAlwaysOnTop": true,
"PerClientSpotlightSize": {
  "EVE - Main Pilot": "960, 540"
},
"PerClientSpotlightRegion": {
  "EVE - Main Pilot": "120, 40, 360, 360"
},
"SpotlightLayout": {
  "EVE - Main Pilot": "20, 20",
  "EVE - Scout": "990, 20"
}
```

## Minimum thumbnail size

Starting with configuration version 2, the minimum thumbnail size is `32x18` instead of `192x108`. Existing configurations are migrated automatically; a custom minimum size set by the user is preserved.
