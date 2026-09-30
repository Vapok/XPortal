using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace XPortalNetworks.Compatibility
{
    internal static class LegacyCompatibility
    {
        public const string LegacyXPortalPluginGuid = "yay.spikehimself.xportal";
        private const string LegacyXPortalHarmonyGuid = "yay.spikehimself.xportal.harmony";

        private static bool _neutralized;

        public static void NeutralizeLegacyXPortal()
        {
            if (_neutralized)
            {
                return;
            }

            if (Chainloader.PluginInfos.TryGetValue(LegacyXPortalPluginGuid, out PluginInfo legacyPlugin))
            {
                if (legacyPlugin != null && legacyPlugin.Instance != null)
                {
                    legacyPlugin.Instance.enabled = false;
                }

                Harmony.UnpatchID(LegacyXPortalHarmonyGuid);
                Harmony.UnpatchID(LegacyXPortalPluginGuid);
                _neutralized = true;

                Log.Warning("Detected legacy XPortal mod (yay.spikehimself.xportal). Neutralized legacy patches and component to prevent conflicts with XPortalNetworks.");
            }
        }
    }
}
