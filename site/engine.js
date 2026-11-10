/* Simulated Downora engine for the web demo. Pure logic, no DOM and no network: nothing here downloads anything.
   It mirrors the real app's rules (URL validation, credentials refused, queue order, concurrency, pause/resume/cancel/retry). */
(function (root) {
  'use strict';

  var SOURCES = {
    youtube: { name: 'YouTube', hosts: ['youtube.com', 'youtu.be', 'music.youtube.com'], color: '#e11d48' },
    soundcloud: { name: 'SoundCloud', hosts: ['soundcloud.com'], color: '#f97316' },
    vimeo: { name: 'Vimeo', hosts: ['vimeo.com'], color: '#0ea5e9' },
    generic: { name: 'Direct link', hosts: [], color: '#64748b' }
  };

  // Fictional sample titles: the demo never looks anything up.
  var VIDEOS = [
    { title: 'Building a native Windows app: a walkthrough', uploader: 'Sample Channel', seconds: 1254 },
    { title: 'Ten minutes of calm ocean waves (sample)', uploader: 'Sample Nature', seconds: 612 },
    { title: 'Conference keynote: the future of tooling (sample)', uploader: 'Sample Conf', seconds: 2710 }
  ];
  var TRACKS = [
    { title: 'Midnight Signal (sample track)', uploader: 'Sample Artist', seconds: 214 },
    { title: 'Slow Orbit (sample track)', uploader: 'Sample Artist', seconds: 187 }
  ];
  var PLAYLIST_TITLES = ['Intro and setup', 'Working with the queue', 'Private video (not available)', 'Audio formats explained', 'Library tips', 'Wrap-up'];

  function hash(text) {
    var h = 0;
    for (var i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
    return h;
  }

  function detectSource(host) {
    host = host.replace(/^www\./, '').toLowerCase();
    for (var key in SOURCES) {
      var hosts = SOURCES[key].hosts;
      for (var i = 0; i < hosts.length; i++) if (host === hosts[i] || host.endsWith('.' + hosts[i])) return key;
    }
    return 'generic';
  }

  function validateUrl(text) {
    var value = (text || '').trim();
    if (!value) return { ok: false, error: 'err.empty' };
    var url;
    try { url = new URL(value); } catch (e) { return { ok: false, error: 'err.invalid' }; }
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return { ok: false, error: 'err.invalid' };
    if (url.username || url.password) return { ok: false, error: 'err.credentials' };
    return { ok: true, url: url, source: detectSource(url.hostname) };
  }

  function videoFormats(seconds) {
    return [
      { id: 'best', height: 1080, label: 'best', bytes: Math.round(seconds * 520000) },
      { id: '1080', height: 1080, label: '1080p', bytes: Math.round(seconds * 520000) },
      { id: '720', height: 720, label: '720p', bytes: Math.round(seconds * 300000) },
      { id: '480', height: 480, label: '480p', bytes: Math.round(seconds * 150000) },
      { id: '360', height: 360, label: '360p', bytes: Math.round(seconds * 90000) }
    ];
  }

  function audioFormats(seconds) {
    return [
      { id: 'mp3', label: 'MP3', bytes: Math.round(seconds * 40000) },
      { id: 'm4a', label: 'M4A', bytes: Math.round(seconds * 32000) },
      { id: 'opus', label: 'Opus', bytes: Math.round(seconds * 24000) },
      { id: 'flac', label: 'FLAC', bytes: Math.round(seconds * 120000) },
      { id: 'wav', label: 'WAV', bytes: Math.round(seconds * 176400) }
    ];
  }

  // Returns { ok:true, info } or { ok:false, error }. The words private/drm/region/gone in the address trigger the real error messages.
  function analyze(text) {
    var v = validateUrl(text);
    if (!v.ok) return v;
    var lower = v.url.href.toLowerCase();
    if (lower.indexOf('private') >= 0) return { ok: false, error: 'err.signin' };
    if (lower.indexOf('drm') >= 0) return { ok: false, error: 'err.drm' };
    if (lower.indexOf('region') >= 0) return { ok: false, error: 'err.region' };
    if (lower.indexOf('gone') >= 0) return { ok: false, error: 'err.unavailable' };

    var h = hash(v.url.href);
    var source = v.source;
    var isList = /[?&]list=|\/playlist|\/sets\//.test(v.url.href);
    if (isList) {
      var entries = PLAYLIST_TITLES.map(function (title, i) {
        return { index: i + 1, title: title, seconds: 240 + ((h >> i) % 400), available: title.indexOf('not available') < 0 };
      });
      return { ok: true, info: { kind: 'playlist', source: source, url: v.url.href, title: 'Sample playlist', uploader: 'Sample Channel', entries: entries } };
    }
    var pool = source === 'soundcloud' ? TRACKS : VIDEOS;
    var pick = pool[h % pool.length];
    return {
      ok: true,
      info: {
        kind: source === 'soundcloud' ? 'audio' : 'video', source: source, url: v.url.href,
        title: pick.title, uploader: pick.uploader, seconds: pick.seconds,
        videoFormats: source === 'soundcloud' ? [] : videoFormats(pick.seconds),
        audioFormats: audioFormats(pick.seconds),
        flaky: lower.indexOf('flaky') >= 0
      }
    };
  }

  var ACTIVE = { extracting: 1, downloading: 1, processing: 1 };
  var FINISHED = { completed: 1, failed: 1, cancelled: 1 };

  /* The queue. Call tick(ms) from a timer. Jobs run in the order they were added, at most `concurrency` at once. */
  function Queue(options) {
    options = options || {};
    this.concurrency = options.concurrency || 3;
    this.autoStart = options.autoStart !== false;
    this.onChange = options.onChange || function () {};
    this.jobs = [];
    this.nextId = 1;
  }

  Queue.prototype.add = function (spec) {
    if (this.jobs.some(function (j) { return j.url === spec.url && j.format === spec.format && !FINISHED[j.state]; })) return null;
    var job = {
      id: this.nextId++, url: spec.url, title: spec.title, uploader: spec.uploader, source: spec.source,
      kind: spec.kind, format: spec.format, bytes: spec.bytes || 0, progress: 0, speed: 0,
      state: this.autoStart ? 'waiting' : 'paused', stageMs: 0, flaky: !!spec.flaky, error: null, finishedAt: null, batch: spec.batch || null
    };
    this.jobs.push(job);
    this._changed();
    return job;
  };

  Queue.prototype.get = function (id) { return this.jobs.filter(function (j) { return j.id === id; })[0]; };

  Queue.prototype.pause = function (id) {
    var j = this.get(id);
    if (j && (j.state === 'waiting' || ACTIVE[j.state])) { j.state = 'paused'; j.speed = 0; this._changed(); }
  };
  Queue.prototype.resume = function (id) {
    var j = this.get(id);
    if (j && j.state === 'paused') { j.state = 'waiting'; this._changed(); }
  };
  Queue.prototype.cancel = function (id) {
    var j = this.get(id);
    if (j && !FINISHED[j.state]) { j.state = 'cancelled'; j.speed = 0; j.progress = 0; j.finishedAt = Date.now(); this._changed(); }
  };
  Queue.prototype.retry = function (id) {
    var j = this.get(id);
    if (!j) return;
    if (j.state === 'failed') { j.state = 'waiting'; j.error = null; this._changed(); }          // continues where it stopped
    else if (j.state === 'cancelled') { j.state = 'waiting'; j.progress = 0; j.finishedAt = null; this._changed(); } // starts over
  };
  Queue.prototype.pauseAll = function () { var self = this; this.jobs.forEach(function (j) { self.pause(j.id); }); };
  Queue.prototype.resumeAll = function () { var self = this; this.jobs.forEach(function (j) { self.resume(j.id); }); };
  Queue.prototype.remove = function (id) {
    var j = this.get(id);
    if (j && FINISHED[j.state]) { this.jobs.splice(this.jobs.indexOf(j), 1); this._changed(); }
  };

  Queue.prototype.counts = function () {
    var c = { active: 0, waiting: 0, paused: 0 };
    this.jobs.forEach(function (j) {
      if (ACTIVE[j.state]) c.active++; else if (j.state === 'waiting') c.waiting++; else if (j.state === 'paused') c.paused++;
    });
    return c;
  };

  Queue.prototype.tick = function (ms) {
    var changed = false, running = 0, self = this;
    this.jobs.forEach(function (j) { if (ACTIVE[j.state]) running++; });
    this.jobs.forEach(function (j) {          // start waiting jobs in the order they were added
      if (j.state === 'waiting' && running < self.concurrency) { j.state = 'extracting'; j.stageMs = 0; running++; changed = true; }
    });
    this.jobs.forEach(function (j) {
      if (!ACTIVE[j.state]) return;
      changed = true;
      j.stageMs += ms;
      if (j.state === 'extracting') { if (j.stageMs >= 600) { j.state = 'downloading'; j.stageMs = 0; } return; }
      if (j.state === 'downloading') {
        var rate = 6e6 + (j.id % 5) * 2.5e6;               // bytes per second, steady so the numbers are readable
        j.speed = rate;
        j.progress = Math.min(1, j.progress + (rate * ms / 1000) / Math.max(j.bytes, 1));
        if (j.flaky && j.progress >= 0.55) { j.flaky = false; j.state = 'failed'; j.speed = 0; j.error = 'err.network'; j.finishedAt = Date.now(); return; }
        if (j.progress >= 1) { j.progress = 1; j.state = 'processing'; j.stageMs = 0; j.speed = 0; }
        return;
      }
      if (j.state === 'processing' && j.stageMs >= 900) { j.state = 'completed'; j.finishedAt = Date.now(); }
    });
    if (changed) this._changed();
  };

  Queue.prototype._changed = function () { this.onChange(this); };

  var api = { SOURCES: SOURCES, validateUrl: validateUrl, analyze: analyze, detectSource: detectSource, Queue: Queue, ACTIVE: ACTIVE, FINISHED: FINISHED };
  if (typeof module !== 'undefined' && module.exports) module.exports = api; else root.Engine = api;
})(typeof window !== 'undefined' ? window : globalThis);
