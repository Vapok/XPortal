namespace XPortalNetworks.RPC.Client
{
    internal static class ClientEvents
    {
        private const string DBG_ISSERVER = "because I am the server";

        /// <summary>
        /// The server sent us all of the portals it knows
        /// </summary>
        /// <param name="sender">The server</param>
        /// <param name="pkg">A ZPackage containing a number followed by a list of packaged portals</param>
        /// <param name="reason">The reason that was given for the Resync Request</param>
        internal static void RPC_Resync(long sender, ZPackage pkg, string reason)
        {
            if (Environment.IsServer)
            {
                Log.Debug($"Ignoring resync package {DBG_ISSERVER}");
                return;
            }

            Log.Info($"Received resync package from server, because: {reason}");
            KnownPortalsManager.Instance.UpdateFromResyncPackage(pkg);
        }

        /// <summary>
        /// The server sent us a KnownPortal that was added or updated
        /// </summary>
        /// <param name="sender">The server</param>
        /// <param name="pkg">A ZPackage containing the KnownPortal that was added or updated</param>
        internal static void RPC_SyncPortal(long sender, ZPackage pkg)
        {
            if (Environment.IsServer)
            {
                Log.Debug($"Ignoring portal update {DBG_ISSERVER}");
                return;
            }

            var incomingPortal = new KnownPortal(pkg);
            Log.Debug($"Received update to portal `{incomingPortal.GetFriendlyName()}` from server");
            KnownPortalsManager.Instance.AddOrUpdate(incomingPortal);
        }

        /// <summary>
        /// Server reply: whether this client is a server admin for portal network UI.
        /// </summary>
        internal static void RPC_AdminSync(long sender, ZPackage pkg)
        {
            if (Environment.IsServer)
            {
                return;
            }

            bool isAdmin = pkg.ReadBool();
            XPortalNetworksAdminSync.ApplyServerReply(isAdmin);
            Log.Debug($"Portal network admin UI allowed: {isAdmin}");
        }
    }
}
