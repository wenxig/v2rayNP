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

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
