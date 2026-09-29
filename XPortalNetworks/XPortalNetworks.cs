using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Vapok.Common.Abstractions;
using Vapok.Common.Managers.Splash;
using XPortalNetworks.Extension;
using XPortalNetworks.RPC;
using XPortalNetworks.UI;

namespace XPortalNetworks
{
    [BepInPlugin(Mod.Info.GUID, Mod.Info.Name, Mod.Info.Version)]
    [BepInIncompatibility("com.sweetgiorni.anyportal")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public class XPortalNetworks : BaseUnityPlugin, IPluginInfo
    {
        //Interface Properties
        public string PluginId => Mod.Info.GUID;
        public string DisplayName => Mod.Info.Name;
        public string Version => Mod.Info.Version;
        public BaseUnityPlugin Instance => this;

        public const string Key_TargetId = Mod.Info.Name + "_TargetId";
        public const string Key_PreviousId = Mod.Info.Name + "_PreviousId";
        public const string Key_NetworkOwnerPlayerId = Mod.Info.Name + "_NetworkOwnerPlayerId";
        public const string Key_NetworkOwnerDisplayName = Mod.Info.Name + "_NetworkOwnerDisplayName";
        public const string Key_IsPrivate = Mod.Info.Name + "_IsPrivate";

        public const string StonePortalPrefabName = "portal";

        private static bool portalRecipeAltered = false;
        private static Dictionary<string, int> portalRecipeOriginal;

        #region Unity Events
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "MonoBehaviour.Awake is called when the script instance is being loaded.")]
        private void Awake()
        {
            Log.Debug("I HAVE ARRIVED!");

            XPortalNetworksConfig.Instance.LoadLocalConfig(Config);

            ModSplashManager.Register(new ModSplashDossier(this)
            {
                Tagline = "Select portal destinations from a list with custom networks and private portals support.",
                ShowOnStartup = XPortalNetworksConfig.Instance.Local.ShowSplashOnStartup,
            });

            XPortalNetworksConfig.Instance.OnLocalConfigChanged += OnLocalConfigChanged;
            XPortalNetworksConfig.Instance.OnServerConfigChanged += OnServerConfigChanged;

            CustomNetworks.ListChanged += OnNetworksListChanged;

            if (!Environment.IsHeadless)
            {
                PortalConfigurationPanel.Instance.AddInputs();
            }

            MinimapManager.OnVanillaMapDataLoaded += MinimapManager_OnVanillaMapDataLoaded;

            Patches.Patcher.Patch();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "MonoBehaviour.Update is called every frame, if the MonoBehaviour is enabled.")]
        private void Update()
        {
            QueuedAction.Update();

            if (Environment.IsHeadless || !Environment.GameStarted || ZInput.instance == null || !PortalConfigurationPanel.Instance.IsActive())
            {
                return;
            }

            PortalConfigurationPanel.Instance.HandleInput();
            PortalConfigurationPanel.Instance.SyncListScroll();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "MonoBehaviour.OnDestroy occurs when a Scene or game ends.")]
        private void OnDestroy()
        {
            Log.Debug("Full portal list:");
            KnownPortalsManager.Instance.ReportAllPortals();

            CustomNetworks.ShutdownServer();
            if (!Environment.IsHeadless)
            {
                PortalConfigurationPanel.Instance?.Dispose();
            }
            KnownPortalsManager.Instance?.Dispose();
        }
        #endregion

        #region Jotunn Events
        private static void MinimapManager_OnVanillaMapDataLoaded()
        {
            SendToServer.ConfigRequest();
            SendToServer.RequestCustomNetworks();

            long myId = ZDOMan.GetSessionID();
            string myName = (Game.instance != null && Game.instance.GetPlayerProfile() != null)
                ? Game.instance.GetPlayerProfile().GetName()
                : "Player";
            SendToServer.SyncRequest($"{myName} ({myId}) has joined the game");
        }
        #endregion

        #region Portal Recipe
        private static void UpdatePortalRecipe()
        {
            if (ObjectDB.instance == null)
            {
                Log.Error("ObjectDB not instantiated");
                return;
            }

            GameObject hammerPrefab = ObjectDB.instance.GetItemPrefab("Hammer");
            ItemDrop hammer = hammerPrefab != null ? hammerPrefab.GetComponent<ItemDrop>() : null;
            if (hammer == null)
            {
                Log.Error("Could not find Hammer prefab");
                return;
            }

            if (hammer.m_itemData?.m_shared?.m_buildPieces?.m_pieces == null)
            {
                return;
            }

            Piece portalPiece = hammer.m_itemData.m_shared.m_buildPieces.m_pieces
                .Where(go => go != null && go.name.Equals("portal_wood"))
                .Select(go => go.GetComponent<Piece>())
                .FirstOrDefault();

            if (portalPiece == null || portalPiece.m_resources == null)
            {
                return;
            }

            BackUpPortalRecipe(portalPiece.m_resources);

            foreach (Piece.Requirement req in portalPiece.m_resources)
            {
                if (req == null || req.m_resItem == null) continue;

                string itemName = req.m_resItem.name;
                if (!portalRecipeOriginal.TryGetValue(itemName, out int originalAmount))
                {
                    continue;
                }

                int newAmount = originalAmount;
                if (XPortalNetworksConfig.Instance.Server.DoublePortalCosts)
                {
                    newAmount = 2 * originalAmount;
                    Log.Debug($"Doubling amount for requirement {req.m_resItem.name} for item {portalPiece.name} from {originalAmount} to {newAmount}");
                }
                else if (portalRecipeAltered)
                {
                    Log.Debug($"Resetting amount for requirement {req.m_resItem.name} for item {portalPiece.name} to {newAmount}");
                }
                req.m_amount = newAmount;
            }

            portalRecipeAltered = XPortalNetworksConfig.Instance.Server.DoublePortalCosts;
        }

        private static void BackUpPortalRecipe(Piece.Requirement[] requirements)
        {
            if (portalRecipeOriginal != null && portalRecipeOriginal.Count > 0)
            {
                return;
            }

            Log.Debug("Copying original requirements for portal recipe");
            portalRecipeOriginal = new Dictionary<string, int>();
            foreach (Piece.Requirement req in requirements)
            {
                if (req != null && req.m_resItem != null)
                {
                    portalRecipeOriginal.Add(req.m_resItem.name, req.m_amount);
                }
            }
        }
        #endregion

        #region Config events
        private static void OnNetworksListChanged()
        {
            if (!Environment.IsHeadless)
            {
                PortalConfigurationPanel.Instance.OnNetworksListChanged();
            }
        }

        internal static void OnLocalConfigChanged()
        {
            // Honestly, nobody cares
        }

        internal static void OnServerConfigChanged()
        {
            UpdatePortalRecipe();
        }
        #endregion

        #region Patch Events
        /// <summary>Game start: reset state and register RPCs.</summary>
        internal static void GameStarted()
        {
            KnownPortalsManager.Instance.Reset();
            XPortalNetworksAdminSync.ResetForNewSession();
            CustomNetworks.ResetSession();
            RPCManager.Register();
            if (Environment.IsServer)
            {
                CustomNetworks.InitializeServer();
            }
        }

        internal static void OnPrePortalHover(out string result, ZDOID portalId, Vector3 location)
        {
            if (!KnownPortalsManager.Instance.ContainsId(portalId))
            {
                Log.Debug($"Hovering over new portal `{portalId}`");
                KnownPortal dummyPortal = new KnownPortal(portalId, location);
                KnownPortalsManager.Instance.AddOrUpdate(dummyPortal);
            }

            KnownPortal portal = KnownPortalsManager.Instance.GetKnownPortalById(portalId);
            if (portal == null)
            {
                result = string.Empty;
                return;
            }

            string outputPortalName = portal.GetFriendlyName();
            string outputPortalDestination = portal.GetFriendlyTargetName();
            string colourTag = string.Empty;

            if (portal.HasTarget())
            {
                ZDOID targetId = portal.Target;
                KnownPortal targetPortal = KnownPortalsManager.Instance.GetKnownPortalById(targetId);
                if (targetPortal == null)
                {
                    Log.Error($"Target portal {targetId} appears to be invalid");
                    SendToServer.SyncRequest($"Hovering over portal `{outputPortalName}` which has invalid target `{targetId}`");
                    result = "Fetching portal info...";
                    return;
                }

                if (XPortalNetworksConfig.Instance.Local.DisplayPortalColour)
                {
                    colourTag = $"<color={targetPortal.Colour}>>> </color>";
                }
            }

            result = Localization.instance.Localize(
                         $"$piece_portal_tag: {outputPortalName}\n"
                       + $"$piece_portal_target: {colourTag}{outputPortalDestination}\n"
                       + $"[<color=yellow><b>$KEY_Use</b></color>] $piece_portal_settag"
                     );
        }

        internal static void OnPortalRequestText(TeleportWorld teleportWorld)
        {
            ZDOID portalId = teleportWorld.m_nview.GetZDO().m_uid;
            KnownPortal portal = KnownPortalsManager.Instance.GetKnownPortalById(portalId);
            if (portal == null)
            {
                Log.Error("Interacting with an unknown portal");
                return;
            }

            Log.Debug($"Interacting with: {portal}");
            Piece piece = teleportWorld.GetComponent<Piece>();
            bool mayEditNetworkAsAdmin = XPortalNetworksAdminSync.IsLocalPortalNetworkAdmin();
            bool isCreator = piece != null && piece.IsCreator();
            bool canEditNetwork = isCreator || mayEditNetworkAsAdmin;
            bool canEditPortalFully = isCreator || mayEditNetworkAsAdmin;
            PortalConfigurationPanel.Instance.ConfigurePortal(portal, canEditNetwork, canEditPortalFully);
        }

        internal static void OnPortalPlaced(ZDOID portalId, Vector3 location)
        {
            Log.Debug($"Portal `{portalId}` was placed");

            if (ZDOMan.instance != null)
            {
                ZDOMan.instance.ForceSendZDO(portalId);
            }

            KnownPortal portal = new KnownPortal(portalId, location);
            KnownPortalsManager.Instance.AddOrUpdate(portal);
            ZdoTools.UpdateFromKnownPortal(state: portal);
            SendToServer.AddOrUpdateRequest(portal);
        }

        internal static void OnPortalDestroyed(ZDOID portalId)
        {
            if (KnownPortalsManager.Instance.ContainsId(portalId))
            {
                KnownPortal portal = KnownPortalsManager.Instance.GetKnownPortalById(portalId);
                string portalName = portal?.Name ?? portalId.ToString();
                Log.Debug($"Portal `{portalName}` is being destroyed");
                KnownPortalsManager.Instance.Remove(portalId);
            }
            else
            {
                Log.Debug($"Portal `{portalId}` destroyed — was not in local known list; notifying server anyway");
            }

            SendToServer.RemoveRequest(portalId);
        }

        internal static bool PrivateUseBlocked(ZDOID sourcePortalId, long playerId)
        {
            if (!KnownPortalsManager.Instance.TryGetValue(sourcePortalId, out KnownPortal source)
                || !source.HasTarget()
                || !KnownPortalsManager.Instance.TryGetValue(source.Target, out KnownPortal dest)
                || !dest.IsPrivate)
            {
                return false;
            }

            ZDO destZdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(dest.Id) : null;
            long creator = destZdo != null ? destZdo.GetLong(ZDOVars.s_creator) : 0L;
            bool isOwnerByCreator = creator != 0L && creator == playerId;
            bool isOwnerByNetwork = dest.NetworkOwnerPlayerId != 0L && dest.NetworkOwnerPlayerId == playerId;
            return !isOwnerByCreator && !isOwnerByNetwork;
        }

        internal static bool LocalPrivateUseBlocked(ZDOID sourcePortalId)
        {
            return Player.m_localPlayer != null && PrivateUseBlocked(sourcePortalId, Player.m_localPlayer.GetPlayerID());
        }

        internal static bool IsUsablePortal(TeleportWorld portal, Player player, bool originalFlag)
        {
            if (!originalFlag || portal == null || player == null || portal.m_nview == null || !portal.m_nview.IsValid())
                return false;

            ZDO zdo = portal.m_nview.GetZDO();
            if (zdo == null)
            {
                return true;
            }
            
            bool useBlocked = PrivateUseBlocked(zdo.m_uid, player.GetPlayerID());

            return !useBlocked;
        }
        #endregion

        #region Process ZDOs
        internal static List<ZDO> ProcessSyncRequest(string reason)
        {
            List<ZDO> allPortalZDOs = GetAllPortalZDOs();
            Log.Debug($"Fetched {allPortalZDOs.Count} portals");

            if (allPortalZDOs != null)
            {
                ForceLocalPortalUpdate(allPortalZDOs);
            }

            if (Environment.IsServer)
            {
                CustomNetworks.MigrateInvalidNetworks();
            }

            SendToClient.Resync(KnownPortalsManager.Instance.Pack(), reason);

            return allPortalZDOs;
        }

        private static List<ZDO> GetAllPortalZDOs()
        {
            return ZDOMan.instance != null ? ZDOMan.instance.GetPortalList() : new List<ZDO>();
        }

        private static void ForceLocalPortalUpdate(List<ZDO> allPortals)
        {
            KnownPortalsManager.Instance.UpdateFromZDOList(allPortals);

            if (!Environment.IsServer)
            {
                string syncRequestReason = "Local portal list was updated";
                Log.Debug($"Send Sync Request, because: {syncRequestReason}");
                SendToServer.SyncRequest(syncRequestReason);
            }
        }
        #endregion

        #region UI Events
        internal static void PortalInfoSubmitted(KnownPortal portal, string newName, ZDOID newTarget, bool defaultPortal, long networkOwnerPlayerId, bool isPrivate)
        {
            if (defaultPortal)
            {
                isPrivate = false;
                XPortalNetworksConfig.Instance.Local.DefaultPortal.Value = portal.Location.Round();
            }
            else
            {
                if (portal.IsDefaultPortal)
                {
                    XPortalNetworksConfig.Instance.Local.DefaultPortal.Value = Vector3.zero;
                }
            }

            long localPlayerId = 0L;
            if (Player.m_localPlayer != null)
            {
                localPlayerId = Player.m_localPlayer.GetPlayerID();
            }
            else if (Game.instance != null && Game.instance.GetPlayerProfile() != null)
            {
                localPlayerId = Game.instance.GetPlayerProfile().GetPlayerID();
            }

            long effectiveNetworkId = networkOwnerPlayerId;
            if (isPrivate)
            {
                effectiveNetworkId = (networkOwnerPlayerId != 0L && !CustomNetworks.IsReservedIdRange(networkOwnerPlayerId))
                    ? networkOwnerPlayerId
                    : localPlayerId;
            }

            string networkOwnerDisplayName = portal.NetworkOwnerDisplayName ?? string.Empty;
            if (effectiveNetworkId == 0L)
            {
                networkOwnerDisplayName = string.Empty;
            }
            else if (CustomNetworks.IsReservedIdRange(effectiveNetworkId))
            {
                networkOwnerDisplayName = CustomNetworks.TryGetDisplayName(effectiveNetworkId, out string customLabel)
                    ? customLabel
                    : string.Empty;
            }
            else if (effectiveNetworkId == localPlayerId)
            {
                string fromPlayer = string.Empty;
                if (Player.m_localPlayer != null)
                {
                    fromPlayer = Player.m_localPlayer.GetPlayerName();
                }
                else if (Game.instance != null && Game.instance.GetPlayerProfile() != null)
                {
                    fromPlayer = Game.instance.GetPlayerProfile().GetName();
                }
                networkOwnerDisplayName = PortalNetwork.SanitizeNetworkOwnerDisplayName(fromPlayer);
            }

            bool networkChanged = portal.NetworkOwnerPlayerId != effectiveNetworkId;
            bool nameOrTargetChanged = !portal.Name.Equals(newName) || !portal.Targets(newTarget);
            bool networkDisplayNameChanged = !string.Equals(portal.NetworkOwnerDisplayName ?? string.Empty, networkOwnerDisplayName, System.StringComparison.Ordinal);
            bool privateChanged = portal.IsPrivate != isPrivate;

            if (nameOrTargetChanged || networkChanged || networkDisplayNameChanged || privateChanged)
            {
                portal.Name = newName;
                portal.Target = newTarget;
                portal.NetworkOwnerPlayerId = effectiveNetworkId;
                portal.NetworkOwnerDisplayName = networkOwnerDisplayName;
                portal.IsPrivate = isPrivate;

                Log.Debug($"Updating portal `{portal.Name}`");
                SendToServer.AddOrUpdateRequest(portal);
            }
        }

        internal static void PingMapButtonClicked(ZDOID targetId)
        {
            try
            {
                if (XPortalNetworksConfig.Instance.Server.PingMapDisabled)
                {
                    return;
                }

                KnownPortal portal = KnownPortalsManager.Instance.GetKnownPortalById(targetId);
                if (portal == null)
                {
                    Log.Warning($"Cannot ping portal: Portal with ID {targetId} not found");
                    return;
                }

                Log.Debug($"Pinging portal: {portal}");

                string name = portal.GetFriendlyName();
                Vector3 location = portal.Location;

                SendToClient.PingMap(location, name);

                if (Minimap.instance != null)
                {
                    Minimap.instance.ShowPointOnMap(location);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Error during PingMapButtonClicked: {ex.Message}");
            }
        }
        #endregion
    }
}
