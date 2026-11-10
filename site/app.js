/* Downora web demo UI. Uses Engine (simulated queue) and I18N. All dynamic text goes through esc() before it reaches innerHTML. */
(function () {
  'use strict';

  var STORE_KEY = 'downora-demo-v1';
  var REPO = 'https://github.com/Andrewaziz99/Downora';
  var PAGES = ['home', 'downloader', 'queue', 'library', 'history', 'settings', 'about'];
  var ICONS = {
    home: 'M3 11l9-8 9 8M5 10v10h5v-6h4v6h5V10',
    downloader: 'M12 3v12M7 11l5 5 5-5M4 20h16',
    queue: 'M4 6h16M4 12h16M4 18h10',
    library: 'M4 5h16v14H4zM10 9l5 3-5 3z',
    history: 'M3 12a9 9 0 1 0 3-6.7M3 4v5h5M12 8v5l3 2',
    settings: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12l2-1-2-4-2 1-2-1V5H9v2L7 8 5 7l-2 4 2 1v0l-2 1 2 4 2-1 2 1v2h6v-2l2-1 2 1 2-4z',
    about: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 11v6M12 7.5v.5',
    pause: 'M8 5v14M16 5v14', play: 'M7 4l12 8-12 8z', x: 'M6 6l12 12M18 6L6 18', retry: 'M4 12a8 8 0 1 1 3 6.2M4 19v-5h5',
    sun: 'M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.5 1.5M17.5 17.5L19 19M5 19l1.5-1.5M17.5 6.5L19 5',
    moon: 'M20 14A8 8 0 0 1 10 4a8 8 0 1 0 10 10z', bolt: 'M13 2L5 14h6l-1 8 8-12h-6z', trash: 'M5 7h14M9 7V4h6v3M7 7l1 13h8l1-13'
  };

  var prefs = { theme: matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark', lang: (navigator.language || '').toLowerCase().indexOf('ar') === 0 ? 'ar' : 'en', concurrency: 3, autoStart: true };
  var saved = load();
  if (saved && saved.prefs) Object.keys(prefs).forEach(function (k) { if (saved.prefs[k] !== undefined) prefs[k] = saved.prefs[k]; });

  var queue = new Engine.Queue({ concurrency: prefs.concurrency, autoStart: prefs.autoStart, onChange: onQueueChange });
  if (saved && Array.isArray(saved.jobs)) {
    queue.jobs = saved.jobs;
    queue.nextId = saved.nextId || saved.jobs.length + 1;
    queue.jobs.forEach(function (j) { if (Engine.ACTIVE[j.state] || j.state === 'waiting') { j.state = 'paused'; j.speed = 0; } });   // like the real app: closing pauses what was running
  }

  var dl = { url: '', busy: false, result: null, error: null, mode: 'video', quality: 'best', audio: 'mp3', picked: {}, notice: null };
  var lib = { search: '', kind: 'all' };
  var hist = { status: 'all' };
  var lastSig = '';
  var view = document.getElementById('view');

  function load() { try { return JSON.parse(localStorage.getItem(STORE_KEY)); } catch (e) { return null; } }
  function save() { try { localStorage.setItem(STORE_KEY, JSON.stringify({ prefs: prefs, jobs: queue.jobs, nextId: queue.nextId })); } catch (e) { /* storage unavailable: the demo still works */ } }

  function t(key, vars) {
    var s = (I18N[prefs.lang] && I18N[prefs.lang][key]) || I18N.en[key] || key;
    if (vars) Object.keys(vars).forEach(function (k) { s = s.split('{' + k + '}').join(vars[k]); });
    return s;
  }
  function esc(v) { return String(v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function icon(name) { return '<svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="' + ICONS[name] + '"/></svg>'; }
  function ltr(text) { return '<bdi class="ltr">' + esc(text) + '</bdi>'; }
  function num(n) { return prefs.lang === 'ar' ? n.toLocaleString('ar-EG') : n.toLocaleString('en-US'); }
  function bytes(b) {
    var units = ['B', 'KB', 'MB', 'GB'], i = 0;
    while (b >= 1024 && i < 3) { b /= 1024; i++; }
    return ltr((i >= 2 ? b.toFixed(1) : Math.round(b)) + ' ' + units[i]);
  }
  function clock(sec) {
    sec = Math.round(sec);
    var h = Math.floor(sec / 3600), m = Math.floor(sec % 3600 / 60), s = sec % 60;
    return (h ? h + ':' + String(m).padStart(2, '0') : m) + ':' + String(s).padStart(2, '0');
  }
  function span(seconds) { return seconds < 60 ? t('time.sec', { n: Math.round(seconds) }) : t('time.min', { m: Math.floor(seconds / 60), s: Math.round(seconds % 60) }); }
  function badge(state) { return '<span class="badge s-' + state + '">' + esc(t('st.' + state)) + '</span>'; }
  function thumb(job) { var c = (Engine.SOURCES[job.source] || Engine.SOURCES.generic).color; return '<div class="thumb" style="background:' + c + '" aria-hidden="true">' + esc(job.kind === 'audio' ? 'AUDIO' : job.format.toUpperCase().slice(0, 5)) + '</div>'; }

  /* ---------- shell ---------- */
  function applyPrefs() {
    var root = document.documentElement;
    root.dataset.theme = prefs.theme;
    root.lang = prefs.lang;
    root.dir = prefs.lang === 'ar' ? 'rtl' : 'ltr';
    queue.concurrency = prefs.concurrency;
    queue.autoStart = prefs.autoStart;
    document.getElementById('banner').innerHTML = esc(t('banner')) + ' <a href="' + REPO + '/releases" rel="noopener">' + esc(t('banner.get')) + '</a>';
    drawNav();
    document.getElementById('sidefoot').innerHTML =
      '<button class="btn sm" id="theme" type="button">' + icon(prefs.theme === 'dark' ? 'sun' : 'moon') + esc(prefs.theme === 'dark' ? t('theme.light') : t('theme.dark')) + '</button>' +
      '<button class="btn sm" id="lang" type="button" lang="' + (prefs.lang === 'ar' ? 'en' : 'ar') + '">' + esc(t('lang.toggle')) + '</button>';
    document.getElementById('theme').onclick = function () { prefs.theme = prefs.theme === 'dark' ? 'light' : 'dark'; save(); applyPrefs(); render(); };
    document.getElementById('lang').onclick = function () { prefs.lang = prefs.lang === 'ar' ? 'en' : 'ar'; save(); applyPrefs(); render(); };
    document.title = 'Downora - ' + t('nav.' + current());
  }
  function current() { var p = location.hash.replace(/^#\/?/, ''); return PAGES.indexOf(p) >= 0 ? p : 'home'; }
  function drawNav() {
    var c = queue.counts(), busy = c.active + c.waiting + c.paused, cur = current();
    document.getElementById('nav').innerHTML = PAGES.map(function (p) {
      return '<a href="#/' + p + '"' + (p === cur ? ' aria-current="page"' : '') + '>' + icon(p === 'queue' ? 'queue' : p) + '<span>' + esc(t('nav.' + p)) + '</span>' +
        (p === 'queue' && busy ? '<span class="count">' + num(busy) + '</span>' : '') + '</a>';
    }).join('');
  }
  function toast(message) {
    var el = document.getElementById('toast');
    el.textContent = message; el.hidden = false;
    clearTimeout(toast.timer); toast.timer = setTimeout(function () { el.hidden = true; }, 3200);
  }

  /* ---------- views ---------- */
  var EXAMPLES = [
    ['ex.video', 'https://www.youtube.com/watch?v=sample-walkthrough'],
    ['ex.playlist', 'https://www.youtube.com/playlist?list=sample-course'],
    ['ex.audio', 'https://soundcloud.com/sample-artist/midnight-signal'],
    ['ex.flaky', 'https://vimeo.com/flaky-sample'],
    ['ex.private', 'https://www.youtube.com/watch?v=private-sample'],
    ['ex.drm', 'https://example.com/drm-movie']
  ];

  function urlBox(idPrefix) {
    return '<form class="row" id="' + idPrefix + '-form" novalidate>' +
      '<input class="input url" id="' + idPrefix + '-url" type="text" inputmode="url" autocomplete="off" spellcheck="false" aria-label="URL" placeholder="' + esc(t('home.placeholder')) + '" value="' + esc(dl.url) + '">' +
      '<button class="btn primary" type="submit"' + (dl.busy ? ' disabled' : '') + '>' + icon('bolt') + esc(dl.busy ? t('dl.analyzing') : t('home.analyze')) + '</button></form>' +
      '<div class="row" style="margin-top:8px"><span class="muted">' + esc(t('home.try')) + ':</span>' +
      EXAMPLES.map(function (e, i) { return '<button class="btn sm" type="button" data-example="' + i + '">' + esc(t(e[0])) + '</button>'; }).join('') + '</div>';
  }

  function viewHome() {
    var done = queue.jobs.filter(function (j) { return j.state === 'completed'; });
    var c = queue.counts(), total = done.reduce(function (s, j) { return s + j.bytes; }, 0);
    var recent = done.slice().sort(function (a, b) { return b.finishedAt - a.finishedAt; }).slice(0, 4);
    return '<h1>' + esc(t('home.title')) + '</h1><p class="sub">' + esc(t('home.sub')) + '</p><div class="stack">' +
      '<div class="card">' + urlBox('home') + '</div>' +
      '<div class="stats">' +
      stat(t('home.statDone'), num(done.length)) + stat(t('home.statActive'), num(c.active)) + stat(t('home.statWaiting'), num(c.waiting)) + stat(t('home.statSize'), bytes(total)) + '</div>' +
      '<h2>' + esc(t('home.recent')) + '</h2>' +
      (recent.length ? '<div class="list">' + recent.map(function (j) { return '<div class="item">' + thumb(j) + '<div class="grow"><div class="title">' + esc(j.title) + '</div><div class="muted">' + esc(j.uploader) + ' · ' + bytes(j.bytes) + '</div></div>' + badge('completed') + '</div>'; }).join('') + '</div>' : '<div class="card empty">' + esc(t('home.none')) + '</div>') +
      '</div>';
  }
  function stat(label, value) { return '<div class="card stat"><span class="muted">' + esc(label) + '</span><b>' + value + '</b></div>'; }

  function viewDownloader() {
    var r = dl.result, body = '';
    if (dl.error) body = '<div class="msg err" role="alert"><span>' + esc(t(dl.error)) + '</span></div>';
    else if (r && r.kind === 'playlist') body = playlistCard(r);
    else if (r) body = infoCard(r);
    return '<h1>' + esc(t('dl.title')) + '</h1><p class="sub">' + esc(t('dl.sub')) + '</p><div class="stack"><div class="card">' + urlBox('dl') + '</div>' +
      (dl.notice ? '<div class="msg" role="status">' + esc(dl.notice) + '</div>' : '') + body + '</div>';
  }
  function infoCard(r) {
    var src = Engine.SOURCES[r.source].name, hasVideo = r.videoFormats.length > 0;
    var mode = hasVideo ? dl.mode : 'audio';
    var opts = mode === 'video'
      ? r.videoFormats.map(function (f) { return '<option value="' + f.id + '"' + (f.id === dl.quality ? ' selected' : '') + '>' + esc(f.id === 'best' ? t('dl.best') : f.label) + ' (' + esc(t('dl.est', { size: stripTags(bytes(f.bytes)) })) + ')</option>'; }).join('')
      : r.audioFormats.map(function (f) { return '<option value="' + f.id + '"' + (f.id === dl.audio ? ' selected' : '') + '>' + esc(f.label) + ' (' + esc(t('dl.est', { size: stripTags(bytes(f.bytes)) })) + ')</option>'; }).join('');
    return '<div class="card stack"><div><h2>' + esc(r.title) + '</h2><div class="muted">' + esc(t('dl.uploader', { name: r.uploader })) + ' · ' + esc(t('dl.length', { time: clock(r.seconds) })) + ' · ' + esc(t('dl.source', { name: src })) + '</div></div>' +
      '<div class="row">' + (hasVideo ? '<div class="seg" role="group"><button type="button" data-mode="video" aria-pressed="' + (mode === 'video') + '">' + esc(t('dl.video')) + '</button><button type="button" data-mode="audio" aria-pressed="' + (mode === 'audio') + '">' + esc(t('dl.audio')) + '</button></div>' : '') +
      '<label class="row"><span class="muted">' + esc(t(mode === 'video' ? 'dl.quality' : 'dl.format')) + '</span><select id="fmt">' + opts + '</select></label>' +
      '<button class="btn primary" id="go" type="button">' + icon('downloader') + esc(t(mode === 'video' ? 'dl.download' : 'dl.downloadAudio')) + '</button></div></div>';
  }
  function stripTags(html) { return html.replace(/<[^>]*>/g, ''); }
  function playlistCard(r) {
    var chosen = r.entries.filter(function (e) { return dl.picked[e.index]; }).length;
    var mode = dl.mode;
    var fmtOpts = mode === 'video'
      ? ['best', '1080', '720', '480'].map(function (q) { return '<option value="' + q + '"' + (q === dl.quality ? ' selected' : '') + '>' + esc(q === 'best' ? t('dl.best') : q + 'p') + '</option>'; }).join('')
      : ['mp3', 'm4a', 'opus', 'flac', 'wav'].map(function (q) { return '<option value="' + q + '"' + (q === dl.audio ? ' selected' : '') + '>' + q.toUpperCase() + '</option>'; }).join('');
    return '<div class="card stack"><div><h2>' + esc(t('pl.title', { name: r.title })) + '</h2><div class="muted">' + esc(t('pl.items', { n: num(r.entries.length) })) + ' · ' + esc(t('pl.selected', { n: num(chosen) })) + '</div></div>' +
      '<div class="row"><button class="btn sm" type="button" id="pl-all">' + esc(t('pl.all')) + '</button><button class="btn sm" type="button" id="pl-none">' + esc(t('pl.none')) + '</button>' +
      '<div class="seg" role="group"><button type="button" data-mode="video" aria-pressed="' + (mode === 'video') + '">' + esc(t('dl.video')) + '</button><button type="button" data-mode="audio" aria-pressed="' + (mode === 'audio') + '">' + esc(t('dl.audio')) + '</button></div>' +
      '<select id="fmt" aria-label="' + esc(t(mode === 'video' ? 'dl.quality' : 'dl.format')) + '">' + fmtOpts + '</select>' +
      '<button class="btn primary" id="pl-go" type="button"' + (chosen ? '' : ' disabled') + '>' + icon('downloader') + esc(t('pl.download')) + '</button></div>' +
      '<div class="list">' + r.entries.map(function (e) {
        return '<label class="item"><input class="check" type="checkbox" data-pick="' + e.index + '"' + (dl.picked[e.index] ? ' checked' : '') + (e.available ? '' : ' disabled') + '>' +
          '<div class="grow"><div class="title">' + num(e.index) + '. ' + esc(e.title) + '</div><div class="muted">' + (e.available ? esc(clock(e.seconds)) : esc(t('pl.unavailable'))) + '</div></div></label>';
      }).join('') + '</div></div>';
  }

  function jobCard(j) {
    var pct = Math.round(j.progress * 100), active = Engine.ACTIVE[j.state], fin = Engine.FINISHED[j.state];
    var eta = j.state === 'downloading' && j.speed ? t('q.eta', { time: span((1 - j.progress) * j.bytes / j.speed) }) : '';
    var btns = '';
    if (j.state === 'waiting' || active) btns += act('pause', j.id, 'q.pause');
    if (j.state === 'paused') btns += act('play', j.id, 'q.resume', 'resume');
    if (j.state === 'failed' || j.state === 'cancelled') btns += act('retry', j.id, 'q.retry');
    if (!fin) btns += act('x', j.id, 'q.cancel', 'cancel');
    return '<div class="job" data-job="' + j.id + '">' + thumb(j) + '<div class="grow"><div class="row"><span class="title grow">' + esc(j.title) + '</span>' + badge(j.state) + '</div>' +
      '<div class="bar' + (j.state === 'extracting' || j.state === 'processing' ? ' wave' : '') + '" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="' + pct + '"><i data-bar="' + j.id + '" style="width:' + pct + '%"></i></div>' +
      '<div class="row mono muted"><span data-meta="' + j.id + '">' + metaText(j) + '</span><span class="grow"></span><span data-eta="' + j.id + '">' + esc(eta) + '</span></div>' +
      (j.error ? '<div class="msg err" style="margin-top:6px"><span>' + esc(t(j.error)) + '</span></div>' : '') + '</div>' +
      '<div class="row" style="flex:none">' + btns + '</div></div>';
  }
  function act(ic, id, label, name) { return '<button class="btn icon" type="button" data-act="' + (name || ic) + '" data-id="' + id + '" title="' + esc(t(label)) + '" aria-label="' + esc(t(label)) + '">' + icon(ic) + '</button>'; }
  function metaText(j) { return '<bdi class="ltr">' + stripTags(bytes(j.progress * j.bytes)) + ' / ' + stripTags(bytes(j.bytes)) + (j.state === 'downloading' && j.speed ? ' · ' + (j.speed / 1e6).toFixed(1) + ' MB/s' : '') + '</bdi>'; }

  function viewQueue() {
    var jobs = queue.jobs.slice().sort(function (a, b) { return a.id - b.id; });
    var any = jobs.some(function (j) { return Engine.FINISHED[j.state]; });
    return '<h1>' + esc(t('q.title')) + '</h1><p class="sub">' + esc(t('q.sub', { n: num(prefs.concurrency) })) + '</p>' +
      '<div class="row" style="margin-bottom:12px"><button class="btn" id="pause-all" type="button">' + icon('pause') + esc(t('q.pauseAll')) + '</button><button class="btn" id="resume-all" type="button">' + icon('play') + esc(t('q.resumeAll')) + '</button>' +
      '<button class="btn" id="clear-fin" type="button"' + (any ? '' : ' disabled') + '>' + esc(t('q.clear')) + '</button></div>' +
      (jobs.length ? '<div class="list">' + jobs.map(jobCard).join('') + '</div>' : '<div class="card empty">' + esc(t('q.empty')) + '</div>');
  }

  function typeLabel(j) { return t(j.kind === 'audio' ? 'dl.audio' : 'dl.video'); }
  function viewLibrary() {
    var q = lib.search.trim().toLowerCase();
    var rows = queue.jobs.filter(function (j) { return j.state === 'completed' && !j.libRemoved && (lib.kind === 'all' || j.kind === lib.kind) && (!q || (j.title + ' ' + j.uploader).toLowerCase().indexOf(q) >= 0); });
    return '<h1>' + esc(t('lib.title')) + '</h1><p class="sub">' + esc(t('lib.sub')) + '</p>' +
      '<div class="row" style="margin-bottom:12px"><input class="input" id="lib-search" type="search" aria-label="' + esc(t('lib.search')) + '" placeholder="' + esc(t('lib.search')) + '" value="' + esc(lib.search) + '" style="width:280px;max-width:100%">' +
      '<div class="seg" role="group">' + [['all', 'lib.all'], ['video', 'lib.videoOnly'], ['audio', 'lib.audioOnly']].map(function (k) { return '<button type="button" data-kind="' + k[0] + '" aria-pressed="' + (lib.kind === k[0]) + '">' + esc(t(k[1])) + '</button>'; }).join('') + '</div></div>' +
      (rows.length ? '<div class="card" style="padding:0;overflow-x:auto"><table><thead><tr><th>' + esc(t('lib.col.title')) + '</th><th>' + esc(t('lib.col.type')) + '</th><th>' + esc(t('lib.col.size')) + '</th><th>' + esc(t('lib.col.format')) + '</th><th></th></tr></thead><tbody>' +
        rows.map(function (j) { return '<tr><td><div class="title" style="max-width:360px">' + esc(j.title) + '</div><div class="muted">' + esc(j.uploader) + '</div></td><td>' + esc(typeLabel(j)) + '</td><td>' + bytes(j.bytes) + '</td><td>' + esc(j.format.toUpperCase()) + '</td><td><button class="btn sm" type="button" data-lib-remove="' + j.id + '">' + icon('trash') + esc(t('lib.remove')) + '</button></td></tr>'; }).join('') +
        '</tbody></table></div>' : '<div class="card empty">' + esc(t('lib.empty')) + '</div>') +
      '<p class="muted">' + esc(t('lib.note')) + '</p>';
  }

  function viewHistory() {
    var rows = queue.jobs.filter(function (j) { return Engine.FINISHED[j.state] && !j.histRemoved && (hist.status === 'all' || j.state === hist.status); }).sort(function (a, b) { return b.finishedAt - a.finishedAt; });
    return '<h1>' + esc(t('hist.title')) + '</h1><p class="sub">' + esc(t('hist.sub')) + '</p>' +
      '<div class="row" style="margin-bottom:12px"><div class="seg" role="group">' + ['all', 'completed', 'failed', 'cancelled'].map(function (s) { return '<button type="button" data-status="' + s + '" aria-pressed="' + (hist.status === s) + '">' + esc(s === 'all' ? t('lib.all') : t('st.' + s)) + '</button>'; }).join('') + '</div>' +
      '<button class="btn" id="hist-clear" type="button"' + (rows.length ? '' : ' disabled') + '>' + esc(t('hist.clear')) + '</button></div>' +
      (rows.length ? '<div class="list">' + rows.map(function (j) { return '<div class="item">' + thumb(j) + '<div class="grow"><div class="title">' + esc(j.title) + '</div><div class="muted">' + esc(j.uploader) + ' · ' + bytes(j.bytes) + (j.error ? ' · ' + esc(t(j.error)) : '') + '</div></div>' + badge(j.state) +
        (j.state === 'failed' || j.state === 'cancelled' ? '<button class="btn sm" type="button" data-act="retry" data-id="' + j.id + '">' + icon('retry') + esc(t('q.retry')) + '</button>' : '') +
        '<button class="btn sm" type="button" data-hist-remove="' + j.id + '">' + esc(t('hist.remove')) + '</button></div>'; }).join('') + '</div>' : '<div class="card empty">' + esc(t('hist.empty')) + '</div>');
  }

  function viewSettings() {
    return '<h1>' + esc(t('set.title')) + '</h1><p class="sub">' + esc(t('set.sub')) + '</p><div class="stack">' +
      '<div class="card"><h2>' + esc(t('set.appearance')) + '</h2>' +
      setting(t('set.theme'), '<select id="set-theme"><option value="dark"' + (prefs.theme === 'dark' ? ' selected' : '') + '>' + esc(t('theme.dark')) + '</option><option value="light"' + (prefs.theme === 'light' ? ' selected' : '') + '>' + esc(t('theme.light')) + '</option></select>') +
      setting(t('set.language'), '<select id="set-lang"><option value="en"' + (prefs.lang === 'en' ? ' selected' : '') + '>English</option><option value="ar"' + (prefs.lang === 'ar' ? ' selected' : '') + '>العربية</option></select>') + '</div>' +
      '<div class="card"><h2>' + esc(t('set.downloads')) + '</h2>' +
      setting(t('set.concurrency'), '<input class="input" id="set-conc" type="number" min="1" max="10" value="' + prefs.concurrency + '" style="width:80px">') +
      setting(t('set.autostart'), '<input class="check" id="set-auto" type="checkbox"' + (prefs.autoStart ? ' checked' : '') + '>', t('set.autostart.hint')) + '</div>' +
      '<div class="card"><div class="setting"><div><b>' + esc(t('set.reset')) + '</b><div class="muted">' + esc(t('set.reset.hint')) + '</div></div><button class="btn" id="reset" type="button">' + icon('trash') + esc(t('set.reset')) + '</button></div></div></div>';
  }
  function setting(label, control, hint) { return '<label class="setting"><span><b>' + esc(label) + '</b>' + (hint ? '<div class="muted">' + esc(hint) + '</div>' : '') + '</span>' + control + '</label>'; }

  function viewAbout() {
    return '<h1>' + esc(t('about.title')) + '</h1><p class="sub">' + esc(t('about.sub')) + '</p><div class="stack"><div class="card stack">' +
      '<h2>' + esc(t('about.real')) + '</h2><p>' + esc(t('about.real1')) + '</p><p>' + esc(t('about.real2')) + '</p>' +
      '<h2>' + esc(t('about.fake')) + '</h2><p>' + esc(t('about.fake1')) + '</p><p>' + esc(t('about.fake2')) + '</p>' +
      '<div class="msg">' + esc(t('about.rules')) + '</div></div>' +
      '<div class="card stack"><h2>' + esc(t('about.links')) + '</h2><div class="row">' +
      '<a class="btn primary" style="text-decoration:none" href="' + REPO + '/releases" rel="noopener">' + esc(t('about.releases')) + '</a>' +
      '<a class="btn" style="text-decoration:none" href="' + REPO + '" rel="noopener">' + esc(t('about.source')) + '</a>' +
      '<a class="btn" style="text-decoration:none" href="' + REPO + '/blob/main/docs/user-guide.md" rel="noopener">' + esc(t('about.guide')) + '</a></div></div></div>';
  }

  var VIEWS = { home: viewHome, downloader: viewDownloader, queue: viewQueue, library: viewLibrary, history: viewHistory, settings: viewSettings, about: viewAbout };

  function render() {
    view.innerHTML = VIEWS[current()]();
    document.title = 'Downora - ' + t('nav.' + current());
    drawNav();
    lastSig = signature();
  }

  /* ---------- behavior ---------- */
  function signature() { return queue.jobs.map(function (j) { return j.id + ':' + j.state + (j.libRemoved ? 'L' : '') + (j.histRemoved ? 'H' : ''); }).join(','); }

  function onQueueChange() {
    var sig = signature();
    if (sig !== lastSig) {
      lastSig = sig; drawNav();
      var p = current();
      if (p === 'queue' || p === 'home' || p === 'library' || p === 'history') { var keep = document.activeElement && document.activeElement.id; render(); if (keep) { var el = document.getElementById(keep); if (el) { el.focus(); if (el.setSelectionRange && el.type === 'search') el.setSelectionRange(el.value.length, el.value.length); } } }
    } else {
      queue.jobs.forEach(function (j) {
        var bar = view.querySelector('[data-bar="' + j.id + '"]');
        if (!bar) return;
        bar.style.width = Math.round(j.progress * 100) + '%';
        bar.parentNode.setAttribute('aria-valuenow', Math.round(j.progress * 100));
        var meta = view.querySelector('[data-meta="' + j.id + '"]'), eta = view.querySelector('[data-eta="' + j.id + '"]');
        if (meta) meta.innerHTML = metaText(j);
        if (eta) eta.textContent = j.state === 'downloading' && j.speed ? t('q.eta', { time: span((1 - j.progress) * j.bytes / j.speed) }) : '';
      });
    }
    save();
  }

  function analyzeNow(text) {
    dl.url = text; dl.notice = null; dl.result = null; dl.error = null; dl.picked = {};
    var res = Engine.analyze(text);
    if (!res.ok) dl.error = res.error;
    else {
      dl.result = res.info;
      dl.mode = res.info.kind === 'audio' ? 'audio' : 'video';
      if (res.info.kind === 'playlist') res.info.entries.forEach(function (e) { if (e.available) dl.picked[e.index] = true; });
    }
  }
  function goAnalyze(text) {
    dl.busy = true; dl.url = text;
    if (current() !== 'downloader') location.hash = '#/downloader'; else render();
    setTimeout(function () { analyzeNow(text); dl.busy = false; render(); }, 450);   // a short pause so the "Analyzing" state is visible
  }
  function queueSpec(info, kind, format, bytesSize, extra) {
    var o = { url: info.url, title: info.title, uploader: info.uploader, source: info.source, kind: kind, format: format, bytes: bytesSize, flaky: info.flaky };
    if (extra) Object.keys(extra).forEach(function (k) { o[k] = extra[k]; });
    return o;
  }

  document.addEventListener('submit', function (e) {
    if (!/-form$/.test(e.target.id)) return;
    e.preventDefault();
    goAnalyze(e.target.querySelector('input').value);
  });
  document.addEventListener('input', function (e) {
    var id = e.target.id;
    if (id === 'home-url' || id === 'dl-url') dl.url = e.target.value;
    else if (id === 'lib-search') { lib.search = e.target.value; var pos = e.target.selectionStart; render(); var el = document.getElementById('lib-search'); el.focus(); el.setSelectionRange(pos, pos); }
  });
  document.addEventListener('change', function (e) {
    var el = e.target, id = el.id;
    if (id === 'fmt') { dl[dl.mode === 'video' ? 'quality' : 'audio'] = el.value; }
    else if (el.dataset.pick) { dl.picked[el.dataset.pick] = el.checked; render(); }
    else if (id === 'set-theme') { prefs.theme = el.value; save(); applyPrefs(); render(); }
    else if (id === 'set-lang') { prefs.lang = el.value; save(); applyPrefs(); render(); }
    else if (id === 'set-conc') { var n = Math.max(1, Math.min(10, parseInt(el.value, 10) || 3)); prefs.concurrency = n; el.value = n; queue.concurrency = n; save(); }
    else if (id === 'set-auto') { prefs.autoStart = el.checked; queue.autoStart = el.checked; save(); }
  });
  document.addEventListener('click', function (e) {
    var b = e.target.closest('button'); if (!b) return;
    var d = b.dataset, id = b.id, info = dl.result, n;
    if (d.example !== undefined) { goAnalyze(EXAMPLES[+d.example][1]); return; }
    if (d.mode) { dl.mode = d.mode; render(); return; }
    if (id === 'go' && info) {
      var audio = dl.mode === 'audio' || !info.videoFormats.length;
      var f = (audio ? info.audioFormats : info.videoFormats).filter(function (x) { return x.id === (audio ? dl.audio : dl.quality); })[0] || (audio ? info.audioFormats : info.videoFormats)[0];
      var job = queue.add(queueSpec(info, audio ? 'audio' : 'video', audio ? f.id : f.label, f.bytes));
      dl.notice = t(job ? 'dl.added' : 'dl.dup'); render(); return;
    }
    if (id === 'pl-all' || id === 'pl-none') { info.entries.forEach(function (x) { if (x.available) dl.picked[x.index] = id === 'pl-all'; }); render(); return; }
    if (id === 'pl-go' && info) {
      var audioList = dl.mode === 'audio', fmt = audioList ? dl.audio : dl.quality, count = 0, batch = Date.now();
      info.entries.filter(function (x) { return dl.picked[x.index] && x.available; }).forEach(function (x) {
        var size = Math.round(x.seconds * (audioList ? 40000 : 300000));
        if (queue.add({ url: info.url + '#' + x.index, title: x.title, uploader: info.uploader, source: info.source, kind: audioList ? 'audio' : 'video', format: audioList ? fmt : (fmt === 'best' ? '1080p' : fmt + 'p'), bytes: size, batch: batch })) count++;
      });
      dl.notice = t('pl.added', { n: num(count) }); render(); return;
    }
    if (d.act) { var j = +d.id; ({ pause: queue.pause, resume: queue.resume, play: queue.resume, cancel: queue.cancel, x: queue.cancel, retry: queue.retry })[d.act].call(queue, j); if (current() === 'history') render(); return; }
    if (id === 'pause-all') { queue.pauseAll(); render(); return; }
    if (id === 'resume-all') { queue.resumeAll(); render(); return; }
    if (id === 'clear-fin') { queue.jobs.filter(function (j) { return Engine.FINISHED[j.state]; }).forEach(function (j) { queue.remove(j.id); }); render(); return; }
    if (d.kind) { lib.kind = d.kind; render(); return; }
    if (d.libRemove) { n = queue.get(+d.libRemove); n.libRemoved = true; save(); render(); return; }
    if (d.status) { hist.status = d.status; render(); return; }
    if (d.histRemove) { queue.get(+d.histRemove).histRemoved = true; save(); render(); return; }
    if (id === 'hist-clear') { queue.jobs.forEach(function (j) { if (Engine.FINISHED[j.state]) j.histRemoved = true; }); save(); render(); return; }
    if (id === 'reset') { queue.jobs = []; queue.nextId = 1; save(); toast(t('set.reset')); render(); drawNav(); }
  });

  window.addEventListener('hashchange', function () { dl.notice = null; applyPrefs(); render(); window.scrollTo(0, 0); });
  var last = Date.now();
  setInterval(function () { var now = Date.now(), ms = Math.min(now - last, 1000); last = now; queue.tick(ms); }, 250);

  applyPrefs();
  render();
  window.__demo = { queue: queue };   // for the browser check in tools/ and tests
})();
