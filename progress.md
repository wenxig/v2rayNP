# Progress

## 2026-10-17

- Changed new-configuration mixed-port default to 20808; existing saved inbound ports remain unchanged. Juicity stays at mixed port +7 and PAC at mixed port +3.
- Verified `git diff --check`, ServiceLib.Tests (147/147), ServiceLib Debug build, and Avalonia Debug build (0 warnings/errors). Original v2rayN was left running and untouched.
- Clarified that prior Juicity/ShadowQUIC remote timeouts are inconclusive under concurrent original-client operation; isolated local listeners prevent assuming a direct port collision.

- Read repository instructions and confirmed the cross-platform ServiceLib architecture.
- Verified Juicity support status in Xray, mihomo, and sing-box; selected the official Juicity CLI.
- Started implementation planning files.


- Resumed after interruption; checked workspace, read planning skill, synchronized/initialized submodule.
- Retrieved upstream Mieru proto/url definitions, mihomo adapters/examples, official Juicity JSON schema and share specification.
- ServiceLib compiled before latest changes; tests and desktop builds still pending.
- Implemented all four protocol models, URI handlers, persistence dispatch, core capability checks, mihomo/Juicity config generation, speed-test dispatch, editor fields, WPF/Avalonia menus and localized resources.
- Added URI field round-trip, malformed-input, capability, and config-generation tests; serial ServiceLib test run passes 141/141.
- Added official Juicity v0.5.0 platform download mappings and synchronized README/package support descriptions.
- Final desktop/WPF builds and diff review remain before signed commit.
- Signed commit `a696d658` created after serial builds, tests, package-script syntax checks, and diff validation.
- Re-ran `dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug --no-restore`: 141/141 passed.
- Attempted to download mihomo v1.19.32 and Juicity v0.5.0 for real-core smoke tests; GitHub release metadata was reachable, but asset download failed with `curl: (35) LibreSSL SSL_connect: SSL_ERROR_SYSCALL` to `release-assets.githubusercontent.com`.
- Marked implementation and commit phases complete. Real-core smoke tests and server connectivity remain unverified for the reasons above.

## Download retry and listener integration

- Downloaded and extracted mihomo v1.19.32 and Juicity v0.5.0 under `/tmp/v2raynp-protocol-smoke`.
- Generated configurations through ServiceLib: all three mihomo `-t` checks passed; Juicity started and returned SOCKS5 greeting `0500`, then was terminated.
- User requested a fixed listener offset; reserved mixed-port +7 for Juicity and connected the existing managed sing-box SOCKS front end. Validation pending.
- Follow-up tests pass 146/146, including offset listener, ordinary/TUN front end, port limits, and speed-test port override.
- Avalonia and WPF Debug builds pass with 0 warnings and 0 errors.
- sing-box v1.14.3 accepts generated ordinary and TUN front-end configurations. Juicity and sing-box simultaneously listen on 19087 and 19080 and each returns SOCKS5 greeting `0500`; smoke-test processes were terminated.
- Fixed sing-box inbound generation to read the context configuration snapshot rather than mutable global configuration after regression tests exposed mismatched ports.
- The temporary harness needed its SQLite native library and a routing record; corrected only under `/tmp`. sing-box asset downloads timed out twice, then succeeded on retry.
- Real server connectivity, UDP forwarding, and actual TUN traffic remain unverified.

## Live node acceptance

- User supplied local node files for connectivity and real-response tests. Inventory is stored only in a restricted temporary directory; configuration contents and credentials will not be committed.

- Five client configurations passed real-core checks. Mieru, Hysteria, and Hysteria2 returned HTTP 200 with valid certificates and expected content from Example Domain and Cloudflare trace.
- Juicity timed out through both the generated sing-box front end and the official client directly with the supplied original JSON; ShadowQUIC timed out through mihomo. These are recorded as failed acceptance, not successful protocol connectivity.
- Added `live-node-validation.md` without credentials or server identifiers. All temporary live-test cores were stopped. System proxy and TUN were not enabled.
