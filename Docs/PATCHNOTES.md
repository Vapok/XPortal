# 2.4.0 - Portal Networks in the Server Config
* **Config-Owned Networks (`XPortalNetworksConfig.cs`, `CustomNetworks.cs`)**
  * Portal networks are now defined by the `Portal Networks` config section: `Network <n> Name` and `Network <n> Allow List` (ids 1-15, empty name = unused slot, empty list = open to everyone). Both are tagged `ConfigurationManagerAttributes.IsAdminOnly`, so ServerSync distributes them and only server admins (or the host) can change them.
  * `RebuildFromConfig()` replaces the JSON load path and runs on every `SettingChanged`; `ResetSession()` now rebuilds instead of clearing, so the list survives a session reset.
* **Legacy Import (`CustomNetworks.cs`)**
  * `InitializeServer()` imports a pre-2.4.0 `BepInEx/config/XPortalNetworks/xportal_networks.json` into the config when no network is defined yet (re-using the old parser, now reachable only from `ImportLegacyJsonIfNeeded()`), then logs that the file is obsolete.
* **Removed RPC (`RPC/RPCManager.cs`, `RPC/ClientEvents.cs`, `RPC/ServerEvents.cs`, `RPC/SendToClient.cs`, `RPC/SendToServer.cs`)**
  * Dropped `RPC_CustomNetworks` / `RPC_RequestCustomNetworks`, the queued re-send helpers and `SendToClient.CustomNetworks` / `SendToServer.RequestCustomNetworks`; `CustomNetworks.PackForClient` / `ApplyFromServer` / `BroadcastToAllPeers` are gone too.
* **Hot-Reload Machinery Removed (`CustomNetworks.cs`, `XPortalNetworks.cs`)**
  * The `FileSystemWatcher`, `ServerTick()` polling, debounce state, `EnsureDefaultConfigExists` and the embedded JSON template were all deleted; `xportal_networks.json` is no longer an embedded resource in `XPortalNetworks.csproj`, and `tools/Build.ps1` no longer validates it.
* **Docs**: The README configuration sections describe the in-game workflow, and the setting lists are complete again - `Docs/Modules/25Configuration.t4` (plus the generated Nexus and package READMEs) now document `DefaultPrivatePortal`, `RestrictPortalRemoval`, `AdminsSeeAllNetworks`, the `Portal Networks` entries and the two `[Local Config]` toggles, and the README settings table gained the matching rows.
* **Doc Templates (`Docs/Modules/*.t4`, `Docs/Docs.csproj`)**
  * Removed the hard-coded self-references that had been by-passing the assembly-derived variables: `10Header.t4` / `11HeaderGitHub.t4` / `20Features.t4` / `25Configuration.t4` / `90InstallationDev.t4` now use `thisModName` (and `thisModGitHubRepo` for the banner image) instead of the literal `XPortal` and the original `SpikeHimself/XPortal` image URL, so a regeneration no longer re-introduces the pre-rename branding. Links that intentionally point at the original mod (`00Urls.t4`) and the historical changelog entries (`52Changelogs-previous.t4`) were left as-is.
  * Removed `Docs/SolutionDir/README.tt`: `Docs/SolutionDir/README.md` is a hybrid of generated and hand-written sections (the configuration and installation sections exist in no template), so regenerating it would have deleted hand-authored content. The file is now explicitly hand-maintained, and `tools/README.md` documents which files are generated and how to regenerate them.

# 2.3.2 - Offline-Capable Research Tooling
* **Local Web Access (`/.vscode/mcp.json`)**
  * Added a locally-hosted MCP fetch server (`docker run -i --rm mcp/fetch`, MCP `2024-11-05` / `mcp-fetch` 1.23.0). MCP servers run locally, so agent web retrieval no longer depends on GitHub-hosted tools (which are gated by a Copilot entitlement and were refusing every request during this work).
  * Verified end-to-end by fetching the previously-failing `https://github.com/BepInEx/BepInEx.ConfigurationManager`.
  * Added a second locally-hosted server, `mcp/brave-search` (keyword web search); the API key is supplied through VS Code's secure `${input:...}` prompt (`password: true`) instead of being written into the repo.
* **Documentation (`REFERENCES.md`)**
  * Section 7 now distinguishes the GitHub-tool outage from the restored local MCP path, so the source provenance record stays accurate.
  * Section 2 now records the verified ConfigurationManager contract: it resolves an attributes class **by type name** (`SettingEntryBase.cs`) and copies only same-named fields, its own class is `internal sealed` with no admin concept, and `IsAdminOnly`/`IsUnlocked` belong to the sync library (Jötunn). It also corrects the earlier claim that non-admin players see server-owned settings locked in the ConfigurationManager window - they do not; enforcement is via ServerSync.
