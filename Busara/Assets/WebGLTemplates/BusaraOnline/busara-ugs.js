(function(root) {
  'use strict';
  class UgsFailure extends Error {
    constructor(status, code) { super(code); this.status = status; this.code = code; }
  }

  class BusaraUgs {
    constructor(config, platform) {
      this.config = config;
      this.platform = platform;
      this.token = null;
      this.expiresAt = 0;
      this.player = null;
      this.scope = config.projectId + ':' + config.environmentName;
      this.authKey = 'busara.ugs.auth.v1:' + this.scope;
    }

    validate() {
      if (!/^[0-9a-f-]{36}$/i.test(this.config.projectId) ||
          !/^[a-zA-Z0-9_-]{1,50}$/.test(this.config.environmentName) ||
          !/^[a-zA-Z0-9_]{1,50}$/.test(this.config.moduleName))
        throw new UgsFailure(503, 'ugs_configuration_required');
      if (!this.platform.navigator.locks)
        throw new UgsFailure(503, 'browser_locks_required');
    }

    async json(url, options) {
      const response = await this.platform.fetch(url, {...options, credentials: 'omit', cache: 'no-store', redirect: 'error'});
      if (!response.ok) {
        // Do not expose Unity's invocation errors: they may contain upstream stack traces.
        throw new UgsFailure(response.status === 422 ? 503 : response.status, 'ugs_service_unavailable');
      }
      const text = await response.text();
      if (text.length > 2097152) throw new UgsFailure(503, 'ugs_response_invalid');
      try { return JSON.parse(text); }
      catch (_) { throw new UgsFailure(503, 'ugs_response_invalid'); }
    }

    async authenticate(create, signal, force) {
      this.validate();
      return this.platform.navigator.locks.request(this.authKey, async () => {
        let saved;
        try {
          const text = this.platform.localStorage.getItem(this.authKey);
          saved = text ? JSON.parse(text) : null;
          if (saved && (typeof saved.playerId !== 'string' || typeof saved.sessionToken !== 'string'))
            throw new Error('Invalid identity');
        } catch (_) { throw new UgsFailure(503, 'identity_storage_unavailable'); }
        if (!saved && !create) throw new UgsFailure(401, 'guest_unavailable');
        if (!force && saved && this.player === saved.playerId && this.token && this.expiresAt > Date.now() + 60000)
          return;
        const data = await this.json('https://player-auth.services.api.unity.com/v1/authentication/' +
          (saved ? 'session-token' : 'anonymous'), {
          method: 'POST', signal,
          headers: {'ProjectId': this.config.projectId, 'UnityEnvironment': this.config.environmentName,
            'Content-Type': 'application/json'},
          body: JSON.stringify(saved ? {sessionToken: saved.sessionToken} : {})
        });
        if (typeof data.userId !== 'string' || !data.userId ||
            typeof data.idToken !== 'string' || !data.idToken ||
            typeof data.sessionToken !== 'string' || !data.sessionToken ||
            !Number.isFinite(data.expiresIn) || data.expiresIn <= 0 ||
            (saved && saved.playerId !== data.userId))
          throw new UgsFailure(503, 'ugs_identity_invalid');
        const serialized = JSON.stringify({playerId: data.userId, sessionToken: data.sessionToken});
        try {
          this.platform.localStorage.setItem(this.authKey, serialized);
          if (this.platform.localStorage.getItem(this.authKey) !== serialized) throw new Error('Write failed');
        } catch (_) { throw new UgsFailure(503, 'identity_storage_unavailable'); }
        this.player = data.userId;
        this.token = data.idToken;
        this.expiresAt = Date.now() + data.expiresIn * 1000;
      });
    }

    async invoke(operation, payload, matchId, signal) {
      const url = 'https://cloud-code.services.api.unity.com/v1/projects/' +
        encodeURIComponent(this.config.projectId) + '/modules/' + encodeURIComponent(this.config.moduleName) + '/Execute';
      const call = () => this.json(url, {
        method: 'POST', signal,
        headers: {'Authorization': 'Bearer ' + this.token, 'Content-Type': 'application/json'},
        body: JSON.stringify({params: {operation, payload, matchId}})
      });
      let data;
      try { data = await call(); }
      catch (error) {
        if (!(error instanceof UgsFailure) || error.status !== 401) throw error;
        await this.authenticate(false, signal, true);
        data = await call();
      }
      if (!data || !data.output || !Number.isInteger(data.output.status) ||
          data.output.status < 200 || data.output.status > 599 || typeof data.output.body !== 'string')
        throw new UgsFailure(503, 'ugs_response_invalid');
      return data.output;
    }

    async request(method, path, body, signal) {
      try {
        await this.authenticate(method === 'POST' && path === '/api/guest', signal, false);
        let operation, matchId = '';
        if (path === '/api/guest') operation = method === 'POST' ? 'register' : 'guest';
        else if (method === 'POST' && path === '/api/rooms') operation = 'create';
        else if (method === 'POST' && path === '/api/rooms/join') operation = 'join';
        else {
          const route = /^\/api\/rooms\/([a-fA-F0-9-]{36})(\/commands(?:-with-view)?)?$/.exec(path);
          if (!route || (route[2] ? method !== 'POST' : method !== 'GET'))
            throw new UgsFailure(400, 'invalid_route');
          matchId = route[1];
          operation = route[2] === '/commands-with-view' ? 'commandWithView' : route[2] ? 'command' : 'view';
        }
        const reply = await this.invoke(operation, body || '{}', matchId, signal);
        if (operation === 'create' && reply.status === 200) {
          const room = JSON.parse(reply.body);
          if (typeof room.inviteUrl !== 'string' || !room.inviteUrl.startsWith('#invite='))
            throw new UgsFailure(503, 'ugs_response_invalid');
          room.inviteUrl = this.platform.location.origin + this.platform.location.pathname + room.inviteUrl;
          return {status: reply.status, body: JSON.stringify(room)};
        }
        return reply;
      } catch (error) {
        if (error instanceof UgsFailure)
          return {status: error.status, body: JSON.stringify({code: error.code})};
        // Network failure, abort or malformed delivery is uncertain, never an ACK.
        return {status: 0, body: ''};
      }
    }
  }
  root.BusaraUgs = BusaraUgs;
  if (typeof module !== 'undefined' && module.exports) module.exports = {BusaraUgs};
})(typeof window !== 'undefined' ? window : globalThis);
