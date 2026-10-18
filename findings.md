# Findings

- `EConfigType`, `Global.ProtocolShares`, `Global.ProtocolTypes`, `ConfigHandler.AddServer`, `FmtHandler`, `ProtocolExtraItem`, and `NodeValidator` are the shared protocol integration points.
- `CoreConfigHandler` currently dispatches regular nodes only to Xray or sing-box; mihomo is limited to custom YAML input.
- `CoreConfigClashService` already owns mihomo YAML normalization, ports, routing, TUN, and mixin handling.
- `ECoreType.juicity` is already registered with `juicity-client`, `run -c {0}`, and release metadata in `CoreInfoManager`.
- mihomo's current configuration example includes outbound types `ssr`, `mieru`, and `shadowquic`, but no `juicity` type.
- The official Juicity client uses JSON with `listen`, `server`, `uuid`, `password`, `sni`, `allow_insecure`, and `congestion_control`; it exposes local SOCKS5/HTTP and runs as `juicity-client run -c config.json`.
- The working tree already has `agent.md` deleted and `AGENTS.md` untracked; these are preserved.


## Verified contracts (2026-10-17)
- Official Mieru `pkg/appctl/url.go` uses Protobuf ClientConfig encoded in standard Base64 for mieru://; human-readable scheme is mierus:// with profile/port/protocol query fields. Earlier URI implementation was incorrect.
- Mihomo ShadowQuicOption requires array ALPN/QUICVersions; Mieru port and port-range are mutually exclusive. ShadowQUIC uses JLS and its mihomo adapter does not expose skip-cert-verify.
- Speed tests require dedicated dispatch for both new cores and single-client processes for Juicity; batching alone is insufficient.
- UI patch failed context matching and made no changes.
- Official Juicity v0.5.0 provides Windows x86_64/arm64, Linux x86_64/arm64/riscv64, and macOS x86_64/arm64 archives; CoreInfo now maps these release assets and uses `run -c {0}`.
- Custom core settings retain the full legacy core list; protocol-specific filtering applies only to supported protocol types.
- Groups containing extended-protocol children now fail validation instead of silently downgrading child errors to warnings.

## Juicity listener integration

- Reserved `EInboundProtocol.juicity = 7`: the official client listens at mixed-port +7 on loopback.
- Reused the existing managed pre-SOCKS sing-box context for public mixed-port entry and routing/TUN generation; Juicity itself is never launched as a TUN core.
- Startup waits for the internal listener before starting the front end; speed tests keep their explicitly allocated listener port.
- Official mihomo v1.19.32, Juicity v0.5.0, and sing-box v1.14.3 local checks passed; these checks do not establish remote connectivity or actual TUN/UDP forwarding.

## Mihomo bare IP regression

- The built-in custom_routing_white DNS bypass rule contains bare IPv4/IPv6 addresses. BuildNodeRules emits these as IP-CIDR/IP-CIDR6 without host prefixes; mihomo rejects rules[4] with payloadRule error before connecting to the node.
- Existing sing-box code already handles single-address prefixes. The mihomo fix must preserve existing subnet prefixes and GEOIP/private rules.

- Real mihomo accepts generated white/global templates for both normal and speed-test configs (4/4). Black template separately fails on unsupported PROTOCOL,bittorrent; no approximation introduced.

## Single-node speed-test eligibility

- RunRealPingAsync/RunUdpTestAsync skip items with AllowTest=false. Existing Xray/sing-box batch generators set AllowTest=true after generating each eligible node; the single-node CoreConfigHandler overload used by mihomo/Juicity never sets the flag. The core can parse its config while the request is still skipped.

## ShadowQUIC remote timeout

- User log shows successful configuration load and a proxied Google dial timing out. Real-ping total timeout is 5 seconds. Source config enables zero-RTT, h3, BBR and initial-mtu=1300; mihomo exposes the first three, but not initial-mtu.
- The same remote node works through official ShadowQUIC v0.4.4: Google generate_204 returned HTTP 204 and example.com returned HTTP 200 with certificate verification enabled. mihomo v1.19.32 timed out against both, including with zero-RTT disabled and explicit QUIC v1/v2 variants. This confirms a remote-node interoperability difference, but does not yet isolate its cause.
- Local official server control (loopback UDP 19443, local HTTP responder) was exercised without system proxy settings. Each run explicitly cleared common proxy environment variables; curl used only the temporary `socks5h://127.0.0.1:19080` listener and `--noproxy ''`. Alternating fresh clients produced 22/24 successful mihomo requests (11/12 with zero-RTT off, 11/12 on) and 24/24 official-client requests. Both mihomo misses were isolated authentication failures during rapid process restarts; the client log showed no upstream dial error. A steady-client follow-up passed 20/20 requests for mihomo and 20/20 for official client, with zero-RTT disabled.
- Local control proves generated mihomo configuration can carry a real HTTP response through ShadowQUIC, and the one-off misses were not reproduced with a persistent client. Combined with the remote-node result, this is not enough to claim a general severe mihomo ShadowQUIC defect. However, the actual remote server configuration is more compatible with the official client, and mihomo lacks the source profile's `initial-mtu` option, so keep that interoperability gap open before treating the protocol support as production-verified.
- User authorized replacing mihomo as the default ShadowQUIC engine with the official standalone client. The existing Juicity path demonstrates the intended integration: dedicated core enum and downloader, per-node config generation, local loopback SOCKS listener at a mixed-port offset, and the managed sing-box front end for the public mixed port.

- Official v0.4.4 source uses strict YAML schemas: `inbounds`/`outbounds`, required tags, `server-name`, `initial-mtu`, `mtu-discovery`, `over-stream`, and `new-reno` congestion spelling. No skip-cert-verify, QUIC-version selector, or mihomo bandwidth/window fields are exposed; unsupported non-default settings are rejected. ShadowQUIC listener reserves mixed-port +8.

- Official v0.4.4 asset mappings were checked against the GitHub release. macOS x86_64 binary SHA-256 matched release metadata: `6b42796026c41da7c39a5b429ea0251769c435b2b2121bc9efa99a71af7d8dd5`. The binary is installed in the current Debug host `bin/shadowquic/shadowquic` (ignored build output).
- Integrated live validation initially passed all 9 local/remote checks, and application real-ping measured 222 ms. A later repeat passed all 4 local checks and failed the 5 remote requests; final original-vs-generated control also failed both. This prevents claiming remote stability, while the local and successful remote results establish that the generated config and process chain work.
