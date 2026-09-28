using HarmonyLib;
using XPortalNetworks.RPC;

namespace XPortalNetworks.Patches
{
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    static class Piece_SetCreator
    {
        static void Postfix(Piece __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.m_name) || !__instance.m_name.Contains("$piece_portal"))
                return;

            WearNTear wearNTear = __instance.GetComponent<WearNTear>();
            if (wearNTear == null)
                return;

            CheckWearNTearCreationTime(true, wearNTear);
        }

        private static void CheckWearNTearCreationTime(bool delayed = true, object state = null)
        {
            if (state is not WearNTear wearNTear || wearNTear == null)
                return;

            if (delayed)
            {
                QueuedAction.Queue(CheckWearNTearCreationTime, delay: 1, state: wearNTear);
                return;
            }

            if (wearNTear.m_createTime == -1f)
            {
                Log.Debug("Portal detection work-around: manually invoking WearNTear.OnPlace postfix");
                WearNTear_OnPlaced.Postfix(wearNTear);
            }
        }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]
    static class Piece_CanBeRemoved
    {
        static void Postfix(Piece __instance, ref bool __result)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.m_name) || !__instance.m_name.Contains("$piece_portal"))
            {
                return;
            }

            if (!XPortalNetworksConfig.Instance.Local.RestrictPortalRemoval)
            {
                return;
            }

            __result = CanRemovePortal(__instance);
        }

        static bool CanRemovePortal(Piece piece)
        {
            if (piece == null)
                return false;

            if (ZNet.instance != null && (ZNet.instance.LocalPlayerIsAdminOrHost() || XPortalNetworksAdminSync.IsLocalPortalNetworkAdmin()))
                return true;

            return piece.IsCreator();
        }
    }
}
