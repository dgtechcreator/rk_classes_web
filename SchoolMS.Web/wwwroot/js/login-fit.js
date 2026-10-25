/* Login pages: safety net so the card never overflows the screen.
   CSS already scales everything from the viewport; if some unusual screen/browser
   still leaves the card taller than the available area, shrink the root size until it fits. */
(function () {
  var root = document.documentElement;

  function overflows(shell, page) {
    var cs = getComputedStyle(page);
    var availH = page.clientHeight - parseFloat(cs.paddingTop) - parseFloat(cs.paddingBottom);
    var availW = page.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
    return shell.offsetHeight > availH + 0.5 || shell.offsetWidth > availW + 0.5;
  }

  function fit() {
    var shell = document.querySelector('.shell');
    var page = document.querySelector('.page');
    if (!shell || !page) return;
    root.style.fontSize = '';
    var size = parseFloat(getComputedStyle(root).fontSize);
    var guard = 0;
    while (overflows(shell, page) && size > 5 && guard++ < 80) {
      size -= 0.25;
      root.style.fontSize = size + 'px';
    }
  }

  var t;
  window.addEventListener('resize', function () { clearTimeout(t); t = setTimeout(fit, 60); });
  window.addEventListener('orientationchange', function () { setTimeout(fit, 200); });
  window.addEventListener('load', fit);
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(fit);
  fit();
  window.loginFit = fit;
})();
