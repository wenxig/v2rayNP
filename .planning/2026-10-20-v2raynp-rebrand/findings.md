# Findings

- Product identity appears in project directories, C# namespaces, assemblies, executable names, UI titles, package scripts, CI artifact names, resource files, and README text.
- Compatibility/source identifiers include `v2rayn://`, `ECoreType.v2rayN` (stored enum values), upstream `2dust/v2rayN` URLs and core archive names, GPL attribution, and legacy `/opt/v2rayN`/LocalAppData paths.
- Existing application artwork is `v2rayN.png`, `v2rayN.icns`, and platform `.ico` resources; no image generation dependency is installed.
- The working tree was clean before this task; root planning records contain unrelated completed protocol work and are preserved.
- Renamed the tracked repository directory, solution/project names, C# namespaces, assemblies, UI labels, installer identifiers, CI assets, and package outputs to `v2rayNP`.
- Existing user data moves from LocalAppData `v2rayN` to `v2rayNP` on first startup when possible; failed moves fall back to the old path. Packaged-install detection recognizes current and legacy paths.
- New artwork is a dark rounded square with a two-node mint route, gold endpoints, and white NP lettering. ICO includes seven sizes; ICNS is regenerated; tray variants preserve their state colors.
- The app's internal share URI and persisted `ECoreType.v2rayN` value remain stable; upstream core URLs and names remain upstream values.
- Both host projects build cleanly, all 258 shared tests pass, workflow YAML and packaging scripts parse, and the renamed gitlink retains the original GlobalHotKeys commit.