* **No mod changes**: version bump only (tooling + docs = PATCH per `.github/copilot-instructions.md`); `ModInfo.cs`, `manifest.json` and `Docs/SolutionDir/Package/Release/manifest.json` kept in sync.

# 2.3.1 - Reference Documentation
* **New Source Inventory (`REFERENCES.md`)**
  * Documents every external source used for this mod: game assemblies and exact versions (Valheim 1.0.16 / Unity 6000.0.75f1), modding framework and libraries (BepInEx 5.4.2350, HarmonyX 2.9.0, Jötunn 2.30.2, Vapok.Valheim.Common 3.21.1015, BepInEx ConfigurationManager contract), build/analysis tooling (AssemblyPublicizer, ILRepack 2.0.44.1, Mono.Cecil, nuget.exe, .NET Framework reference assemblies), the offline XML-doc/NuGet/IL sources used for verification, in-repo prior art, referenced third-party mods, and attribution/licensing notes.
  * Records which facts came from which source, including the `AdminOnlyStrictness` semantics quoted from `Jotunn.xml` and the decompilation-verified ServerSync data path.
* **No code changes**: version bump only (documentation is a PATCH per `.github/copilot-instructions.md`); `ModInfo.cs`, `manifest.json` and `Docs/SolutionDir/Package/Release/manifest.json` kept in sync.

# 2.3.0 - Server-Owned Config via Jotunn ServerSync
* **ServerSync Opt-In (`XPortalNetworks.cs`)**
  * Added `[SynchronizationMode(AdminOnlyStrictness.Always)]` to the plugin, which registers its config file with Jotunn's `SynchronizationManager` (ServerSync).
* **Server-Owned Entries (`XPortalNetworksConfig.cs`)**
  * `PingMapDisabled`, `DoublePortalCosts`, `HidePortalDistance`, `RestrictPortalRemoval` and `AdminsSeeAllNetworks` now carry `ConfigurationManagerAttributes.IsAdminOnly`, so ServerSync pushes the server's values into every client's config file, unlocks the entries for server admins/host in the ConfigurationManager window and locks them for everyone else.
  * Removed the `Server` settings mirror and `TrackServerConfig()`: synced entries hold the server's values in the local config, so the cached settings are read from `Local` (`CustomNetworks.cs`, `Patches/Piece.cs`, `UI/PortalConfigurationPanel.cs`, `XPortalNetworks.cs`).
  * Removed `PackLocalConfig()`/`ReceiveServerConfig()` and the now unused `System.IO`/`XPortalNetworks.RPC` usings.
* **Retired Config RPC (`RPC/RPCManager.cs`, `RPC/ClientEvents.cs`, `RPC/ServerEvents.cs`, `RPC/SendToClient.cs`, `RPC/SendToServer.cs`)**
  * Dropped `RPC_Config`/`RPC_ConfigRequest` and the `SendToClient.Config`/`SendToServer.ConfigRequest` helpers (server-to-client only). `LocalConfigChanged` on the server still re-broadcasts the per-client portal network lists, so `AdminsSeeAllNetworks` changes still take effect immediately.
* **Docs**
  * Configuration sections now describe the server-owned settings as synchronized and admin-editable instead of "enforced (but not overwritten) by the server".

# 2.2.1 - Admin Bypass Live Toggle Fix
* **Server Config Aliasing (`XPortalNetworksConfig.cs`, `XPortalNetworks.cs`)**
  * Re-assert `Server = Local` whenever the config reloads and at server session start (`TrackServerConfig`), so server-enforced settings (incl. `AdminsSeeAllNetworks`) are read correctly even though the plugin loads before `ZNet` exists.
* **Live Network Re-Push (`XPortalNetworksConfig.cs`)**
  * On a server config change, re-broadcast each client's permitted network list and refresh the local UI, so toggling `AdminsSeeAllNetworks` takes effect immediately.

# 2.2.0 - Admin Network Bypass Option
* **New Config Option (`XPortalNetworksConfig.cs`)**
  * Added `AdminsSeeAllNetworks` (General section, default `false`, server-enforced). When disabled, server admins/host are treated like normal players for portal-network allow lists; when enabled, they can see and use every network.
