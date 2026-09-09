// Açık / koyu tema geçişi. İlk tema _Layout.cshtml'deki inline script ile,
// FOUC olmadan <html data-theme> üzerinden ayarlanıyor; burada sadece düğme.
(function () {
    var btn = document.getElementById('themeToggle');
    if (!btn) return;
    btn.addEventListener('click', function () {
        var cur = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
        var next = cur === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('yt-theme', next); } catch (e) { }
    });
})();
