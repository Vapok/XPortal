# tools

Helper scripts for building XPortalNetworks.

## `Build.ps1` — build, validate, and package (start here)

One command for the whole pipeline: **NuGet restore → validation → MSBuild (+ ILRepack) →
Release packaging (+ zip)**. Runs from any working directory and exits non-zero on failure
(CI/agent friendly).

```powershell
# Release build + package + zip (default)
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1

# Debug build (compiles only, no packaging)
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Configuration Debug

# Skip steps you know aren't needed
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SkipRestore -SkipValidation
```

### What it does

1. **Restore** — `nuget restore` for `packages.config` (auto-downloads `nuget.exe` to
   `tools\.cache` if it isn't on `PATH`).
2. **Validate** — fails the build when:
   - `ModInfo.Version` isn't `MAJOR.MINOR.PATCH`, or
   - it doesn't match `manifest.json` **and** `Docs/SolutionDir/Package/Release/manifest.json`, or
   - `XPortalNetworks\xportal_networks.json` or any `Translations\**\*.json` is invalid JSON, or
   - the reference assemblies aren't present at `-ReferencesRoot` (default `<repo>\.references`).
3. **Build** — MSBuild; `ILRepack` internalizes `Vapok.Valheim.Common` into a single DLL.
4. **Package** (Release) — assembles the Thunderstore-style folder and a zip.

### Parameters

| Parameter | Default | Purpose |
|---|---|---|
| `-Configuration` | `Release` | `Release` (packages) or `Debug` (builds only). |
| `-ReferencesRoot` | `<repo>\.references` | Folder with the publicized reference assemblies. |
| `-SkipRestore` | off | Skip the NuGet restore step. |
| `-SkipValidation` | off | Skip the validation checks. |
| `-NoPackage` | off | Build the release folder but don't create the zip. |

### Outputs

```
XPortalNetworks\bin\<Configuration>\XPortalNetworks.dll   single, ILRepack-merged DLL
Release\XPortalNetworks-Vapok\                            package (plugins\ + docs)
Release\XPortalNetworks-release.zip                       Release only
```

Deploy the package's `plugins` contents to `BepInEx\plugins\` on **both** the server and the
client (same build on each side).

### Exit codes

`0` = success, non-zero = failure. The last line is always `BUILD OK ...` or `BUILD FAILED: ...`.

## `New-ValheimRefs.ps1`

Creates the reference-assembly layout that `XPortalNetworks/XPortalNetworks.csproj`
compiles against (the `$(ReferencesRoot)` folder), using
[CabbageCrow/AssemblyPublicizer](https://github.com/CabbageCrow/AssemblyPublicizer)
to publicize the Valheim game assemblies.

It produces exactly what the csproj expects:

```
<OutputPath>\Valheim\<GameVersion>\
    assembly_valheim_publicized.dll
    assembly_utils_publicized.dll
    assembly_guiutils_publicized.dll
    assembly_postprocessing_publicized.dll
    Managed\                (UnityEngine*.dll, Unity.TextMeshPro.dll, ...)
<OutputPath>\BepInEx\<BepInExVersion>\BepInEx\core\
    BepInEx.dll  BepInEx.Harmony.dll  0Harmony.dll
```

### Prerequisites

- **Valheim** installed (Steam) or `-ValheimPath` provided.
- **BepInEx 5.4.2350** installed into the Valheim folder, or `-BepInExCorePath`
  pointing at a folder containing `BepInEx.dll`.
- **AssemblyPublicizer.exe** — either pass `-PublicizerPath`, or use
  `-BuildPublicizer` (requires Visual Studio / MSBuild, which you need anyway).

### Usage

```powershell
# From the repo root. Builds the publicizer from source if needed.
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 -BuildPublicizer

# Use a downloaded release binary (preferred) and a specific Valheim install:
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 `
    -ValheimPath "D:\Steam\steamapps\common\Valheim" `
    -PublicizerPath "C:\tools\AssemblyPublicizer.exe"

# Write somewhere other than the repo's .references and match a different game version:
powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 `
    -OutputPath "C:\ValheimRefs" -GameVersion 0.217.46
```

Default output is `<repo>\.references` (gitignored).

### After running

Point the project at the generated folder by setting `ReferencesRoot`, e.g. add a
`Directory.Build.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <ReferencesRoot>C:\ValheimRefs</ReferencesRoot>
  </PropertyGroup>
</Project>
```

Then build in Visual Studio, or:

```powershell
msbuild XPortalNetworks.sln /p:ReferencesRoot="C:\ValheimRefs"
```

> **Note:** If you change `-GameVersion` / `-BepInExVersion`, keep them in sync with
> `VALHEIM_INSTALL` / `BEPINEX_PATH` (or `ReferencesRoot`) in the csproj.

### Legal

Reference assemblies are derived from the game and tooling binaries and are **not**
distributed with this repo. Only publicize assemblies you are entitled to work with.
XPortalNetworks is not affiliated with AssemblyPublicizer.