* **Consistent Gating (`CustomNetworks.cs`, `XPortalNetworks.cs`, `RPC/ServerEvents.cs`)**
  * Per-client network push, client-side visibility (dropdowns + hover), portal interaction, teleport gating, and server-side portal edit/link validation now all honour the setting through a single `AdminsBypassNetworks` gate.

# 2.1.2 - Valheim 1.0.16 Alignment
* **Game References (`Directory.Build.props`, `XPortalNetworks/XPortalNetworks.csproj`, `tools/*`)**:
  * Re-publicized the Valheim 1.0.16 game assemblies and regenerated the reference layout.
  * Added `ValheimGameVersion` to `Directory.Build.props` as the single source of truth; `VALHEIM_INSTALL` and the reference `HintPath`s (now `$(VALHEIM_INSTALL)`/`$(BEPINEX_PATH)`) plus the `tools/` scripts all derive from it, so future game updates are a one-line change.
* **Build**: Verified the mod compiles against the 1.0.16 assemblies (no source changes required).

# 2.1.1 - Config Hot-Reload Resilience
* **Hot-Reload Poll (`CustomNetworks.cs`)**:
  * `DetectFileChange` now treats the config file disappearing as a change, so deleting `xportal_networks.json` while the server is running reliably re-seeds it from the embedded default and reloads (previously this depended on a `FileSystemWatcher` delete event; the polling fallback ignored the file being absent).
  * Added a warning log when the file is found missing and recreated.
  * `UpdateFileBaseline` now records the "missing" state explicitly, and the reload re-baselines after reading so the just-read file isn't flagged again on the next poll.

# 2.1.0 - Team Portal Networks
* **Team Networks via `allow_list` (`CustomNetworks.cs`, `PortalNetwork.cs`)**:
  * Network entries in `xportal_networks.json` now accept an optional `"allow_list"` of player ids (e.g. `Steam_12345678901234567`); the parser was rewritten to support the new object form (`id` / `name` / `allow_list`).
  * Only players on a network's allow list can see the network in the configuration UI, edit its portals, or step through them. An omitted or empty `allow_list` keeps the network open to everyone.
* **Server-Authoritative Network Policy (`CustomNetworks.cs`, `RPC/ServerEvents.cs`, `RPC/ClientEvents.cs`, `RPC/SendToClient.cs`, `NetPeerUtility.cs`)**:
  * The server now sends each client only the networks that client is permitted to use (id + name only); allow lists never leave the server.
  * Portal add/update requests are rejected when they assign a network, edit a restricted portal, or link to a portal on a team network the requester cannot access.
  * Added `NetPeerUtility` helpers to resolve a player's platform id (`Steam_...`) for allow-list matching.
* **Client Privacy (`XPortalNetworks.cs`, `UI/PortalConfigurationPanel.cs`)**:
  * Portals on a restricted network the player is not a member of are hidden from hover text and from the network/destination dropdowns, and show a localized "cannot access" message on interaction.
* **Config Hot-Reload Hardening (`CustomNetworks.cs`, `XPortalNetworks.cs`)**:
  * `xportal_networks.json` reloads now run on the game's main thread (via the frame update pump) instead of a background thread.
  * Added a file-timestamp polling fallback so edits are picked up even when `FileSystemWatcher` events are missed, with debounced coalescing of rapid saves.
* **Localization**:
  * Added `hud_xportal_network_restricted` to the shipped translations.

# 2.0.10 - Portal Connection Fix
* **Portal Reconnection & Target Resolution (`Patches/ZDOMan.cs`)**:
  * Resolved cross-session portal scrambling in `ZDOMan_ConnectPortals` by eliminating premature current-session ID collision check (`GetZDO(targetId)`).
  * Built an $O(1)$ dictionary lookup (`portalsByPreviousId`) to resolve previous session target IDs directly against each portal's loaded `Key_PreviousId`.
  * Expanded portal enumeration to `ZDOMan.instance.GetPortalList()` supplemented with `ZDOExtraData` connection IDs to ensure all loaded portals are captured.
  * Ensured unresolvable targets are safely cleared (`ZDOID.None`) rather than attaching to mismatched runtime entities.

# 2.0.9 - Dedicated Server UI Patch Hardening & Dependency Updates
* **Dedicated Server Isolation (`Environment.cs`, `Patches/Patcher.cs`, `Patches/Dropdown.cs`)**:
  * Switched headless detection to `Jotunn.Managers.GUIManager.IsHeadless()` directly, removing `SystemInfo.graphicsDeviceType` in compliance with repository invariants.
  * Added early returns in `Dropdown_*` patches and guarded UI patch registrations in `Patcher.Patch()` when running on headless servers.
