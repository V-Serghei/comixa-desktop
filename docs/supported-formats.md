# Supported Local Formats

Comixa Desktop MVP format support is intentionally narrow and honest. The official MVP formats are `CBZ`, `ZIP`, and `PDF`.

## MVP Behavior

| Format | Library scan | Reader preview | Notes |
| --- | --- | --- | --- |
| CBZ | Yes | Yes | ZIP archive with image pages. Ignored if no image pages are found. |
| ZIP | Yes | Yes | Treated as a comic only when image pages are found. Generic ZIP backups are ignored. |
| PDF | Yes | Yes | Basic reader pipeline through PDFtoImage/PDFium. |

## Not MVP Behavior

These formats and shapes are not scanned, listed, or presented as supported MVP behavior:

- CBR/RAR
- 7z/CB7
- EPUB
- nested archives
- loose image folders
- encrypted archives
- network sources

Placeholders may exist only when useful for future design, but they must not be user-visible supported behavior until deliberately scoped.

## Archive Image Entries

For `CBZ` and comic `ZIP` archives, image entries may use common image extensions such as `.jpg`, `.jpeg`, `.png`, `.webp`, `.avif`, `.gif`, `.bmp`, `.tif`, and `.tiff`.
