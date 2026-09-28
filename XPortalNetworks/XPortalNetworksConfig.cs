using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

            // Portal networks (ids 1-15). One section per network, so the ConfigurationManager UI lists
            // them in numeric order. Within a section ConfigurationManager sorts by Order *descending*
            // and then by display name - ConfigurationManager.cs:
            //   Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList();
            // i.e. "higher number is higher on the list" - so `Name` needs the higher value to sit above
            // `Permitted` (leaving both at 0 would fall back to the alphabetical tie-break, which puts
            // "Name" first only because of its spelling).
            for (var id = CustomNetworks.MinId; id <= CustomNetworks.MaxId; id++)
            {
                Local.NetworkNames[id] = configFile.Bind(
                    $"Portal Network {id}",
                    "Name",
                    string.Empty,
                    new ConfigDescription(
                        $"Display name of portal network {id}. Leave empty to keep this network unused." + Desc_EnforcedByServer,
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 2 }));

                Local.NetworkAllowLists[id] = configFile.Bind(
                    $"Portal Network {id}",
                    "Permitted",
                    string.Empty,
                    new ConfigDescription(
                        $"Comma separated player ids allowed to use portal network {id} (e.g. Steam_12345678901234567). " +
                        "Leave empty to let everyone use it; ignored while the network has no name." + Desc_EnforcedByServer,
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 1 }));
            }

            MigrateLegacyNetworkSections();

            // ConfigurationManager sorts settings within a section by Order *descending* (higher number
            // is higher on the list), so the splash toggle takes the higher value to stay on top.
            Local.ShowSplashOnStartup = configFile.Bind(
                "Local Config",
                "Show Splash on Startup",
                true,
                new ConfigDescription("If enabled, displays the mod overview and links splash screen on game startup.",
                    null, new Vapok.Common.Shared.ConfigurationManagerAttributes { Order = 5 }));

            Local.EnableTelemetry = configFile.Bind(
                "Local Config",
                "Enable Anonymous Telemetry",
                true,
                new ConfigDescription("If enabled, sends anonymous mod launch and heartbeat telemetry to help improve mod stability and track active versions.",
                    null, new Vapok.Common.Shared.ConfigurationManagerAttributes { Order = 4 }));
        }

        /// <summary>
        /// Carries network definitions over from the pre-2.5.0 layout, where every network lived in a
        /// single <c>[Portal Networks]</c> section as <c>Network &lt;n&gt; Name</c> and
        /// <c>Network &lt;n&gt; Allow List</c>. Those keys are no longer bound (so BepInEx keeps them
        /// out of reach), so they are read straight from the config file; anything an admin configured
        /// is copied into the new per-network sections. The old keys stay behind as an inert section -
        /// they are not bound any more, so they do not appear in the ConfigurationManager UI.
        /// </summary>
        private void MigrateLegacyNetworkSections()
        {
            var path = configFile?.ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            Dictionary<long, (string Name, string Permitted)> legacy;
            try
            {
                var text = File.ReadAllText(path);
                if (text.IndexOf("Portal Networks", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                legacy = ParseLegacyNetworkSection(text);
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not read the legacy portal networks from the config file: {ex.Message}");
                return;
            }

            if (legacy.Count == 0)
            {
                return;
            }

            var migrated = 0;
            foreach (var entry in legacy)
            {
                var id = (int)entry.Key;
                migrated += AssignIfEmpty(Local.NetworkNames[id], entry.Value.Name);
                migrated += AssignIfEmpty(Local.NetworkAllowLists[id], entry.Value.Permitted);
            }

            if (migrated == 0)
            {
                return;
            }

            try
            {
                configFile.Save();
                Log.Info($"Migrated {migrated} legacy portal-network setting(s) from the old `[Portal Networks]` section into the per-network config sections.");
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not save the config after migrating the portal networks: {ex.Message}");
            }
        }

        /// <summary>Fills an entry that is still empty. Returns 1 when a value was carried over.</summary>
        private static int AssignIfEmpty(ConfigEntry<string> target, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(target.Value))
            {
                return 0;
            }

            target.Value = value;
            return 1;
        }

        /// <summary>
        /// Reads <c>Network &lt;n&gt; Name</c> / <c>Network &lt;n&gt; Allow List</c> values out of the
        /// legacy <c>[Portal Networks]</c> section of a config file.
        /// </summary>
        private static Dictionary<long, (string Name, string Permitted)> ParseLegacyNetworkSection(string text)
        {
            var result = new Dictionary<long, (string Name, string Permitted)>();
            var inLegacySection = false;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();

                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    inLegacySection = line.Equals("[Portal Networks]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inLegacySection)
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                var match = Regex.Match(key, @"^Network\s+(\d+)\s+(Name|Allow List)$", RegexOptions.IgnoreCase);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var id) || id < CustomNetworks.MinId || id > CustomNetworks.MaxId)
                {
                    continue;
                }

                result.TryGetValue(id, out var existing);
                result[id] = match.Groups[2].Value.Equals("Name", StringComparison.OrdinalIgnoreCase)
                    ? (value, existing.Permitted)
                    : (existing.Name, value);
            }

            return result;
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
