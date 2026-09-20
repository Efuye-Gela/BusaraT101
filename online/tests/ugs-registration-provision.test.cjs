'use strict';
const assert = require('node:assert/strict');
const {provision, decodeCliOutput} = require('../scripts/provision-ugs-registration.cjs');
const layout = {prefix: 'busara_registration_v1_', count: 8,
  empty: {schemaVersion: 1, registrations: {}}};
const result = value => ({Items: value ? [{key: 'document', value}] : [], Next: null});

async function main() {
  assert.equal(decodeCliOutput('set', ''), null, 'CLI writes can succeed without a JSON result.');
  assert.equal(decodeCliOutput('set', 'Data saved'), null);
  assert.deepEqual(decodeCliOutput('get', '{"Items":[]}'), {Items: []});
  assert.throws(() => decodeCliOutput('get', ''), SyntaxError);
  let writes = 0;
  const values = new Map([['busara_registration_v1_00',
    {schemaVersion: 1, registrations: {existing: {documentId: 'preserve'}}}]]);
  const original = JSON.stringify(values.get('busara_registration_v1_00'));
  const run = async (op, id, value) => {
    if (op === 'get') return result(values.get(id));
    assert(!values.has(id), 'Existing shards must never be initialized.');
    writes++;
    values.set(id, JSON.parse(value));
    return {};
  };
  assert.deepEqual(await provision({layout, run}), {existing: 1, missing: 7, created: 0});
  assert.equal(writes, 0);
  await assert.rejects(provision({layout, run, apply: true}), /maintenance/);
  assert.equal(writes, 0);
  assert.deepEqual(await provision({layout, run, apply: true, maintenanceConfirmed: true}),
    {existing: 1, missing: 7, created: 7});
  assert.equal(JSON.stringify(values.get('busara_registration_v1_00')), original);
  assert.deepEqual(await provision({layout, run, apply: true, maintenanceConfirmed: true}),
    {existing: 8, missing: 0, created: 0});
  assert.equal(writes, 7);
  await assert.rejects(provision({layout, run: async () => result({schemaVersion: 2})}), /schema/);
  await assert.rejects(provision({layout: {...layout, prefix: '../'}, run}), /layout/);
  const ids = [];
  await provision({layout: {...layout, count: 64}, run: async (_, id) => { ids.push(id); return result(); }});
  assert.equal(ids.length, 64);
  assert(ids.includes('busara_registration_v1_10') && ids.includes('busara_registration_v1_63'));
  assert(!ids.includes('busara_registration_v1_0a'), 'Runtime shard suffixes are decimal, not hex.');
  let inFlight = 0;
  await assert.rejects(provision({layout, apply: true, maintenanceConfirmed: true,
    run: async (op, id) => {
      if (op === 'get') return result();
      inFlight++;
      await new Promise(resolve => setTimeout(resolve, 5));
      inFlight--;
      throw new Error('Uncertain initialization');
    }}), /initialization/);
  assert.equal(inFlight, 0, 'No initializer may remain active when provisioning returns.');
  console.log('PASS: read-only planning, maintenance guard, no overwrites, idempotence, schema rejection and draining.');
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
