'use strict';
const fs = require('node:fs');
const path = require('node:path');
const {execFile} = require('node:child_process');
const {promisify, parseArgs} = require('node:util');
const execute = promisify(execFile);

function validateLayout(layout) {
  const fields = Object.keys(layout.empty || {}).filter(key => key !== 'schemaVersion');
  if (!/^busara_[a-z0-9_]+_$/.test(layout.prefix) ||
      !Number.isInteger(layout.count) || layout.count < 1 || layout.count > 256 ||
      layout.empty?.schemaVersion !== 1 || fields.length !== 1 ||
      !layout.empty[fields[0]] || Array.isArray(layout.empty[fields[0]]) ||
      typeof layout.empty[fields[0]] !== 'object' || Object.keys(layout.empty[fields[0]]).length)
    throw new Error('Invalid empty registration shard layout.');
  return fields[0];
}

function readDocument(response, layout, field) {
  if (!response || !Array.isArray(response.Items) || response.Items.length > 1)
    throw new Error('Unexpected Cloud Save CLI result; contents withheld.');
  if (!response.Items.length) return null;
  const item = response.Items[0];
  if (item.key !== 'document') throw new Error('Unexpected Cloud Save item key.');
  let value;
  try { value = typeof item.value === 'string' ? JSON.parse(item.value) : item.value; }
  catch { throw new Error('Stored shard is not valid JSON; no overwrite attempted.'); }
  if (value?.schemaVersion !== layout.empty.schemaVersion || !value[field] ||
      typeof value[field] !== 'object' || Array.isArray(value[field]) ||
      Object.keys(value).sort().join() !== Object.keys(layout.empty).sort().join())
    throw new Error('Existing shard schema differs; no overwrite attempted.');
  return value;
}

async function provision({layout, apply = false, maintenanceConfirmed = false, run}) {
  const field = validateLayout(layout);
  if (apply && !maintenanceConfirmed)
    throw new Error('Writes require a confirmed, drained maintenance window and one deployment operator.');
  const report = {existing: 0, missing: 0, created: 0};
  let next = 0, failure;
  async function worker() {
    while (!failure && next < layout.count) {
      const id = layout.prefix + (next++).toString(10).padStart(2, '0');
      try {
        const current = readDocument(await run('get', id), layout, field);
        if (current) { report.existing++; continue; }
        report.missing++;
        if (!apply) continue;
        await run('set', id, JSON.stringify(layout.empty));
        const saved = readDocument(await run('get', id), layout, field);
        if (!saved || Object.keys(saved[field]).length)
          throw new Error('Shard initialization verification failed; keep maintenance enabled.');
        report.created++;
      } catch (error) { failure = error; }
    }
  }
  // Drain all initializers before allowing the caller to activate the runtime.
  await Promise.all(Array.from({length: Math.min(4, layout.count)}, worker));
  if (failure) throw failure;
  return report;
}

const decodeCliOutput = (operation, stdout) => operation === 'get' ? JSON.parse(stdout) : null;

function cliRunner(cli, project, environment) {
  if (!/^[a-f0-9-]{36}$/i.test(project || '') ||
      !/^[a-z0-9_-]+$/i.test(environment || '') || environment.toLowerCase() === 'production')
    throw new Error('An explicit project UUID and non-production environment are required.');
  return async (operation, id, value) => {
    const args = ['cloud-save', 'data', 'custom', operation, '--custom-id', id,
      '--visibility', 'private', '-p', project, '-e', environment, '--json'];
    args.push(...(operation === 'get' ? ['--keys', 'document'] : ['--key', 'document', '--value', value]));
    try {
      const {stdout} = await execute(cli, args, {timeout: 60000, maxBuffer: 8 * 1024 * 1024, windowsHide: true});
      return decodeCliOutput(operation, stdout);
    } catch (error) {
      const diagnostic = String(error.stderr || '') + String(error.stdout || '');
      const reason = /forbidden|\b403\b/i.test(diagnostic) ? ' (403 access denied)' :
        /unauthorized|\b401\b/i.test(diagnostic) ? ' (401 authentication)' : '';
      throw new Error(`Cloud Save CLI ${operation} failed${reason}; check permissions/tooling. Private output withheld.`);
    }
  };
}

async function main() {
  const {values} = parseArgs({options: {
    'project-id': {type: 'string'}, environment: {type: 'string'}, layout: {type: 'string'},
    cli: {type: 'string'}, apply: {type: 'boolean'}, 'maintenance-confirmed': {type: 'boolean'}
  }});
  if (!values.layout) throw new Error('Provide --layout with the reviewed public registration layout JSON.');
  const layout = JSON.parse(fs.readFileSync(values.layout, 'utf8').replace(/^\uFEFF/, ''));
  const cli = values.cli || (process.platform === 'win32'
    ? path.join(process.env.APPDATA || '', 'npm', 'node_modules', 'ugs', 'bin', 'ugs.exe') : 'ugs');
  const report = await provision({layout, apply: values.apply,
    maintenanceConfirmed: values['maintenance-confirmed'],
    run: cliRunner(cli, values['project-id'], values.environment)});
  console.log(JSON.stringify(report));
}

module.exports = {provision, readDocument, validateLayout, decodeCliOutput};
if (require.main === module) main().catch(error => { console.error(error.message); process.exitCode = 1; });
