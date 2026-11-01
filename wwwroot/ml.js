/* MemoryLand AI – tiện ích dùng chung */
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const money = n => (Math.round(n) || 0).toLocaleString('vi-VN') + 'đ';
const moneyM = n => (n / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' tr';
const fmtDate = d => d ? new Date(d).toLocaleDateString('vi-VN') : '—';
// Server lưu mốc thời gian theo UTC nhưng không kèm "Z" → thêm vào để hiện đúng giờ Việt Nam
const asUtc = d => typeof d === 'string' && /T\d\d:\d\d/.test(d) && !/(Z|[+-]\d\d:?\d\d)$/.test(d) ? d + 'Z' : d;
const fmtTime = d => d ? new Date(asUtc(d)).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }) : '—';
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

async function api(path, opt = {}) {
  const o = { headers: { 'Content-Type': 'application/json' }, ...opt };
  if (o.body && typeof o.body !== 'string') o.body = JSON.stringify(o.body);
  const r = await fetch('/api/ml' + path, o);
  const t = await r.text();
  let d = null; try { d = t ? JSON.parse(t) : null; } catch { }
  if (!r.ok) throw new Error((d && (d.error || d.title)) || ('Lỗi ' + r.status));
  return d;
}

function toast(msg, type = '') {
  let box = $('.toast-box');
  if (!box) { box = document.createElement('div'); box.className = 'toast-box'; document.body.appendChild(box); }
  const t = document.createElement('div'); t.className = 'toast ' + type; t.innerHTML = msg; box.appendChild(t);
  setTimeout(() => { t.style.opacity = 0; t.style.transition = '.3s'; setTimeout(() => t.remove(), 300); }, 3800);
}

function download(name, text, mime = 'text/plain') {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob(['﻿' + text], { type: mime + ';charset=utf-8' }));
  a.download = name; a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 2000);
}

// Thanh trên cùng cho các công cụ của ban quản lý (mở từ trang quản lý)
function mlNav(active) {
  const items = [
    ['/index.html', 'fa-arrow-left', 'Trang quản lý', 'admin'],
    ['/lotmap.html', 'fa-map-location-dot', 'Bản đồ phân lô', 'map'],
    ['/ai.html', 'fa-wand-magic-sparkles', 'Quy hoạch & dự báo', 'ai'],
  ];
  document.body.insertAdjacentHTML('afterbegin', `
  <header class="ml-top">
    <a class="ml-brand" href="/index.html"><span class="logo"><i class="fas fa-dove"></i></span><span><b>MemoryLand AI</b></span></a>
    <nav class="ml-nav">${items.map(i => `<a href="${i[0]}" class="${i[3] === active ? 'on' : ''}"><i class="fas ${i[1]}"></i>${i[2]}</a>`).join('')}</nav>
  </header>`);
}

// Thanh trên cùng cho trang gia đình / đối tác (người dùng bên ngoài, không thấy menu quản trị)
function userNav(subtitle, right = '') {
  document.body.insertAdjacentHTML('afterbegin', `
  <header class="ml-top">
    <span class="ml-brand"><span class="logo"><i class="fas fa-dove"></i></span><span><b>MemoryLand</b><small>${subtitle}</small></span></span>
    <span class="sp"></span>${right}
  </header>`);
}

