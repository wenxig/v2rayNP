# v2rayNP Rebrand

## Goal

Rebrand application identity across source, assemblies, package artifacts, UI, and documentation; replace inherited app icons with an original design; preserve upstream protocol, attribution, and legacy compatibility identifiers.

## Phases

- [completed] 1. Inventory identity strings and define compatibility boundaries.
- [completed] 2. Rename source/project/build/package identity to v2rayNP and preserve legacy data migration.
- [completed] 3. Create original cross-platform application icons and wire them into both hosts.
- [completed] 4. Rewrite README for v2rayNP, run focused validation and review residual v2rayN references.
- [completed] 5. Update progress/findings and create a signed Conventional Commit.

## Decisions

- Preserve `v2rayn://` as a public import/share URI scheme.
- Preserve upstream `2dust/v2rayN` references, core archive names, license attribution, and old install/data locations only where these are compatibility/source identifiers.
- Product app IDs, assemblies, executable names, package names, UI labels, project namespaces, and release asset names use `v2rayNP`.

## Next Step

Verify the signed commit and clean worktree.

## Validation

- Avalonia Debug build: passed, 0 warnings and 0 errors.
- WPF Debug build with `EnableWindowsTargeting=true`: passed, 0 warnings and 0 errors.
- ServiceLib.Tests: 258 passed, 0 failed.
- Packaging shell syntax and all GitHub workflow YAML parses: passed.
- `git diff --cached --check`: passed after removing trailing workflow whitespace.
- Submodule path resolves at `v2rayNP/GlobalHotKeys` and remains at the original commit.

## Compatibility References Retained

- `ECoreType.v2rayN = 99` keeps the persisted core enum value/name stable.
- `v2rayn://`, `v2rayNAutoRun`, the `v2rayN` instance mutex, old LocalAppData path, and legacy package paths remain compatibility identifiers.
- Upstream URLs, upstream core archive names, and GPL attribution identify the source project and downloaded binaries.
- Existing root history files remain intact; they document prior work rather than current app branding.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---:|---|
