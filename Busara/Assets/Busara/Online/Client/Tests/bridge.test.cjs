'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {createHash} = require('node:crypto');
const source = fs.readFileSync(path.resolve(__dirname, '..', '..', '..', '..', 'Plugins', 'WebGL', 'BusaraOnline.jslib'), 'utf8');
const settle = () => new Promise(resolve => setImmediate(resolve));

async function browser(href, stored = new Map(), config = {backend: 'legacy'}) {
  const messages = [], requests = [], library = {};
  const location = new URL(href);
  const context = {
    LibraryManager: {library},
    mergeInto: (target, value) => Object.assign(target, value),
    UTF8ToString: value => value,
    SendMessage: (_, __, json) => messages.push(JSON.parse(json)),
    URL, URLSearchParams, Set, Promise, Object, JSON, Error, TextEncoder, Uint8Array,
    crypto: {subtle: {digest: async (_, bytes) => Uint8Array.from(createHash('sha256').update(bytes).digest()).buffer}},
    location, window: {busaraConfig: config, BusaraUgs: class {
      constructor() { this.scope = 'project:development'; }
      validate() {}
      async request(method, path, body) {
        requests.push({ugs: true, method, path, body});
        return {status: 200, body: '{"version":"1"}'};
      }
    }}, history: {replaceState: (_, __, value) => { href = value; }},
    navigator: {
      locks: {request: async (_, __, callback) => callback({name: 'exclusive-test-lock'})},
      clipboard: {writeText: async () => {}}
    },
    localStorage: {
      getItem: key => stored.get(key) ?? null,
      setItem: (key, value) => stored.set(key, value),
      removeItem: key => stored.delete(key)
    },
    setTimeout: () => 1, clearTimeout: () => {}, AbortController,
    WebSocket: class { close() {} },
    fetch: async (url, options) => {
      requests.push({url, options});
      return {ok: true, status: 200, text: async () => '{"matchId":"room","version":"1"}'};
    }
  };
  vm.createContext(context);
  vm.runInContext(source, context);
  context.BusaraOnline = library.$BusaraOnline;
  library.Busara_Init('BusaraOnline');
  assert(!href.includes('invite'), 'Invite fragment removed before asynchronous work');
  await settle();
  return {context, library, messages, requests, stored};
}