* **Portal Reconnection & Identity (`Patches/ZDOMan.cs`, `KnownPortal.cs`, `KnownPortalsManager.cs`)**:
  * Fixed ZDOID type mismatch in `ZDOMan_ConnectPortals` where `Key_PreviousId` was checked via `GetString()` instead of `GetZDOID()`, avoiding spurious fallback lookup on session load.
  * Implemented `IEquatable<KnownPortal>`, `Equals`, and `GetHashCode` based on `ZDOID` on `KnownPortal`.
  * Updated `KnownPortalsManager.UpdateFromList` to reconcile using `HashSet<ZDOID>`, eliminating object reference mismatch during network resync.
* **Placement & State Hardening (`Patches/Piece.cs`, `Patches/WearNTear.cs`, `Patches/Player.cs`)**:
  * Eliminated static `m_WearNTear` field in `Piece_SetCreator`, scoped check strictly to portal pieces, and passed instance via `QueuedAction` state.
  * Added null safety guards on `Piece`, `piece.m_name`, and `ZNetView` in `WearNTear_OnPlaced.Postfix`, resolving `XPORTALNETWORKS-9`.
  * Removed dead `Patches/Player.cs` stub.
* **RPC & Server Hardening (`RPC/ServerEvents.cs`, `RPC/XPortalNetworksAdminSync.cs`, `NetPeerUtility.cs`, `RPC/RPCManager.cs`)**:
  * Guarded `peer.m_socket != null` before `GetHostName()` in `RPC_RequestAdminSync` and `NetPeerUtility.IsPeerPrivilegedForPortalNetwork`.
  * Protected `UserInfo.GetLocalUser()` with try-catch in `XPortalNetworksAdminSync.IsLocalPortalNetworkAdmin()`.
  * Guarded `ZRoutedRpc.instance == null` in `RPCManager.Register()`.
* **Unity Lifecycle & Code Hygiene (`UI/PortalConfigurationPanel.cs`, `XPortalNetworks.cs`)**:
  * Replaced `?.` on Unity objects (`Dropdown`, `ScrollRect`, `Component`, `GameObject`) with explicit `!= null` checks adhering to Unity lifecycle semantics.
  * Removed legacy XML summary blocks across codebase.
