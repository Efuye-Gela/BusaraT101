'use strict';
const assert = require('node:assert/strict');
const {checkStorageDenial, tokenRouting, diagnosePolicy} = require('../scripts/smoke-ugs.cjs');

for (const probe of ['privateRead', 'defaultRead', 'playerRead', 'playerWrite']) {
  assert.doesNotThrow(() => checkStorageDenial(403, probe));
  for (const status of [0, 200, 201, 204, 301, 400, 404, 408, 409, 422, 429, 500, 503]) {
    assert.throws(() => checkStorageDenial(status, probe),
      error => error.message.includes('HTTP ' + status),
      'Successful access, routing errors and transient failures must not pass: ' + probe);
  }
}
assert.doesNotThrow(() => checkStorageDenial(401, 'privateRead'));
assert.throws(() => checkStorageDenial(401, 'defaultRead'), /expected 403/);
assert.throws(() => checkStorageDenial(401, 'playerRead'), /expected 403/);
assert.throws(() => checkStorageDenial(401, 'playerWrite'), /expected 403/);
assert.throws(() => checkStorageDenial(403, 'unknown'), /Unknown/);
console.log('PASS: private 401/403 denied; player-accessible probes require 403; success and unrelated failures rejected.');

const project = 'a1111111-1111-4111-8111-111111111111';
const environment = 'b2222222-2222-4222-8222-222222222222';
const token = claims => 'header.' + Buffer.from(JSON.stringify(claims)).toString('base64url') + '.signature';
assert.deepEqual(tokenRouting(token({project_id: project, environment_id: environment, sub: 'private-player'})),
  {project_id: project, environment_id: environment, env_id: 'absent or not a UUID'});
assert.equal(tokenRouting(token({project_id: 'secret-not-uuid'})).project_id, 'absent or not a UUID');
assert.throws(() => tokenRouting('private-invalid-token'), error =>
  error.message.includes('contents suppressed') && !error.message.includes('private-invalid-token'));

async function checkDiagnostic(statuses) {
  const logs = [], calls = [];
  let cancelled = 0, authenticated = false;
  const client = {
    config: {projectId: project, environmentName: 'development'}, player: 'private-player',
    token: token({project_id: project, environment_id: environment, sub: 'private-player'}),
    authenticate: async create => { assert.equal(create, true); authenticated = true; }
  };
  const run = diagnosePolicy(client, async (url, options) => {
    assert.equal(authenticated, true);
    assert.equal(options.method, 'GET');
    assert.equal(options.redirect, 'manual');
    assert.equal(options.headers.Authorization, 'Bearer ' + client.token);
    calls.push(url);
    return {status: statuses[calls.length - 1], body: {cancel: async () => { cancelled++; }}};
  }, line => logs.push(line));
  if (statuses.slice(1).some(status => status !== 403))
    await assert.rejects(run, /expected 403/);
  else await run;
  assert.equal(calls.length, 4);
  assert.equal(cancelled, 4);
  assert.ok(calls[1].endsWith('/items?keys=document'));
  assert.ok(calls[2].endsWith('/items'));
  assert.ok(calls[3].includes('/players/private-player/items'));
  const output = logs.join('\n');
  assert.ok(output.includes(environment));
  for (const secret of [client.token, client.player, 'signature']) assert.ok(!output.includes(secret));
  assert.equal(output.includes('PASS:'), statuses.slice(1).every(status => status === 403));
}
(async () => {
  await checkDiagnostic([401, 200, 200, 200]);
  await checkDiagnostic([401, 403, 403, 403]);
  await checkDiagnostic([403, 403, 403, 401]);
  await checkDiagnostic([401, 302, 403, 403]);
  console.log('PASS: diagnostics use reads only, suppress private data, compare query routing and fail closed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
