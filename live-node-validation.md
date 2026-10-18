# Live node validation — 2026-10-17

The user supplied local client configurations. This report intentionally excludes server addresses, ports, credentials, UUIDs, full configuration files, and exit IPs. The original files were not modified or executed. Downloaded official macOS amd64 cores were used with isolated loopback listeners; system proxy and TUN were not enabled. The original v2rayN remained running to preserve the Codex network connection, so concurrent client traffic may have affected remote QUIC results even though the test listeners used isolated ports.

## Method

1. Identify configuration protocols and generate isolated client configurations. Juicity, Mieru, and ShadowQUIC use the current ServiceLib generators. Hysteria/Hysteria2 use equivalent mihomo outbounds for this connectivity check.
2. Check generated configurations with real cores. Probe the Mieru TCP server port; assess QUIC/UDP protocols through actual proxy traffic rather than assuming a TCP probe proves UDP reachability.
3. Start each proxy, verify local SOCKS5 negotiation, request an external TCP connection, and attempt a certificate-verified TLS handshake.
4. Request `https://example.com/` and `https://www.cloudflare.com/cdn-cgi/trace` through `socks5h` with explicit proxy use, no TLS-verification bypass, and bounded timeouts. Check HTTP 200, curl TLS verification result 0, expected HTML title/text or trace fields. Raw response bodies and logs are kept outside the repository during verification.
5. Retry failures. Compare Juicity with the supplied original JSON using the official client directly, bypassing the sing-box front end.

## Results

All five generated configurations passed real-core configuration checks, and all five local SOCKS5 listeners started. These facts alone do not establish remote connectivity. No SSR configuration was found among the supplied JSON/YAML client configurations.

| Protocol | Remote connectivity | Real HTTPS response | Observation |
| --- | --- | --- | --- |
| Mieru | TCP server connected in about 30 ms; first target TLS attempt closed early, later requests succeeded | Both sites HTTP 200, certificate checks and expected content passed | Final requests: Example Domain 577 bytes / 2.53 s; Cloudflare trace 219 bytes / 2.87 s. Multiple successful response rounds. |
| Juicity | Local listener works; remote QUIC session times out | Both sites failed; HTTP 000 and no response body | Same result with application-generated JSON plus sing-box, supplied original JSON plus sing-box, and supplied original JSON through the official client directly. Official client reports `getQuicConn: timeout: no recent network activity`. |
| ShadowQUIC | Local listener works; remote connection times out | Both sites failed; HTTP 000 and no response body | Repeated mihomo attempts report `context deadline exceeded`; compatibility with the supplied original ShadowQUIC client has not been established. |
| Hysteria2 | Target TLS 1.3 handshake and certificate validation succeeded | Both sites HTTP 200, certificate checks and expected content passed | Example Domain 577 bytes / 5.33 s; Cloudflare trace 215 bytes / 1.14 s. |
| Hysteria | Target TLS 1.3 handshake and certificate validation succeeded | Both sites HTTP 200, certificate checks and expected content passed | Example Domain 577 bytes / 6.40 s; Cloudflare trace 213 bytes / 5.17 s. |

## Scope and limits

- Cores: mihomo v1.19.32, official juicity-client v0.5.0, sing-box v1.14.3 on macOS amd64.
- The successful responses verify actual HTTPS traffic through the supplied nodes, not just open local ports. They do not measure throughput or establish long-term reliability.
- Juicity direct-client failure rules out the sing-box front end as a necessary cause of the observed failure. It does not distinguish service availability, UDP reachability, server/client compatibility, authentication configuration, or effects from running the original client concurrently.
- ShadowQUIC remote connectivity remains unverified; server availability, UDP reachability, client compatibility, and effects from running the original client concurrently are unresolved.
- No system proxy, actual TUN traffic, or target UDP forwarding acceptance was performed.
- All temporary core processes were terminated after testing; user configurations and Windows scripts were left unchanged.
