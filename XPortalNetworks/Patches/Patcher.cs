using HarmonyLib;
using XPortalNetworks.Compatibility;

namespace XPortalNetworks.Patches
{
    internal static class Patcher
    {
        private static readonly Harmony patcher = new Harmony(Mod.Info.HarmonyGUID);

        public static void Patch()
        {
            LegacyCompatibility.NeutralizeLegacyXPortal();

            if (!Environment.IsHeadless)
            {
                patcher.PatchAll(typeof(Dropdown_OnSubmit));
                patcher.PatchAll(typeof(Dropdown_Show));
                patcher.PatchAll(typeof(Dropdown_Hide));
                patcher.PatchAll(typeof(Dropdown_SetValue));
                patcher.PatchAll(typeof(TextInput_RequestText));
                patcher.PatchAll(typeof(TeleportWorld_GetHoverText));
            }

            patcher.PatchAll(typeof(Game_Awake));
            patcher.PatchAll(typeof(Game_Start));
            patcher.PatchAll(typeof(Game_ConnectPortals));
            patcher.PatchAll(typeof(Game_ConnectPortalsCoroutine));
            patcher.PatchAll(typeof(TeleportWorld_Teleport_PrivateDestination));
            patcher.PatchAll(typeof(TeleportWorld_UpdatePortal_Transpiler));
            patcher.PatchAll(typeof(WearNTear_Destroy));
            patcher.PatchAll(typeof(Piece_CanBeRemoved));
            patcher.PatchAll(typeof(ZDOMan_ConnectPortals));
            patcher.PatchAll(typeof(ZNet_RPC_PeerInfo_Postfix));
            patcher.PatchAll(typeof(WearNTear_OnPlaced));
        }

        public static void Unpatch() => patcher?.UnpatchSelf();
    }
}
