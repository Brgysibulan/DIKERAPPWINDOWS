// Asset conversion only. Run with Node and sharp installed; not an application dependency.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
(async () => {
  const dir = path.join(__dirname, '../src/Dikerma.Windows/Assets');
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const frames = await Promise.all(sizes.map(size => sharp(path.join(dir, 'AppIcon.svg')).resize(size, size).png().toBuffer()));
  const header = Buffer.alloc(6 + 16 * sizes.length);
  header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  frames.forEach((frame, i) => {
    const p = 6 + i * 16;
    header[p] = sizes[i] % 256; header[p + 1] = sizes[i] % 256;
    header.writeUInt16LE(1, p + 4); header.writeUInt16LE(32, p + 6);
    header.writeUInt32LE(frame.length, p + 8); header.writeUInt32LE(offset, p + 12);
    offset += frame.length;
  });
  const icon = Buffer.concat([header, ...frames]);
  for (const name of ['dikerma.ico', 'AppIcon.ico']) fs.writeFileSync(path.join(dir, name), icon);
})();
