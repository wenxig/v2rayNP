# Protocol Support Implementation

## Goal

Support SSR and Mieru through mihomo, and ShadowQUIC and Juicity through their official standalone clients.

## Phases

- [completed] 1. Add protocol models, registries, URI format handlers, and persistence dispatch.
- [completed] 2. Add core capability validation and mihomo/Juicity configuration generation.
- [completed] 3. Add node editor fields, core filtering, and localized UI strings.
- [completed] 4. Add focused tests, update support documentation, and run validation.
- [completed] 5. Review the diff and create a signed Conventional Commit.
- [completed] 6. Reserve mixed-port +7 for Juicity, connect the managed SOCKS front end, validate, and sign the follow-up commit.
- [completed] 7. Validate supplied nodes for connectivity and real HTTPS responses without recording credentials.
- [completed] 8. Move the new-configuration mixed port to 20808 to avoid the original client's default port.

## Decisions

- SSR and Mieru run through mihomo; ShadowQUIC now moves to its official standalone client.
- Juicity runs through the already registered official `juicity-client`.
- Xray and sing-box reject these protocols; no silent fallback or semantic mapping.
- Existing user changes are preserved.

## Validation Limits

- `dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug --no-restore` passes (146/146).
- The desktop and WPF builds and package-script syntax checks passed before commit, as recorded in `progress.md`.
- Download retry succeeded: mihomo v1.19.32 accepted all three generated YAML files; Juicity v0.5.0 started with generated JSON and returned SOCKS5 greeting `0500`.
- sing-box v1.14.3 accepted generated ordinary and TUN front-end configurations; Juicity and sing-box ran together on ports 19087 and 19080 and each returned SOCKS5 greeting `0500`.
- Supplied Mieru, Hysteria, and Hysteria2 nodes passed real HTTPS response checks; supplied Juicity and ShadowQUIC nodes timed out. See `live-node-validation.md`. UDP forwarding and actual TUN traffic remain unverified.

## Completion

Implementation and signed commit are complete. Existing unrelated `agent.md` deletion and untracked `AGENTS.md` are preserved.

## Default port follow-up

- New configurations now use mixed port 20808; existing saved inbound ports are preserved.
- Derived listeners move with the configured mixed port, including Juicity at mixed port +7 and PAC at mixed port +3.
- The original v2rayN process was not stopped, restarted, or modified during this change.
- The previous Juicity and ShadowQUIC timeouts are inconclusive because the original client stayed online for Codex networking; isolated listener ports remove direct local-port conflict as evidence, but do not eliminate concurrent network effects.
- `dotnet test ... --no-restore` passes 147/147; ServiceLib and Avalonia Debug builds pass with 0 warnings and 0 errors.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
| SQLiteHelper.ExecuteAsync accepts only SQL text | 2 | Use integer enum constants; added persisted migration test. |
| Debug build after Release restore omitted conditional diagnostics dependency | 1 | Rebuild Debug with restore; passed. |

## Mihomo routing regression (2026-10-18)

- [completed] 9. Normalize bare IPv4/IPv6 rule addresses, add routing regression coverage, validate generated YAML with mihomo, and sign the fix.

## Next Step

Official ShadowQUIC integration is implemented and verified; restart the rebuilt desktop to load the migrated mapping.

## Single-node speed-test skip regression (2026-10-18)

- [completed] 10. Mark successfully written single-node speed-test configurations eligible; cover mihomo/Juicity success and failure, run tests/build, and sign the fix.

## ShadowQUIC remote timeout investigation (2026-10-18)

- [completed] 11. Compare parameters, test isolated listeners with longer timeout and zero-RTT variants, record conclusions and sign findings. The supplied remote node succeeds with official ShadowQUIC and times out with mihomo, while local control and steady-state runs succeed; this is an interoperability gap, not yet evidence of a general severe mihomo defect.

- [completed] 12. Run local mihomo and official ShadowQUIC server controls with generated client config and real local HTTP responses. Use no system proxy and an explicit loopback SOCKS listener. The steady-client test passed 20/20 for each client; defer introducing another engine until the remote-profile mismatch is isolated.

