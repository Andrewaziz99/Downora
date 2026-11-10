// Run with: node --test site/tests
const test = require('node:test');
const assert = require('node:assert/strict');
const Engine = require('../engine.js');
const I18N = require('../i18n.js');

const spec = (n, extra) => Object.assign({ url: 'https://example.com/v' + n, title: 'T' + n, uploader: 'U', source: 'generic', kind: 'video', format: '720p', bytes: 20e6 }, extra);
const run = (q, seconds) => { for (let i = 0; i < seconds * 4; i++) q.tick(250); };

test('rejects empty, malformed and non-web links, and links with credentials', () => {
  assert.equal(Engine.validateUrl('').error, 'err.empty');
  assert.equal(Engine.validateUrl('not a link').error, 'err.invalid');
  assert.equal(Engine.validateUrl('ftp://example.com/a').error, 'err.invalid');
  assert.equal(Engine.validateUrl('https://user:pw@example.com/a').error, 'err.credentials');
  assert.equal(Engine.validateUrl('  https://youtu.be/abc ').source, 'youtube');
  assert.equal(Engine.validateUrl('https://www.vimeo.com/1').source, 'vimeo');
  assert.equal(Engine.validateUrl('https://evilyoutube.com/1').source, 'generic');
});

test('analysis is deterministic and reports the real error kinds', () => {
  const a = Engine.analyze('https://www.youtube.com/watch?v=x'), b = Engine.analyze('https://www.youtube.com/watch?v=x');
  assert.deepEqual(a, b);
  assert.equal(a.info.kind, 'video');
  assert.ok(a.info.videoFormats.length > 0 && a.info.audioFormats.length === 5);
  assert.equal(Engine.analyze('https://example.com/private').error, 'err.signin');
  assert.equal(Engine.analyze('https://example.com/drm').error, 'err.drm');
  assert.equal(Engine.analyze('https://example.com/region').error, 'err.region');
  assert.equal(Engine.analyze('https://example.com/gone').error, 'err.unavailable');
  assert.equal(Engine.analyze('https://soundcloud.com/a/b').info.kind, 'audio');
});

test('playlists list every item and mark private ones unavailable', () => {
  const r = Engine.analyze('https://www.youtube.com/playlist?list=abc');
  assert.equal(r.info.kind, 'playlist');
  assert.equal(r.info.entries.length, 6);
  assert.equal(r.info.entries.filter(e => !e.available).length, 1);
});

test('at most `concurrency` jobs run and the rest start in the order added', () => {
  const q = new Engine.Queue({ concurrency: 2 });
  for (let i = 1; i <= 4; i++) q.add(spec(i));
  q.tick(250);
  assert.deepEqual(q.jobs.map(j => !!Engine.ACTIVE[j.state]), [true, true, false, false]);
  run(q, 60);
  assert.ok(q.jobs.every(j => j.state === 'completed'));
});

test('pause keeps progress and resume continues; cancel resets', () => {
  const q = new Engine.Queue({ concurrency: 1 });
  const j = q.add(spec(1, { bytes: 200e6 }));
  run(q, 6);
  q.pause(j.id);
  const kept = j.progress;
  assert.ok(kept > 0 && j.state === 'paused');
  run(q, 3);
  assert.equal(j.progress, kept);
  q.resume(j.id); run(q, 2);
  assert.ok(j.progress > kept);
  q.cancel(j.id);
  assert.equal(j.state, 'cancelled'); assert.equal(j.progress, 0);
});

test('a failed job continues where it stopped; a cancelled one starts over', () => {
  const q = new Engine.Queue();
  const j = q.add(spec(1, { bytes: 60e6, flaky: true }));
  run(q, 20);
  assert.equal(j.state, 'failed'); assert.equal(j.error, 'err.network');
  const at = j.progress;
  assert.ok(at >= 0.55);
  q.retry(j.id); assert.equal(j.progress, at);
  run(q, 20); assert.equal(j.state, 'completed');
  const k = q.add(spec(2)); q.cancel(k.id); q.retry(k.id);
  assert.equal(k.state, 'waiting'); assert.equal(k.progress, 0);
});

test('turning auto-start off holds new downloads, and duplicates are skipped', () => {
  const q = new Engine.Queue({ autoStart: false });
  const j = q.add(spec(1));
  assert.equal(j.state, 'paused');
  assert.equal(q.add(spec(1)), null);
  q.resumeAll(); q.tick(250);
  assert.ok(Engine.ACTIVE[j.state]);
});

test('English and Arabic define exactly the same keys', () => {
  assert.deepEqual(Object.keys(I18N.ar).sort(), Object.keys(I18N.en).sort());
  for (const k of Object.keys(I18N.en)) assert.ok(I18N.ar[k].trim(), k);
});
