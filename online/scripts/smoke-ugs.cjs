'use strict';
const {BusaraUgs} = require('../../Busara/Assets/WebGLTemplates/BusaraOnline/busara-ugs.js');
const {randomUUID} = require('node:crypto');
const projectId = process.env.BUSARA_UGS_PROJECT_ID;
const environmentName = process.env.BUSARA_UGS_ENVIRONMENT;

function seat() {
  const memory = new Map();
  return new BusaraUgs({projectId, environmentName, moduleName: 'BusaraUgs'}, {
    fetch, location: new URL('https://localhost/'),
    localStorage: {getItem: key => memory.get(key) ?? null, setItem: (key, value) => memory.set(key, value)},
    navigator: {locks: {request: async (_, callback) => callback()}}
  });
}
const call = (client, method, path, body = {}) =>
  client.request(method, path, JSON.stringify(body), AbortSignal.timeout(20000));
function expect(reply, status, step) {
  if (reply.status !== status) {
    const code = (() => { try { return JSON.parse(reply.body).code; } catch (_) { return ''; } })();
    throw new Error(step + ' failed (HTTP ' + reply.status + ', ' +
      (/^[a-z_]+$/.test(code) ? code : 'unexpected_response') + '). Check UGS deployment, private directory, policy and environment.');
  }
  return JSON.parse(reply.body);
}
function check(condition, step) { if (!condition) throw new Error(step); }

function checkStorageDenial(status, probe) {
  const labels = {privateRead: 'Private Cloud Save read', defaultRead: 'Default-class Cloud Save read',
    playerRead: 'Player Cloud Save read', playerWrite: 'Player Cloud Save write'};
  if (!Object.hasOwn(labels, probe)) throw new Error('Unknown storage denial probe.');
  // Private Game Data is server-only and can reject a player token before
  // policy evaluation. Player-accessible endpoints must still prove policy denial.
  const denied = status === 403 || (probe === 'privateRead' && status === 401);
  check(denied, labels[probe] + ' returned HTTP ' + status +
    ' (expected ' + (probe === 'privateRead' ? '401 or 403' : '403') +
    '). Access-control verification stopped; inspect the status and deployed policy before changing permissions.');
}

