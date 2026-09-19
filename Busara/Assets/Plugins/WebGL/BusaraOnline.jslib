mergeInto(LibraryManager.library, {
  $BusaraOnline: {
    receiver: null, guest: null, match: null, csrf: null, socket: null,
    timer: null, generation: 0, key: null, invite: null, requests: null,
    ownsOutbox: false, releaseOutbox: null,
    roomKey: null, ownsRoomOutbox: false, releaseRoomOutbox: null, inviteHash: null, ugs: null,
    emit: function(kind, body, id, status) {
      if (BusaraOnline.receiver)
        SendMessage(BusaraOnline.receiver, 'OnBrowserEvent',
          JSON.stringify({kind: kind, body: body || '', requestId: id || '', status: status || 0}));
    },
    connect: function(generation) {
      if (generation !== BusaraOnline.generation || !BusaraOnline.match) return;
      var url = new URL('/api/rooms/' + encodeURIComponent(BusaraOnline.match) + '/events', location.origin);
      url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
      var socket = new WebSocket(url, ['busara.v1', 'csrf.' + BusaraOnline.csrf]);
      BusaraOnline.socket = socket;
      socket.onopen = function() {
        if (generation === BusaraOnline.generation) BusaraOnline.emit('connected');
      };
      socket.onmessage = function() {
        // Notifications are invalidations only; never render socket payloads as state.
        if (generation === BusaraOnline.generation) BusaraOnline.emit('invalidate');
      };
      socket.onerror = function() { socket.close(); };
      socket.onclose = function() {
        if (generation !== BusaraOnline.generation) return;
        BusaraOnline.emit('reconnecting');
        BusaraOnline.timer = setTimeout(function() { BusaraOnline.connect(generation); }, 3000);
      };
    }
  },
  Busara_Init__deps: ['$BusaraOnline'],
  Busara_Init: function(receiver) {
    BusaraOnline.receiver = UTF8ToString(receiver);
    BusaraOnline.requests = new Set();
    BusaraOnline.ugs = null;
    var config = window.busaraConfig;
    if (config && config.backend === 'ugs') {
      try {
        BusaraOnline.ugs = new window.BusaraUgs(config, window);
        BusaraOnline.ugs.validate();
      } catch (_) {
        BusaraOnline.emit('unsupported', 'UGS is not configured. Set projectId and environmentName in busara-config.js and deploy the BusaraUgs module. No local-server fallback was started.');
        return;
      }
    } else if (!config || config.backend !== 'legacy') {
      BusaraOnline.emit('unsupported', 'Unknown online backend. Choose ugs or legacy explicitly.');
      return;
    }
    var route = new URLSearchParams(location.hash.slice(1));
    BusaraOnline.invite = window.__busaraInvite || route.get('invite');
    delete window.__busaraInvite;
    if (route.has('invite')) history.replaceState(null, '', location.pathname + location.search);
    if (location.protocol !== 'https:') {
      BusaraOnline.emit('unsupported', 'Online play requires an HTTPS-hosted Unity Web build. No guest or match request was sent.');
      return;
    }
    var ready = function() {
      BusaraOnline.emit('route', JSON.stringify({matchId: route.get('match'), hasInvite: !!BusaraOnline.invite,
        backend: BusaraOnline.ugs ? 'ugs' : 'legacy',
        pollSeconds: BusaraOnline.ugs ? Math.max(5, Math.min(60, Number(config.pollSeconds) || 10)) : 4}));
    };
    if (!BusaraOnline.invite) ready();
    else crypto.subtle.digest('SHA-256', new TextEncoder().encode(BusaraOnline.invite)).then(function(bytes) {
      // A fingerprint binds retries to the original high-entropy invitation without storing its secret.
      BusaraOnline.inviteHash = Array.from(new Uint8Array(bytes)).map(function(b) { return b.toString(16).padStart(2, '0'); }).join('');
      ready();
    }).catch(function() { BusaraOnline.emit('protocolError'); });
  },
  Busara_Request__deps: ['$BusaraOnline'],
  Busara_Request: function(idPtr, methodPtr, pathPtr, bodyPtr, csrfPtr) {
    var id = UTF8ToString(idPtr), method = UTF8ToString(methodPtr);
    var path = UTF8ToString(pathPtr), body = UTF8ToString(bodyPtr), csrf = UTF8ToString(csrfPtr);
    if (!path.startsWith('/api/') || path.startsWith('//')) {
      BusaraOnline.emit('response', '', id, 400); return;
    }
    if (path === '/api/rooms/join') {
      try {
        var operation = JSON.parse(localStorage.getItem(BusaraOnline.roomKey));
        if (!BusaraOnline.ownsRoomOutbox || !operation || operation.path !== path ||
            operation.body !== body || !BusaraOnline.invite || operation.inviteHash !== BusaraOnline.inviteHash) {
          BusaraOnline.emit('response', '{"code":"original_invitation_required"}', id, 409);
          return;
        }
        var parsed = JSON.parse(body);
        parsed.inviteToken = BusaraOnline.invite;
        body = JSON.stringify(parsed);
      } catch (_) { BusaraOnline.emit('protocolError', '', id); return; }
    }
    var controller = new AbortController();
    BusaraOnline.requests.add(controller);
    var timer = setTimeout(function() { controller.abort(); }, 15000);
    if (BusaraOnline.ugs) {
      BusaraOnline.ugs.request(method, path, body, controller.signal)
        .then(function(response) { BusaraOnline.emit('response', response.body, id, response.status); })
        .catch(function() { BusaraOnline.emit('response', '', id, 0); })
        .finally(function() { clearTimeout(timer); BusaraOnline.requests.delete(controller); });
      return;
    }
    var headers = {'Accept': 'application/json'};
    if (method !== 'GET') {
      headers['Content-Type'] = 'application/json';
      if (csrf) headers['X-CSRF-Token'] = csrf;
    }
    fetch(path, {method: method, headers: headers, body: method === 'GET' ? undefined : body,
      credentials: 'include', cache: 'no-store', redirect: 'error', signal: controller.signal})
      .then(async function(response) {
        var text = await response.text();
        if (text.length > 1048576) throw new Error('Response too large');
        BusaraOnline.emit('response', text, id, response.status);
      })
      .catch(function() { BusaraOnline.emit('response', '', id, 0); })
      .finally(function() { clearTimeout(timer); BusaraOnline.requests.delete(controller); });
  },
  Busara_RestoreRoomOutbox__deps: ['$BusaraOnline'],
  Busara_RestoreRoomOutbox: function(guestPtr) {
    var guest = UTF8ToString(guestPtr);
    BusaraOnline.roomKey = 'busara.room.pending.v1:' +
      (BusaraOnline.ugs ? BusaraOnline.ugs.scope + ':' : '') + guest;
    if (BusaraOnline.releaseRoomOutbox) BusaraOnline.releaseRoomOutbox();
    BusaraOnline.ownsRoomOutbox = false;
    if (!navigator.locks) { BusaraOnline.emit('storageError'); return; }
    navigator.locks.request(BusaraOnline.roomKey, {ifAvailable: true}, function(lock) {
      if (!lock) { BusaraOnline.emit('roomOutboxLocked'); return; }
      BusaraOnline.ownsRoomOutbox = true;
      try {
        var saved = localStorage.getItem(BusaraOnline.roomKey);
        if (saved) {
          var operation = JSON.parse(saved);
          operation.matchingInvite = !!BusaraOnline.invite && operation.inviteHash === BusaraOnline.inviteHash;
          BusaraOnline.emit('roomOutbox', JSON.stringify(operation));
        } else BusaraOnline.emit('roomOutbox', '');
      } catch (_) { BusaraOnline.emit('storageError'); }
      return new Promise(function(resolve) { BusaraOnline.releaseRoomOutbox = resolve; });
    }).catch(function() { BusaraOnline.emit('storageError'); });
  },
  Busara_StoreRoomOutbox__deps: ['$BusaraOnline'],
  Busara_StoreRoomOutbox: function(operationPtr) {
    if (!BusaraOnline.ownsRoomOutbox) return 0;
    try {
      var operation = JSON.parse(UTF8ToString(operationPtr));
      if (operation.path !== '/api/rooms' && operation.path !== '/api/rooms/join') return 0;
      var joining = operation.path === '/api/rooms/join';
      if (joining && (!BusaraOnline.invite || !BusaraOnline.inviteHash)) return 0;
      var body = JSON.stringify({path: operation.path, body: operation.body,
        inviteHash: joining ? BusaraOnline.inviteHash : null});
      var saved = localStorage.getItem(BusaraOnline.roomKey);
      if (saved && saved !== body) return 0;
      localStorage.setItem(BusaraOnline.roomKey, body);
      return localStorage.getItem(BusaraOnline.roomKey) === body ? 1 : 0;
    } catch (_) { BusaraOnline.emit('storageError'); return 0; }
  },
  Busara_ClearRoomOutbox__deps: ['$BusaraOnline'],
  Busara_ClearRoomOutbox: function(commandPtr) {
    if (!BusaraOnline.ownsRoomOutbox) return 0;
    var command = UTF8ToString(commandPtr);
    try {
      var saved = localStorage.getItem(BusaraOnline.roomKey);
      if (saved && JSON.parse(JSON.parse(saved).body).commandId !== command) return 0;
      localStorage.removeItem(BusaraOnline.roomKey);
      return 1;
    } catch (_) { BusaraOnline.emit('storageError'); return 0; }
  },
  Busara_Bind__deps: ['$BusaraOnline'],
  Busara_Bind: function(guestPtr, matchPtr, csrfPtr) {
    BusaraOnline.invite = null;
    BusaraOnline.inviteHash = null;
    BusaraOnline.ownsRoomOutbox = false;
    if (BusaraOnline.releaseRoomOutbox) BusaraOnline.releaseRoomOutbox();
    BusaraOnline.releaseRoomOutbox = null;
    BusaraOnline.guest = UTF8ToString(guestPtr);
    BusaraOnline.match = UTF8ToString(matchPtr);
    BusaraOnline.csrf = UTF8ToString(csrfPtr);
    BusaraOnline.key = 'busara.pending.v1:' + (BusaraOnline.ugs ? BusaraOnline.ugs.scope + ':' : '') +
      BusaraOnline.guest + ':' + BusaraOnline.match;
    history.replaceState(null, '', location.pathname + location.search + '#match=' + encodeURIComponent(BusaraOnline.match));
    BusaraOnline.generation++;
    clearTimeout(BusaraOnline.timer);
    if (BusaraOnline.socket) BusaraOnline.socket.close();
    if (BusaraOnline.releaseOutbox) BusaraOnline.releaseOutbox();
    BusaraOnline.ownsOutbox = false;
    var generation = BusaraOnline.generation;
    if (!navigator.locks) BusaraOnline.emit('storageError');
    else navigator.locks.request(BusaraOnline.key, {ifAvailable: true}, function(lock) {
      if (generation !== BusaraOnline.generation) return;
      if (!lock) { BusaraOnline.emit('outboxLocked'); return; }
      BusaraOnline.ownsOutbox = true;
      try { BusaraOnline.emit('outbox', localStorage.getItem(BusaraOnline.key) || ''); }
      catch (_) { BusaraOnline.emit('storageError'); }
      return new Promise(function(resolve) { BusaraOnline.releaseOutbox = resolve; });
    }).catch(function() { BusaraOnline.emit('storageError'); });
    if (BusaraOnline.ugs) BusaraOnline.emit('polling');
    else BusaraOnline.connect(BusaraOnline.generation);
  },
  Busara_StoreOutbox__deps: ['$BusaraOnline'],
  Busara_StoreOutbox: function(bodyPtr) {
    var body = UTF8ToString(bodyPtr);
    if (!BusaraOnline.key || !BusaraOnline.ownsOutbox) return 0;
    try {
      var existing = localStorage.getItem(BusaraOnline.key);
      if (existing && existing !== body) return 0;
      localStorage.setItem(BusaraOnline.key, body);
      return localStorage.getItem(BusaraOnline.key) === body ? 1 : 0;
    } catch (_) { BusaraOnline.emit('storageError'); return 0; }
  },
  Busara_ClearOutbox__deps: ['$BusaraOnline'],
  Busara_ClearOutbox: function(idPtr) {
    var id = UTF8ToString(idPtr);
    if (!BusaraOnline.ownsOutbox) return 0;
    try {
      var saved = localStorage.getItem(BusaraOnline.key);
      if (saved && JSON.parse(saved).commandId !== id) return 0;
      localStorage.removeItem(BusaraOnline.key);
      return 1;
    } catch (_) { BusaraOnline.emit('storageError'); return 0; }
  },
  Busara_CopyInvite__deps: ['$BusaraOnline'],
  Busara_CopyInvite: function(invitePtr) {
    var value = UTF8ToString(invitePtr);
    navigator.clipboard.writeText(value).then(function() {
      BusaraOnline.emit('copied', 'Invitation copied. Share privately; it cannot recover an existing seat.');
    }).catch(function() {
      BusaraOnline.emit('copied', 'Clipboard permission denied. Allow clipboard access and try again.');
    });
  },
  Busara_VisibleControls__deps: ['$BusaraOnline'],
  Busara_VisibleControls: function(jsonPtr) {
    var value = JSON.parse(UTF8ToString(jsonPtr));
    value.controls.forEach(Object.freeze);
    Object.freeze(value.controls);
    Object.freeze(value);
    // Coordinates are normalized to the actual Unity canvas, origin at top-left.
    // Read-only, visible UI only. No state snapshots, secrets or action functions.
    Object.defineProperty(window, 'busaraVisibleUi', {value: value, configurable: true, writable: false});
  },
  Busara_Dispose__deps: ['$BusaraOnline'],
  Busara_Dispose: function() {
    BusaraOnline.generation++;
    clearTimeout(BusaraOnline.timer);
    if (BusaraOnline.socket) BusaraOnline.socket.close();
    BusaraOnline.requests.forEach(function(request) { request.abort(); });
    BusaraOnline.requests.clear();
    BusaraOnline.receiver = null;
    BusaraOnline.invite = null;
    BusaraOnline.inviteHash = null;
    BusaraOnline.csrf = null;
    BusaraOnline.ugs = null;
    BusaraOnline.ownsOutbox = false;
    if (BusaraOnline.releaseOutbox) BusaraOnline.releaseOutbox();
    BusaraOnline.releaseOutbox = null;
    BusaraOnline.ownsRoomOutbox = false;
    if (BusaraOnline.releaseRoomOutbox) BusaraOnline.releaseRoomOutbox();
    BusaraOnline.releaseRoomOutbox = null;
    delete window.busaraVisibleUi;
  }
});
