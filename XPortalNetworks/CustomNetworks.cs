using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using XPortalNetworks.RPC;

namespace XPortalNetworks
{
    /// <summary>
    /// Configured portal networks (ids 1–15). Id 0 is normal Global.
    ///
    /// Networks are defined by the server in the <c>Portal Networks</c> section of
    /// <c>vapok.mods.xportalnetworks.cfg</c>. Those entries are handed to Jotunn's ServerSync, so the
    /// server pushes them to every client and only an admin (or the host) can change them - networks
    /// can therefore be managed from inside the game instead of editing files on the server.
    ///
    /// Before 2.4.0 the definitions lived in <c>BepInEx/config/XPortalNetworks/xportal_networks.json</c>;
    /// that file is read once to seed the config (see <see cref="ImportLegacyJsonIfNeeded"/>) and is
    /// otherwise unused.
    /// </summary>
    internal static class CustomNetworks
    {
        internal const int MinId = 1;
        internal const int MaxId = 15;

        /// <summary>Legacy definition file, imported once and no longer watched.</summary>
        internal const string LegacyConfigFileName = "xportal_networks.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// A configured portal network (id 1–15) together with its optional team allow list.
        /// When <see cref="AllowList"/> is empty the network is open to everyone.
        /// </summary>
        internal sealed class PortalNetworkDefinition
        {
            internal long Id { get; }

            internal string Name { get; }

            /// <summary>Player identifiers (e.g. <c>Steam_12345678901234567</c>) permitted on this network.</summary>
            internal IReadOnlyList<string> AllowList { get; }

            internal PortalNetworkDefinition(long id, string name, IReadOnlyList<string> allowList)
            {
                Id = id;
                Name = name ?? string.Empty;
                AllowList = allowList ?? Array.Empty<string>();
            }

            /// <summary>True when the network is visible/usable by everyone.</summary>
            internal bool IsOpen => AllowList == null || AllowList.Count == 0;

