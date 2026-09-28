namespace XPortalNetworks
{
    internal static class NetPeerUtility
    {
        internal static long GetPeerPlayerId(long peerId)
        {
            if (ZNet.instance == null || ZDOMan.instance == null)
            {
                return 0L;
            }

            ZNetPeer peer = ZNet.instance.GetPeer(peerId);
            if (peer == null)
            {
                if (ZNet.instance.IsServer() && peerId == ZNet.GetUID())
                {
                    if (Player.m_localPlayer != null)
                    {
                        return Player.m_localPlayer.GetPlayerID();
                    }

                    return (Game.instance != null && Game.instance.GetPlayerProfile() != null)
                        ? Game.instance.GetPlayerProfile().GetPlayerID()
                        : 0L;
                }

                return 0L;
            }

            if (peer.m_characterID.IsNone())
            {
                return 0L;
            }

            ZDO characterZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            if (characterZdo == null)
            {
                return 0L;
            }

            return characterZdo.GetLong(ZDOVars.s_playerID);
        }

        internal static bool IsPeerPrivilegedForPortalNetwork(long peerId)
        {
            if (ZNet.instance == null)
            {
                return false;
            }

            ZNetPeer peer = ZNet.instance.GetPeer(peerId);
            if (peer == null)
            {
                if (ZNet.instance.IsServer() && peerId == ZNet.GetUID())
                {
                    return ZNet.instance.LocalPlayerIsAdminOrHost();
                }

                return false;
            }

            return peer.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
        }

        /// <summary>The local player's numeric character id (0 when unavailable).</summary>
        internal static long GetLocalPlayerId()
        {
            if (Player.m_localPlayer != null)
            {
                return Player.m_localPlayer.GetPlayerID();
            }

            if (Game.instance != null && Game.instance.GetPlayerProfile() != null)
            {
                return Game.instance.GetPlayerProfile().GetPlayerID();
            }

            return 0L;
        }

        /// <summary>
        /// The local player's platform user id (e.g. <c>Steam_12345678901234567</c>), or an empty string.
        /// Used to match entries in a network's allow list.
        /// </summary>
        internal static string GetLocalUserId()
        {
            try
            {
                UserInfo localUser = UserInfo.GetLocalUser();
                if (localUser != null && localUser.UserId.IsValid)
                {
                    return localUser.UserId.ToString();
                }
            }
            catch
            {
                // Ignore; fall through to an empty identifier.
            }

            return string.Empty;
        }

        /// <summary>The local player's numeric id as a string, or an empty string.</summary>
        internal static string GetLocalPlayerIdString()
        {
            var id = GetLocalPlayerId();
            return id != 0L ? id.ToString() : string.Empty;
        }

        /// <summary>
        /// Resolves the platform user id (e.g. <c>Steam_12345678901234567</c>) for a connected peer,
        /// falling back to the socket host name. Returns an empty string when it cannot be determined.
        /// </summary>
        internal static string GetPeerUserId(long peerId)
        {
            if (ZNet.instance == null)
            {
                return string.Empty;
            }

            try
            {
                ZNetPeer peer = ZNet.instance.GetPeer(peerId);
                if (peer == null)
                {
                    if (ZNet.instance.IsServer() && peerId == ZNet.GetUID())
                    {
                        return GetLocalUserId();
                    }

                    return string.Empty;
                }

                if (!peer.m_characterID.IsNone())
                {
                    foreach (var info in ZNet.instance.GetPlayerList())
                    {
                        if (info.m_characterID != peer.m_characterID)
                        {
                            continue;
                        }

                        if (info.m_userInfo.m_id.IsValid)
                        {
                            return info.m_userInfo.m_id.ToString();
                        }
                    }
                }

                if (peer.m_socket != null)
                {
                    var host = peer.m_socket.GetHostName();
                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        return host;
                    }
                }
            }
            catch
            {
                // Ignore; fall through to an empty identifier.
            }

            return string.Empty;
        }

        /// <summary>The peer's numeric character id as a string, or an empty string.</summary>
        internal static string GetPeerPlayerIdString(long peerId)
        {
            var id = GetPeerPlayerId(peerId);
            return id != 0L ? id.ToString() : string.Empty;
        }
    }
}

