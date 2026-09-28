namespace XPortalNetworks.RPC.Server
{
    internal static class ServerEvents
    {
        private const string ERR_NOTSERVER = "but I am not the server!";

        internal static void RPC_SyncRequest(long sender, string reason)
        {
            Log.Info($"Received sync request from `{sender}` because: {reason}");
            XPortalNetworks.ProcessSyncRequest(reason);
        }

        internal static void RPC_AddOrUpdateRequest(long sender, ZPackage pkg)
        {
            if (!Environment.IsServer)
            {
                Log.Error($"`{sender}` wants a portal to be added or updated, {ERR_NOTSERVER}");
                return;
            }

            var portal = new KnownPortal(pkg);
            Log.Debug($"{sender} wants `{portal.Id}` to be added or updated");

            var requesterPlayerId = NetPeerUtility.GetPeerPlayerId(sender);
            var portalZdo = ZDOMan.instance.GetZDO(portal.Id);
            if (portalZdo == null)
            {
                Log.Error($"Portal ZDO `{portal.Id}` not found for network validation");
                return;
            }

            KnownPortalsManager.Instance.TryGetValue(portal.Id, out var existing);

            var pieceCreator = portalZdo.GetLong(ZDOVars.s_creator);
            var currentNetworkOwner = existing != null
                ? existing.NetworkOwnerPlayerId
                : ZdoTools.GetNetworkOwnerPlayerId(portalZdo);

            var requesterIsPrivileged = NetPeerUtility.IsPeerPrivilegedForPortalNetwork(sender);
            var requesterIsCreator = requesterPlayerId != 0L && requesterPlayerId == pieceCreator;
            var requesterIsNetworkOwner = requesterPlayerId != 0L && requesterPlayerId == currentNetworkOwner;

            var requesterMayChangeNetwork = requesterIsCreator || requesterIsNetworkOwner || requesterIsPrivileged;
            var requesterMayEditPrivatePortal = requesterIsCreator || requesterIsNetworkOwner || requesterIsPrivileged;

            // Admins/host only bypass allow lists when the server config allows it (AdminsSeeAllNetworks).
            var requesterBypassesNetworks = requesterIsPrivileged && CustomNetworks.AdminsBypassNetworks;

            var requesterUserId = NetPeerUtility.GetPeerUserId(sender);
            var requesterPlayerIdString = requesterPlayerId != 0L ? requesterPlayerId.ToString() : string.Empty;
            var authoritativeNetworkId = existing != null
                ? existing.NetworkOwnerPlayerId
                : ZdoTools.GetNetworkOwnerPlayerId(portalZdo);
            var currentNetworkRestricted = CustomNetworks.IsReservedIdRange(authoritativeNetworkId)
                && !CustomNetworks.IsPlayerAllowed(authoritativeNetworkId, requesterUserId, requesterPlayerIdString, requesterBypassesNetworks);

            if (!requesterMayChangeNetwork)
            {
                var authoritativeNetwork = existing != null
                    ? existing.NetworkOwnerPlayerId
                    : ZdoTools.GetNetworkOwnerPlayerId(portalZdo);
                portal.NetworkOwnerPlayerId = authoritativeNetwork;
                portal.NetworkOwnerDisplayName = existing != null
                    ? existing.NetworkOwnerDisplayName
                    : ZdoTools.GetNetworkOwnerDisplayName(portalZdo);
            }
            else
            {
                if (portal.NetworkOwnerPlayerId == 0L)
                {
                    portal.NetworkOwnerDisplayName = string.Empty;
                }
                else if (CustomNetworks.IsReservedIdRange(portal.NetworkOwnerPlayerId))
                {
                    if (CustomNetworks.IsActiveId(portal.NetworkOwnerPlayerId)
                        && CustomNetworks.TryGetDisplayName(portal.NetworkOwnerPlayerId, out var customName))
                    {
                        portal.NetworkOwnerDisplayName = customName;
                    }
                    else
                    {
                        portal.NetworkOwnerPlayerId = 0L;
                        portal.NetworkOwnerDisplayName = string.Empty;
                    }
                }
                else
                {
                    // Personal network
                    if (requesterIsPrivileged || portal.NetworkOwnerPlayerId == requesterPlayerId || portal.NetworkOwnerPlayerId == currentNetworkOwner)
                    {
                        if (portal.NetworkOwnerPlayerId == requesterPlayerId)
                        {
                            var fromClient = PortalNetwork.SanitizeNetworkOwnerDisplayName(portal.NetworkOwnerDisplayName);
                            portal.NetworkOwnerDisplayName = !string.IsNullOrEmpty(fromClient)
                                ? fromClient
                                : (PortalNetwork.TryResolveByWorldState(requesterPlayerId) ?? string.Empty);
                        }
                        else
                        {
                            portal.NetworkOwnerDisplayName = PortalNetwork.TryResolveByWorldState(portal.NetworkOwnerPlayerId)
                                ?? existing?.NetworkOwnerDisplayName
                                ?? ZdoTools.GetNetworkOwnerDisplayName(portalZdo)
                                ?? string.Empty;
                        }
                    }
                    else
                    {
                        portal.NetworkOwnerPlayerId = requesterPlayerId != 0L ? requesterPlayerId : currentNetworkOwner;
                        portal.NetworkOwnerDisplayName = PortalNetwork.TryResolveByWorldState(portal.NetworkOwnerPlayerId) ?? string.Empty;
                    }
                }
            }

            if (existing != null && existing.IsPrivate && !requesterMayEditPrivatePortal)
            {
                portal.Name = existing.Name;
                portal.Target = existing.Target;
                portal.IsPrivate = existing.IsPrivate;
                portal.NetworkOwnerPlayerId = existing.NetworkOwnerPlayerId;
                portal.NetworkOwnerDisplayName = existing.NetworkOwnerDisplayName ?? string.Empty;
            }
            else if (existing != null && !existing.IsPrivate && !requesterMayEditPrivatePortal)
            {
                portal.IsPrivate = false;
                if (portal.HasTarget()
                    && KnownPortalsManager.Instance.TryGetValue(portal.Target, out var blockedTarget)
                    && blockedTarget.IsPrivate
                    && blockedTarget.NetworkOwnerPlayerId == requesterPlayerId)
                {
                    portal.Target = existing.Target;
                }
            }

            var requestedNetworkRestricted = CustomNetworks.IsReservedIdRange(portal.NetworkOwnerPlayerId)
                && !CustomNetworks.IsPlayerAllowed(portal.NetworkOwnerPlayerId, requesterUserId, requesterPlayerIdString, requesterBypassesNetworks);

            // Team networks ("allow_list") are server-authoritative: a player who is not on the network's
            // allow list may not view, edit, or move portals on that network.
            if (!requesterBypassesNetworks && existing != null && currentNetworkRestricted)
            {
                portal.Name = existing.Name;
                portal.Target = existing.Target;
                portal.IsPrivate = existing.IsPrivate;
                portal.NetworkOwnerPlayerId = existing.NetworkOwnerPlayerId;
                portal.NetworkOwnerDisplayName = existing.NetworkOwnerDisplayName ?? string.Empty;
            }
            else if (!requesterBypassesNetworks && requestedNetworkRestricted)
            {
                portal.NetworkOwnerPlayerId = authoritativeNetworkId;
                portal.NetworkOwnerDisplayName = existing != null
                    ? (existing.NetworkOwnerDisplayName ?? string.Empty)
                    : (ZdoTools.GetNetworkOwnerDisplayName(portalZdo) ?? string.Empty);
            }

            if (portal.IsPrivate)
            {
                if (portal.NetworkOwnerPlayerId == 0L)
                {
                    portal.NetworkOwnerPlayerId = requesterPlayerId != 0L ? requesterPlayerId : pieceCreator;
                }

                if (portal.NetworkOwnerPlayerId == 0L)
                {
                    portal.IsPrivate = false;
                }
            }

            if (portal.HasTarget() && KnownPortalsManager.Instance.TryGetValue(portal.Target, out var destForValidation) && destForValidation.IsPrivate)
            {
                var targetZdo = ZDOMan.instance.GetZDO(destForValidation.Id);
                var destPieceCreator = targetZdo != null ? targetZdo.GetLong(ZDOVars.s_creator) : 0L;
                var mayTargetPrivatePortal = NetPeerUtility.IsPeerPrivilegedForPortalNetwork(sender)
                    || (destPieceCreator != 0L && destPieceCreator == requesterPlayerId)
                    || (destForValidation.NetworkOwnerPlayerId != 0L && destForValidation.NetworkOwnerPlayerId == requesterPlayerId);
                if (!mayTargetPrivatePortal)
                {
                    portal.Target = existing != null ? existing.Target : ZDOID.None;
                }
            }

            // Do not allow linking to a portal that sits on a team network the requester cannot access.
            if (portal.HasTarget()
                && !requesterBypassesNetworks
                && KnownPortalsManager.Instance.TryGetValue(portal.Target, out var destNetworkValidation)
                && CustomNetworks.IsReservedIdRange(destNetworkValidation.NetworkOwnerPlayerId)
                && !CustomNetworks.IsPlayerAllowed(destNetworkValidation.NetworkOwnerPlayerId, requesterUserId, requesterPlayerIdString, false))
            {
                portal.Target = existing != null ? existing.Target : ZDOID.None;
            }

            var updatedPortal = KnownPortalsManager.Instance.AddOrUpdate(portal);

            Log.Info($"Setting portal tag `{updatedPortal.Name}`, network `{updatedPortal.NetworkOwnerPlayerId}` (`{updatedPortal.NetworkOwnerDisplayName}`), private `{updatedPortal.IsPrivate}`, target `{updatedPortal.Target}` on behalf of {sender}");
            ZdoTools.UpdateFromKnownPortal(state: updatedPortal);

            SendToClient.SyncPortal(updatedPortal);

            if (updatedPortal.HasTarget())
            {
                // Set the target of the other portal to this portal, if that portal does not currently have a target
                var targetPortal = KnownPortalsManager.Instance.GetKnownPortalById(updatedPortal.Target);
                if (targetPortal != null && !targetPortal.HasTarget())
                {
                    Log.Info("Target portal does not have a target itself, setting target portal's target to this portal");
                    targetPortal.Target = updatedPortal.Id;
                    SendToServer.AddOrUpdateRequest(targetPortal);
                }
            }
        }

        /// <summary>
        /// A client wishes for a portal to be removed
        /// </summary>
        /// <param name="sender">The id of the sender</param>
        /// <param name="portalId">The ZDOID of the portal that should be removed</param>
        internal static void RPC_RemoveRequest(long sender, ZDOID portalId)
        {
            if (!Environment.IsServer)
            {
                Log.Error($"{sender} wants `{portalId}` to be removed, {ERR_NOTSERVER}");
                return;
            }

            if (!KnownPortalsManager.Instance.ContainsId(portalId))
            {
                Log.Debug($"{sender} wants `{portalId}` to be removed, but it doesn't exist");
                return;
            }

            Log.Debug($"{sender} wants `{portalId}` to be removed");

            if (KnownPortalsManager.Instance.Remove(portalId))
            {
                Log.Debug($"`{portalId}` removed, checking other portals' targets..");

                var portalsWithInvalidTarget = KnownPortalsManager.Instance.GetPortalsWithTarget(portalId);
                foreach (var portalWithInvalidTarget in portalsWithInvalidTarget)
                {
                    Log.Debug($"Removing target from `{portalWithInvalidTarget.Name}`");

                    portalWithInvalidTarget.Target = ZDOID.None;
                    SendToServer.AddOrUpdateRequest(portalWithInvalidTarget);
                }

                SendToClient.Resync(KnownPortalsManager.Instance.Pack(), "A portal was removed");
            }
        }

        internal static void RPC_RequestAdminSync(long sender, ZPackage _)
        {
            if (!Environment.IsServer)
            {
                return;
            }

            bool isAdmin = false;
            ZNetPeer peer = ZNet.instance != null ? ZNet.instance.GetPeer(sender) : null;
            if (peer != null)
            {
                isAdmin = peer.m_socket != null && ZNet.instance != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
            }
            else if (ZNet.instance != null && ZNet.instance.IsServer() && sender == ZNet.GetUID())
            {
                isAdmin = ZNet.instance.LocalPlayerIsAdminOrHost();
            }

            ZPackage outPkg = new ZPackage();
            outPkg.Write(isAdmin);
            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, RPCManager.RPC_ADMINSYNC, outPkg);
            }
        }
    }
}
