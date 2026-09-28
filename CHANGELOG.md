# 2.0.11 - Dependency Updates & Stability
* Updated internal dependencies for stability.

<details>
<summary><b>2.0 Changelog History (Valheim Release)</b> (<i>click to expand</i>)</summary>

### 2.0.10 - Portal Connection Fix
> **Author's Note:** Apologies for the update earlier which messed up portals connections. This has been fixed.

* Fixed portal destinations reconnecting to incorrect portals on world reload or server restart.

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

