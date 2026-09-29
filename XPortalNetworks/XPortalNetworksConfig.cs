using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using Vapok.Common.Abstractions;
using Vapok.Common.Managers.Configuration;
using XPortalNetworks.RPC;

namespace XPortalNetworks
{
    internal sealed class XPortalNetworksConfig : ConfigSyncBase
    {
        private static XPortalNetworksConfig _instance;
        public static XPortalNetworksConfig Instance
        {
            get
            {
                if (_instance == null && XPortalNetworks.PluginInstance != null)
                {
                    _instance = new XPortalNetworksConfig(XPortalNetworks.PluginInstance);
                }
                return _instance;
            }
        }

        public static XPortalNetworksConfig Initialize(IPluginInfo mod)
        {
            if (_instance == null)
            {
                _instance = new XPortalNetworksConfig(mod);
            }
            return _instance;
        }

        public event Action OnLocalConfigChanged;
        public event Action OnServerConfigChanged;

        private const string Desc_EnforcedByServer = " This setting is enforced (but not overwritten) by the server.";

        private bool _eventsSubscribed;

        public class ConfigSettings
        {
            public bool PingMapDisabled;
            public bool DisplayPortalColour;
            public bool DoublePortalCosts;
            public ConfigEntry<Vector3> DefaultPortal;
            public ConfigEntry<bool> DefaultPrivatePortal;
            public bool HidePortalDistance;
            public bool RestrictPortalRemoval;
            public ConfigEntry<bool> ShowSplashOnStartup;
        }

        public ConfigSettings Local { get; set; }
        public ConfigSettings Server { get; set; }

        public XPortalNetworksConfig(IPluginInfo mod) : base(mod)
        {
            _instance = this;
            Local = new ConfigSettings();
            Server = new ConfigSettings();

            InitializeConfigurationSettings();

            if (Environment.IsServer)
            {
                Server = Local;
            }
        }

        public override void InitializeConfigurationSettings()
        {
            ConfigFile cfg = _instanceConfig ?? Config;
            if (cfg == null)
                return;

            ReloadLocalConfig();
            SubscribeConfigEvents(cfg);
        }

        private void SubscribeConfigEvents(ConfigFile cfg)
        {
            if (_eventsSubscribed || cfg == null)
                return;

            cfg.ConfigReloaded += LocalConfigChanged;
            cfg.SettingChanged += LocalConfigChanged;
            _eventsSubscribed = true;
        }

        public void LoadLocalConfig(ConfigFile configFile)
        {
            ReloadLocalConfig();

            if (Environment.IsServer)
            {
                Server = Local;
            }
        }

        private void ReloadLocalConfig()
        {
            ConfigFile cfg = _instanceConfig ?? Config;
            if (cfg == null)
                return;

            cfg.Bind("General", "NexusID", Mod.Info.NexusId, "Nexus mod ID for updates (do not change)");

            ConfigEntry<bool> cfgPingMapDisabled = cfg.Bind("General", "PingMapDisabled", false, "Disable the Ping Map button completely. For players who wish to play without a map." + Desc_EnforcedByServer);
            Local.PingMapDisabled = cfgPingMapDisabled.Value;

            ConfigEntry<bool> cfgDisplayPortalColour = cfg.Bind("General", "DisplayPortalColour", false, "Show a \">>\" tag in the list of portals that has the same colour as the light that the portal emits (integration with \"Advanced Portals\" by RandyKnapp).");
            Local.DisplayPortalColour = cfgDisplayPortalColour.Value;

            ConfigEntry<bool> cfgDoublePortalCosts = cfg.Bind("General", "DoublePortalCosts", false, "By using XPortalNetworks, you effectively only need half the amount of portals. To compensate for that, we can double the costs of portals." + Desc_EnforcedByServer);
            Local.DoublePortalCosts = cfgDoublePortalCosts.Value;

            Local.DefaultPortal = cfg.Bind("General", "DefaultPortal", Vector3.zero, "The Portal that newly built Portals immediately connect to.");

            Local.DefaultPrivatePortal = cfg.Bind(
                "General",
                "DefaultPrivatePortal",
                true,
                "If true, newly placed portals start as private (owner-only). If false, they start public on the Global network until changed.");

            ConfigEntry<bool> cfgHidePortalDistance = cfg.Bind("General", "HidePortalDistance", false, "In the list of portals, do not show how far away other portals are." + Desc_EnforcedByServer);
            Local.HidePortalDistance = cfgHidePortalDistance.Value;

            ConfigEntry<bool> cfgRestrictPortalRemoval = cfg.Bind(
                "General",
                "RestrictPortalRemoval",
                false,
                "When true, only the player who placed the portal or a server admin may remove it with the hammer. Other removal (e.g. structural damage) is unchanged." + Desc_EnforcedByServer);
            Local.RestrictPortalRemoval = cfgRestrictPortalRemoval.Value;

            Local.ShowSplashOnStartup = InstanceShowSplashOnStartup ?? ShowSplashOnStartup;
        }

        private void LocalConfigChanged(object sender, EventArgs e)
        {
            ReloadLocalConfig();

            if (Environment.IsServer)
            {
                Log.Debug("The config was changed, propagating to clients..");
                SendToClient.Config(PackLocalConfig());
            }

            OnLocalConfigChanged?.Invoke();
        }

        public ZPackage PackLocalConfig()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Local.PingMapDisabled);
            pkg.Write(Local.DoublePortalCosts);
            pkg.Write(Local.HidePortalDistance);
            pkg.Write(Local.RestrictPortalRemoval);
            return pkg;
        }

        public void ReceiveServerConfig(ZPackage pkg)
        {
            Server.PingMapDisabled = pkg.ReadBool();
            Server.DoublePortalCosts = pkg.ReadBool();
            Server.HidePortalDistance = pkg.ReadBool();
            try
            {
                Server.RestrictPortalRemoval = pkg.ReadBool();
            }
            catch (EndOfStreamException)
            {
                Server.RestrictPortalRemoval = false;
            }

            Log.Debug($"PingMapDisabled {{ Local: {Local.PingMapDisabled}, Server: {Server.PingMapDisabled} }}");
            Log.Debug($"DoublePortalCosts {{ Local: {Local.DoublePortalCosts}, Server: {Server.DoublePortalCosts} }}");
            Log.Debug($"HidePortalDistance {{ Local: {Local.HidePortalDistance}, Server: {Server.HidePortalDistance} }}");
            Log.Debug($"RestrictPortalRemoval {{ Local: {Local.RestrictPortalRemoval}, Server: {Server.RestrictPortalRemoval} }}");

            OnServerConfigChanged?.Invoke();
        }
    }
}
