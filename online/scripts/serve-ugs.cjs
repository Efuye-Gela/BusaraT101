'use strict';
// Static files only. Game commands run in UGS, not in this process.
const https = require('node:https');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..', 'web');
const certificatePath = process.env.BUSARA_WEB_CERTIFICATE_FILE;
if (!certificatePath) throw new Error('Set BUSARA_WEB_CERTIFICATE_FILE to a private JSON file containing PFX path and password.');
let certificate;
try {
  certificate = JSON.parse(fs.readFileSync(certificatePath, 'utf8'));
  if (!certificate || typeof certificate.path !== 'string' || typeof certificate.password !== 'string')
    throw new Error('Invalid certificate configuration');
} catch (_) {
  throw new Error('Cannot read certificate configuration. Expected a private JSON object with path and password strings.');
}
const port = Number(process.env.BUSARA_WEB_PORT || 7443);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid loopback HTTPS port.');
if (!fs.existsSync(path.join(root, 'index.html'))) throw new Error('Build the Unity UGS Web player first.');
const mime = {'.html': 'text/html; charset=utf-8', '.js': 'application/javascript',
  '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json',
  '.png': 'image/png', '.ico': 'image/x-icon'};
const server = https.createServer({
  pfx: fs.readFileSync(certificate.path), passphrase: certificate.password
}, (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('Referrer-Policy', 'no-referrer');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405).end(); return; }
  let file;
  try {
    const name = decodeURIComponent(new URL(req.url, 'https://localhost').pathname);
    file = path.resolve(root, '.' + (name === '/' ? '/index.html' : name));
  } catch (_) { res.writeHead(400).end(); return; }
  if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
  fs.stat(file, (error, stat) => {
    if (error || !stat.isFile()) { res.writeHead(404).end(); return; }
    res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
    res.setHeader('Content-Length', stat.size);
    if (req.method === 'HEAD') { res.writeHead(200).end(); return; }
    const stream = fs.createReadStream(file);
    stream.on('error', () => { console.error('Static file read failed.'); res.destroy(); });
    stream.pipe(res);
  });
});
server.on('error', error => { console.error('Static HTTPS server failed:', error.code); process.exitCode = 1; });
server.listen(port, '127.0.0.1', () => console.log('Unity static player: https://127.0.0.1:' + port + ' (UGS backend)'));
