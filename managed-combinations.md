# Managed combinations design

A sing-box policy group owns a graph of native sing-box members and per-node loopback SOCKS backends. SSR/Mieru backends run mihomo; ShadowQUIC/Juicity use their official clients. No native protocol substitution occurs. Each backend retains its source IndexId, independent config and deterministic port. The persisted group and profiles are never rewritten into SOCKS nodes.

The dedicated range defaults to 26000–26199 and is configurable in CoreBasicItem. Nodes sorted by IndexId receive consecutive ports. Application listeners/API ports are excluded; occupied ports fail explicitly. Single-node +7/+8 remains unchanged. Bind preflight cannot eliminate the child-process bind race.

Prepare every backend and frontend config and executable before stopping the old graph. Start backends before sing-box; stop in reverse order. Every backend must become ready during startup; a startup failure rolls back the whole graph. After readiness, a backend exit is reported while the frontend and surviving backends remain available for real URL probes. The sing-box frontend is required. There is no unbounded restart loop.

Managed speed tests own a separate lifecycle, loopback-only mixed listener and backend range immediately after the configured production range. Tests are serialized, omit the production controller/cache/routing, and release all owned processes/configs on stop or cancellation. Port preflight still cannot eliminate external bind races.

Only LeastPing is represented by sing-box urltest; the existing controller UI provides manual selection. Fallback, random, round-robin and least-load are refused for sing-box until equivalent behavior is proven. Leaf tags use stable IndexIds. Per-connection counters must be attributed to the actual leaf rather than the selected group.

ProxyChain remains a separate graph. Any chain containing an extended backend (including inside a nested group) is rejected: a SOCKS connection to a local standalone client cannot redirect that client's upstream QUIC dial. Subscription prev/next references use the same rejection and cycle validation. No TCP/UDP chain support is inferred from HTTP through a selector.

Acceptance must distinguish generated-config validation, controlled TCP/UDP forwarding, lifecycle failure/cleanup and target-platform operation. Missing evidence remains pending in remediation-plan.md.
