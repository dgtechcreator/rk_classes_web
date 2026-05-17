document.addEventListener('DOMContentLoaded', () => {
  // Toast from TempData
  const ts = document.getElementById('_ts'), te = document.getElementById('_te');
  if (ts?.textContent.trim()) toast(ts.textContent.trim(), 'success');
  if (te?.textContent.trim()) toast(te.textContent.trim(), 'danger');
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
