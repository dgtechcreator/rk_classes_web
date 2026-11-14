// Fingerprint / passkey (WebAuthn) for the website. Used by the login pages and by the "Turn on fingerprint login" links.
(function () {
  const b64uToBuf = (s) => {
    s = s.replace(/-/g, '+').replace(/_/g, '/');
    const bin = atob(s + '='.repeat((4 - (s.length % 4)) % 4));
    const a = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) a[i] = bin.charCodeAt(i);
    return a.buffer;
  };
  const bufToB64u = (b) => {
    let s = '';
    new Uint8Array(b).forEach((x) => (s += String.fromCharCode(x)));
    return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  };
  async function post(url, body) {
    const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: body ? JSON.stringify(body) : undefined, credentials: 'same-origin' });
    let j = null;
    try { j = await r.json(); } catch (e) { /* no body */ }
    if (!r.ok) throw new Error((j && (j.error || j.message)) || 'Request failed (' + r.status + ')');
    return j;
  }
  const supported = () => !!(window.PublicKeyCredential && navigator.credentials && window.isSecureContext);

  // Signed in: register THIS device (the browser asks for the fingerprint / face / screen lock).
  window.rkFingerprintRegister = async function () {
    if (!supported()) { alert('Fingerprint login needs a secure (https) page and a browser / device that supports it.'); return; }
    try {
      const o = await post('/WebAuthn/RegisterOptions');
      o.challenge = b64uToBuf(o.challenge);
      o.user.id = b64uToBuf(o.user.id);
      (o.excludeCredentials || []).forEach((c) => (c.id = b64uToBuf(c.id)));
      const cred = await navigator.credentials.create({ publicKey: o });
      await post('/WebAuthn/RegisterComplete', {
        id: cred.id, rawId: bufToB64u(cred.rawId), type: cred.type,
        response: { attestationObject: bufToB64u(cred.response.attestationObject), clientDataJSON: bufToB64u(cred.response.clientDataJSON) },
        extensions: cred.getClientExtensionResults(),
      });
      alert('Done. Next time you can sign in with your fingerprint on this device.');
      if (window.rkFingerprintListRefresh) window.rkFingerprintListRefresh();
    } catch (e) {
      if (e && e.name === 'NotAllowedError') return;   // cancelled
      alert(e.message || 'Could not turn on fingerprint login.');
    }
  };

  // Login page: sign in without a password.
  window.rkFingerprintLogin = async function (errId) {
    const err = (m) => { const el = document.getElementById(errId || 'faErr'); if (el) { el.textContent = m; el.style.display = m ? 'block' : 'none'; } };
    err('');
    if (!supported()) { err('Fingerprint login needs a secure (https) page and a supported browser / device.'); return; }
    try {
      const o = await post('/WebAuthn/LoginOptions');
      o.challenge = b64uToBuf(o.challenge);
      (o.allowCredentials || []).forEach((c) => (c.id = b64uToBuf(c.id)));
      const cred = await navigator.credentials.get({ publicKey: o });
      const j = await post('/WebAuthn/LoginComplete', {
        id: cred.id, rawId: bufToB64u(cred.rawId), type: cred.type,
        response: {
          authenticatorData: bufToB64u(cred.response.authenticatorData), clientDataJSON: bufToB64u(cred.response.clientDataJSON),
          signature: bufToB64u(cred.response.signature), userHandle: cred.response.userHandle ? bufToB64u(cred.response.userHandle) : null,
        },
        extensions: cred.getClientExtensionResults(),
      });
      window.location.href = j.redirect || '/';
    } catch (e) {
      if (e && e.name === 'NotAllowedError') return;   // cancelled
      err(e.message || 'Fingerprint sign-in failed.');
    }
  };
  window.rkFingerprintSupported = supported;
})();