* **Ecosystem Compatibility**:
  * Noted that an issue in [ValheimCommunityPatch](https://thunderstore.io/c/valheim/p/MidnightMods/ValheimCommunityPatch/) prevented portal network connections; resolved in ValheimCommunityPatch 0.29.0.
* **Dependency Updates**:
  * Updated internalized `Vapok.Valheim.Common` to 3.19.1015.
  * Updated `JotunnLib` dependency to 2.30.2.

# 2.0.8 - Valheim 1.0.15 Alignment & Internalized Dependency Updates
* **Valheim 1.0.15 Alignment**:
  * Aligned publicized game assembly and UnityEngine references to Valheim 1.0.15.
  * Updated internalized `Vapok.Valheim.Common` dependency to 3.13.1015.
* **Transpiler & Patch Hardening**:
  * Added index bounds validation (`i + 2 < instrs.Count`) and null-safe operand equality checks (`Equals(instrs[i+2].operand, mTargetFound)`) in `TeleportWorld_UpdatePortal_Transpiler`.
  * Added safety guards against unresolvable target members (`m_target_found` and `IsUsablePortal`) to prevent Harmony `ArgumentException` during patch initialization.
* **Stability & Localization**:
  * Synchronized all 35 game localizations for splash screen and configuration registry.
  * Audited network RPCs, ZDO portal mappings, and headless UI isolation against game version 1.0.15.

# 2.0.7 - Scene Transition & Portal Target Exception Hardening
* **Scene Transition Exception Resolution**:
  * Fixed `ArgumentException: The scene is invalid` thrown by `Environment.IsHeadless` when queried during active scene loading and logout transitions.
  * Cached headless state in `Environment.IsHeadless` and implemented a protected fallback to `SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null`.
  * Updated `PortalConfigurationPanel.InitialiseUI()` to reference the cached `Environment.IsHeadless` property rather than querying `GUIManager.IsHeadless()` directly.
* **Portal Lookup Null-Safety & Dictionary Resilience**:
  * Replaced unsafe dictionary indexer (`knownPortals[id]`) in `KnownPortalsManager.GetKnownPortalById(ZDOID id)` with `knownPortals.TryGetValue(id, out var portal) ? portal : null` to avoid `KeyNotFoundException`.
  * Added null guards across all callers (`KnownPortal.GetFriendlyTargetName()`, `XPortalNetworks.OnPrePortalHover()`, `XPortalNetworks.OnPortalRequestText()`, `XPortalNetworks.OnPortalDestroyed()`, `ServerEvents.RPC_AddOrUpdateRequest()`, and `PortalConfigurationPanel.ResolveInitialDestinationNetworkOwnerId()`).
* **Map Ping Hardening**:
  * Guarded `SendToClient.PingMap()` against null `ZRoutedRpc.instance` and null `UserInfo.GetLocalUser()` instances.
  * Added exception handling and fallback name string assignment to prevent UI cancellation during map ping requests.

# 2.0.6 - Splash Window Updates & Valheim 1.0.14 Alignment
* **Splash Window Updates**:
  * Updated telemetry default to unchecked on first launch (Opt-In).
  * Added Send Error Logs toggle (Opt-Out) to capture anonymous crash diagnostics and error reports.
  * Added in-game scrollable Privacy Policy overlay with responsive mouse wheel support.
  * Added interactive tooltip data disclaimers on checkbox hover.
* **Valheim 1.0.14 Alignment**:
  * Aligned publicized game assembly and UnityEngine references to Valheim 1.0.14.
  * Updated internalized Vapok.Valheim.Common dependency to 3.12.1014.

# 2.0.5 - Jewelcrafting Font Compatibility
* **Compatibility Fix**: Fixed issue where Jewelcrafting packages its own font which was overriding part of a vanilla font, causing the Splash screen to appear blank.
* **Vapok.Common Dependency Bump**: Updated internalized dependency to `Vapok.Valheim.Common` 3.11.1012.

# 2.0.4 - Updated README with Telemetry Information
* **Documentation Update**: Updated the README.md with Anonymous Telemetry and Privacy section per request of mod stores.
* **Vapok.Common Dependency Bump**: Updated internalized dependency to `Vapok.Valheim.Common` 3.9.1012.

# 2.0.3 - Unified Splash Screen & Telemetry Controls
* **Unified Startup Splash Screen & Telemetry**:
  * Updated `Vapok.Valheim.Common` dependency reference to `v3.5.1012`.
  * Registered mod metadata with centralized `ModSplashManager`.
  * Added `ShowSplashOnStartup` and `Enable Anonymous Telemetry` configuration bindings to `ConfigRegistry`.

# 2.0.1 - Dependency & Compatibility Maintenance
* **Runtime & Dependency Updates**:
  * Synchronized package manifest and project references with Jotunn `2.30.0` and BepInEx `5.4.2350`.
  * Verified build pipeline and ILRepack bundling with `Vapok.Valheim.Common` `3.2.1012`.
* **Compatibility & Documentation**:
  * Validated portal destination selection UI and network configuration hot-reloading against current Valheim 1.0 builds.
  * Standardized mod documentation, changelog tiers, and release staging.

# 2.0.0 - Portal Networks & Valheim 1.0+ Overhaul
* **Portal Networks Architecture**:
  * Overhauled portal mechanics to introduce an expansive multi-tier **Portal Networks** system:
    * **Global / Public Network**: Accessible to all players on the server without restriction.
    * **Player Networks & Private Portals**: Dedicated per-player network channels with private portal protection to restrict unauthorized access.
    * **Custom Named Networks**: Dynamic support for up to 15 server-defined custom networks configured in `xportal_networks.json` with live hot-reloading support.
* **Server Administration & Permission Controls**:
  * Implemented permission checks restricting portal deconstruction and destruction to the original creator or authenticated server admins.
  * Synchronized portal network configurations and permission sets strictly across dedicated servers via Jotunn ServerSync.
* **Valheim 1.0 Compatibility & Core Updates**:
  * Updated assembly references for Valheim 1.0 (`1.0.12`), BepInEx 5.4.2350, and Jotunn 2.30.0.
  * Rebuilt on .NET Framework 4.8.
  * Bundled `Vapok.Valheim.Common` 3.2.1012 via ILRepack.
* **UI, Gamepad & Networking Fixes**:
  * Resolved controller legend rendering artifacts and gamepad input focus issues in portal configuration dialogs.
  * Fixed dedicated server admin portal destruction permission validation.
  * Improved ZDO network key synchronization and portal pairing resolution to eliminate connection dropouts under high network load.

# 1.0.0 - Initial Portal Management Release
* Initial release of portal grouping and tag management mechanics.
