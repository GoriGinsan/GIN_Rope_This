# Button source assets

- `RopeThis.png`: the 32 × 32 button embedded in the plugin.
- `RopeThis-48.png`: an alternate 48 × 48 render.
- `RopeThis-preview.png`: a larger preview.
- `generate_button.py`: the original procedural image source. It draws the background and the two text lines, then downsamples for the small button.

Regeneration is optional. Building the plugin uses the supplied `RopeThis.png` and requires no Python image libraries.

To regenerate the images on Windows, install [Pillow](https://pillow.readthedocs.io/en/stable/installation.html), then run:

```powershell
python -m pip install Pillow
python assets/generate_button.py
```

The generator uses the system's Arial Bold font. Font files are not distributed with this repository.
