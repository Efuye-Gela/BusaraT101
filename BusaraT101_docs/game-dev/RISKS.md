# Risks

| Risk | Likelihood | Impact | Trigger | Mitigation or contingency | Owner |
| --- | --- | --- | --- | --- | --- |
| Client and Cloud Code deployed separately | Medium | High | Protocol or "unknown ruleset" errors in the browser | Deploy module and Web build together; keep reload-to-upgrade path | Release owner |
| v3 powers lack Unity-UI browser coverage | Medium | Medium | A power's decision has no usable control | Domain/adapter tests cover rules; add two-browser cases per power | Online dev |
| Eg auto-boot runs in offline scenes | Low | Medium | Offline input, audio or timing changes | Run offline EditMode suites; add `EgSettings` or disable boot for offline if needed | Online dev |
| UGS access policy regression | Low | High | `smoke-ugs.cjs --policy-check` returns 200 for player Cloud Save reads | Recheck after Unity's fix; keep deny-all policy | Release owner |
| Imagination deviation is surprising to players | Low | Low | Player feedback | Documented in D-002; revisit with design | Design |
| Polling latency on UGS (3–5 s per move measured) | Medium | Medium | Players report slow turns | Latency console; adaptive polling; consider push transport | Online dev |
