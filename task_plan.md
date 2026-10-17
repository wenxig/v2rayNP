# Protocol Support Implementation

## Goal

Add complete SSR, Mieru, ShadowQUIC, and Juicity support using mihomo for the first three protocols and the official Juicity client for Juicity.

## Phases

- [completed] 1. Add protocol models, registries, URI format handlers, and persistence dispatch.
- [completed] 2. Add core capability validation and mihomo/Juicity configuration generation.
- [completed] 3. Add node editor fields, core filtering, and localized UI strings.
- [completed] 4. Add focused tests, update support documentation, and run validation.
- [in_progress] 5. Review the diff and create a signed Conventional Commit.

## Decisions

- SSR, Mieru, and ShadowQUIC run through mihomo.
- Juicity runs through the already registered official `juicity-client`.
- Xray and sing-box reject these protocols; no silent fallback or semantic mapping.
- Existing user changes are preserved.

## Next Step

Run final serial validation, inspect the diff, and commit only task files.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