/* ── Dựng "ảnh trực giao" mô phỏng của khu nghĩa trang (offscreen) ── */
function rng(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

function buildOrtho(site, ppm = 8) {
  const W = site.width * ppm, H = site.height * ppm;
  const c = document.createElement('canvas'); c.width = W; c.height = H; const g = c.getContext('2d');
  const R = rng(11);
  // nền cỏ + đất
  const grd = g.createLinearGradient(0, 0, W, H); grd.addColorStop(0, '#6f9150'); grd.addColorStop(1, '#5d8043'); g.fillStyle = grd; g.fillRect(0, 0, W, H);
  for (let i = 0; i < 9000; i++) {
    const x = R() * W, y = R() * H, v = R();
    g.fillStyle = v < .45 ? 'rgba(48,84,36,.28)' : v < .8 ? 'rgba(150,176,96,.22)' : 'rgba(120,98,60,.2)';
    g.fillRect(x, y, 2 + R() * 4, 2 + R() * 3);
  }
  // đường mòn đất
  g.strokeStyle = 'rgba(170,140,92,.55)'; g.lineWidth = 2.2 * ppm; g.lineCap = 'round';
  g.beginPath(); g.moveTo(site.width * .5 * ppm, H); g.bezierCurveTo(W * .46, H * .7, W * .56, H * .4, W * .48, 0); g.stroke();
  // hillshade nhẹ từ DEM
  const cs = site.cell * ppm;
  for (let j = 0; j < site.rows; j++) for (let i = 0; i < site.cols; i++) {
    const z = site.dem[j * site.cols + i], zr = site.dem[j * site.cols + Math.min(site.cols - 1, i + 1)], zd = site.dem[Math.min(site.rows - 1, j + 1) * site.cols + i];
    const sh = (z - zr) * 0.9 + (z - zd) * 0.6;
    g.fillStyle = sh > 0 ? `rgba(255,255,230,${Math.min(.18, sh * .35)})` : `rgba(0,20,0,${Math.min(.22, -sh * .4)})`;
    g.fillRect(i * cs, j * cs, cs + 1, cs + 1);
  }
  // cây (bóng + tán)
  site.trees.forEach(t => {
    const x = t.x * ppm, y = t.y * ppm, r = t.r * ppm;
    g.fillStyle = 'rgba(0,0,0,.28)'; g.beginPath(); g.ellipse(x + r * .35, y + r * .35, r * 1.02, r * .9, 0, 0, 7); g.fill();
    const rg = g.createRadialGradient(x - r * .3, y - r * .3, r * .1, x, y, r);
    rg.addColorStop(0, '#4e8a3a'); rg.addColorStop(1, '#1f4a1d'); g.fillStyle = rg; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill();
    for (let k = 0; k < 9; k++) { g.fillStyle = 'rgba(120,170,80,.35)'; g.beginPath(); g.arc(x + (R() - .5) * r, y + (R() - .5) * r, r * .22, 0, 7); g.fill(); }
  });
  // mộ
  site.graves.forEach(gr => {
    const x = gr.x * ppm, y = gr.y * ppm, w = gr.w * ppm, d = gr.d * ppm;
    g.save(); g.translate(x, y); g.rotate(gr.a * Math.PI / 180);
    g.fillStyle = 'rgba(0,0,0,.3)'; g.fillRect(-w / 2 + 2, -d / 2 + 2, w, d);
    g.fillStyle = gr.type === 'Mộ gia tộc' ? '#d8d2c2' : gr.type === 'Mộ đôi' ? '#cfcab9' : '#c4bfae'; g.fillRect(-w / 2, -d / 2, w, d);
    g.fillStyle = 'rgba(255,255,255,.35)'; g.fillRect(-w / 2, -d / 2, w, Math.max(2, d * .13));
    g.strokeStyle = 'rgba(70,64,50,.6)'; g.lineWidth = 1; g.strokeRect(-w / 2, -d / 2, w, d);
    // cỏ mọc trên/mộ quanh
    const n = Math.floor(gr.weed * 10);
    for (let k = 0; k < n; k++) { g.fillStyle = R() < .6 ? 'rgba(60,98,40,.8)' : 'rgba(120,100,50,.7)'; g.fillRect((R() - .5) * (w + 6), (R() - .5) * (d + 6), 2 + R() * 3, 2 + R() * 3); }
    g.restore();
  });
  return c;
}

/* tọa độ site (m) → WGS84 giả lập */
function siteToGps(x, y) { return { lat: 21.0300 - y / 110574, lng: 105.8500 + x / (111320 * Math.cos(21.03 * Math.PI / 180)) }; }

/* aHash 64-bit (16 ký tự hex) từ canvas/ảnh + tỷ lệ "cỏ/rác" xanh-nâu */
function imageHash(source) {
  const c = document.createElement('canvas'); c.width = 8; c.height = 8; const g = c.getContext('2d', { willReadFrequently: true });
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  g.drawImage(source, 0, 0, 8, 8);
  const d = g.getImageData(0, 0, 8, 8).data, lum = [];
  for (let i = 0; i < 64; i++) lum.push(.299 * d[i * 4] + .587 * d[i * 4 + 1] + .114 * d[i * 4 + 2]);
  const m = lum.reduce((a, b) => a + b) / 64;
  let bits = lum.map(v => v > m ? 1 : 0), hex = '';
  for (let i = 0; i < 64; i += 4) hex += parseInt(bits.slice(i, i + 4).join(''), 2).toString(16);
  return hex;
}
function hamming(a, b) {
  let n = 0; for (let i = 0; i < a.length; i++) { let x = parseInt(a[i], 16) ^ parseInt(b[i], 16); while (x) { n += x & 1; x >>= 1; } } return n;
}
