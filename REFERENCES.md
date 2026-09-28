# References

External sources used while developing, fixing and documenting **XPortalNetworks** (a Valheim
BepInEx mod). Everything listed is a third-party artifact or publication; none of it is
distributed with this repository.

- **Mod version at time of writing:** 2.3.0
- **Game:** Valheim 1.0.16 — Unity **6000.0.75f1** (`UnityPlayer.dll` reports `6000.0.75.2503836`)
- **Last updated:** 2026-09-28

Paths below are the ones used on the development machine; the repository expects the same
layout under `$(ReferencesRoot)` (`.references/`) — see [`.references/README.md`](.references/README.md).

---

## 1. Game assemblies (the runtime API surface)

| Source | Location / version | Used for |
|---|---|---|
| Valheim game assemblies | `<Steam>\steamapps\common\Valheim\Valheim_Data\Managed\` — 1.0.16 | All gameplay API facts: `ZNet`, `ZDO`/`ZDOMan`, `ZRoutedRpc`, `ZPackage`, `ZNetView`, `ZDOID`, `Character.RPC_TeleportTo`, `TeleportWorld`, `Piece`, `ZInput`, `ZoneSystem`, `Game` |
| Valheim platform identity | `Splatform.PlatformUserID`, `UserInfo`, `ZNet.PlayerInfo`, `ZNet.CrossNetworkUserInfo` (same assemblies) | Player identity for `allow_list` matching (`Steam_<id>` format) |
| Unity engine modules | `<Steam>\...\Valheim_Data\Managed\UnityEngine*.dll`, `Unity.TextMeshPro.dll` | UI (`UI/PortalConfigurationPanel.cs`), `Vector3`, `RectTransform`, `FileSystemWatcher`-adjacent file IO |
| Valheim dedicated-server admin list | `ZNet.PlayerIsAdmin`, `ZNet.LocalPlayerIsAdminOrHost` | Admin detection for the portal-network bypass and admin-only config entries |

> The four `assembly_*.dll` files are publicized for compilation (see §3) so internal/private
> members can be used directly. They are copyrighted game binaries and are **not** redistributed.

## 2. Modding framework and libraries

| Source | Version | Where it comes from | Used for |
|---|---|---|---|
| **BepInEx** — <https://github.com/BepInEx/BepInEx> | 5.4.2350 (`BepInEx.dll`, `BepInEx.Harmony.dll`) | `denikson-BepInExPack_Valheim-5.4.2350` (Thunderstore); `.references/BepInEx/5.4.2350/BepInEx/core/` | Plugin/base framework, `ConfigFile`/`ConfigEntry`/`ConfigDescription`, config reload + `SettingChanged` events |
| **HarmonyX** — <https://github.com/BepInEx/HarmonyX> | 2.9.0 (`0Harmony.dll`) | BepInEx pack / `packages.config` reference | All runtime patches (`Patches/*`) |
| **Jötunn (Jotunn)** — <https://github.com/ValheimModding/Jotunn> | 2.30.2 | NuGet package `JotunnLib` 2.30.2 (`packages.config`); <https://www.nuget.org/packages/JotunnLib> | Mod-framework helpers; **`Jotunn.Managers.SynchronizationManager`** (ServerSync), `SynchronizationModeAttribute` + `AdminOnlyStrictness`, `NetworkCompatibility`, `GUIManager.IsHeadless()`, `MinimapManager`, `ConfigEntryBaseExtension` |
| **Vapok.Valheim.Common** — <https://github.com/Vapok/Vapok.Common> | NuGet 3.21.1015 (assembly 2.11.2214.0) | NuGet; <https://www.nuget.org/packages/Vapok.Valheim.Common> | `ModSplashManager`/`ModSplashDossier`, `TelemetryManager`, logging, `Vapok.Common.Shared.ConfigurationManagerAttributes`. Internalized into the shipped DLL by ILRepack |
| **Official BepInEx ConfigurationManager** — <https://github.com/BepInEx/BepInEx.ConfigurationManager> | source @ `master`, read 2026-09-28 | Cite reference only | The in-game config UI. Resolves an attributes class **by type name** (not assembly identity); its own `internal sealed` class defines `Order`, `ReadOnly`, `IsAdvanced`, `Browsable`, `Category`, `CustomDrawer`, `CustomHotkeyDrawer`, `DispName`, `Description`, `HideDefaultButton`, `HideSettingName`, `DefaultValue`, `ShowRangeAsPercent`, `ObjToStr`, `StrToObj` — and **no** admin concept |

### Notes on the ConfigurationManager / ServerSync behaviour

The ConfigurationManager implementation was read from its **source** (fetched 2026-09-28 — see §7), together with offline primary sources:

- ConfigurationManager resolves the attributes class **by type name**, and copies only the fields whose
  names match its own (`ConfigurationManager.Shared/SettingEntryBase.cs`):

  ```csharp
  var attrType = attrib.GetType();
  if (attrType.Name == "ConfigurationManagerAttributes")
  {
      var otherFields = attrType.GetFields(BindingFlags.Instance | BindingFlags.Public);
      foreach (var propertyPair in _myProperties.Join(otherFields, my => my.Name, other => other.Name, ...))
  ```

  Extra fields are ignored rather than rejected — which is exactly why Jötunn, Vapok.Common and blaxxun's
  ServerSync can each ship their own copy of the class.
- ConfigurationManager's own class has **no** `IsAdminOnly` and **no** `IsUnlocked`; a search for
  `Unlocked|IsAdmin|AdminOnly` across its whole source returns nothing. Both fields belong to the
  **sync library**: Jötunn's public `ConfigurationManagerAttributes` carries
  `IsAdminOnly`/`IsUnlocked`, and `Jotunn.Managers.SynchronizationManager` reads them back with
  `OfType<ConfigurationManagerAttributes>()` (Jötunn's own type).
- **Consequence for this mod:** server-owned entries are enforced by ServerSync pushing the server's
  values and by Jötunn's patch on `ConfigEntryBase.SetSerializedValue` (so a local write to a syncable
  entry is blocked / replaced). ConfigurationManager has no admin concept, so it renders those entries
  as ordinary editable fields — there is no lock badge. (An earlier note in this repo claimed the fields
  appear "locked" in the ConfigurationManager window; that was **wrong**.)
- `Jotunn.xml` (shipped with the `JotunnLib` NuGet package) documents the duck-typed
  `ConfigurationManagerAttributes` class embedded in `Jotunn.dll`, including the remark
  *"You can read more and see examples in the readme at
  https://github.com/BepInEx/BepInEx.ConfigurationManager"* and the statement that
  *"You can optionally remove fields that you won't use from this class"* — i.e. the class is
  matched by shape/name, not by assembly identity.
- `Jotunn.xml` also documents the strictness semantics that the mod relies on:

  > **`AdminOnlyStrictness.Always`** — "AdminOnly is always enforced for Config Entries even if
  > the mod is not installed on the server. This means that AdminOnly configs cannot be edited
  > in multiplayer if the mod is not on the server."
  >
  > **`AdminOnlyStrictness.IfOnServer`** — "AdminOnly is only enforced for Config Entries if the
  > mod is installed on the server."
  >
  > **`SynchronizationModeAttribute`** — "how Jotunn should enforce synchronization of Config
  > Entries. Only relevant for Config Entries that have the
  > `ConfigurationManagerAttributes.IsAdminOnly` applied."

  This is why the plugin uses `[SynchronizationMode(AdminOnlyStrictness.Always)]`: XPortalNetworks
  is `EveryoneMustHaveMod`, so it is always present on the server and the `Always` caveat cannot apply.

## 3. Build and analysis tooling

| Source | Version | Used for |
|---|---|---|
| **AssemblyPublicizer** (CabbageCrow) — <https://github.com/CabbageCrow/AssemblyPublicizer> (binaries: <https://github.com/CabbageCrow/AssemblyPublicizer/releases>) | cached at `tools/.cache/publicizer/AssemblyPublicizer/AssemblyPublicizer.exe` (binary self-reports file version 1.0); LGPL-2.1 (bundled `Licenses/AssemblyPublicizer.LICENSE.txt`) | Publicizing `assembly_valheim`/`_utils`/`_guiutils`/`_postprocessing`; driven by [`tools/New-ValheimRefs.ps1`](tools/README.md) |
| **BepInEx.AssemblyPublicizer** — <https://github.com/BepInEx/BepInEx.AssemblyPublicizer> | — | Documented manual alternative for producing the `_publicized` assemblies (see `.references/README.md`) |
| **ILRepack** (`ILRepack.Lib.MSBuild.Task` 2.0.44.1) — <https://github.com/gluck/il-repack> / <https://www.nuget.org/packages/ILRepack.Lib.MSBuild.Task> | 2.0.44.1 | Post-build merge that internalizes `Vapok.Valheim.Common.dll` into `XPortalNetworks.dll` |
| **Mono.Cecil** — <https://github.com/jbevain/cecil> | ships in the Jotunn package: `%USERPROFILE%\.nuget\packages\jotunnlib\2.30.2\build\Mono.Cecil.dll` | Reading IL/metadata for API verification (types, members, string literals, attribute arguments) |
| **nuget.exe** — <https://dist.nuget.org/win-x86-commandline/latest/nuget.exe> | latest (auto-downloaded to `tools/.cache`) | `packages.config` restore in `tools/Build.ps1` |
| **Microsoft.NETFramework.ReferenceAssemblies** (+ `.net48`) | 1.0.3 | Compiling against .NET Framework 4.8 without a machine-wide targeting pack |
| **MSBuild** (Visual Studio 18 Community) | local toolchain | Compiling the legacy (non-SDK) csproj |

## 4. Offline documentation and metadata sources

These were the primary ("always reliable") references; all are generated from the
corresponding upstream sources, so they are authoritative rather than summarised:

1. **Library XML API docs on disk** — `<nuget-cache>\<package>\<version>\lib\<tfm>\<Assembly>.xml`
   (e.g. `jotunnlib\2.30.2\lib\net462\Jotunn.xml`; a copy also lands in
   `XPortalNetworks/bin/Release/Jotunn.xml`). Source of every Jotunn/ServerSync statement in this repo.
2. **NuGet package metadata** — `packages.config`, `.nuspec`, and package READMEs
   (the `Vapok.Valheim.Common` README documents "Seamless admin lock enforcement and
   client-server setting replication").
3. **Decompiled IL** of `Jotunn.dll`, `Vapok.Valheim.Common.dll` and the publicized Valheim
   assemblies (via Mono.Cecil) — used whenever docs were silent or ambiguous.
4. **Local Copilot session history** for this repository (Chronicle session store) — earlier
   decisions, build-toolchain findings and version history.

## 5. In-repository prior art

Internal, but they record externally-derived facts and are the source of the versioning and
packaging rules followed here:

- [`.github/copilot-instructions.md`](.github/copilot-instructions.md) — build + SemVer rules
- [`tools/README.md`](tools/README.md) — `Build.ps1` / `New-ValheimRefs.ps1` usage
- [`.references/README.md`](.references/README.md) — reference-assembly layout and provenance
- [`Docs/PATCHNOTES.md`](Docs/PATCHNOTES.md) — release history (e.g. the 2.0.x note recording that
  portal networks/permissions were "synchronized … via Jotunn ServerSync")
- [`Docs/Modules/*.t4`](Docs/Modules) — sources for the generated README configuration sections
- [`Docs/SolutionDir/`](Docs/SolutionDir) — generated README + package manifest

## 6. Referenced third-party mods and services

| Item | As referenced in this repo |
|---|---|
| **Advanced Portals** (RandyKnapp) | Integration described in the `DisplayPortalColour` config option |
| **Stone Portal** | Integration described alongside `DisplayPortalColour` |
| **AnyPortal** | Declared incompatible via `[BepInIncompatibility("com.sweetgiorni.anyportal")]` |
| **ValheimCommunityPatch** (MidnightMods) | Compatibility notice in `CHANGELOG.md` (<https://thunderstore.io/c/valheim/p/MidnightMods/ValheimCommunityPatch/>) |
| **Nexus Mods** — Nexus ID 3719 / Nexus Update Check | `Mod.Info.NexusId`, `General/NexusID` config key (<https://www.nexusmods.com/valheim/mods/102>) |
| **Thunderstore / BepInExPack_Valheim** | Distribution target + BepInEx core source (`denikson-BepInExPack_Valheim`) |

## 7. Source-availability notes

Recorded for transparency about where each fact came from:

- The **web-fetch tool was unavailable** for the whole of the research phase (`fetch_webpage`
  returned *"Thank you for using GitHub Copilot. Your subscription has ended. You are currently
  logged in as ghstwhl."* for every URL, including
  <https://github.com/BepInEx/BepInEx.ConfigurationManager>). The GitHub repository search/index
  tools likewise failed (`github_repo`: *"Github repo index not yet"*) or returned **empty results
  without an error** for valid public repositories. These are GitHub-hosted tools gated by a Copilot
  entitlement, so swapping the model provider does not bring them back — see the next bullet.
- **Web retrieval was restored locally** through a containerized MCP fetch server instead of the
  GitHub-hosted tool: `mcp/fetch` (MCP protocol `2024-11-05`, server `mcp-fetch` 1.23.0) run as
  `docker run -i --rm mcp/fetch`, configured in [`.vscode/mcp.json`](.vscode/mcp.json). MCP servers
  are hosted locally, so they are unaffected by the GitHub entitlement. Verified end-to-end by
  fetching <https://github.com/BepInEx/BepInEx.ConfigurationManager> — the exact URL that previously
  failed.
- Consequently **every** external fact in §1–§6 was taken from a local primary artifact
  (§1–§4), not from a web page. Where a tool returned an empty result, that was treated
  as *unknown* — never as *"the source does not contain it"*.
- Verified-by-decompilation facts worth naming, because they are not documented upstream in a
  retrievable form: `ConfigEntryBase.BoxedValue` is what ServerSync writes on receipt
  (`ApplyConfigZPackage`); only entries carrying `IsAdminOnly` are collected for synchronisation
  (`GetSyncConfigValues`); a client's change is sent to the server peer
  (`SynchronizeChangedConfig` → `CustomRPC.SendPackage(ZRoutedRpc.GetServerPeerID(), …)`) and the
  server logs *"Received configuration data from client {0}"*; the push is triggered by the
  ConfigurationManager **window being closed** and by `Config_ConfigReloaded`.

## 8. Attribution and licensing

- **Valheim** and its assemblies are © Iron Gate Studio AB / Coffee Stain Publishing. They are
  used here as compile-time references only and are not redistributed.
- **BepInEx**, **HarmonyX**, **Jötunn**, **Vapok.Valheim.Common**, **ILRepack**, **Mono.Cecil** and
  **AssemblyPublicizer** each remain under their own upstream licences; see the respective links
  in §2 and §3 for terms.
- Note the caveat carried by `tools/New-ValheimRefs.ps1`: XPortalNetworks is **not affiliated**
  with AssemblyPublicizer, and only assemblies you are legally entitled to work with should be
  publicized.
