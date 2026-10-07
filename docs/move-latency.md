# Local move latency diagnostics

In a hosted Unity Web build (release or development), open the browser console:

```js
busaraLatency.enable()   // opt in for future local command attempts
busaraLatency.report()   // tables + detached report object
busaraLatency.clear()    // erase samples; stay enabled
busaraLatency.disable()  // stop observing and erase samples
```

Disabled by default; no SDK, network exporter, storage, auth/outbox changes or
polling changes. Reload/disposal disables observation. Clearing diagnostics does
**not** clear a pending game action. Reports contain only local sample numbers,
fixed origin/outcome labels, durations and counts, not commands, choices, versions,
names, identifiers, URLs, payloads or credentials. Do not substitute a network
trace or screenshot containing private game data when sharing this report.

## What the numbers mean

All timestamps use this browser's monotonic `performance.now()` clock. There is
no cross-machine clock subtraction, expiry/TTL measurement or server timing.
The last 100 attempts are retained in memory; `evicted` counts overwritten
attempts since clear. Each metric reports its own observed count, arithmetic
mean and nearest-rank p50/p95 over retained samples. Null means unobserved, not
zero; pending/missing measurements are excluded from that metric's statistics.

- **Start -> request dispatch:** accepted local Submit entry, including command
  serialization and durable outbox write, until the provider request is invoked.
- **Request dispatch -> provider reply:** response body available from the
  transport provider. Legacy includes fetch and body reading; UGS also includes
  adapter/authentication work and any provider-internal requests/retries. It is
  **not** pure HTTP wire latency, server compute time or opponent latency.
- **Provider reply -> verified receipt:** browser-to-Unity delivery and existing
  receipt identity/status/version checks. Rejected but verified receipts count;
  malformed or mismatched receipts never get a verified timestamp.
- **Start -> authorized view applied:** the first locally validated, non-stale
  projection at or beyond an accepted receipt's version. Reconfirming the
  already-current authorized projection counts; rejected receipts do not.
  A concurrent poll may apply the view before the command ACK. Such samples
  set `view_before_receipt`; receipt-to-view is null instead of a negative time.
- **Authorized view applied -> frame boundary** and **Start -> frame boundary:**
  after the screen rebuilds that view (or a superseding one), Unity's
  `WaitForEndOfFrame`, then the first subsequent browser `requestAnimationFrame`
  callback. This is a scheduling boundary, **not proof of displayed pixels,
  GPU completion or remote-player rendering**. Hidden tabs may delay it.

Every explicit retry is a separate `retry` sample. A restored durable outbox
starts a `recovery` sample at the local resend; the original submit time is
unknown. A timeout/abort or lost ACK leaves delivery uncertain, even if the
server committed. No gameplay conclusion may be inferred from these diagnostics.
Only observed verified receipts supply success/rejection labels.

Samples retain at most 16 projection observations while awaiting correlation;
frame tickets/callbacks are also bounded. Extreme delay/rapid updates may leave
a missing or later observation. Clear/disable invalidate in-flight observations,
so old responses/frames cannot populate new samples.

## Client integration and validation

`IOnlineDiagnostics` is optional, provider-neutral observation; the browser
implementation safely no-ops outside WebGL or when unavailable. It is separate
from transport/protocol contracts. After a successful regular UGUI rebuild call
`session.NotifyViewRendered(view.version)`; do not put it behind a debug flag.
The diagnostic bridge must not parse or rewrite accepted response bodies.

From the repository root:

```powershell
node .\Busara\Assets\Busara\Online\Client\Tests\bridge.test.cjs
node .\Busara\Assets\Busara\Online\Client\Tests\latency.test.cjs
```

Run Unity EditMode `OnlineDiagnosticsTests;OnlineClientTests;CommandReplyTests`,
then a release Web build with two real browser profiles to verify console
availability, normal moves, retry/recovery and frame-boundary values.
Unit/mock timing tests do not establish real network or rendering performance.
