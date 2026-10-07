'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {browser} = require('./bridge.test.cjs');
const source = fs.readFileSync(path.resolve(__dirname, '..', '..', '..', '..',
  'WebGLTemplates', 'BusaraOnline', 'busara-latency.js'), 'utf8');
const settle = () => new Promise(resolve => setImmediate(resolve));
function clock(window = {}) {
  let time = 0, next = 0;
  const callbacks = new Map(), logs = [];
  Object.assign(window, {
    performance: {now: () => time},
    requestAnimationFrame: callback => { callbacks.set(++next, callback); return next; },
    cancelAnimationFrame: id => callbacks.delete(id),
    console: {info: value => logs.push(value), table: value => logs.push(value)}
  });
  vm.runInNewContext(source, {window});
  return {api: window.busaraLatency, hook: window.__busaraLatency,
    observe: (stage, id = 0, value = '') => window.__busaraLatency.observe(stage, id, value),
    at: value => { time = value; },
    frame: () => { const batch = [...callbacks.values()]; callbacks.clear(); batch.forEach(callback => callback()); },
    callbacks, logs};
}
async function main() {
  const c = clock(), {api, observe} = c;
  assert.equal(observe('begin', 0, 'submit'), 0, 'Opt-in is required');
  api.enable();
  const sample = observe('begin', 0, 'submit');
  c.at(2); observe('dispatch', sample);
  c.at(12); observe('reply', sample);
  c.at(13); observe('accepted', sample, '9007199254740993');
  c.at(14); observe('view', 0, '9007199254740992');
  assert.equal(api.report().samples[0].start_to_authorized_view_applied_ms, null, 'Stale view is not correlated');
  c.at(15); observe('view', 0, '9007199254740993');
  const ticket = observe('prepareFrame', 0, '9007199254740993');
  c.at(16); observe('frame', ticket);
  assert.equal(api.report().samples[0].start_to_frame_boundary_ms, null);
  c.at(20); c.frame();
  let report = api.report(), row = report.samples[0];
  assert.equal(row.request_dispatch_to_provider_reply_ms, 10);
  assert.equal(row.start_to_frame_boundary_ms, 20);
  assert.equal(row.verified_receipt_to_authorized_view_applied_ms, 2);
  assert.equal(report.summary.start_to_frame_boundary_ms.p95, 20);
  assert.equal(observe('prepareFrame', 0, '9007199254740993'), 0, 'Repeated rebuilds do not add frames');

  api.clear(); c.at(100);
  const early = observe('begin', 0, 'retry');
  c.at(101); observe('dispatch', early);
  c.at(105); observe('view', 0, '12');
  observe('frame', observe('prepareFrame', 0, '12'));
  c.at(108); c.frame();
  c.at(110); observe('reply', early); observe('accepted', early, '11');
  row = api.report().samples[0];
  assert.equal(row.view_before_receipt, true, 'A concurrent poll may apply before ACK');
  assert.equal(row.start_to_frame_boundary_ms, 8);
  assert.equal(row.verified_receipt_to_authorized_view_applied_ms, null, 'No misleading negative latency');
  assert.equal(row.origin, 'retry');

  api.clear(); c.at(120);
  const advanced = observe('begin', 0, 'submit');
  observe('accepted', advanced, '20'); observe('view', 0, '20');
  c.at(122); observe('view', 0, '21'); observe('frame', observe('prepareFrame', 0, '21'));
  c.at(125); c.frame();
  assert.equal(api.report().samples[0].start_to_frame_boundary_ms, 5, 'Superseding rendered view qualifies');
  api.clear();
  const rejected = observe('begin', 0, 'submit');
  observe('view', 0, '30'); observe('rejected', rejected, '30');
  assert.equal(api.report().samples[0].start_to_frame_boundary_ms, null);
  assert.equal(api.report().samples[0].outcome, 'rejected');
  const malformed = observe('begin', 0, 'submit');
  observe('failed', malformed, 'unverified_receipt'); observe('accepted', malformed, '40');
  assert.equal(api.report().samples[1].outcome, 'unverified_receipt');
  const recovery = observe('begin', 0, 'recovery');
  observe('failed', recovery, 'aborted'); observe('failed', recovery, 'delivery_uncertain');
  assert.equal(api.report().samples[2].outcome, 'aborted');
  assert.equal(api.report().samples[2].origin, 'recovery');

  api.clear();
  const old = observe('begin', 0, 'submit');
  observe('view', 0, '50'); const oldTicket = observe('prepareFrame', 0, '50');
  api.clear(); const fresh = observe('begin', 0, 'submit');
  observe('frame', oldTicket); observe('reply', old); observe('accepted', old, '50');
  assert(fresh > old, 'Clear cannot reuse sample identities');
  assert.equal(api.report().samples[0].outcome, 'pending');
  observe('view', 0, '50'); observe('frame', observe('prepareFrame', 0, '50'));
  assert.equal(c.callbacks.size, 1);
  api.disable(); api.enable(); c.frame();
  assert.equal(api.report().retained, 0);
  assert.equal(c.callbacks.size, 0, 'Disable cancels queued browser callbacks');
  for (let i = 0; i < 130; i++) observe('begin', 0, 'retry');
  report = api.report();
  assert.equal(report.retained, 100); assert.equal(report.evicted, 30);
  assert.equal(observe('begin', 0, 'private-choice'), 0);
  assert(!JSON.stringify(report).includes('9007199254740993'), 'Versions remain internal');
  assert(!JSON.stringify(report).includes('private-choice'));
  api.clear();
  for (const duration of [10, 20, 30, 40]) {
    c.at(200); const id = observe('begin', 0, 'submit'); observe('dispatch', id);
    c.at(200 + duration); observe('reply', id);
  }
  const summary = api.report().summary.request_dispatch_to_provider_reply_ms;
  assert.equal(summary.count, 4); assert.equal(summary.mean, 25);
  assert.equal(summary.p50, 20); assert.equal(summary.p95, 40);

  for (const backend of ['legacy', 'ugs']) {
    const b = await browser('https://localhost:7443/', new Map(), {backend});
    const d = clock(b.context.window); d.api.enable();
    const id = d.observe('begin', 0, 'submit');
    const body = '{"commandId":"private-command","paymentIds":["private-choice"]}';
    b.library.Busara_Request('private-request', 'POST', '/api/rooms/private-room/commands', body, 'private-csrf', id);
    b.library.Busara_Request('overlapping-get', 'GET', '/api/rooms/private-room', '', '', 0);
    await settle();
    assert.equal(b.requests.length, 2);
    assert.equal(d.api.report().retained, 1, 'GET never becomes a command sample');
    assert.notEqual(d.api.report().samples[0].request_dispatch_to_provider_reply_ms, null);
    assert(!JSON.stringify(d.api.report()).includes('private-'));
    const before = b.messages.length;
    b.context.window.__busaraLatency = {observe() { throw new Error('observer failure'); }};
    b.library.Busara_Request('observer-failure', 'POST', '/api/rooms/private-room/commands', body, '', id);
    await settle();
    assert.equal(b.messages.length, before + 1, 'Broken diagnostics cannot lose a gameplay response');
    assert.equal(b.messages.at(-1).status, 200);
    b.library.Busara_Dispose();
  }
  const lost = await browser('https://localhost:7443/');
  const l = clock(lost.context.window); l.api.enable();
  const lostId = l.observe('begin', 0, 'submit');
  lost.context.fetch = async () => { throw new Error('lost ACK'); };
  lost.library.Busara_Request('lost', 'POST', '/api/rooms/room/commands', '{}', '', lostId);
  await settle();
  assert.equal(l.api.report().samples[0].outcome, 'delivery_uncertain');
  assert.equal(lost.messages.at(-1).status, 0);
  const abortedId = l.observe('begin', 0, 'retry');
  lost.context.fetch = (_, options) => new Promise((resolve, reject) => {
    options.signal.addEventListener('abort', () => reject(new Error('aborted')));
  });
  lost.library.Busara_Request('aborted', 'POST', '/api/rooms/room/commands', '{}', '', abortedId);
  for (const controller of lost.context.BusaraOnline.requests) controller.abort();
  await settle();
  assert.equal(l.api.report().samples[1].outcome, 'aborted');
  l.api.clear();
  assert.equal(lost.stored.size, 0, 'Diagnostics do not write storage');
  lost.library.Busara_Dispose();
  console.log('PASS: bounded private latency, clocks, races, frames, retries, aborts, provider-neutral bridge');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
