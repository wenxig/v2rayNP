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

## 2026-10-18 Mihomo routing fix

- Read AGENTS.md, project instructions and existing plans; preserved unrelated workspace changes and initialized submodule.
- Reproduced the offending generated rule by inspecting the built-in DNS bypass addresses. Prior temporary smoke-test directory no longer exists; current Debug mihomo is available.

- Before fix, new routing regressions failed in 3 cases exactly as expected. After fix, all 6 new cases passed; the full run hit an existing AppManager singleton race (PAC port read 12083 instead of 20811). Rechecking serially. The temporary smoke harness creation had a quoting error, corrected before running it.

- Serial tests passed 154/154. Real mihomo -t passed 4 white/global configs; 2 black configs fail on existing unsupported PROTOCOL,bittorrent. Temporary harness needed built DLL references after an external project-reference error. No client/system proxy was started or changed. Documentation update script had quoting errors; corrected.

- Avalonia Debug build passed with 0 warnings/errors; git diff --check passed. Fix limited to mihomo IP rule generation and 6 routing regression cases; unrelated user changes preserved.

## 2026-10-18 Single-node speed-test skip

- Traced skip to missing AllowTest transition in the single-node config generation overload; preserving original client processes/configuration and unrelated workspace changes.

- Six regression cases reproduced the missing eligibility transition before the fix. After resetting AllowTest at entry and enabling it only after successful config write, all 160 tests passed serially. Avalonia Debug build passed with 0 warnings/errors; git diff --check passed. Actual remote latency was not retested.

## 2026-10-18 ShadowQUIC timeout investigation

- Verified mihomo v1.19.32 and upstream adapter source. All local tests cleared common proxy environment variables, used an explicit temporary loopback SOCKS port, and left the active正版 v2rayN process and system proxy untouched.
- The supplied remote node worked with official ShadowQUIC v0.4.4 (HTTP 204/200), while mihomo timed out. Local official-server testing showed generated mihomo could return the expected HTTP body. Fresh-client rapid-restart tests had two isolated mihomo authentication misses; persistent-client repeat passed 20/20 for both mihomo and official client with zero-RTT disabled.
- Result: confirmed a remote-node interoperability gap, but not a broad severe mihomo defect. Keep alternate-engine introduction deferred pending isolation of the remote node's configuration differences, especially unsupported `initial-mtu`.
