using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace XPortalNetworks
{
    internal sealed class XPortalNetworksConfig
    {
        ////////////////////////////
        //// Singleton instance ////
        private static readonly Lazy<XPortalNetworksConfig> lazy = new Lazy<XPortalNetworksConfig>(() => new XPortalNetworksConfig());
        public static XPortalNetworksConfig Instance { get { return lazy.Value; } }
        ////////////////////////////

        public event Action OnLocalConfigChanged;

        /// <summary>
        /// Suffix for settings owned by the server. Those entries carry the
        /// <see cref="ConfigurationManagerAttributes.IsAdminOnly"/> attribute (Jotunn's attribute
        /// type), which hands them over to Jotunn's ServerSync: the server pushes its values into
        /// this config file on every client, and only server admins (or the host) may change them.
        /// </summary>
        private const string Desc_EnforcedByServer = " This setting is owned by the server: it is synchronized from the server to all clients and can only be changed by server admins (or the host).";

        private ConfigFile configFile;

        /// <summary>
        /// Container class for all of XPortal's config settings
        /// </summary>
        public class ConfigSettings
        {
            public bool PingMapDisabled;
            public bool DisplayPortalColour;
            public bool DoublePortalCosts;
            public ConfigEntry<Vector3> DefaultPortal;
            public ConfigEntry<bool> DefaultPrivatePortal;
            public bool HidePortalDistance;
            /// <summary>Server-enforced portal hammer removal rules.</summary>
            public bool RestrictPortalRemoval;
            /// <summary>Server-enforced: when true, server admins/host bypass portal-network allow lists.</summary>
            public bool AdminsSeeAllNetworks;

            /// <summary>
            /// Server-owned portal network names, indexed by network id (1–15). An empty name leaves
            /// that slot unused.
            /// </summary>
            public ConfigEntry<string>[] NetworkNames = new ConfigEntry<string>[CustomNetworks.MaxId + 1];

            /// <summary>
            /// Server-owned allow lists, indexed by network id (1–15): comma separated player ids
            /// (e.g. <c>Steam_12345678901234567</c>). Empty means the network is open to everyone.
            /// </summary>
            public ConfigEntry<string>[] NetworkAllowLists = new ConfigEntry<string>[CustomNetworks.MaxId + 1];

            public ConfigEntry<bool> ShowSplashOnStartup;
            public ConfigEntry<bool> EnableTelemetry;
        }

        /// <summary>
        /// Track the config settings. Server-owned entries are synchronized into this config file
        /// by Jotunn's ServerSync, so the values read here are authoritative on every peer.
        /// </summary>
        public ConfigSettings Local { get; set; }

        private XPortalNetworksConfig()
        {
            Local = new ConfigSettings();
        }

        /// <summary>
        /// Load the config file, and track the settings inside it
        /// </summary>
        /// <param name="configFile">The config file being loaded</param>
        public void LoadLocalConfig(ConfigFile configFile)
        {
            this.configFile = configFile;
            ReloadLocalConfig();

            this.configFile.ConfigReloaded += LocalConfigChanged;
            this.configFile.SettingChanged += LocalConfigChanged;
        }

        /// <summary>
        /// Reload the settings inside the config file
        /// </summary>
        private void ReloadLocalConfig()
        {
            // Add Nexus ID to config for Nexus Update Check (https://www.nexusmods.com/valheim/mods/102)
            configFile.Bind("General", "NexusID", Mod.Info.NexusId, "Nexus mod ID for updates (do not change)");

            // Add PingMapDisabled option which disables the Ping Map button
            var cfgPingMapDisabled = configFile.Bind(
                "General",
                "PingMapDisabled",
                false,
                new ConfigDescription(
                    "Disable the Ping Map button completely. For players who wish to play without a map." + Desc_EnforcedByServer,
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Local.PingMapDisabled = cfgPingMapDisabled.Value;

            var cfgDisplayPortalColour = configFile.Bind("General", "DisplayPortalColour", false, "Show a \">>\" tag in the list of portals that has the same colour as the light that the portal emits (integration with \"Advanced Portals\" by RandyKnapp).");
            Local.DisplayPortalColour = cfgDisplayPortalColour.Value;

            var cfgDoublePortalCosts = configFile.Bind(
                "General",
                "DoublePortalCosts",
                false,
                new ConfigDescription(
                    "By using XPortalNetworks, you effectively only need half the amount of portals. To compensate for that, we can double the costs of portals." + Desc_EnforcedByServer,
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Local.DoublePortalCosts = cfgDoublePortalCosts.Value;

            Local.DefaultPortal = configFile.Bind("General", "DefaultPortal", Vector3.zero, "The Portal that newly built Portals immediately connect to.");

            Local.DefaultPrivatePortal = configFile.Bind(
                "General",
                "DefaultPrivatePortal",
                true,
                "If true, newly placed portals start as private (owner-only). If false, they start public on the Global network until changed.");

            var cfgHidePortalDistance = configFile.Bind(
                "General",
                "HidePortalDistance",
                false,
                new ConfigDescription(
                    "In the list of portals, do not show how far away other portals are." + Desc_EnforcedByServer,
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Local.HidePortalDistance = cfgHidePortalDistance.Value;

            var cfgRestrictPortalRemoval = configFile.Bind(
                "General",
                "RestrictPortalRemoval",
                false,
                new ConfigDescription(
                    "When true, only the player who placed the portal or a server admin may remove it with the hammer. Other removal (e.g. structural damage) is unchanged." + Desc_EnforcedByServer,
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Local.RestrictPortalRemoval = cfgRestrictPortalRemoval.Value;

            var cfgAdminsSeeAllNetworks = configFile.Bind(
                "General",
                "AdminsSeeAllNetworks",
                false,
                new ConfigDescription(
                    "When true, server admins (and the host) can see and use every portal network, bypassing allow lists. When false, admins are treated like normal players and only see/use unrestricted networks or networks they are members of." + Desc_EnforcedByServer,
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Local.AdminsSeeAllNetworks = cfgAdminsSeeAllNetworks.Value;

            // Portal networks (ids 1-15). Server-owned and admin-editable in-game: an empty name
            // leaves the slot unused, an empty allow list means the network is open to everyone.
            for (var id = CustomNetworks.MinId; id <= CustomNetworks.MaxId; id++)
            {
                Local.NetworkNames[id] = configFile.Bind(
                    "Portal Networks",
                    $"Network {id} Name",
                    string.Empty,
                    new ConfigDescription(
                        $"Display name of portal network {id}. Leave empty to keep this network unused." + Desc_EnforcedByServer,
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true }));

                Local.NetworkAllowLists[id] = configFile.Bind(
                    "Portal Networks",
                    $"Network {id} Allow List",
                    string.Empty,
                    new ConfigDescription(
                        $"Comma separated player ids allowed to use portal network {id} (e.g. Steam_12345678901234567). " +
                        "Leave empty to let everyone use it; ignored while the network has no name." + Desc_EnforcedByServer,
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true }));
            }

            Local.ShowSplashOnStartup = configFile.Bind(
                "Local Config",
                "Show Splash on Startup",
                true,
                new ConfigDescription("If enabled, displays the mod overview and links splash screen on game startup.",
                    null, new Vapok.Common.Shared.ConfigurationManagerAttributes { Order = 4 }));

            Local.EnableTelemetry = configFile.Bind(
                "Local Config",
                "Enable Anonymous Telemetry",
                true,
                new ConfigDescription("If enabled, sends anonymous mod launch and heartbeat telemetry to help improve mod stability and track active versions.",
                    null, new Vapok.Common.Shared.ConfigurationManagerAttributes { Order = 5 }));
        }

        /// <summary>Configured display name for a network id (empty when the slot is unused).</summary>
        internal string GetNetworkName(int id)
        {
            if (id < CustomNetworks.MinId || id > CustomNetworks.MaxId)
            {
                return string.Empty;
            }

            return Local.NetworkNames[id]?.Value ?? string.Empty;
        }

        /// <summary>Raw allow-list setting for a network id.</summary>
        internal string GetNetworkAllowList(int id)
        {
            if (id < CustomNetworks.MinId || id > CustomNetworks.MaxId)
            {
                return string.Empty;
            }

            return Local.NetworkAllowLists[id]?.Value ?? string.Empty;
        }

        /// <summary>True when at least one network slot has a name.</summary>
        internal bool HasAnyNetworkDefined()
        {
            for (var id = CustomNetworks.MinId; id <= CustomNetworks.MaxId; id++)
            {
                if (!string.IsNullOrWhiteSpace(GetNetworkName(id)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Seeds the network entries from the legacy JSON import and saves the config. Only empty
        /// entries are filled, so nothing an admin already configured is overwritten.
        /// </summary>
        internal void ApplyImportedNetworks(IEnumerable<CustomNetworks.PortalNetworkDefinition> definitions)
        {
            var changed = false;

            foreach (var definition in definitions)
            {
                var id = (int)definition.Id;
                if (id < CustomNetworks.MinId || id > CustomNetworks.MaxId)
                {
                    continue;
                }

                var nameEntry = Local.NetworkNames[id];
                if (nameEntry != null && string.IsNullOrWhiteSpace(nameEntry.Value) && !string.IsNullOrWhiteSpace(definition.Name))
                {
                    nameEntry.Value = definition.Name;
                    changed = true;
                }

                var listEntry = Local.NetworkAllowLists[id];
                if (listEntry != null && string.IsNullOrWhiteSpace(listEntry.Value) && definition.AllowList.Count > 0)
                {
                    listEntry.Value = string.Join(", ", definition.AllowList);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            try
            {
                configFile?.Save();
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not save the config after importing the legacy portal networks: {ex.Message}");
            }
        }

        /// <summary>
        /// The config file was reloaded or a setting was changed. The values themselves are distributed
        /// by Jotunn's ServerSync, so each peer only has to rebuild its in-memory network list.
        /// </summary>
        private void LocalConfigChanged(object sender, EventArgs e)
        {
            ReloadLocalConfig();

            Log.Debug("The config was changed, rebuilding the portal network list..");
            CustomNetworks.RebuildFromConfig();

            OnLocalConfigChanged?.Invoke();
        }

    }
}
