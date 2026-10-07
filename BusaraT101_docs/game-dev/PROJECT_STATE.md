# Project State

Last verified: 2026-10-07

## Direction

- Game promise: a strategic kingdom board game where hidden kingdoms, forged
  virtues and reactive powers decide who meets their goal first.
- Target player and platform: offline Unity (all rules, bots); private
  two-player online match in Unity Web.
- Process profile: standard
- Overall stage: online feature-complete for kingdom powers (v3), with the new
  online presentation verified locally; not yet deployed.
- Hard constraints: Unity 6000.3.6f1; server-authoritative online state; UGS
  is the default backend and legacy ASP.NET/PostgreSQL is opt-in; offline game
  untouched; no commit, push or deploy without approval.

## Current Iteration

- Objective: all fifteen powers online, online client on EgComponents with
  one consistent style from the authored art.
- Hypothesis: a generated, authored Eg scene keeps the online UI predictable
  and testable while matching the board/panel art.
- Playable increment: new rooms use `busara-online-v3`; online client is the
  generated `OnlineMVP` scene (Entry/Lobby/Match pages).
- Success signals: domain, UGS, PostgreSQL, Unity EditMode and two-browser
  suites pass; screenshots show one consistent style.
- Review point: after a UGS cloud deployment and a live two-browser match.

## Focus

### NOW

- Approve and deploy: the Cloud Code module and the Web build must ship
  together (v3 views need the new client).
- Reverify the UGS access policy (`smoke-ugs.cjs --policy-check`) after Unity's
  fix.

### NEXT

- Two-browser coverage of each v3 power through the Unity UI (currently
  covered by domain/adapter tests, plus browser coverage of the earlier rules).
- Offline UI migration to Eg and the same theme.
- Disasters online; reconnect-aware bots; matchmaking or spectators if wanted.

## Risks and Unknowns

- See [RISKS.md](RISKS.md).

## Evidence and Links

- 2026-10-07: Unity EditMode online fixtures 58/58; legacy two-browser Unity
  suite 4/4 on the Eg client (development build, loopback PostgreSQL).
- Earlier: domain 84 tests, UGS adapter 63/63, PostgreSQL server 25/25, JS
  bridge/transport passing.
- Rules and protocol: [online-multiplayer.md](../../docs/online-multiplayer.md);
  hosting: [multiplayer-architecture-and-hosting.md](../../docs/multiplayer-architecture-and-hosting.md).
