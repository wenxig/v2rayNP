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

## Official ShadowQUIC engine integration

- Added native client YAML, mixed-port +8, sing-box front end and process/test dispatch, official release mappings and raw-binary update installation. Added deterministic startup/import migration from the retired ShadowQUIC/mihomo pair.
- Added initial-MTU model/editor/URI support in both hosts and explicit validation of unsupported legacy options. Validation in progress; first build exposed a SQLite helper argument mismatch, corrected to its array contract.

- Final shared tests: `dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug --no-restore -- --maximum-parallel-tests 1` passed 173/173.
- `dotnet build v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Debug` passed; after Release restore a Debug `--no-restore` attempt missed conditional Avalonia diagnostics, resolved by the normal restore-enabled Debug build. Existing GlobalHotKeys CS8625 warning remains (submodule unchanged).
- `dotnet build v2rayN/v2rayN/v2rayN.csproj -c Debug --no-restore -p:EnableWindowsTargeting=true` passed; Windows runtime behavior remains untested on macOS.
- `dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r osx-x64 -p:SelfContained=true -o /private/tmp/v2raynp-shadowquic-publish` passed (existing GlobalHotKeys warning).
- Generated normal and speed-test YAML through CoreConfigHandler; official v0.4.4 parsed and ran them. `sing-box check -c generated-front.json` passed. Initial integration returned expected local HTTP bodies (4/4) and remote HTTP 204/200 (5/5) through native and sing-box listeners. Application ProcessService + ConnectionHandler.GetRealPingTime returned 222 ms.
- Repeated integration: local 4/4 still passed; all 5 remote requests timed out. Final control tested original upstream client YAML and generated YAML with the same official binary; original returned curl 35 (TLS failure), generated returned curl 28 (timeout). Remote reliability remains unverified; success in one run is not treated as a stability guarantee.
- All core processes used only task-owned loopback ports; common proxy environment variables were cleared and curl used explicit proxies. System proxy, TUN, and the original v2rayN process were not modified. All temporary test processes were stopped.
- Verified release asset SHA-256 and placed the official binary at `v2rayN/v2rayN.Desktop/bin/Debug/net10.0/bin/shadowquic/shadowquic` for the user's manual retest; this ignored binary and all credential-bearing smoke inputs are excluded from Git.
- `git diff --check` passed. TUN/UDP live forwarding, Windows/Linux execution and long-term remote stability remain unverified. Unrelated `agent.md`, `AGENTS.md`, and `live-node-validation.md` edits are preserved.

## Separated ShadowQUIC acceptance

- Started a fresh isolated run using task-owned ports 24180/24188/24221/24230/24443/24580; proxy environment variables will be removed. Testing the local official server separately from the supplied remote ShadowQUIC node, with original upstream configuration as a remote control.

- Generated node configs through CoreConfigHandler with original zero-rtt=true and other supplied fields, sing-box check passed. Corrected a temporary harness YamlMappingNode setter compile error before running any network checks; product source unchanged.
- Ran `python3 /private/tmp/v2raynp-sq-acceptance-20261018/validate.py`: all 7 SOCKS5 listener greetings passed, loopback HTTP body 20/20 passed, loopback example.com 4/4 passed, loopback Google 0/12 failed like the direct host baseline. Supplied remote Google 11/12 passed, example.com 0/4 failed with curl 35; original config later passed 4/4.
- Ran `python3 /private/tmp/v2raynp-sq-acceptance-20261018/interleave.py`: generated and original configs each passed 6/6 alternating HTTPS requests. Generated app real-ping 232/230/306 ms; original 232/227/228 ms. All failures from the first run remain counted in the report.
- Final report: shadowquic-validation.md; remote generated aggregate Google 14/15, example.com 3/7; original control 10/10. Local app delays 1–14 ms, remote mixed 196–200 ms. No system proxy or TUN changes; all task processes and loopback listeners stopped. Only documentation changed, so no product rebuild or unit-test rerun was needed.

## Evidence-driven follow-up

- Re-read instructions, existing plans, test report and source. Preserved unrelated user changes; original client/system proxy remain untouched. Memory lookup found no relevant repository entries.
- Investigating native-default fidelity and real-ping result handling; no server reliability conclusion will be inferred solely from the earlier small unequal samples.

- Original/generated comparison succeeded after correcting a temporary harness Dictionary TryGetValue call; no node values printed. The first test run passed 181/185 and reproduced four target failures. Test HTTP responses now include a small deterministic response delay to distinguish HTTP status behavior from sub-millisecond rounding.
- Implemented independent sample failure handling inside the existing total deadline, successful-status validation, response disposal and 1 ms minimum. Preserved caller cancellation. Native absent zero-rtt now defers to upstream; editor shows upstream default and persists explicit false; URI export preserves nullable booleans.

- Editor tests required ReactiveUI's builder initialization; fixed the fixture using the installed builder API after an initial missing initialization error and an extension-import compile error. Editor initialization/save, URI tri-state round-trip, HTTP/network failure handling, timeout and caller cancellation all passed in the 191-test serial run. Strengthened editor checks to verify database persistence, then reran the suite.
- Avalonia Debug and Windows-targeted WPF Debug builds passed with zero warnings/errors. Live loopback response and error injection verified fixes through the actual official core. This run's remote curl comparisons failed with both original and generated configs; app probes remained intermittent, recorded without claiming transport stability.
- sing-box config check and 7 SOCKS greetings passed. All test processes were stopped, TCP listeners confirmed closed; system proxy, TUN and original v2rayN were untouched. Report and README updated; node configs/logs remain private under /private/tmp.

- Final shared suite including database persistence checks: 191/191 passed. Final diff checks passed; source review confirmed no endpoint/credential changes, HTTP target fallback or extra attempts. Completed work ready for signed commit.

## Functional remediation design

- Inspected current launch/readiness/caller system-proxy paths, custom import fallback, mihomo routing/group generation and release SDK settings.
- Wrote remediation-plan.md: A lifecycle, B diagnostics, C native import and parameter fidelity, D routing/group semantics, E multi-core groups/chains, F distribution and runtime acceptance. Includes explicit production gates and continued original-client isolation.
- This task changes documentation only. Product implementation, live proxy changes, builds and network tests were not run; final verification is plan coverage and git diff --check. Unrelated agent.md, AGENTS.md and live-node-validation.md changes are preserved.

## Remediation execution

- User authorized implementation of remediation-plan.md. Read repository instructions, README and project files; initialized submodule; .NET SDK 10.0.402 available. Preserved pre-existing agent.md deletion, AGENTS.md and live-node-validation.md edits.
- Applying planning-with-files. No relevant memory registry entries. Lifecycle inspection confirms incomplete readiness and unconditional system proxy updates; native custom imports must retain their contract.

- Lifecycle implementation compiled. First regression build caught TUnit assertions missing await and using Be rather than BeEqualTo; corrected before rerunning. Initial tool payload quoting errors made no product changes.

- Lifecycle regression suite: 200/200 passed after final cleanup review. Avalonia Debug and WPF Debug with EnableWindowsTargeting passed, both with zero warnings/errors. No system proxy, TUN, original-client process or real user credentials were used by tests.

- Stage B: added per-sample HTTP/DNS/TLS/proxy/connection/timeout classifications and sanitized target summaries in existing logs and ProfileEx test messages. Fastest successful sample retained. Shared tests 205/205 passed, including HTTP 503 and exception/URI secret exclusion. Core version metadata and actual proxy/TLS injection remain acceptance follow-ups.
