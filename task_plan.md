# Protocol Support Implementation

## Goal

Add complete SSR, Mieru, ShadowQUIC, and Juicity support using mihomo for the first three protocols and the official Juicity client for Juicity.

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

- SSR, Mieru, and ShadowQUIC run through mihomo.
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

## Mihomo routing regression (2026-10-18)

- [completed] 9. Normalize bare IPv4/IPv6 rule addresses, add routing regression coverage, validate generated YAML with mihomo, and sign the fix.

## Next Step

Completed single-node speed-test eligibility fix and validation; restart the rebuilt desktop to retest actual node latency.

## Single-node speed-test skip regression (2026-10-18)

- [completed] 10. Mark successfully written single-node speed-test configurations eligible; cover mihomo/Juicity success and failure, run tests/build, and sign the fix.

## ShadowQUIC remote timeout investigation (2026-10-18)

- [completed] 11. Compare parameters, test isolated listeners with longer timeout and zero-RTT variants, record conclusions and sign findings. The supplied remote node succeeds with official ShadowQUIC and times out with mihomo, while local control and steady-state runs succeed; this is an interoperability gap, not yet evidence of a general severe mihomo defect.

- [completed] 12. Run local mihomo and official ShadowQUIC server controls with generated client config and real local HTTP responses. Use no system proxy and an explicit loopback SOCKS listener. The steady-client test passed 20/20 for each client; defer introducing another engine until the remote-profile mismatch is isolated.
