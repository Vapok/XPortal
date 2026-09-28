using BepInEx.Configuration;
using System;
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

        /// <summary>
        /// The config file was reloaded or a setting was changed.
        /// Server-owned settings are pushed to the clients by Jotunn's ServerSync, so only the
        /// portal network lists (which depend on those settings) have to be re-sent by us.
        /// </summary>
        private void LocalConfigChanged(object sender, EventArgs e)
        {
            ReloadLocalConfig();

            if (Environment.IsServer)
            {
                // Changing settings such as AdminsSeeAllNetworks alters which networks each client
                // may see, so re-push the per-client network lists and refresh the local UI.
                Log.Debug("The config was changed, re-propagating the portal network lists..");
                CustomNetworks.BroadcastToAllPeers();
                if (!Environment.IsHeadless)
                {
                    CustomNetworks.NotifyListChangedLocal();
                }
            }

            OnLocalConfigChanged?.Invoke();
        }

    }
}
