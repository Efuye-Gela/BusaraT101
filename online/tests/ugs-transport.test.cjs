'use strict';
const assert = require('node:assert/strict');
const {BusaraUgs} = require('../../Busara/Assets/WebGLTemplates/BusaraOnline/busara-ugs.js');
const config = {projectId: '11111111-1111-1111-1111-111111111111', environmentName: 'development', moduleName: 'BusaraUgs'};
const json = (value, status = 200) => ({ok: status === 200, status, text: async () => JSON.stringify(value)});

function platform(stored = new Map()) {
  const calls = [];
  let behavior = null;
  return {
    stored, calls, setBehavior: value => { behavior = value; },
    location: new URL('https://localhost:7443/game/'),
    navigator: {locks: {request: async (_, callback) => callback()}},
    localStorage: {
      getItem: key => stored.get(key) ?? null,
      setItem: (key, value) => stored.set(key, value)
    },
    fetch: async (url, options) => {
      calls.push({url, options});
      if (behavior) { const result = await behavior(url, options); if (result) return result; }
      if (url.includes('player-auth')) return json({
        userId: 'seat-A', idToken: 'private-access', sessionToken: 'rotated-session', expiresIn: 3600
      });
      return json({output: {status: 200, body: JSON.stringify({guestId: 'seat-A', csrfToken: 'ugs-bearer'})}});
    }
  };
}

async function main() {
  const p = platform();
  let client = new BusaraUgs(config, p);
  assert.equal((await client.request('GET', '/api/guest', '')).status, 401);
  assert.equal(p.calls.length, 0, 'No silent anonymous replacement');
  assert.equal((await client.request('POST', '/api/guest', '{}')).status, 200);
  assert(p.calls[0].url.endsWith('/anonymous'));
  assert.equal(p.calls[0].options.headers.ProjectId, config.projectId);
  assert.equal(p.calls[0].options.headers.UnityEnvironment, 'development');
  assert.equal(p.calls[0].options.credentials, 'omit');
  assert(!Array.from(p.stored.values()).some(value => value.includes('private-access')));
  assert.equal(JSON.parse(p.calls[1].options.body).params.operation, 'register');
  client = new BusaraUgs(config, p);
  await client.request('GET', '/api/guest', '');
  assert(p.calls[2].url.endsWith('/session-token'));
  assert.equal(JSON.parse(p.calls[2].options.body).sessionToken, 'rotated-session');
  const command = '{"commandId":"same-command","expectedVersion":"99999999999999999","kind":"use","paymentIds":["a","b"]}';
  const match = '22222222-2222-2222-2222-222222222222';
  let lost = true;
  p.setBehavior(async url => {
    if (url.includes('cloud-code') && lost) { lost = false; throw new Error('Lost ACK'); }
  });
  assert.equal((await client.request('POST', '/api/rooms/' + match + '/commands', command)).status, 0);
  await client.request('POST', '/api/rooms/' + match + '/commands', command);
  assert.equal(p.calls.at(-1).options.body, p.calls.at(-2).options.body, 'Exact command string survives retry');
  assert.equal(JSON.parse(p.calls.at(-1).options.body).params.payload, command);
  assert.equal(JSON.parse(p.calls.at(-1).options.body).params.operation, 'command', 'Old clients keep bare receipt operation');
  await client.request('POST', '/api/rooms/' + match + '/commands-with-view', command);
  const embeddedRequest = JSON.parse(p.calls.at(-1).options.body).params;
  assert.equal(embeddedRequest.operation, 'commandWithView');
  assert.equal(embeddedRequest.payload, command, 'Changing reply format never changes the saved command');
  assert.equal(embeddedRequest.matchId, match);
  assert.equal((await client.request('GET', '/api/rooms/' + match + '/commands-with-view', '')).status, 400);
  assert.equal(p.calls.at(-1).options.headers.Authorization, 'Bearer private-access');
  p.setBehavior(async () => json({details: [{message: 'PRIVATE STATE'}]}, 422));
  const failure = await client.request('GET', '/api/rooms/' + match, '');
  assert.equal(failure.status, 503);
  assert(!failure.body.includes('PRIVATE'));
  p.setBehavior(async () => json({result: {status: 200, body: '{}'}}));
  assert.equal((await client.request('GET', '/api/rooms/' + match, '')).status, 503, 'Reject wrong module envelope');
  p.setBehavior(async () => json({output: {status: 200, body: JSON.stringify({matchId: match, version: '1', inviteUrl: '#invite=private'})}}));
  const created = await client.request('POST', '/api/rooms', '{"commandId":"create"}');
  assert.equal(JSON.parse(created.body).inviteUrl, 'https://localhost:7443/game/#invite=private');
  const restored = new BusaraUgs(config, p);
  p.setBehavior(async () => json({userId: 'DIFFERENT', sessionToken: 'wrong', idToken: 'wrong', expiresIn: 3600}));
  assert.equal((await restored.request('GET', '/api/guest', '')).status, 503);
  assert.equal(JSON.parse(p.stored.get(restored.authKey)).playerId, 'seat-A', 'Never replace identity during recovery');
  const invalid = new BusaraUgs({...config, projectId: ''}, p);
  assert.equal((await invalid.request('POST', '/api/guest', '{}')).status, 503);
  console.log('PASS: UGS auth, refresh, identity binding, exact retries, private error suppression, envelopes and config');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
