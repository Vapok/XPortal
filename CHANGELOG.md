# 2.3.2 - Offline-Capable Research Tooling
* **Local web access for development**: `.vscode/mcp.json` now configures two locally-hosted MCP servers - `mcp/fetch` for URL retrieval (verified against the previously-failing GitHub URL) and `mcp/brave-search` for keyword web search. The Brave API key is requested through VS Code's secure prompt (`${input:...}`, `password: true`), so no key is stored in the repository.
* **Documented correction (`REFERENCES.md`)**: ConfigurationManager's source confirms that `IsAdminOnly`/`IsUnlocked` are **sync-library** fields, not ConfigurationManager ones - it resolves an attributes class by type name and copies only matching fields, and its own class has no admin concept. Server-owned settings are therefore enforced by ServerSync (the server's value is pushed and local writes to synced entries are blocked), not by a lock in the ConfigurationManager window.
* No mod behaviour changes: this release is tooling/documentation only.

# 2.3.1 - Reference Documentation
* **New `REFERENCES.md`**: every external source used for this project is now documented in one place - the Valheim 1.0.16 (Unity 6000.0.75f1) game assemblies, BepInEx 5.4.2350, HarmonyX 2.9.0, Jötunn 2.30.2, Vapok.Valheim.Common 3.21.1015, the build tooling (AssemblyPublicizer, ILRepack, Mono.Cecil, nuget.exe), the offline API-doc and metadata sources used for verification, referenced third-party mods, and the source-availability caveats.

# 2.3.0 - Server-Owned Config via Jotunn ServerSync
* **Server settings are now editable from the game client by admins**: The server-owned settings (`PingMapDisabled`, `DoublePortalCosts`, `HidePortalDistance`, `RestrictPortalRemoval` and `AdminsSeeAllNetworks`) are handed to Jotunn's ServerSync. Server admins and the host can change them from within the game through the ConfigurationManager window, and the new value is sent to the server and pushed to every client immediately.
* **Non-admins can no longer change server-owned settings**: Those entries are tagged admin-only, so players without admin rights cannot edit them, and any local edit is overwritten by the server's value.
* **Removed the custom config sync**: The bespoke `XPortalNetworks_Config` RPC was server-to-client only, so client-side changes were silently ignored. It has been removed in favour of ServerSync; portal network lists are still re-pushed by the server whenever a relevant setting changes.

# 2.2.1 - Admin Bypass Live Toggle Fix
* **`AdminsSeeAllNetworks` now applies on the server**: The server's effective settings were only aliased to the local config at plugin load (before the network existed), so the option could read as disabled on a dedicated server. The alias is now re-asserted at session start and on every config reload.
* **Live toggle re-pushes networks**: Changing the setting on the server now re-pushes each client's permitted network list, so admins gain/lose network access immediately without relogging.

# 2.2.0 - Admin Network Bypass Option
* **New config option `AdminsSeeAllNetworks`** (default **disabled**): when disabled, server admins/host are treated like normal players for portal networks — they only see and use unrestricted networks, or networks they are members of. When enabled, admins can see and use every network (the previous behaviour).
* The admin bypass is now applied consistently across network visibility (dropdowns/hover), portal interaction, teleporting, and server-side portal edits.

# 2.1.2 - Valheim 1.0.16 Alignment
* **Valheim 1.0.16**: Re-publicized the game assemblies and updated all reference paths so the mod compiles against Valheim 1.0.16.
* **Single-source game version**: The Valheim version is now defined once (`ValheimGameVersion` in `Directory.Build.props`) and shared by the csproj and the `tools/` scripts.

# 2.1.1 - Config Hot-Reload Resilience
* **Deleted config is re-seeded**: If `xportal_networks.json` is removed while the server is running, the reload poll now detects the deletion and recreates the file from the embedded default — previously this relied solely on a file-watch delete event.
* **No redundant reloads**: The watcher now re-baselines the file's timestamp/size after each reload, avoiding a spurious second reload pass.

# 2.1.0 - Team Portal Networks
* **Team Networks (`allow_list`)**: `xportal_networks.json` entries can now include an optional `"allow_list"` of player ids. Only listed players can see the network in the portal configuration UI, edit its portals, or step through them; an omitted/empty list keeps the network open to everyone.
* **Server-Authoritative Policy**: Each client now receives only the networks it is permitted to use (names only — allow lists never leave the server), and the server rejects portal edits and links on networks a player cannot access.
* **Privacy & UI**: Portals on a restricted network the player isn't a member of are hidden from hover text and from the network/destination dropdowns.
* **Config Hot-Reload**: `xportal_networks.json` edits are now applied live on the game's main thread, with a polling fallback so changes are picked up even when file-watch events are missed — no server restart required.

