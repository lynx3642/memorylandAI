/* Xử lý ảnh nghiệm thu: mã băm 64-bit (aHash), ước lượng cỏ/rác, ảnh thu nhỏ, ảnh mẫu */
function seeded(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

function aHash(src) {
  const c = document.createElement('canvas'); c.width = c.height = 8;
  const g = c.getContext('2d', { willReadFrequently: true });
  g.imageSmoothingQuality = 'high'; g.drawImage(src, 0, 0, 8, 8);
  const d = g.getImageData(0, 0, 8, 8).data, lum = [];
  for (let i = 0; i < 64; i++) lum.push(.299 * d[i * 4] + .587 * d[i * 4 + 1] + .114 * d[i * 4 + 2]);
  const m = lum.reduce((a, b) => a + b) / 64, bits = lum.map(v => v > m ? 1 : 0);
  let hex = ''; for (let i = 0; i < 64; i += 4) hex += parseInt(bits.slice(i, i + 4).join(''), 2).toString(16);
  return hex;
}
const invertHash = h => [...h].map(c => (parseInt(c, 16) ^ 15).toString(16)).join('');

// Ước lượng tỷ lệ cỏ/rác (mảng xanh, nâu) từ ảnh thật
function estimateWeed(src) {
  const c = document.createElement('canvas'); c.width = 64; c.height = 48;
  const g = c.getContext('2d', { willReadFrequently: true }); g.drawImage(src, 0, 0, 64, 48);
  const d = g.getImageData(0, 0, 64, 48).data; let n = 0;
  for (let i = 0; i < d.length; i += 4) { const r = d[i], gr = d[i + 1], b = d[i + 2]; if (gr > 60 && gr > r * 1.08 && gr > b * 1.12) n++; }
  return Math.min(1, +(n / (64 * 48) * 1.6).toFixed(2));
}

// Ảnh mẫu: cùng một "mộ" (seed) cho ảnh trước/sau, khác nhau ở lượng cỏ mọc
function sampleScene(seed, weed) {
  const W = 320, H = 240, c = document.createElement('canvas'); c.width = W; c.height = H;
  const g = c.getContext('2d'), R = seeded(seed);
  for (let by = 0; by < 4; by++) for (let bx = 0; bx < 4; bx++) {
    const L = 60 + 120 * R();
    g.fillStyle = `rgb(${L * .72 | 0},${L * .9 | 0},${L * .52 | 0})`; g.fillRect(bx * W / 4, by * H / 4, W / 4 + 1, H / 4 + 1);
  }
  const tx = 90 + R() * 60, ty = 60 + R() * 40;
  g.fillStyle = '#b9b3a2'; g.fillRect(tx, ty + 30, 120, 70);                    // nền mộ
  g.fillStyle = '#8f8a7b'; g.fillRect(tx + 38, ty - 10, 44, 48);                // bia
  g.fillStyle = '#6e6a5e'; g.fillRect(tx + 50, ty, 20, 4);
  const W2 = seeded(Math.random() * 1e9 | 0);
  const n = Math.round(weed * 260);
  for (let i = 0; i < n; i++) {
    const x = tx - 40 + W2() * 200, y = ty - 20 + W2() * 150, s = 3 + W2() * 6;
    g.fillStyle = W2() < .7 ? 'rgba(40,100,30,.9)' : 'rgba(120,95,45,.85)'; g.fillRect(x, y, s, s * 1.4);
  }
  return c;
}

function thumbOf(src) {
  const c = document.createElement('canvas'); c.width = 240; c.height = 180;
  c.getContext('2d').drawImage(src, 0, 0, 240, 180); return c.toDataURL('image/jpeg', .55);
}

function loadImage(file) {
  return new Promise((res, rej) => {
    const url = URL.createObjectURL(file), img = new Image();
    img.onload = () => { URL.revokeObjectURL(url); res(img); }; img.onerror = () => rej(new Error('Không đọc được ảnh'));
    img.src = url;
  });
}
function coverCanvas(img) {
  const c = document.createElement('canvas'); c.width = 320; c.height = 240;
  const r = Math.max(320 / img.width, 240 / img.height), w = img.width * r, h = img.height * r;
  c.getContext('2d').drawImage(img, (320 - w) / 2, (240 - h) / 2, w, h); return c;
}

/* opts: { kind:'before'|'after', scenario, seed, beforeWeed } → { hash, weed, thumb } */
async function makePhoto(file, o) {
  const after = o.kind === 'after';
  let cv, weed;
  if (file) { cv = coverCanvas(await loadImage(file)); weed = estimateWeed(cv); }
  else {
    weed = after ? 0.03 + Math.random() * .04 : 0.38 + Math.random() * .15;
    cv = sampleScene(after && o.scenario === 'wrong' ? o.seed + 101 : o.seed, weed);
  }
  if (after && o.scenario === 'lazy') weed = o.beforeWeed ?? weed;
  let hash = aHash(cv);
  if (after && o.scenario === 'wrong') hash = invertHash(hash);
  return { hash, weed: +weed.toFixed(2), thumb: thumbOf(cv) };
}
