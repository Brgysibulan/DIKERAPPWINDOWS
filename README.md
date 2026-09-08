# DIKERAPPWINDOWS

## Windows v0.3.0 — Publisher Studio foundation

Developed by **Joshua Apal Pudi**. DIKERMA remains a fully offline .NET 8 WPF Barangay ID Maker with a fixed **85 × 115 mm** ID canvas and two-person A4 front/back PDF export.

v0.3.0 starts the Layout Studio redesign from a form-style editor into a Publisher-like master-design editor. The goal is to make the design reusable while keeping every employee's personal data isolated from every other record.

### Critical record-data isolation fix

The following layers are now explicitly **RECORD DATA**:

- Employee photo
- Holder signature
- QR image
- Employee name
- Designation
- Employee / Control number
- Date of birth
- Sex
- Civil status
- Address

Record-bound text ignores shared `TextOverride` values. Record-bound image layers ignore any stale shared `ImagePath` during PDF export and always resolve the image from the employee currently being rendered. This prevents a photo, name, birthdate, address, signature, or QR from Person 1 from being reused accidentally on Person 2.

On first v0.3 load, old shared text/image overrides attached to record-bound fields are cleared from the saved master layout. Cropping a record-bound image changes the master crop only; it no longer replaces the employee-specific image path.

### Publisher-style Layers panel

Layout Studio now has a dedicated, always-visible **LAYERS** panel instead of relying on the small element drop-down.

- Built-in personal layers are clearly named with `RECORD DATA`.
- Static labels and decorative elements remain master-design layers.
- Custom layers use clearer names such as `Custom text 1`, `Image / PNG 1`, `Rectangle 1`, and can be renamed.
- Show/Hide selected layer.
- Lock/Unlock selected layer.
- Move one step Forward/Backward.
- Move directly To front/To back.
- Locked layers are protected from drag, group transforms, deletion, and crop replacement.

The old element ComboBox is kept internally for compatibility with the existing editor logic but is hidden from the normal Studio UI.

### Publisher appearance controls

Images and shapes now have reusable master-layer appearance settings:

- Fill color for rectangle/ellipse shapes
- Border / picture frame On/Off
- Border color picker
- Border thickness
- Rounded-corner radius
- Element opacity
- Per-layer lock

The employee-photo frame is now part of the master layer rather than being tied to one employee. Therefore the same frame style is rendered for Person 1, Person 2, and future records. Legacy Photo/QR outline settings are migrated into the new layer-frame system when possible.

Image frames and rounded corners are rendered by the same 300-dpi element renderer used by the PDF engine, so preview and export use the same styling path.

### Layout and alignment improvements

- Layout Studio now uses three work areas: **Canvas | Layers | Properties**.
- Preview employee selector is available so a real record can be used while designing without making that person's values part of the master template.
- Align Left / Center / Right and Top / Middle / Bottom controls are available for the current selection.
- Existing Group/Ungroup, Duplicate, crop, zoom 25–800%, Undo/Redo, snap-to-grid, nudge, Center X/Y, font upload, underline, text outline, shadow, and color pickers remain available.
- `SAVE MASTER DESIGN • APPLY TO ALL IDs` makes the distinction between template design and record data explicit.

### v0.3 safety model

`MASTER DESIGN`
- position and size
- font and text styling
- frames, colors, shapes, lines and PNG decorations
- crop percentages
- visibility and layer order

`EMPLOYEE RECORD`
- name
- designation
- control number
- date of birth
- sex / civil status / address
- photo / signature / QR

`PDF ENGINE`
- combines the same master design with each employee independently
- Person 1 and Person 2 are resolved separately
- record-bound fields cannot be replaced by a shared Studio override

### Still planned for later Publisher iterations

The v0.3 foundation does **not** claim full Microsoft Publisher parity yet. Planned follow-ups include draggable rulers/guides, eight resize handles, rotation, distribute-spacing commands, richer shape border styles, multi-selection directly from the Layers panel, template Save As / multiple template profiles, and more advanced text-box controls such as line spacing and letter spacing.

---

## Windows v0.2.1 — Advanced BG Eraser and developer branding

The white **D** on a refined green tile is shared by the executable, window and sidebar. v0.2.1 added the advanced fully-offline background eraser while retaining original source photos/signatures separately.

### Refine an individual photo or signature

1. Open **Records**, select a record or start a new one, then choose a photo/signature.
2. Click **Advanced BG Eraser • photo** or **Advanced BG Eraser • signature**.
3. Adjust **Removal strength** and **Edge feather**, then click **Auto remove background**.
4. Use **Erase brush** to remove leftovers or **Restore brush** to recover detail. Undo/Redo work per brush stroke.
5. Use Fit/zoom or Ctrl+mouse-wheel to inspect edges.
6. Choose white or transparent output, click Apply image, then Save Record.

The eraser is offline adaptive color removal plus manual mask refinement, not AI segmentation. Plain backgrounds remain recommended.

## Core Windows features

- .NET 8 WPF desktop application
- Fully offline at runtime
- Fixed physical ID size: **85 × 115 mm**
- Uploaded Front/Back artwork is the actual ID design
- Employee records stored locally on the PC
- Photo cleanup defaults to white background
- Signature cleanup defaults to transparent background
- Manual QR image upload
- A4 PDF output supports up to **2 people**, each with Front/Back pairing
- DOB output uses full English month format such as **January 12, 1987**
- Imported Windows fonts via local TTF/OTF without system-wide installation
- PDF renderer outputs Studio elements at **300 dpi**

## Local data

Runtime data is stored under the current Windows user's local application-data folder in `DIKERMA`:

- `data/employees.json`
- `data/settings.json`
- `data/layout.json`
- `assets/`
- `exports/`

No online database, API, authentication server, or networking code is required.

## Shortcuts

| Action | Shortcut |
| --- | --- |
| Save master design | Ctrl+S |
| Undo / redo | Ctrl+Z / Ctrl+Y |
| Duplicate / delete | Ctrl+D / Delete |
| Group / ungroup | Ctrl+G / Ctrl+Shift+G |
| Select all visible layers | Ctrl+A |
| Multi-select on canvas | Shift+click |
| Move / larger step | Arrow / Shift+arrow |
| Zoom | Ctrl+wheel or Ctrl+plus/minus |
| Fit preview | Ctrl+0 |

## Printing rule

Print PDFs at **Actual Size / 100%**. Do not use **Fit to Page** when validating the 85 × 115 mm physical size.

## Build and validation

GitHub Actions on `windows-latest` performs offline-source verification, restore, Release compilation, Studio smoke tests, self-contained `win-x64` publishing, optional Authenticode signing, and SHA-256 checksum generation.

v0.3 smoke coverage includes:

- layout schema v3
- explicit record-bound field classification
- Publisher appearance persistence
- frame/opacity clamping
- picture-frame rendering
- all layout element render types at 300 dpi
- crop rendering
- advanced eraser behavior
- two-person PDF export with a deliberately stale master photo path to exercise record-binding protection

The Android repository `Brgysibulan/dikerma` remains separate and is not modified by Windows development.
