/* MemoryLand – tiện ích dùng chung */
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const money = n => (Math.round(n) || 0).toLocaleString('vi-VN') + ' đ';
const fmtDate = d => d ? new Date(d).toLocaleDateString('vi-VN') : '—';
const fmtTime = d => d ? new Date(d).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '—';
const fmtDT = d => d ? fmtTime(d) + ' ' + fmtDate(d) : '—';
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
// Máy chủ lưu giờ UTC không kèm "Z" -> thêm để hiển thị đúng giờ địa phương
const utc = d => d && !/[zZ]|[+-]\d\d:?\d\d$/.test(d) ? d + 'Z' : d;

async function api(path, opt = {}) {
  const o = { ...opt };
  if (o.body && !(o.body instanceof FormData) && typeof o.body !== 'string') {
    o.headers = { 'Content-Type': 'application/json', ...(o.headers || {}) };
    o.body = JSON.stringify(o.body);
  }
  const r = await fetch('/api' + path, o);
  const t = await r.text();
  let d = null; try { d = t ? JSON.parse(t) : null; } catch { }
  if (!r.ok) throw new Error((d && (d.error || d.message || d.title)) || ('Lỗi ' + r.status));
  return d;
}

function toast(msg, type = '') {
  let box = $('.toasts');
  if (!box) { box = document.createElement('div'); box.className = 'toasts'; document.body.appendChild(box); }
  const t = document.createElement('div'); t.className = 'toast ' + type; t.textContent = msg; box.appendChild(t);
  setTimeout(() => t.remove(), 4500);
}

function topbar(active) {
  const items = [['/', 'Trang chủ', 'home'], ['/quy-hoach.html', 'Quy hoạch nghĩa trang', 'qh'], ['/cung-bai.html', 'Chăm sóc mộ từ xa', 'cb']];
  document.body.insertAdjacentHTML('afterbegin', `
  <header class="top"><div class="top-in">
    <a class="brand" href="/"><i class="fas fa-dove"></i> MemoryLand AI</a>
    <nav>${items.map(i => `<a href="${i[0]}" class="${i[2] === active ? 'on' : ''}">${i[1]}</a>`).join('')}</nav>
  </div></header>`);
}

function tabs(el, onChange) {
  const bs = $$('.tab', el);
  const show = id => {
    bs.forEach(b => b.classList.toggle('on', b.dataset.t === id));
    $$('.pane').forEach(p => p.classList.toggle('on', p.id === 'p-' + id));
    history.replaceState(null, '', '#' + id);
    onChange && onChange(id);
  };
  bs.forEach(b => b.onclick = () => show(b.dataset.t));
  const h = location.hash.slice(1);
  show(bs.some(b => b.dataset.t === h) ? h : bs[0].dataset.t);
}

function openModal(id) { $('#' + id).classList.add('on'); }
function closeModal(id) { $('#' + id).classList.remove('on'); }