## Official ShadowQUIC engine integration (2026-10-18)

- [completed] 13. Add a first-class official ShadowQUIC core type, release/download metadata, node config generator, default mapping and capability validation; route through the existing managed mixed-port front end.
- [completed] 14. Add speed-test and single-node process handling, tests and UI core selection/localization; verify official CLI config and local HTTP response without system proxy.
- [completed] 15. Run serial ServiceLib.Tests, Avalonia build, diff checks, update these records and create a signed Chinese Conventional Commit while preserving unrelated workspace changes.

## Next Step

Restart the rebuilt desktop and retest ShadowQUIC using the installed official core. Remote repeatability remains limited by observed TLS/QUIC path failures, reproduced with the original upstream config.

## Official engine validation

- Serial shared tests: 173/173 passed. Avalonia Debug and WPF Debug builds passed. macOS x64 self-contained Release publish passed. GlobalHotKeys has its pre-existing CS8625 warning; no submodule sources were edited.
- Real official v0.4.4 and sing-box processes: local HTTP body verified; initial remote direct and front-end requests returned 204/200, speed-test listener returned 204. Application ProcessService + ConnectionHandler measured 222 ms.
- A repeat run passed local checks but timed out remotely. A final side-by-side control failed with both the original official YAML and newly generated YAML. Remote stability is not accepted as proven. No system proxy or TUN settings were changed.

## Separated ShadowQUIC acceptance (2026-10-18)

- [completed] 16. Independently test a loopback official server and the supplied remote ShadowQUIC profile through generated native, mixed and speed-test listeners; record response validation, timings and repeated results without system proxy. Save the redacted report and sign a progress commit.

- Stage 16 result: local body 20/20 and local-server example.com 4/4 passed; host direct Google and local-server Google timed out. Supplied remote via generated config passed Google 14/15 and example.com 3/7 across initial and interleaved runs; original config control passed 10/10. See shadowquic-validation.md. Remote stability remains partial.

## Evidence-driven ShadowQUIC follow-up (2026-10-18)

- [completed] 17. Compare the original/generated outbound semantics and trace real-ping false negatives; add deterministic behavioral regressions before changing defaults or request handling.
- [completed] 18. Implement confirmed fixes, verify isolated local and interleaved supplied-node responses without system proxy, run shared tests and both desktop builds.
- [completed] 19. Update the redacted validation report, review the diff and create a signed Chinese Conventional Commit.

## Next Step

Isolate actual semantic differences and reproduce the real-ping failure behavior with local HTTP responses.

## Next Step

Run behavioral regressions and isolated live response comparisons with rebuilt ServiceLib.

## Next Step

Confirm final persistence regressions, review only task files and sign the implementation commit.

## Next Step

Restart the rebuilt desktop to load the fixes; retain remote stability as unverified.

## Functional remediation design (2026-10-18)

- [completed] 20. Inspect the live lifecycle, raw-config import, routing/group generation and release SDK settings; write remediation-plan.md with priorities, dependencies, scope and acceptance evidence. Planning only; proposed product work has not started.

## Next Step

The repair design is ready for review in remediation-plan.md. Product implementation remains outside this planning task.

## Remediation implementation (2026-10-18)

### Phase A: Lifecycle and readiness
**Status:** complete (code and local regression; privileged/platform acceptance remains in F)

### Phase B: Safe actionable diagnostics
**Status:** in_progress

### Phase C: Native config import and fidelity
**Status:** pending

### Phase D: Routing and strategy semantics
**Status:** pending

### Phase F: Distribution and acceptance evidence
**Status:** pending

### Phase E: Managed combinations and capability boundaries
**Status:** pending

## Next Step
Implement and test the coordinated lifecycle before extending import/routing/group behavior. External platform and 24-hour acceptance must stay pending until actual evidence exists.

## Next Step
Finish diagnostic classification and redaction regressions; then native import/schema fidelity.
