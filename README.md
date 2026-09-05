# DIKERAPPWINDOWS

Windows desktop port of the Barangay Sibulan **DIKERMA / Barangay ID Maker**.

Android reference baseline: `Brgysibulan/dikerma` **v0.7.1** at commit `889ca191dadb131a56c10deadd3e6d5d65c2b7c7`.

## Windows v0.1.0 direction

- .NET 8 WPF desktop application
- Fully offline at runtime
- Fixed physical ID size: **85 × 115 mm**
- Uploaded Front/Back images are the actual ID design
- Desktop Layout Studio controls dynamic overlays only
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