# 2.0.10 - Portal Connection Fix
> **Author's Note:** Apologies for the update earlier which messed up portals connections. This has been fixed.

* Fixed portal destinations reconnecting to incorrect portals on world reload or server restart.

<details>
<summary><b>2.0 Changelog History (Valheim Release)</b> (<i>click to expand</i>)</summary>

### 2.0.9 - Dedicated Server UI Patch Hardening & Dependency Updates
* **Dedicated Server Safety**: Ensured UI hooks and hover text patches are bypassed on headless dedicated servers.
* **Compatibility Notice**: An issue in [ValheimCommunityPatch](https://thunderstore.io/c/valheim/p/MidnightMods/ValheimCommunityPatch/) prevented portals from connecting properly. This has been resolved in version 0.29.0 of that mod; please ensure you update if you use it.
* **Game Shutdown & Placement Safety**: Cleaned up shutdown routines to prevent harmless errors on game exit and hardened piece placement checks.
* **Dependency Updates**: Updated Jotunn to 2.30.2 and internal dependencies for stability.

### 2.0.8 - Valheim 1.0.15 Alignment & Internalized Dependency Updates
* **Valheim 1.0.15 Alignment**: Updated game assembly references and internalized `Vapok.Valheim.Common` 3.13.1015.
* **Transpiler & Patch Hardening**: Added bounds validation and null-safety guards to the `TeleportWorld.UpdatePortal` transpiler.
* **Localization & Stability**: Re-synchronized 35-language splash localizations and verified patch compatibility.

### 2.0.7 - Scene Transition & Portal Target Exception Hardening
* **Scene Transition Fix**: Resolved an `ArgumentException: The scene is invalid` during world loading and logout scene transitions by safely caching headless environment checks.
* **Portal Target Resilience**: Fixed a `KeyNotFoundException` crash when inspecting, hovering over, or interacting with portals whose linked destination had been destroyed or moved out of the active zone.
* **Map Ping Hardening**: Hardened the map ping broadcast RPC with safe fallbacks and exception protection when user or network instances are initializing.

### 2.0.6 - Splash Window Updates & Valheim 1.0.14 Alignment
* **Splash Window Updates**:
  * Telemetry is now unchecked when first loaded (Opt-In visibility)
  * Added Send Error Logs (Opt-Out)
  * Privacy Policy is now available directly in-game
  * Added Data Disclaimers on hover over checkboxes for transparency on what data is sent
* **Valheim 1.0.14 Alignment**: Updated game assembly references and internalized Vapok.Valheim.Common 3.12.1014.


### 2.0.5 - Jewelcrafting Font Compatibility
* Fixed: Jewelcrafting packages it's own font which was overriding part of a vanilla font, causing the Splash screen to appear blank.
### 2.0.4 - Updated README with Telemetry Information
* Updated the README.md with Anonymous Telemetry information per request of mod stores.

### 2.0.3 - Unified Splash Screen & Telemetry Controls
* **Unified Startup Splash Screen**: Integrated with a centralized startup splash screen.
  * Added configurable `Show on Game Startup` which can be enabled or disabled in the configuration file.
* **Anonymous Telemetry**: 
  * Added configurable `Enable Anonymous Telemetry` configuration which can be enabled or disabled in the configuration file.
    * Defaults to enabled with auto-opt-in on launch. Uncheck to Opt-Out
    * ANONYMOUS DATA ONLY - I track version number and usage data. No personal data is ever collected. For more information, see the [Privacy Policy](https://vapok.io/privacy-policy/).

### 2.0.1 - Dependency & Compatibility Maintenance
* **Dependency Updates**: Updated Jotunn and BepInEx runtime package bindings.
* **Compatibility Maintenance**: Verified compatibility against the latest Valheim 1.0 release.
* **Documentation Improvements**: Standardized README, user guides, and technical patch documentation.

### 2.0.0 - Portal Networks & Valheim 1.0+ Overhaul
* **Portal Networks Architecture**:
  * Expanded into **XPortal Networks** with support for Global (Public), Player-Specific (Private), and Custom Named networks (up to 15 configured in `xportal_networks.json` with live hot-reloading).
* **Server Admin & Permission Controls**:
  * Added permission settings to restrict portal destruction to the creator or authenticated server admins.
  * Synchronized portal network settings and permissions across dedicated servers.
* **Modernization & Bug Fixes**:
  * Updated for Valheim 1.0+, .NET Framework 4.8, BepInEx 5.4.2350, and Jotunn 2.30.0.
  * Resolved controller legend display and gamepad navigation issues.
  * Improved network synchronization and portal pairing reliability.

</details>

