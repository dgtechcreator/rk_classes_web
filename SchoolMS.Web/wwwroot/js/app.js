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
