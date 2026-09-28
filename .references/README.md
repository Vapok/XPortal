# Reference assemblies

This folder is **gitignored** (only this README is tracked) and is bind-mounted
into the dev container at `/home/vapok/Modding/References`, which is the default
`$(ReferencesRoot)` the project expects. Populate it with the layout below
before building.

> **Easiest way to populate this folder:** run
> [`tools/New-ValheimRefs.ps1`](../tools/README.md), which publicizes the Valheim
> assemblies with AssemblyPublicizer and assembles this exact layout automatically:
> `powershell -ExecutionPolicy Bypass -File .\tools\New-ValheimRefs.ps1 -BuildPublicizer`

> The version folder comes from `ValheimGameVersion` in
> [`Directory.Build.props`](../Directory.Build.props) (currently **1.0.16**) — the single
> source of truth the csproj and the `tools/` scripts all use.

> These are copyrighted game/tooling binaries and are **not** distributed with
> this repo. You must obtain them yourself.

```
.references/
├── Valheim/1.0.16/
│   ├── assembly_valheim_publicized.dll
│   ├── assembly_utils_publicized.dll
│   ├── assembly_guiutils_publicized.dll
│   ├── assembly_postprocessing_publicized.dll
│   └── Managed/
│       ├── UnityEngine.dll
│       ├── UnityEngine.CoreModule.dll
│       ├── UnityEngine.ParticleSystemModule.dll
│       ├── UnityEngine.AnimationModule.dll
│       ├── UnityEngine.AssetBundleModule.dll
│       ├── UnityEngine.TextRenderingModule.dll
│       ├── UnityEngine.UI.dll
│       └── Unity.TextMeshPro.dll
└── BepInEx/5.4.2350/BepInEx/core/
    ├── BepInEx.dll
    ├── BepInEx.Harmony.dll
    └── 0Harmony.dll
```

## Where to get them

1. **Valheim 1.0.16** — the game's `Valheim_Data/Managed/` folder provides the
   `UnityEngine*`/`Unity.TextMeshPro` assemblies. Copy that folder to
   `Valheim/1.0.16/Managed/`, and copy `assembly_valheim.dll`,
   `assembly_utils.dll`, `assembly_guiutils.dll`, `assembly_postprocessing.dll`
   alongside it.
2. **Publicize** the four `assembly_*.dll` files (strips `internal`) and rename
   the output with the `_publicized` suffix, e.g. with
   [BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer):
   ```bash
   dotnet tool install -g BepInEx.AssemblyPublicizer.Cli
   assembly-publicizer -o assembly_valheim_publicized.dll assembly_valheim.dll
   ```
   (A pre-publicized reference pack from the Valheim modding community works too.)
3. **BepInEx 5.4.2350** — from the `denikson-BepInExPack_Valheim-5.4.2350`
   Thunderstore package; the three core DLLs are all in its `BepInEx/core/`.

Different game/tooling versions are fine as long as you either match the paths
above or update `VALHEIM_INSTALL`/`BEPINEX_PATH`/`ReferencesRoot` in
`XPortalNetworks/XPortalNetworks.csproj` (or pass `/p:ReferencesRoot=...` to
`msbuild`).
