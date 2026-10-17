# Protocol Support Implementation

## Goal

Add complete SSR, Mieru, ShadowQUIC, and Juicity support using mihomo for the first three protocols and the official Juicity client for Juicity.

## Phases

- [completed] 1. Add protocol models, registries, URI format handlers, and persistence dispatch.
- [completed] 2. Add core capability validation and mihomo/Juicity configuration generation.
- [completed] 3. Add node editor fields, core filtering, and localized UI strings.
- [completed] 4. Add focused tests, update support documentation, and run validation.
- [completed] 5. Review the diff and create a signed Conventional Commit.

## Decisions

- SSR, Mieru, and ShadowQUIC run through mihomo.
- Juicity runs through the already registered official `juicity-client`.
- Xray and sing-box reject these protocols; no silent fallback or semantic mapping.
- Existing user changes are preserved.

## Validation Limits

- `dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug --no-restore` passes (141/141).
- The desktop and WPF builds and package-script syntax checks passed before commit, as recorded in `progress.md`.
- Real mihomo and Juicity smoke tests remain unavailable: GitHub release metadata is reachable, but downloading release assets fails during TLS connection to `release-assets.githubusercontent.com`.
- Real server connectivity still requires a target platform and reachable test server.

## Completion

Implementation and signed commit are complete. Existing unrelated `agent.md` deletion and untracked `AGENTS.md` are preserved.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
