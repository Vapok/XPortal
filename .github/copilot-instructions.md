# XPortalNetworks — Repository Instructions

This is a Valheim BepInEx mod (`XPortalNetworks`). Keep the mod version in sync with
every change.

## Builds (required)

To compile, validate, or package the mod, use the tool script — do **not** run ad-hoc
`msbuild` / `nuget` / `Compress-Archive` command sequences:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1                 # Release + package + zip
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug
```

It restores packages, validates the version + JSON, builds (with ILRepack), and packages in a
single step, and exits non-zero on failure. See `tools/README.md` for parameters and outputs.

## Versioning rule (required)

**Every change set must bump the mod version** using Semantic Versioning
(`MAJOR.MINOR.PATCH`), and all version references must match.

**Single source of truth**

- `XPortalNetworks/ModInfo.cs` → `public const string Version = "x.y.z";`

**Keep in sync (same value)**

- `manifest.json` → `"version_number"` (Thunderstore)
- `Docs/SolutionDir/Package/Release/manifest.json` → `"version_number"` (the generated release
  manifest; it mirrors `Mod.Info`, so keep it identical to the root `manifest.json`)
- Changelog: add a new top entry (`# x.y.z - <Title>`) in `Docs/PATCHNOTES.md`; the
  root `CHANGELOG.md` is generated from it and is kept in sync.

`XPortalNetworks/Properties/AssemblyInfo.cs` derives from `Mod.Info.Version` automatically —
do not edit it.

**Choosing the level**

- **MAJOR** (`x.0.0`) — breaking / incompatible changes, e.g.:
  - RPC / network-protocol changes that older clients can't interoperate with,
  - portal-network config incompatibilities (e.g. removing the `Portal Networks` entries,
    changing how networks/allow lists are stored, or breaking the `xportal_networks.json` import),
  - removed features or config options, or changed defaults that break existing setups.
- **MINOR** (`x.y.0`) — new, backwards-compatible functionality, e.g.:
  - new features, UI, portal behaviours, or **new config options** (like an added
    `allow_list`). Reset `PATCH` to `0`.
- **PATCH** (`x.y.z`) — backwards-compatible fixes and internal work, e.g.:
  - bug fixes, reliability/hardening, performance, refactors, docs, localization,
    dependency/build updates.

**Rules**

- One bump per change set. When a set mixes levels, use the **highest** applicable level
  (e.g. a breaking change plus fixes → MAJOR).
- Never reuse, skip past, or decrease a version that has already been released.
- Include the version bump and changelog entry in the same change set as the code change.
- If the correct level is genuinely ambiguous (e.g. it *might* break older clients),
  explain your reasoning and ask before deciding.
