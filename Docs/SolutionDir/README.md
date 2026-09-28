# XPortal Networks

**XPortal Networks** is a Valheim mod that overhauls the vanilla portal system. Instead of being restricted to matching identical portal tags or constructing massive portal hubs, XPortal Networks lets you select any portal's destination directly from an interactive list—now featuring an expanded **Portal Networks** system with Public, Private, and Custom Named Networks!

<p align="center">
  <img src="https://raw.githubusercontent.com/Vapok/XPortalNetworks/main/images/controller.gif" alt="XPortal Networks in Action" height="180" />
</p>

---

## Where to Download

<div align="center">

[![Nexus Mods](https://raw.githubusercontent.com/SpikeHimself/resources/main/images/thirdparty/nexus-logo-small.png)](https://www.nexusmods.com/valheim/mods/2239) &nbsp;&nbsp;&nbsp; [![Thunderstore](https://raw.githubusercontent.com/SpikeHimself/resources/main/images/thirdparty/thunderstore-logo-small.png)](https://valheim.thunderstore.io/package/Vapok/XPortalNetworks/)

</div>

---

## What's New in XPortal Networks

XPortal Networks builds upon the solid foundation of the original XPortal mod by SpikeHimself, expanding it into a dedicated networking framework with extensive multiplayer features and numerous bug fixes:

* **Portal Networks**: Group portals into distinct networks:
  * **Global / Public Network**: Accessible to all players on the server.
  * **Player Networks & Private Portals**: Portals tied to individual players. Toggle the **Private** setting so unauthorized players cannot view or teleport through your personal portals.
  * **Custom Named Networks**: Define up to 15 server-wide custom networks (such as *Trade Hub*, *Clan Base*, *Mining Outposts*, or *Admin Only*) via configuration, complete with real-time hot-reloading.
* **Server Admin & Permission Controls**: Configurable permissions allowing server admins to manage networks and prevent non-owners from deconstructing portals.
* **Bug Fixes & Modernization**:
  * Upgraded for the latest Valheim versions and .NET Framework 4.8.
  * Resolved controller UI legend and navigation issues.
  * Fixed dedicated server admin destruction and permission edge-cases.
  * Enhanced ZDO network synchronization and reconnection reliability.

---

## Features

### 🌐 Destination Selection Menu
When interacting with a portal, a clean UI opens allowing you to select your target destination from a dropdown menu. The list displays:
* The destination portal name
* Distance to the destination (in meters)
* Portal light color indicator (when paired with mods like Advanced Portals or Stone Portal)

### 🔒 Public, Private & Custom Networks
Organize your world’s transportation:
* **Global Network**: The shared network open to everyone.
* **Personal Network**: Portals automatically grouped under your character.
* **Private Portals**: Mark sensitive portals as private so other players cannot use or retarget them.
* **Custom Named Networks**: Admin-defined channels defined in `xportal_networks.json` that organize portals by faction, region, or purpose.

### ⭐ Default Portal Destination
You can set a portal as your **Default Portal**. Newly constructed portals will immediately link to your default portal automatically, saving you time when setting up forward operating bases.

### 🏷️ Uncapped Portal Name Length
XPortal Networks removes the vanilla character limit on portal tags, allowing you to give your portals descriptive and memorable names.

### 📍 Ping Portal on Map
Forgot where a portal leads? Click the **Ping** button to highlight the destination portal directly on your map and alert your fellow adventurers with a map ping.

### 🎮 Full Gamepad & Controller Support
Fully navigable using controllers with integrated on-screen key hints:

<p align="center">
  <img src="https://raw.githubusercontent.com/Vapok/XPortalNetworks/main/images/ui-keyhints-small.png" alt="Gamepad Keyhints" />
</p>

| Button (Xbox / PlayStation) | Action |
| :--- | :--- |
| **A** / **Cross** | Confirm / Submit portal configuration |
| **B** / **Circle** | Cancel / Close menu |
| **Y** / **Triangle** | Ping selected destination on map |
| **X** / **Square** | Open / close destination dropdown list |
| **D-Pad Up / Down** | Navigate destination list |

---

## Mod Compatibility & Integration

* **[Jötunn, the Valheim Library](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)** (Required)
* **[Advanced Portals](https://valheim.thunderstore.io/package/RandyKnapp/AdvancedPortals/)**: Fully integrated—displays matching colored light icons in dropdowns.
* **[Stone Portal](https://valheim.thunderstore.io/package/JereKuusela/Stone_Portal/)**: Supported with distinctive portal coloring.
* **[VHVR - Valheim VR](https://valheim.thunderstore.io/package/Maynard/VHVR/)**: Compatible.
* **[Nexus Update Check](https://valheim.thunderstore.io/package/nexusreupload/aedenthorn_Nexus_Update_Check/)**: Compatible.

*Note: Incompatible with AnyPortal (XPortal Networks replaces and supersedes AnyPortal functionality).*

---

## How to Use

1. **Build a Portal**: Place a portal as normal.
2. **Access the Configuration UI**: Walk up to the portal and press your interact key (`E` / `A`).
3. **Configure Your Portal**:
   * **Portal Name**: Enter a name for the current portal.
   * **Network**: Choose whether this portal belongs to the *Global* network, your *Personal* network, or a *Custom Named Network*.
   * **Destination**: Select the destination portal from the dropdown list.
   * **Make Private**: (Optional) Check to restrict access so only you (and admins) can use or alter the portal.
   * **Set as Default**: (Optional) Check to make this portal the automatic destination for newly built portals.
4. **Confirm**: Click **OK** to save and activate the connection.

---

## Configuration

### General & Server Settings
The main configuration file is located at `BepInEx/config/vapok.mods.xportalnetworks.cfg`. Server-owned settings (`PingMapDisabled`, `DoublePortalCosts`, `HidePortalDistance`, `RestrictPortalRemoval` and `AdminsSeeAllNetworks`) are synchronized from the server to every connected client via Jotunn's ServerSync, and can only be changed by server admins (or the host) - including from within the game client through the ConfigurationManager window.

* **`PingMapDisabled`** *(Server Enforced)*: Disables map pinging for servers playing with `nomap` or immersive navigation rules.
* **`HidePortalDistance`** *(Server Enforced)*: Hides the meter distance displayed next to portal names in the dropdown.
* **`DoublePortalCosts`** *(Server Enforced)*: Doubles portal crafting costs to balance the convenience of one-to-many portal routing.
* **`RestrictPortalRemoval`** *(Server Enforced)*: Restricts deconstructing/destroying portals to the original creator or server admins.
* **`DisplayPortalColour`**: Displays colored indicators matching portal types in the menu.

### Custom Named Networks (`xportal_networks.json`)
Servers can define custom networks by editing `BepInEx/config/XPortalNetworks/xportal_networks.json`. Changes to this file are automatically detected and reloaded live without needing to restart the server:

```json
[
  { "id": 1, "name": "Admin Network" },
  { "id": 2, "name": "Trade Hub" },
  { "id": 3, "name": "North Outposts" }
]
```
*(Supports network IDs 1 through 15).*

---

## Installation

### Prerequisites
* **[BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)** (v5.4.2200+)
* **[Jötunn (ValheimLib)](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)** (v2.20.0+)

### Automatic (Recommended)
Use a mod manager like **r2modman** or **Vortex** to download and install XPortal Networks with one click.

### Manual Installation
1. Download the latest release `.zip` from Thunderstore, Nexus Mods, or GitHub Releases.
2. Extract the archive contents into your `Valheim/BepInEx/plugins/` directory.
3. Ensure both client and dedicated server have XPortal Networks installed.

---

## Bugs, Feature Requests & Translations

* **Bug Reports**: Please submit an issue on the [GitHub Issues](https://github.com/Vapok/XPortalNetworks/issues) page using the `Bug report` template. Please include your `LogOutput.log` file.
* **Feature Requests**: Open an issue on GitHub selecting the `Feature request` template.
* **Translations**: Contributions for new languages or localization updates are welcome via GitHub pull requests or issues.

---

## Credits & Acknowledgements

* **[SpikeHimself](https://github.com/SpikeHimself)**: Creator of the original **XPortal** mod, upon which XPortal Networks is built and expanded.
* **[sweetgiorni](https://valheim.thunderstore.io/package/sweetgiorni/AnyPortal/)**: Creator of the original AnyPortal concept.
* **Translations & Community**: Thanks to *kaiqueknup*, *makou*, *Smok3y97*, *MexExe*, *hanawa07*, *bonesbro*, *VasariRulez*, *Felix*, and *cawa-93* for original translations and community contributions.
