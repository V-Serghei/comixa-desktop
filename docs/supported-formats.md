# Supported Local Formats

Comixa Desktop is local-first. Format support should be explicit and honest.

## Current Desktop Behavior

| Format | Library scan | Reader preview | Notes |
| --- | --- | --- | --- |
| CBZ | Yes | First-class preview | ZIP archive with image pages. Ignored if no image pages are found. |
| ZIP | Yes | First-class preview | Treated as a comic only when image pages are found. Generic ZIP backups are ignored. |
| PDF | Yes | Not yet | Detected and listed. Rendering needs a dedicated PDF pipeline. |
| Image folder | Yes | First-class preview | A folder with direct image files is treated as a loose comic chapter. |
| CBR/RAR | Yes | Not yet | Detected and listed. Needs a deliberate RAR dependency decision. |
| CB7/7z | Yes | Not yet | Detected and listed. Needs a 7z reader pipeline. |
| EPUB | Yes | Not yet | Detected and listed. Needs a separate book/comic pipeline decision. |

## Image Extensions

- `.jpg`
- `.jpeg`
- `.png`
- `.webp`
- `.gif`
- `.bmp`
- `.tif`
- `.tiff`

## Not Implemented Yet

- nested archives
- encrypted archives
- network sources

CBR/RAR needs a deliberate dependency and licensing decision before implementation.
