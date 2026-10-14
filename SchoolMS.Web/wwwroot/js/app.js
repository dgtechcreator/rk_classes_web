document.addEventListener('DOMContentLoaded', () => {
  // Toast from TempData
  const ts = document.getElementById('_ts'), te = document.getElementById('_te');
  if (ts?.textContent.trim()) toast(ts.textContent.trim(), 'success');
  if (te?.textContent.trim()) toast(te.textContent.trim(), 'danger');

  // ── Mobile Sidebar Toggle ──────────────────────────────
  const sidebar  = document.querySelector('.sidebar');
  const overlay  = document.getElementById('sbOverlay');
  const menuTog  = document.getElementById('menuTog');

  function openSidebar() {
    sidebar?.classList.add('open');
    overlay?.classList.add('show');
    document.body.style.overflow = 'hidden';
  }
  function closeSidebar() {
    sidebar?.classList.remove('open');
    overlay?.classList.remove('show');
    document.body.style.overflow = '';
  }

  menuTog?.addEventListener('click', () => {
    sidebar?.classList.contains('open') ? closeSidebar() : openSidebar();
  });

  // Tap overlay to close
  overlay?.addEventListener('click', closeSidebar);

  // Close sidebar when a nav link is clicked on mobile
  sidebar?.querySelectorAll('.nav-item').forEach(link => {
    link.addEventListener('click', () => {
      if (window.innerWidth <= 768) closeSidebar();
    });
  });

  // Close on resize to desktop
  window.addEventListener('resize', () => {
    if (window.innerWidth > 768) closeSidebar();
  });

  // Blue background on selects that have a value selected
  function updateSelect(sel) {
    if (sel.value && sel.value !== '') {
      sel.classList.add('fs-filled');
    } else {
      sel.classList.remove('fs-filled');
    }
  }
  document.querySelectorAll('select.fs').forEach(sel => {
    updateSelect(sel);
    sel.addEventListener('change', () => updateSelect(sel));
  });
});

function toast(msg, type = 'success') {
  const el = document.createElement('div');
  el.className = `alert alert-${type}`;
  const ico = type === 'success' ? '✅' : '❌';
  el.innerHTML = `${ico} ${msg}`;
  Object.assign(el.style, {
    position:'fixed', top:'76px', right:'22px', zIndex:'9999',
    minWidth:'260px', maxWidth:'380px',
    boxShadow:'0 4px 20px rgba(0,0,0,.13)',
    animation:'fadeUp .22s ease'
  });
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 4500);
}

function openModal(id)  { const m=document.getElementById(id); if(m){m.style.display='flex'; document.body.style.overflow='hidden';} }
function closeModal(id) { const m=document.getElementById(id); if(m){m.style.display='none'; document.body.style.overflow='';} }

// Close modal on backdrop click
document.addEventListener('click', e => {
  if (e.target.classList.contains('modal-backdrop')) {
    e.target.style.display = 'none';
    document.body.style.overflow = '';
  }
});

/* ===== WhatsApp sender: choose Normal / Business (phone) or Web / Desktop app (PC) =====
   rkWaOpen(number, message) - number = digits with country code. Asks every time which WhatsApp
   to use; the last choice is listed first. Android phones can target the Business app directly;
   on a PC the browser can only pick WhatsApp Web (whatever account is logged in there) or the
   installed desktop app. */
function rkWaOpen(number, message) {
  let num = String(number || '').replace(/\D/g, '');
  if (num.length === 10) num = '91' + num;
  if (!num) { alert('Phone number not available'); return; }
  const text = encodeURIComponent(message || '');
  const isAndroid = /Android/i.test(navigator.userAgent);
  const fallback = 'https://wa.me/' + num + '?text=' + text;
  const intent = pkg => 'intent://send/?phone=' + num + '&text=' + text +
    '#Intent;scheme=whatsapp;package=' + pkg + ';S.browser_fallback_url=' + encodeURIComponent(fallback) + ';end';
  const options = isAndroid ? [
    { id: 'normal',   icon: 'fa-whatsapp',        title: 'Normal WhatsApp',   sub: '',  go: () => { location.href = intent('com.whatsapp'); } },
    { id: 'business', icon: 'fa-briefcase',       title: 'Business WhatsApp', sub: '',  go: () => { location.href = intent('com.whatsapp.w4b'); } }
  ] : [
    { id: 'web',      icon: 'fa-globe',           title: 'WhatsApp Web (browser)', sub: 'Jis account ka QR scan kiya hai usi se jayega', go: () => { window.open('https://web.whatsapp.com/send?phone=' + num + '&text=' + text, '_blank'); } },
    { id: 'desktop',  icon: 'fa-desktop',         title: 'WhatsApp Desktop app',   sub: 'PC par installed WhatsApp / WhatsApp Business app', go: () => { location.href = 'whatsapp://send?phone=' + num + '&text=' + text; } }
  ];
  let last = null;
  try { last = localStorage.getItem('rkWaApp'); } catch (e) {}
  options.sort((a, b) => (b.id === last) - (a.id === last));

  const old = document.getElementById('rkWaPicker'); if (old) old.remove();
  const wrap = document.createElement('div');
  wrap.id = 'rkWaPicker';
  wrap.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:100000;display:flex;align-items:center;justify-content:center;padding:16px;';
  const box = document.createElement('div');
  box.style.cssText = 'background:#fff;border-radius:12px;padding:18px;width:100%;max-width:340px;box-shadow:0 10px 40px rgba(0,0,0,.3);font-family:inherit;';
  const h = document.createElement('div');
  h.textContent = 'Kis WhatsApp se bhejna hai?';
  h.style.cssText = 'font-weight:800;font-size:15px;margin-bottom:12px;text-align:center;color:#111;';
  box.appendChild(h);
  options.forEach(o => {
    const b = document.createElement('button');
    b.type = 'button';
    b.style.cssText = 'display:flex;align-items:center;gap:12px;width:100%;text-align:left;background:#f0fdf4;border:1.5px solid #25d366;color:#111;padding:12px;border-radius:8px;margin-bottom:8px;cursor:pointer;font-size:14px;font-weight:700;';
    const i = document.createElement('i');
    i.className = (o.icon === 'fa-whatsapp' ? 'fab ' : 'fas ') + o.icon;
    i.style.cssText = 'color:#25d366;font-size:20px;width:24px;text-align:center;';
    const t = document.createElement('span');
    t.textContent = o.title + (o.id === last ? '  (last used)' : '');
    b.appendChild(i); b.appendChild(t);
    if (o.sub) { const s = document.createElement('div'); s.textContent = o.sub; s.style.cssText = 'font-weight:500;font-size:11px;color:#555;margin-top:2px;'; t.appendChild(s); t.style.display = 'block'; }
    b.onclick = () => {
      try { localStorage.setItem('rkWaApp', o.id); } catch (e) {}
      wrap.remove();
      o.go();
    };
    box.appendChild(b);
  });
  const c = document.createElement('button');
  c.type = 'button'; c.textContent = 'Cancel';
  c.style.cssText = 'width:100%;background:#f3f4f6;border:none;color:#374151;padding:10px;border-radius:8px;cursor:pointer;font-weight:600;font-size:13px;';
  c.onclick = () => wrap.remove();
  box.appendChild(c);
  wrap.appendChild(box);
  wrap.onclick = e => { if (e.target === wrap) wrap.remove(); };
  document.body.appendChild(wrap);
}
