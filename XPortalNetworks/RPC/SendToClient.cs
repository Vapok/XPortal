using System;
using UnityEngine;

namespace XPortalNetworks.RPC
{
    internal static class SendToClient
    {
        /// <summary>
        /// Send one portal to all clients
        /// </summary>
        /// <param name="portal">The KnownPortal to send to the clients</param>
        public static void SyncPortal(KnownPortal portal)
        {
            if (ZNet.instance.GetConnectedPeers().Count == 0)
            {
                Log.Debug("Not sending portal update: nobody is connected");
                return;
            }

            Log.Debug($"Sending {portal} to everybody");

            var pkg = portal.Pack();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPCManager.RPC_SYNCPORTAL, pkg);
        }

        /// <summary>
        /// Send a package of all portals to all clients
        /// </summary>
        /// <param name="pkg">A ZPackage containing a count followed by a ZPackage for each KnownPortal</param>
        /// <param name="reason">The reason that was given for the Resync Request</param>
        public static void Resync(ZPackage pkg, string reason)
        {
            if (ZNet.instance.GetConnectedPeers().Count == 0)
            {
                Log.Debug("Not sending resync package: nobody is connected");
                return;
            }

            Log.Debug($"Sending all portals to everybody, because: {reason}");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPCManager.RPC_RESYNC, pkg, reason);
        }

        /// <summary>
        /// Send a ping to everyone
        /// </summary>
        /// <param name="location">The location in the world that should be pinged</param>
        /// <param name="text">The text that should appear on the ping message</param>
        public static void PingMap(Vector3 location, string text)
        {
            try
            {
                if (ZRoutedRpc.instance == null)
                {
                    Log.Warning("Cannot ping map: ZRoutedRpc instance is null");
                    return;
                }

                Log.Debug($"Calling RPC `{RPCManager.RPC_CHATMESSAGE}` to ping portal `{text}` at `{location}`");

                // Since Valheim patch 0.214.2 (2023-03-13), the ChatMessage RPC requires a UserInfo object instead of the player name string
                var localUserInfo = UserInfo.GetLocalUser() ?? new UserInfo();
                // ..but XPortal much prefers to show the name of the portal, instead of the name of the player
                localUserInfo.Name = !string.IsNullOrEmpty(text) ? text : "Portal";

                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPCManager.RPC_CHATMESSAGE, location, (int)Talker.Type.Ping, localUserInfo, string.Empty);
            }
            catch (Exception ex)
            {
                Log.Warning($"Error while pinging map: {ex.Message}");
            }
        }
    }
}