function tokenRouting(token) {
  let claims;
  try { claims = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8')); }
  catch (_) { throw new Error('Cannot decode authentication token routing; token contents suppressed.'); }
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const routing = {};
  // Diagnostic only, not token verification. Never emit arbitrary claims.
  for (const key of ['project_id', 'environment_id', 'env_id'])
    routing[key] = typeof claims?.[key] === 'string' && uuid.test(claims[key]) ?
      claims[key] : 'absent or not a UUID';
  return routing;
}

async function diagnosePolicy(client, fetcher = fetch, log = console.log) {
  await client.authenticate(true, AbortSignal.timeout(20000), false);
  log('Policy diagnostic UTC: ' + new Date().toISOString());
  log('Configured project: ' + client.config.projectId + '; environment: ' + client.config.environmentName);
  log('Token routing (decoded, not independently verified): ' + JSON.stringify(tokenRouting(client.token)));
  const base = 'https://cloud-save.services.api.unity.com/v1/data/projects/' +
    encodeURIComponent(client.config.projectId);
  const custom = base + '/custom/busara_directory_v1';
  const probes = [
    ['privateRead', 'Private read', custom + '/private/items?keys=document'],
    ['defaultRead', 'Default read with query', custom + '/items?keys=document'],
    ['defaultRead', 'Default read without query', custom + '/items'],
    ['playerRead', 'Own-player read', base + '/players/' + encodeURIComponent(client.player) + '/items']
  ];
  const results = [];
  for (const [probe, label, url] of probes) {
    const response = await fetcher(url, {method: 'GET', redirect: 'manual', cache: 'no-store',
      headers: {Authorization: 'Bearer ' + client.token}, signal: AbortSignal.timeout(20000)});
    log(label + ': HTTP ' + response.status);
    if (response.body) await response.body.cancel();
    results.push({probe, status: response.status});
  }
  for (const {probe, status} of results) checkStorageDenial(status, probe);
  log('PASS: read-only denial probes. This does not replace the full smoke or verify write denial.');
}

async function main() {
  const args = process.argv.slice(2);
  if (args.length && (args.length !== 1 || args[0] !== '--policy-check'))
    throw new Error('Usage: node smoke-ugs.cjs [--policy-check]');
  if (!projectId || !environmentName || environmentName === 'production' ||
      process.env.BUSARA_UGS_SMOKE_CONFIRM !== environmentName)
    throw new Error('Set BUSARA_UGS_PROJECT_ID, BUSARA_UGS_ENVIRONMENT (not production), and BUSARA_UGS_SMOKE_CONFIRM to that environment name. This creates real test identities; the full smoke also creates a match.');
  if (args[0] === '--policy-check') return diagnosePolicy(seat());
  const host = seat(), guest = seat();
  expect(await call(host, 'POST', '/api/guest'), 200, 'Host registration');
  expect(await call(guest, 'POST', '/api/guest'), 200, 'Guest registration');
  check(host.player !== guest.player, 'Expected independent player identities.');
  const direct = 'https://cloud-save.services.api.unity.com/v1/data/projects/' +
    encodeURIComponent(projectId) + '/custom/busara_directory_v1/private/items';
  const headers = {'Authorization': 'Bearer ' + host.token, 'Content-Type': 'application/json'};
  const read = await fetch(direct + '?keys=document', {headers, signal: AbortSignal.timeout(20000)});
  checkStorageDenial(read.status, 'privateRead');
  const defaultRead = await fetch(direct.replace('/private/items', '/items') + '?keys=document',
    {headers, signal: AbortSignal.timeout(20000)});
  checkStorageDenial(defaultRead.status, 'defaultRead');
  const playerData = 'https://cloud-save.services.api.unity.com/v1/data/projects/' +
    encodeURIComponent(projectId) + '/players/' + encodeURIComponent(host.player) + '/items';
  const write = await fetch(playerData, {
    method: 'POST', headers, body: JSON.stringify({key: 'probe', value: 'access-policy-test'}),
    signal: AbortSignal.timeout(20000)
  });
  checkStorageDenial(write.status, 'playerWrite');
  const create = {commandId: randomUUID()};
  const created = await Promise.all([call(host, 'POST', '/api/rooms', create), call(host, 'POST', '/api/rooms', create)]);
  const room = expect(created[0], 200, 'Create room');
  check(created[0].body === created[1].body, 'Concurrent create must return the same room and invitation.');
  const route = '/api/rooms/' + room.matchId;
  expect(await call(guest, 'GET', route), 404, 'Non-member projection denial');
  const inviteToken = new URL(room.inviteUrl).hash.slice('#invite='.length);
  const join = {commandId: randomUUID(), inviteToken};
  const joined = await call(guest, 'POST', '/api/rooms/join', join);
  expect(joined, 200, 'Join room');
  check(joined.body === (await call(guest, 'POST', '/api/rooms/join', join)).body, 'Join retry changed the receipt.');
  for (const [client, name] of [[host, 'Host'], [guest, 'Guest']]) {
    const view = expect(await call(client, 'GET', route), 200, 'Read lobby');
    const command = {commandId: randomUUID(), expectedVersion: view.version,
      kind: 'configure', name, ready: true, paymentIds: []};
    const replies = await Promise.all([call(client, 'POST', route + '/commands', command),
      call(client, 'POST', route + '/commands', command)]);
    expect(replies[0], 200, 'Configure seat');
    check(replies[0].body === replies[1].body, 'Concurrent duplicate was not idempotent.');
  }
  const lobby = expect(await call(host, 'GET', route), 200, 'Ready lobby');
  const start = {commandId: randomUUID(), expectedVersion: lobby.version, kind: 'start', paymentIds: []};
  expect(await call(guest, 'POST', route + '/commands', start), 409, 'Wrong-seat start denial');
  start.commandId = randomUUID();
  expect(await call(host, 'POST', route + '/commands', start), 200, 'Host start');
  const projection = expect(await call(host, 'GET', route), 200, 'Started match');
  check(projection.players[1].kingdom == null && !('deck' in projection) && !('snapshot' in projection),
    'Unexpected private projection content.');
  host.token = null;
  const restored = expect(await call(host, 'GET', route), 200, 'Session token refresh');
  check(restored.matchId === room.matchId && restored.seat === 0, 'Refresh changed room membership.');
  console.log('PASS: real UGS registration, direct-access denial, concurrent create/commands, join retry, ownership, start, privacy and token refresh.');
  console.log('This API smoke does not verify browser CORS, Unity rendering, or Retraction recovery. Test players/room remain in the selected development environment; credentials were not saved.');
}
module.exports = {checkStorageDenial, tokenRouting, diagnosePolicy};
if (require.main === module)
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
