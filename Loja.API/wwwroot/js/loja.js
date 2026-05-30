const API = '';
const token = () => sessionStorage.getItem('dg_token') || '';
const userId = () => parseInt(sessionStorage.getItem('dg_uid') || '0');
const userRole = () => sessionStorage.getItem('dg_role') || '';
const userName = () => sessionStorage.getItem('dg_name') || '';
let cart = JSON.parse(sessionStorage.getItem('dg_cart') || '[]');

function salvarCarrinho() {
  sessionStorage.setItem('dg_cart', JSON.stringify(cart));
}

async function req(method, path, body) {
  const opts = {
    method,
    headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token() }
  };
  if (body) opts.body = JSON.stringify(body);
  const r = await fetch(API + path, opts);
  const d = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(d.erro || d.error || 'Erro ' + r.status);
  return d;
}

function toast(msg, tipo = 'success') {
  const el = document.createElement('div');
  el.className = 'toast align-items-center text-white border-0 show mb-2 bg-' + (tipo === 'success' ? 'primary' : 'danger');
  el.setAttribute('role', 'alert');
  el.innerHTML = '<div class="d-flex"><div class="toast-body">' + msg + '</div><button type="button" class="btn-close btn-close-white me-2 m-auto" onclick="this.closest(\'.toast\').remove()"></button></div>';
  let container = document.getElementById('toast-container');
  if (!container) { container = document.createElement('div'); container.id = 'toast-container'; document.body.appendChild(container); }
  container.appendChild(el);
  setTimeout(() => el.remove(), 3500);
}

function sair() {
  ['dg_token','dg_role','dg_name','dg_uid','dg_cart'].forEach(k => sessionStorage.removeItem(k));
  location.href = '/';
}

function atualizarBadgeCarrinho() {
  const badges = document.querySelectorAll('.cart-badge');
  const total = cart.reduce((s, i) => s + i.qty, 0);
  badges.forEach(b => b.textContent = total);
}
