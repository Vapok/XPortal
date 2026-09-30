# 2.0.12 - UI Interaction Hardening & Legacy Mod Coexistence
* **UI Priority & Escape Key Fallback (`UI/PortalConfigurationPanel.cs`)**:
  * Elevated `mainPanel`'s `UIGroupHandler.m_groupPriority` to `100`, preventing background HUDs and third-party mod panels from deactivating its `CanvasGroup.interactable` state.
  * Added an immediate `Input.GetKeyDown(KeyCode.Escape)` / `ZInput.GetKeyDown(KeyCode.Escape)` escape hatch inside `PortalConfigurationPanel.HandleInput()`, ensuring the menu can be closed and `GUIManager.BlockInput(false)` released at any time regardless of `UIGroupHandler` / `UIGamePad` interactive state.
* **Legacy XPortal Neutralization & Coexistence (`Compatibility/LegacyCompatibility.cs`, `XPortalNetworks.cs`, `Patches/Patcher.cs`)**:
  * Added soft dependency `[BepInDependency(LegacyCompatibility.LegacyXPortalPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]` ensuring legacy `yay.spikehimself.xportal` loads before `XPortalNetworks`.
  * Created `LegacyCompatibility.NeutralizeLegacyXPortal()`: checks `Chainloader.PluginInfos`, sets `legacyPlugin.Instance.enabled = false` to stop legacy update loops, and calls `Harmony.UnpatchID` for both `yay.spikehimself.xportal.harmony` and `yay.spikehimself.xportal` to cleanly strip conflicting hooks without raising compatibility crash dialogs.
* **TMP Font Fallback Warning Resolution (`UI/UIFonts.cs`, `UI/PortalConfigurationPanel.cs`)**:
  * Created `UIFonts.cs` to resolve and cache `Valheim-AveriaSansLibre` and ensure `TMP_Settings.defaultFontAsset` is populated before TMP components awake.
  * Refactored `CreateGamepadHint` to avoid premature `TextMeshProUGUI.Awake()` with unassigned fonts, eliminating the 8 consecutive `The LiberationSans SDF Font Asset was not found` warnings.
* **Defensive Portal Interaction Safety (`XPortalNetworks.cs`)**:
  * Added defensive null and validity guards on `teleportWorld`, `teleportWorld.m_nview`, and `teleportWorld.m_nview.GetZDO()` in `OnPortalRequestText`, resolving Sentry issue `XPORTALNETWORKS-B`.

# 2.0.11 - Dependency Updates & Stability
* **Library & Configuration Updates**:
  * Internalized `Vapok.Valheim.Common` 3.1016.24.
  * Migrated `XPortalNetworksConfig` to inherit from `ConfigSyncBase`, enabling centralized configuration syncing for telemetry, error reports, and log levels while preserving existing `[General]` configuration keys and RPC synchronization.
  * Converted mod logging to use `Vapok.Common.Managers.LogManager`.
  * Updated game assembly references to 1.0.16.

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
