(function (host) {
  'use strict';
  const capacity = 100, observationLimit = 16;
  let enabled = false, sequence = 0, frameSequence = 0, epoch = 0, evicted = 0;
  let samples = [];
  const frames = new Map(), animations = new Set();
  const now = () => host.performance.now();
  const version = value => typeof value === 'string' && /^\d+$/.test(value);
  const compare = (a, b) => {
    a = a.replace(/^0+/, ''); b = b.replace(/^0+/, '');
    return a.length === b.length ? (a > b ? 1 : a < b ? -1 : 0) : a.length - b.length;
  };
  const find = id => enabled ? samples.find(sample => sample.id === id) : null;
  const alive = sample => enabled && samples.includes(sample);
  function clear() {
    epoch++;
    for (const id of animations) {
      try { host.cancelAnimationFrame(id); } catch (_) { /* Optional observer only. */ }
    }
    animations.clear(); frames.clear(); samples = []; evicted = 0;
  }
  function matchView(sample) {
    if (sample.outcome !== 'accepted' || !sample.version) return;
    for (const seen of sample.views) {
      if (compare(seen.version, sample.version) < 0) continue;
      if (sample.applied === undefined || seen.applied < sample.applied) sample.applied = seen.applied;
      if (seen.boundary !== undefined && (sample.boundary === undefined || seen.boundary < sample.boundary))
        sample.boundary = seen.boundary;
    }
  }
  const hooks = {
    begin(origin) {
      if (!enabled || !['submit', 'retry', 'recovery'].includes(origin)) return 0;
      const sample = {id: ++sequence, origin, start: now(), outcome: 'pending', views: []};
      if (samples.length === capacity) { samples.shift(); evicted++; }
      samples.push(sample);
      return sample.id;
    },
    dispatch(id) {
      const sample = find(id);
      if (sample && sample.dispatch === undefined) sample.dispatch = now();
    },
    reply(id) {
      const sample = find(id);
      if (sample && sample.reply === undefined) sample.reply = now();
    },
    accepted(id, value) { hooks.receipt(id, value, true); },
    rejected(id, value) { hooks.receipt(id, value, false); },
    receipt(id, value, accepted) {
      const sample = find(id);
      if (!sample || !version(value) || sample.outcome !== 'pending') return;
      sample.receipt = now(); sample.version = value;
      sample.outcome = accepted ? 'accepted' : 'rejected';
      matchView(sample);
      if (!accepted) sample.views = [];
    },
    failed(id, outcome) {
      const sample = find(id);
      const allowed = ['not_dispatched', 'aborted', 'delivery_uncertain', 'http_rejected', 'unverified_receipt'];
      if (!sample || sample.outcome !== 'pending' || !allowed.includes(outcome)) return;
      sample.outcome = outcome; sample.views = [];
    },
    view(_, value) {
      if (!enabled || !version(value)) return;
      const time = now();
      for (const sample of samples) {
        if (!['pending', 'accepted'].includes(sample.outcome) || sample.boundary !== undefined) continue;
        if (!sample.views.some(view => view.version === value)) {
          if (sample.views.length === observationLimit) sample.views.shift();
          sample.views.push({version: value, applied: time});
        }
        matchView(sample);
      }
    },
    prepareFrame(_, value) {
      if (!enabled || !version(value)) return 0;
      const observations = [];
      for (const sample of samples) {
        const view = sample.views.find(item => item.version === value && !item.queued);
        if (view) { view.queued = true; observations.push({sample, view}); }
      }
      if (!observations.length) return 0;
      const id = ++frameSequence;
      if (frames.size === 128) frames.delete(frames.keys().next().value);
      frames.set(id, {epoch, observations});
      return id;
    },
    frame(id) {
      const frame = frames.get(id); frames.delete(id);
      if (!enabled || !frame || frame.epoch !== epoch || animations.size >= 128) return;
      const animation = host.requestAnimationFrame(() => {
        animations.delete(animation);
        if (!enabled || frame.epoch !== epoch) return;
        try {
          const time = now();
          for (const {sample, view} of frame.observations) {
            if (!alive(sample)) continue;
            view.boundary = time; matchView(sample);
          }
        } catch (_) { /* The frame observer cannot interfere with Unity. */ }
      });
      animations.add(animation);
    }
  };
  const metrics = {
    start_to_request_dispatch_ms: ['start', 'dispatch'],
    request_dispatch_to_provider_reply_ms: ['dispatch', 'reply'],
    provider_reply_to_verified_receipt_ms: ['reply', 'receipt'],
    start_to_verified_receipt_ms: ['start', 'receipt'],
    start_to_authorized_view_applied_ms: ['start', 'applied'],
    verified_receipt_to_authorized_view_applied_ms: ['receipt', 'applied'],
    authorized_view_applied_to_frame_boundary_ms: ['applied', 'boundary'],
    start_to_frame_boundary_ms: ['start', 'boundary']
  };
  function report() {
    const rows = samples.map(sample => {
      const row = {sample: sample.id, origin: sample.origin, outcome: sample.outcome,
        view_before_receipt: sample.applied !== undefined && sample.receipt !== undefined &&
          sample.applied < sample.receipt};
      for (const [label, [from, to]] of Object.entries(metrics)) {
        const duration = sample[to] - sample[from];
        row[label] = Number.isFinite(duration) && duration >= 0 ? Math.round(duration * 100) / 100 : null;
      }
      return row;
    });
    const summary = {};
    for (const label of Object.keys(metrics)) {
      const values = rows.map(row => row[label]).filter(value => value !== null).sort((a, b) => a - b);
      const percentile = fraction => values.length ? values[Math.ceil(values.length * fraction) - 1] : null;
      summary[label] = {count: values.length,
        mean: values.length ? Math.round(values.reduce((a, b) => a + b, 0) / values.length * 100) / 100 : null,
        p50: percentile(0.5), p95: percentile(0.95)};
    }
    const result = {enabled, capacity, retained: rows.length, evicted, summary, samples: rows};
    try {
      host.console.info('Busara local move latency (ms): provider round trip includes adapter/auth/body work; ' +
        'not server compute or opponent latency. Frame boundary is not pixels/GPU. Null means unobserved.');
      host.console.table(summary); host.console.table(rows);
    } catch (_) { /* Reporting must also work without a console. */ }
    return result;
  }
  host.busaraLatency = Object.freeze({
    enable() { enabled = true; return 'Busara latency enabled for future attempts only (memory only).'; },
    disable() { enabled = false; clear(); return 'Busara latency disabled; samples cleared.'; },
    clear, report
  });
  // No request bodies, IDs, URLs or credentials are accepted by the diagnostic bridge.
  host.__busaraLatency = Object.freeze({
    observe(stage, id, value) {
      try {
        if (!Object.prototype.hasOwnProperty.call(hooks, stage)) return 0;
        return hooks[stage](stage === 'begin' ? value : id, value) || 0;
      } catch (_) { return 0; }
    },
    dispose() { enabled = false; clear(); }
  });
})(window);