async function main() {
  const b = await browser('https://localhost:7443/');
  b.library.Busara_Bind('guest', 'room', 'csrf-only-in-memory');
  await settle();
  const body = '{"commandId":"same-id","expectedVersion":"11","kind":"use","paymentIds":["art","security"]}';
  assert.equal(b.library.Busara_StoreOutbox(body), 1);
  assert.equal(b.library.Busara_StoreOutbox('{"commandId":"replacement"}'), 0);
  assert.equal(b.library.Busara_ClearOutbox('wrong-id'), 0);
  assert.equal(b.stored.get('busara.pending.v1:guest:room'), body);
  b.library.Busara_Request('request', 'POST', '/api/rooms/room/commands', body, 'csrf-only-in-memory');
  await settle();
  assert.equal(b.requests[0].options.body, body);
  assert.equal(b.requests[0].options.credentials, 'include');
  assert.equal(b.requests[0].options.headers['X-CSRF-Token'], 'csrf-only-in-memory');
  assert.equal(b.requests[0].options.redirect, 'error');
  assert.equal(b.library.Busara_ClearOutbox('same-id'), 1);
  assert.equal(b.stored.size, 0);
  b.library.Busara_VisibleControls('{"controls":[{"id":"slot-4","label":"4 Fire"}],"phase":"Action"}');
  assert(Object.isFrozen(b.context.window.busaraVisibleUi));
  assert(Object.isFrozen(b.context.window.busaraVisibleUi.controls[0]));
  b.library.Busara_Dispose();
  assert.equal(b.context.window.busaraVisibleUi, undefined);

  const stored = new Map();
  let room = await browser('https://localhost:7443/', stored);
  room.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  const createBody = '{"commandId":"persisted-create"}';
  const create = JSON.stringify({path: '/api/rooms', body: createBody});
  assert.equal(room.library.Busara_StoreRoomOutbox(create), 1);
  assert.equal(room.library.Busara_StoreRoomOutbox(JSON.stringify({path: '/api/rooms', body: '{"commandId":"new-id"}'})), 0);
  room.context.fetch = async () => { throw new Error('Simulated lost acknowledgment'); };
  room.library.Busara_Request('lost', 'POST', '/api/rooms', createBody, 'csrf');
  await settle();
  assert(room.messages.some(m => m.requestId === 'lost' && m.status === 0));
  room.library.Busara_Dispose();
  room = await browser('https://localhost:7443/', stored);
  room.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  assert.equal(JSON.parse(room.messages.find(m => m.kind === 'roomOutbox').body).body, createBody);
  room.library.Busara_Request('retry', 'POST', '/api/rooms', createBody, 'csrf');
  await settle();
  assert.equal(room.requests[0].options.body, createBody, 'Reload retries the exact original room command');
  assert.equal(room.library.Busara_ClearRoomOutbox('wrong-id'), 0);
  assert.equal(room.library.Busara_ClearRoomOutbox('persisted-create'), 1);
  room.library.Busara_Dispose();

  const invite = 'private-test-invite';
  let join = await browser('https://localhost:7443/#invite=' + invite, stored);
  join.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  const joinBody = '{"commandId":"persisted-join"}';
  const joinOperation = JSON.stringify({path: '/api/rooms/join', body: joinBody});
  assert.equal(join.library.Busara_StoreRoomOutbox(joinOperation), 1);
  assert(!Array.from(stored.values()).some(value => value.includes(invite) || value.includes('csrf')),
    'Only a one-way invitation fingerprint is persisted, never the invitation or credentials');
  join.library.Busara_Request('join', 'POST', '/api/rooms/join', joinBody, 'csrf');
  await settle();
  const originalBody = join.requests[0].options.body;
  assert.equal(JSON.parse(originalBody).inviteToken, invite);
  join.library.Busara_Dispose();
  join = await browser('https://localhost:7443/', stored);
  join.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  assert.equal(JSON.parse(join.messages.find(m => m.kind === 'roomOutbox').body).matchingInvite, false);
  join.library.Busara_Request('without-invite', 'POST', '/api/rooms/join', joinBody, 'csrf');
  await settle();
  assert.equal(join.requests.length, 0, 'Reload cannot substitute a missing invitation');
  join.library.Busara_Dispose();
  join = await browser('https://localhost:7443/#invite=different-test-invite', stored);
  join.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  assert.equal(JSON.parse(join.messages.find(m => m.kind === 'roomOutbox').body).matchingInvite, false);
  assert.equal(join.library.Busara_StoreRoomOutbox(joinOperation), 0, 'A different invite cannot rewrite the pending operation');
  join.library.Busara_Dispose();
  join = await browser('https://localhost:7443/#invite=' + invite, stored);
  join.library.Busara_RestoreRoomOutbox('guest');
  await settle();
  assert.equal(JSON.parse(join.messages.find(m => m.kind === 'roomOutbox').body).matchingInvite, true);
  join.library.Busara_Request('join-retry', 'POST', '/api/rooms/join', joinBody, 'csrf');
  await settle();
  assert.equal(join.requests[0].options.body, originalBody, 'Original invitation replays byte-identical join payload');
  assert.equal(join.library.Busara_ClearRoomOutbox('persisted-join'), 1);
  assert.equal(stored.size, 0);
  join.library.Busara_Dispose();
  const ugs = await browser('https://localhost:7443/', new Map(), {backend: 'ugs', pollSeconds: 10});
  ugs.library.Busara_Bind('guest', 'room', 'unused-by-ugs');
  await settle();
  assert(ugs.messages.some(message => message.kind === 'polling'));
  assert.equal(JSON.parse(ugs.messages.find(message => message.kind === 'route').body).pollSeconds, 10);
  assert.equal(ugs.context.BusaraOnline.socket, null, 'UGS does not start the legacy WebSocket');
  assert.equal(ugs.library.Busara_StoreOutbox(body), 1);
  assert.equal(ugs.stored.get('busara.pending.v1:project:development:guest:room'), body);
  ugs.library.Busara_Request('ugs-command', 'POST', '/api/rooms/room/commands', body, 'unused-by-ugs');
  await settle();
  assert.equal(ugs.requests[0].ugs, true);
  assert.equal(ugs.requests[0].body, body);
  ugs.library.Busara_Dispose();
  const noConfig = await browser('https://localhost:7443/', new Map(), null);
  assert(noConfig.messages.some(message => message.kind === 'unsupported'), 'Missing config never falls back to legacy');
  assert.equal(noConfig.requests.length, 0);
  console.log('PASS: credentialed fetch, game outbox, room reload/lost-ACK recovery, invitation binding, safe descriptors');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