            /// <summary>True when any supplied player identifier matches an entry in the allow list.</summary>
            internal bool Allows(params string[] playerIdentifiers)
            {
                if (IsOpen)
                {
                    return true;
                }

                foreach (var allowed in AllowList)
                {
                    if (string.IsNullOrWhiteSpace(allowed))
                    {
                        continue;
                    }

                    foreach (var candidate in playerIdentifiers)
                    {
                        if (string.IsNullOrWhiteSpace(candidate))
                        {
                            continue;
                        }

                        if (string.Equals(allowed.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private static readonly Dictionary<long, PortalNetworkDefinition> ActiveById = new Dictionary<long, PortalNetworkDefinition>();

        // The regexes are used only by the one-time import of the pre-2.4.0 JSON file above.
        private static readonly Regex NetworkObjectRegex = new Regex(
            @"\{[^{}]*\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NetworkIdRegex = new Regex(
            @"""id""\s*:\s*(\d+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NetworkNameRegex = new Regex(
            @"""name""\s*:\s*""((?:[^""\\]|\\.)*)""",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NetworkAllowListRegex = new Regex(
            @"""allow_list""\s*:\s*\[(.*?)\]",
            RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex JsonStringRegex = new Regex(
            @"""((?:[^""\\]|\\.)*)""",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Pre-2.4.0 hot-reload state (FileSystemWatcher + main-thread polling) lived here. It is gone:
        // network definitions now come from the synced config, and BepInEx raises SettingChanged for us.

        /// <summary>Fired when the active network list changes.</summary>
        internal static event Action ListChanged;

        #region Registry (client + server)

        /// <summary>
        /// Rebuilds the active network list from the synced server-owned config. Runs on every peer
        /// against the same (ServerSync-delivered) values: visibility is evaluated locally in
        /// <see cref="IsLocalPlayerAllowed"/> for the UI, and authoritatively on the server in
        /// <see cref="IsPlayerAllowed"/> for portal edits and links.
        /// </summary>
        internal static void ResetSession()
        {
            RebuildFromConfig(notify: false);
        }

        /// <summary>Rebuilds the active network list from the config and notifies listeners.</summary>
        internal static void RebuildFromConfig(bool notify = true)
        {
            var definitions = new Dictionary<long, PortalNetworkDefinition>();
            var config = XPortalNetworksConfig.Instance;

            for (var id = MinId; id <= MaxId; id++)
            {
                var rawName = config.GetNetworkName(id);
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    continue;
                }

                var name = PortalNetwork.SanitizeNetworkOwnerDisplayName(rawName);
                if (string.IsNullOrEmpty(name))
                {
                    Log.Warning($"Portal network {id}: name `{rawName}` is invalid after sanitization; ignoring this network.");
                    continue;
                }

                definitions[id] = new PortalNetworkDefinition(id, name, ParseAllowListSetting(config.GetNetworkAllowList(id), id));
            }

            lock (ActiveById)
            {
                ActiveById.Clear();
                foreach (var kv in definitions)
                {
                    ActiveById[kv.Key] = kv.Value;
                }
            }

            Log.Debug($"Portal networks rebuilt from config: {definitions.Count} active.");

            if (notify)
            {
                ListChanged?.Invoke();
            }
        }

        /// <summary>Parses the comma/semicolon separated <c>allow list</c> setting for one network.</summary>
        private static IReadOnlyList<string> ParseAllowListSetting(string raw, long id)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return list;
            }

            foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var value = PortalNetwork.SanitizeNetworkOwnerDisplayName(part.Trim());
                if (string.IsNullOrEmpty(value) || list.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(value);
            }

            if (list.Count == 0)
            {
                Log.Warning($"Portal network {id}: the allow list could not be parsed; the network is open to everyone.");
            }

            return list;
        }

        internal static bool IsActiveId(long id)
        {
            if (id < MinId || id > MaxId)
            {
                return false;
            }

            lock (ActiveById)
            {
                return ActiveById.ContainsKey(id);
            }
        }

        internal static bool TryGetDisplayName(long id, out string displayName)
        {
            if (TryGetDefinition(id, out var definition))
            {
                displayName = definition.Name;
                return true;
            }

            displayName = null;
            return false;
        }

        internal static bool TryGetDefinition(long id, out PortalNetworkDefinition definition)
        {
            lock (ActiveById)
            {
                return ActiveById.TryGetValue(id, out definition);
            }
        }

        internal static List<long> GetSortedActiveIds()
        {
            lock (ActiveById)
            {
                return ActiveById.Keys.OrderBy(k => k).ToList();
            }
        }

        /// <summary>Active network ids the local player is permitted to see and use.</summary>
        internal static List<long> GetVisibleSortedActiveIds()
        {
            if (IsLocalPlayerNetworkPrivileged())
            {
                return GetSortedActiveIds();
            }

            var userId = NetPeerUtility.GetLocalUserId();
            var numericId = NetPeerUtility.GetLocalPlayerIdString();

            lock (ActiveById)
            {
                return ActiveById
                    .Where(kv => kv.Value == null || kv.Value.Allows(userId, numericId))
                    .Select(kv => kv.Key)
                    .OrderBy(k => k)
                    .ToList();
            }
        }

        /// <summary>
        /// True when the supplied player may use the given network. Non-configured ids are always
        /// allowed, and privileged players (server admins/host) bypass allow lists. A reserved id that
        /// is not configured counts as not allowed, so a portal is never silently placed on a network
        /// whose membership cannot be evaluated.
        /// </summary>
        internal static bool IsPlayerAllowed(long id, string userId, string numericPlayerId, bool privileged = false)
        {
            if (!IsReservedIdRange(id))
            {
                return true;
            }

            if (privileged)
            {
                return true;
            }

            PortalNetworkDefinition definition;
            lock (ActiveById)
            {
                ActiveById.TryGetValue(id, out definition);
            }

            return definition != null && definition.Allows(userId, numericPlayerId);
        }

        /// <summary>
        /// True when the local player may see/use the given network id. Every peer holds the same
        /// synchronized definitions, so this is evaluated locally against the player's identity; the
        /// server independently applies the same rule to portal edits and links.
        /// </summary>
        internal static bool IsLocalPlayerAllowed(long id)
        {
            if (!IsReservedIdRange(id))
            {
                return true;
            }

            PortalNetworkDefinition definition;
            lock (ActiveById)
            {
                ActiveById.TryGetValue(id, out definition);
            }

            if (definition == null)
            {
                return false;
            }

            if (definition.IsOpen || IsLocalPlayerNetworkPrivileged())
            {
                return true;
            }

            return definition.Allows(NetPeerUtility.GetLocalUserId(), NetPeerUtility.GetLocalPlayerIdString());
        }

        /// <summary>
        /// True when the local player may bypass allow-list restrictions: they are a server
        /// admin/host AND the server config allows admins to see all networks.
        /// </summary>
        private static bool IsLocalPlayerNetworkPrivileged()
        {
            return AdminsBypassNetworks && XPortalNetworksAdminSync.IsLocalPortalNetworkAdmin();
        }

        /// <summary>
        /// Server-owned setting (<c>AdminsSeeAllNetworks</c>, synchronized to every client): when
        /// true, server admins/host bypass portal-network allow lists. Defaults to false, so admins
        /// are treated like normal players.
        /// </summary>
        internal static bool AdminsBypassNetworks => XPortalNetworksConfig.Instance.Local.AdminsSeeAllNetworks;

        /// <summary>True if id is in the 1–15 configured range.</summary>
        internal static bool IsReservedIdRange(long id)
        {
            return id >= MinId && id <= MaxId;
        }

        internal static void MigrateInvalidNetworks()
        {
            if (!Environment.IsServer)
            {
                return;
            }

            foreach (var p in KnownPortalsManager.Instance.GetList().ToList())
            {
                if (!IsReservedIdRange(p.NetworkOwnerPlayerId))
                {
                    continue;
                }

                if (IsActiveId(p.NetworkOwnerPlayerId))
                {
                    continue;
                }

                p.NetworkOwnerPlayerId = 0L;
                p.NetworkOwnerDisplayName = string.Empty;
                KnownPortalsManager.Instance.AddOrUpdate(p);
                ZdoTools.UpdateFromKnownPortal(state: p);
                SendToClient.SyncPortal(p);
                Log.Info($"Migrated portal `{p.Id}` to Global network (network id was removed or invalid).");
            }
        }

        internal static void NotifyListChangedLocal()
        {
            ListChanged?.Invoke();
        }

        #endregion

        #region Server setup

        /// <summary>
        /// Server-side setup: seed the config from a pre-2.4.0 JSON file (only when the config does not
        /// define any network yet) and publish the definitions. Clients receive the same values through
        /// Jotunn's ServerSync, so nothing has to be pushed from here.
        /// </summary>
        internal static void InitializeServer()
        {
            if (!Environment.IsServer)
            {
                return;
            }

            ImportLegacyJsonIfNeeded();
            RebuildFromConfig();
        }

        /// <summary>
        /// One-time import of <c>xportal_networks.json</c>. Guarded by "the config defines no network",
        /// so an admin's in-game edits are never overwritten by the legacy file.
        /// </summary>
        private static void ImportLegacyJsonIfNeeded()
        {
            try
            {
                var config = XPortalNetworksConfig.Instance;
                if (config.HasAnyNetworkDefined())
                {
                    return;
                }

                if (!TryReadConfigFile(out var text, out _) || string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var imported = ParseConfigJson(text);
                if (imported.Count == 0)
                {
                    return;
                }

                config.ApplyImportedNetworks(imported.Values);
                Log.Info($"Imported {imported.Count} portal network(s) from the legacy `{LegacyConfigFileName}`. They are now " +
                         "managed in the config (`Portal Networks` section) and the JSON file is no longer used - you can delete it.");
            }
            catch (Exception ex)
            {
                Log.Error($"Legacy portal network import failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ShutdownServer(), ServerTick(), MarkReloadPending(), DetectFileChange() and UpdateFileBaseline()
        // were removed in 2.4.0 - there is no file to watch any more.

        private static string GetConfigFilePath()
        {
            return Path.Combine(Paths.ConfigPath, Mod.Info.Name, LegacyConfigFileName);
        }

        /// <summary>
        /// Reads the legacy network file, if present. Returns false when it does not exist (not an error).
        /// Sets <paramref name="readError"/> true when the file exists but could not be read.
        /// </summary>
        private static bool TryReadConfigFile(out string text, out bool readError)
        {
            text = null;
            readError = false;
            var path = GetConfigFilePath();
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                text = File.ReadAllText(path, Utf8NoBom);
                return true;
            }
            catch (Exception ex)
            {
                readError = File.Exists(path);
                Log.Error($"Could not read custom networks file `{path}`: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static Dictionary<long, PortalNetworkDefinition> ParseConfigJson(string raw)
        {
            var result = new Dictionary<long, PortalNetworkDefinition>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            raw = StripUtf8Bom(raw.Trim());

            var invalidId = 0;
            var emptyOrSanitizedName = 0;
            var duplicateId = 0;
            var decodeErrors = 0;
            var restrictedNetworks = 0;

            try
            {
                foreach (Match entry in NetworkObjectRegex.Matches(raw))
                {
                    var body = entry.Value;

                    var idMatch = NetworkIdRegex.Match(body);
                    if (!idMatch.Success || !int.TryParse(idMatch.Groups[1].Value, out var id) || id < MinId || id > MaxId)
                    {
                        invalidId++;
                        continue;
                    }

                    var nameMatch = NetworkNameRegex.Match(body);
                    if (!nameMatch.Success)
                    {
                        emptyOrSanitizedName++;
                        continue;
                    }

                    string name;
                    try
                    {
                        name = UnescapeJsonString(nameMatch.Groups[1].Value);
                    }
                    catch (Exception ex)
                    {
                        decodeErrors++;
                        Log.Warning($"Custom networks JSON: skipped entry for id {id} (invalid escape sequence in name): {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        emptyOrSanitizedName++;
                        continue;
                    }

                    name = PortalNetwork.SanitizeNetworkOwnerDisplayName(name);
                    if (string.IsNullOrEmpty(name))
                    {
                        emptyOrSanitizedName++;
                        continue;
                    }

                    var idLong = (long)id;
                    if (result.ContainsKey(idLong))
                    {
                        duplicateId++;
                        continue;
                    }

                    var allowList = ParseAllowList(body, id);
                    if (allowList.Count > 0)
                    {
                        restrictedNetworks++;
                    }

                    result.Add(idLong, new PortalNetworkDefinition(idLong, name, allowList));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Custom networks JSON: unexpected failure while scanning `{LegacyConfigFileName}`: {ex.GetType().Name}: {ex.Message}");
                return result;
            }

            if (invalidId > 0)
            {
                Log.Warning($"Custom networks JSON: skipped {invalidId} {(invalidId == 1 ? "entry" : "entries")} with id outside {MinId}–{MaxId}.");
            }

            if (emptyOrSanitizedName > 0)
            {
                Log.Warning($"Custom networks JSON: skipped {emptyOrSanitizedName} {(emptyOrSanitizedName == 1 ? "entry" : "entries")} with empty or invalid name after sanitization.");
            }

            if (duplicateId > 0)
            {
                Log.Warning($"Custom networks JSON: skipped {duplicateId} duplicate id {(duplicateId == 1 ? "entry" : "entries")}.");
            }

            if (decodeErrors > 0)
            {
                Log.Warning($"Custom networks JSON: skipped {decodeErrors} {(decodeErrors == 1 ? "entry" : "entries")} with undecodable text.");
            }

            if (restrictedNetworks > 0)
            {
                Log.Debug($"Custom networks JSON: {restrictedNetworks} {(restrictedNetworks == 1 ? "network is" : "networks are")} restricted by an allow list.");
            }

            // Non-trivial content but nothing usable — likely malformed structure, wrong key order, or bad syntax.
            if (result.Count == 0 && raw.Length > 2)
            {
                Log.Warning(
                    $"Custom networks JSON: no valid entries found in `{LegacyConfigFileName}`. Expected objects like \"id\": 1, \"name\": \"...\" (optionally with \"allow_list\": [\"...\"]) with ids in {MinId}–{MaxId}. Check the file format.");
            }

            return result;
        }

        /// <summary>Extracts the optional <c>allow_list</c> array from a single network object.</summary>
        private static List<string> ParseAllowList(string body, int id)
        {
            var list = new List<string>();

            var match = NetworkAllowListRegex.Match(body);
            if (!match.Success)
            {
                return list;
            }

            foreach (Match entry in JsonStringRegex.Matches(match.Groups[1].Value))
            {
                string value;
                try
                {
                    value = UnescapeJsonString(entry.Groups[1].Value);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Custom networks JSON: skipped an allow_list entry for id {id} (invalid escape sequence): {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                value = PortalNetwork.SanitizeNetworkOwnerDisplayName(value);
                if (string.IsNullOrEmpty(value) || list.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(value);
            }

            return list;
        }

        private static string StripUtf8Bom(string s)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '\uFEFF')
            {
                return s;
            }

            return s.Substring(1);
        }

        private static string UnescapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0)
            {
                return s;
            }

            try
            {
                var sb = new StringBuilder(s.Length);
                for (var i = 0; i < s.Length; i++)
                {
                    if (s[i] != '\\' || i + 1 >= s.Length)
                    {
                        sb.Append(s[i]);
                        continue;
                    }

                    i++;
                    switch (s[i])
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '/':
                            sb.Append('/');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            if (i + 4 < s.Length
                                && uint.TryParse(s.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out var code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            else
                            {
                                sb.Append('u');
                            }

                            break;
                        default:
                            sb.Append(s[i]);
                            break;
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to unescape JSON string fragment.", ex);
            }
        }

        // IsOurConfigFile(), OnWatcherRenamed(), OnWatcherEvent() and ReloadFromDiskAndBroadcast() were
        // removed in 2.4.0 with the file watcher they served.

        #endregion
    }
}
