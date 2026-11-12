// By Astra. The ON/OFF lights of 3rd party programs on a pet program page: a click on red opens the program, a click on
// green closes it, through Inference Iguana's watch (Benchmark Rock's program-watch) on this PC (Astra, 2026-11-06:
// "press the red button, then it launches that program and the button turns green if its on and detected").
// The page script (pet-program-page-20261018.js) sends nothing itself: it asks here with a `pet-program-switch` event
// and hears back with `pet-program-switched`. The watch's address comes from the watch's own file on this PC, never
// from the hub, and only an address on this PC is used. Claude Opus 5.5 (claude-opus-5-5), 2026-11-06.
(function () {
  'use strict';
  var ON_THIS_PC = /^http:\/\/(127\.0\.0\.1|localhost):\d+$/;
  function done(item, ok, message) {
    document.dispatchEvent(new CustomEvent('pet-program-switched', { detail: { item: item, ok: ok, message: message } }));
  }
  document.addEventListener('pet-program-switch', function (e) {
    var d = e.detail || {};
    if (typeof d.item !== 'string' || typeof d.id !== 'string' || typeof d.control !== 'string' || !ON_THIS_PC.test(d.control)) return;
    fetch(d.control + (d.open ? '/open' : '/close'), { method: 'POST', headers: { 'Content-Type': 'text/plain' },
      body: JSON.stringify({ id: d.id }), cache: 'no-store' })
      .then(function (response) { return response.json(); })
      .then(function (r) { done(d.item, true, r && typeof r.message === 'string' ? r.message : ''); },
        function () { done(d.item, false, ''); });
  });
})();
