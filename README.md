# DIKERAPPWINDOWS

Windows desktop port of the Barangay Sibulan **DIKERMA / Barangay ID Maker**.

Android reference baseline: `Brgysibulan/dikerma` **v0.7.1** at commit `889ca191dadb131a56c10deadd3e6d5d65c2b7c7`.

## Windows v0.2.0 Layout Studio

- .NET 8 WPF desktop application
- Fully offline at runtime
- Fixed physical ID size: **85 × 115 mm**
- Uploaded Front/Back images are the actual ID design
- Desktop Layout Studio edits dynamic overlays and custom text, images, shapes and straight lines
- Separate `STA. CRUZ` and `DAVAO DEL SUR` elements
- Per-element position and size stored in millimetres
- Per-text font family, font size, color, bold, alignment, underline, outline, shadow, and visibility
- Safe-margin, center, and 5 mm preview guides
- Snap-to-grid, precision nudge, Center X/Y, reset selected, reset side
- One saved layout applies to all current and future employee IDs
- Layout lock prevents accidental edits
- Employee records stored locally on the PC
- ID photo cleanup defaults to white background
- Signature cleanup defaults to transparent background
- Manual QR image upload only
- A4 PDF output supports up to **2 people**, with Front/Back pairing
- DOB output uses full English month format such as **January 12, 1987**
- Optional cut/photo/QR/signature/back divider outlines

## Local data

Runtime data is stored under the current Windows user's local application-data folder in `DIKERMA`:

- `data/employees.json`
- `data/settings.json`
- `data/layout.json`
- `assets/`
- `exports/`

No online database, API, authentication server, or networking code is required.

## Build

GitHub Actions builds on `windows-latest`, verifies the runtime source contains no common networking clients, restores dependencies, compiles Release, and publishes a self-contained `win-x64` artifact named:

`DIKERMA-Windows-x64`

The PDF engine uses **PDFsharp-WPF 6.2.4**.

## Printing rule

For physical-size validation, print PDFs at **Actual Size / 100%**. Do not use **Fit to Page** when measuring the 85 × 115 mm card.

## Repository separation

This Windows repository is independent from `Brgysibulan/dikerma`. Windows development here does not modify the Android stable baseline.

## New Studio controls

- Zoom from 25% to 800%, Fit, and Ctrl+mouse-wheel zoom with scrollbars.
- Add text, PNG/images, rectangles, ellipses, horizontal and vertical lines. Lines always remain axis-aligned.
- Shift-click multiple elements; Group/Ungroup; drag to move and drag the gold bottom-right handle to resize. Group movement respects card boundaries.
- Duplicate, delete custom elements, hide/restore standard fields, bring to front/send to back. Duplicated employee fields retain their data binding.
- Undo/redo (80 recent edits), save placement, and unsaved-layout prompt on close.
- Editable text overrides: leave blank to preserve employee/default data. An override is shared by all IDs.
- Crop image margins non-destructively with a preview and reset.
- Installed Windows fonts plus local TTF/OTF upload; no system-wide font installation needed.
- Italic, underline, outline, shadow color/offset/opacity/blur. Shape and line color use the color picker.
- White D on green application/window icon.

### Shortcuts

| Action | Shortcut |
| --- | --- |
| Save placement | Ctrl+S |
| Undo / redo | Ctrl+Z / Ctrl+Y |
| Duplicate / delete | Ctrl+D / Delete |
| Group / ungroup | Ctrl+G / Ctrl+Shift+G |
| Select all visible layers | Ctrl+A |
| Multi-select | Shift+click |
| Move / larger step | Arrow / Shift+arrow |
| Zoom | Ctrl+wheel or Ctrl+plus/minus |
| Fit preview | Ctrl+0 |

Text-entry controls retain their normal editing shortcuts. Apply selected settings before saving property edits.

### Image cleanup and printing limits

Background cleanup is fully offline and uses border-connected color removal with adjustable tolerance and edge softness. It preserves enclosed similar-colored regions better than the old whole-image threshold. It is **not AI subject segmentation**: use a plain background and inspect hair/clothing in the preview. White and transparent output are available; originals are not overwritten. Signature import retains its dedicated cleanup.

The crop/image dialog operates on the selected layout image. Applying cleanup there creates a static image shared across IDs; import individual employee portraits through Records instead. A crop alone keeps the dynamic image binding.

Studio elements and PDF export share a WPF renderer at **300 dpi**, including imported fonts, crops, shapes, and effects. Text in exported IDs is rasterized, not selectable PDF text. Effects are clipped to the element bounds; leave space inside text boxes for shadows/outlines. The 85 × 115 mm size and two-person A4 front/back arrangement are retained. Print at Actual Size / 100%.

Existing version-1 layout files remain supported. Custom elements, groups, crop settings and font references are saved in layout.json. Imported assets/fonts remain in the local assets folder. Copying only layout.json to another PC does not copy those assets.

### Validation

The Windows workflow is configured to compile the application, run smoke tests for window initialization, legacy/custom layout persistence, edge-connected cleanup, cropped-image pixels, all element render types and a two-person PDF export, then publish the self-contained app. Interactive drag/resize, font selection and physical print alignment still need a Windows user check.

The implementation workspace has no .NET SDK. The new Windows build and smoke tests have not yet run; pushing the branch requires authorization. Local XML/event-handler and whitespace checks are used as preliminary checks only.

### How to group elements

Use a group when text, lines, shapes, or logos must stay together while you arrange the ID.

1. In **Layout Studio**, click the first element.
2. Hold **Shift**, then click each additional element. Selected elements show a gold border.
3. Click **Group**, or press **Ctrl+G**.
4. Drag any element in the group to move all of them together.
5. Drag the small gold handle at the bottom-right of a selected group to resize all selected elements together.
6. Click **Ungroup**, or press **Ctrl+Shift+G**, when you need to edit each element separately.

Example: select the name, designation, underline, and logo, then group them so the complete name area can be moved as one unit. Save placement with **Ctrl+S** after arranging it. You can use **Ctrl+Z** to undo an unwanted group movement.
