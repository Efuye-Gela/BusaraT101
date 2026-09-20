'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const https = require('node:https');
const net = require('node:net');
const {spawn} = require('node:child_process');
const {once} = require('node:events');

async function freePort() {
  const listener = net.createServer();
  listener.listen(0, '127.0.0.1');
  await once(listener, 'listening');
  const port = listener.address().port;
  await new Promise(resolve => listener.close(resolve));
  return port;
}

async function main() {
  const [server, certificateFile, publicCertificate] = process.argv.slice(2);
  assert.ok(server && certificateFile && publicCertificate, 'Supply server, private config and public test CA paths.');
  const port = await freePort();
  const ca = fs.readFileSync(publicCertificate);
  const child = spawn(process.execPath, [server], {env: {...process.env,
    BUSARA_WEB_CERTIFICATE_FILE: certificateFile, BUSARA_WEB_PORT: String(port)}});
  const exited = once(child, 'exit');
  try {
    await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Loopback server startup timed out.')), 15000);
      child.once('error', error => { clearTimeout(timer); reject(error); });
      child.once('exit', () => { clearTimeout(timer); reject(new Error('Loopback server exited before readiness.')); });
      child.stdout.on('data', chunk => {
        if (chunk.toString().includes('https://127.0.0.1:' + port)) { clearTimeout(timer); resolve(); }
      });
      child.stderr.on('data', () => { clearTimeout(timer); reject(new Error('Loopback server reported an error.')); });
    });
    const request = (path, method = 'GET') => new Promise((resolve, reject) => {
      // Trust only this fixture's public certificate, on this loopback request.
      const req = https.request({hostname: '127.0.0.1', port, path, method, ca}, response => {
        let body = '';
        response.setEncoding('utf8');
        response.on('data', chunk => { body += chunk; });
        response.on('end', () => resolve({status: response.statusCode, headers: response.headers, body}));
      });
      req.setTimeout(10000, () => req.destroy(new Error('Local HTTPS request timed out.')));
      req.on('error', reject);
      req.end();
    });
    const index = await request('/');
    assert.equal(index.status, 200);
    assert.equal(index.headers['cache-control'], 'no-store');
    const wasm = /codeUrl:\s*['"]([^'"]+)/.exec(index.body);
    assert.ok(wasm, 'Unity Web build must reference its wasm file.');
    const binary = await request('/' + wasm[1], 'HEAD');
    assert.equal(binary.status, 200);
    assert.equal(binary.headers['content-type'], 'application/wasm');
    assert.equal((await request('/busara-config.js')).status, 200);
    assert.equal((await request('/busara-ugs.js')).status, 200);
    assert.equal((await request('/certificate.json')).status, 404);
    assert.equal((await request('/', 'POST')).status, 405);
    console.log('PASS: isolated loopback HTTPS, certificate hostname, Unity MIME, public files and method restrictions.');
  } finally {
    if (child.exitCode === null) child.kill();
    await exited;
  }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
