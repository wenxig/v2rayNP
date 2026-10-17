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
