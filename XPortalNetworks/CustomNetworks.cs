using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using XPortalNetworks.RPC;

namespace XPortalNetworks
{
    /// <summary>Configured portal networks (ids 1–15). Id 0 is normal Global.</summary>
    internal static class CustomNetworks
    {
        internal const int MinId = 1;
        internal const int MaxId = 15;

        internal const string ConfigFileName = "xportal_networks.json";

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

        // Each network entry is a flat JSON object, e.g. { "id": 1, "name": "Bosses", "allow_list": ["Steam_..."] }.
        // The parse below extracts the fields we care about without pulling in a full JSON dependency.
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

        private static FileSystemWatcher _watcher;
        private static readonly object ReloadGate = new object();

        // Hot-reload state. The FileSystemWatcher (background thread) and the poller only raise a
        // flag; the actual reload always runs on the game's main thread via ServerTick().
        private static volatile bool _hotReloadActive;
        private static volatile bool _reloadPending;
        private static volatile int _reloadDebounceFrames;
        private static DateTime _lastWriteUtc = DateTime.MinValue;
        private static long _lastFileSize = -1L;
        private static int _pollCountdown;

        /// <summary>Frames to wait after the last detected change before reloading (coalesces editor saves).</summary>
        private const int ReloadDebounceFrames = 45;

        /// <summary>How often (in frames) to poll the file as a fallback for missed watcher events.</summary>
        private const int PollIntervalFrames = 30;

        /// <summary>Fired when the active network list changes.</summary>
        internal static event Action ListChanged;

        #region Registry (client + server)

        internal static void ResetSession()
        {
            lock (ActiveById)
            {
                ActiveById.Clear();
            }
        }

        internal static void ServerSetFromParsed(Dictionary<long, PortalNetworkDefinition> parsed)
        {
            lock (ActiveById)
            {
                ActiveById.Clear();
                foreach (var kv in parsed.OrderBy(k => k.Key))
                {
                    ActiveById[kv.Key] = kv.Value;
                }
            }
        }

        /// <summary>
        /// Applies the network list the server selected for this client. The server only sends
        /// the networks this client may use and never includes allow-list data, so a client
        /// cannot learn about (or display) restricted networks it isn't a member of.
        /// </summary>
        internal static void ApplyFromServer(ZPackage pkg)
        {
            var count = pkg.ReadInt();
            lock (ActiveById)
            {
                ActiveById.Clear();
                for (var i = 0; i < count; i++)
                {
                    var id = pkg.ReadLong();
                    var name = pkg.ReadString();

                    if (id < MinId || id > MaxId || string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    ActiveById[id] = new PortalNetworkDefinition(id, name, Array.Empty<string>());
                }
            }

            ListChanged?.Invoke();
        }

        /// <summary>
        /// Builds the network list for a single client: only the networks that client is
        /// permitted to use, and without any allow-list data.
        /// </summary>
        internal static ZPackage PackForClient(long peerId)
        {
            var privileged = NetPeerUtility.IsPeerPrivilegedForPortalNetwork(peerId) && AdminsBypassNetworks;
            var userId = privileged ? null : NetPeerUtility.GetPeerUserId(peerId);
            var numericId = privileged ? null : NetPeerUtility.GetPeerPlayerIdString(peerId);

            List<PortalNetworkDefinition> snapshot;
            lock (ActiveById)
            {
                snapshot = ActiveById.Values.OrderBy(d => d.Id).ToList();
            }

            var allowed = privileged
                ? snapshot
                : snapshot.Where(d => d.Allows(userId, numericId)).ToList();

            var pkg = new ZPackage();
            pkg.Write(allowed.Count);
            foreach (var definition in allowed)
            {
                pkg.Write(definition.Id);
                pkg.Write(definition.Name);
            }

            return pkg;
        }

        /// <summary>Pushes each connected peer its own tailored network list.</summary>
        internal static void BroadcastToAllPeers()
        {
            if (ZNet.instance == null)
            {
                return;
            }

            var peers = ZNet.instance.GetConnectedPeers();
            if (peers == null || peers.Count == 0)
            {
                return;
            }

            foreach (var peer in peers)
            {
                if (peer == null || peer.m_uid == 0L || peer.m_uid == ZNet.GetUID())
                {
                    continue;
                }

                SendToClient.CustomNetworks(peer.m_uid, PackForClient(peer.m_uid));
            }
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
        /// allowed; privileged players (server admins/host) bypass allow lists. A reserved id that
        /// is unknown to this instance is treated as not allowed - on clients the server only sends
        /// the networks that client may use.
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
        /// True when the local player may see/use the given network id. On clients the server only
        /// delivers permitted networks, so an id that is absent is not allowed.
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

        #region Server: file + watcher

        internal static void InitializeServer()
        {
            if (!Environment.IsServer)
            {
                return;
            }

            EnsureDefaultConfigExists();
            ReloadFromDiskAndBroadcast(isInitial: true);
            UpdateFileBaseline();

            // Hot-reload is enabled regardless of whether the watcher can be created: the
            // main-thread poll in ServerTick() guarantees changes are still picked up.
            _hotReloadActive = true;
            _pollCountdown = 0;

            var dir = Path.GetDirectoryName(GetConfigFilePath());
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            try
            {
                _watcher?.Dispose();
                _watcher = new FileSystemWatcher(dir)
                {
                    Filter = ConfigFileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                };
                _watcher.Changed += OnWatcherEvent;
                _watcher.Created += OnWatcherEvent;
                _watcher.Deleted += OnWatcherEvent;
                _watcher.Renamed += OnWatcherRenamed;
                _watcher.EnableRaisingEvents = true;
                Log.Debug($"Watching `{dir}` for `{ConfigFileName}` changes.");
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not watch custom networks config folder; falling back to polling only: {ex.Message}");
            }
        }

        internal static void ShutdownServer()
        {
            _hotReloadActive = false;
            _reloadPending = false;

            _watcher?.Dispose();
            _watcher = null;
        }

        /// <summary>
        /// Called every frame from the plugin's Update() (main thread). Polls the file as a
        /// fallback for missed watcher events and performs the debounced reload. No-ops when
        /// hot-reload is not active (clients / dedicated hosts only).
        /// </summary>
        internal static void ServerTick()
        {
            if (!_hotReloadActive)
            {
                return;
            }

            if (_pollCountdown-- <= 0)
            {
                _pollCountdown = PollIntervalFrames;
                if (DetectFileChange())
                {
                    MarkReloadPending();
                }
            }

            if (!_reloadPending)
            {
                return;
            }

            if (_reloadDebounceFrames > 0)
            {
                _reloadDebounceFrames--;
                return;
            }

            _reloadPending = false;
            try
            {
                ReloadFromDiskAndBroadcast(isInitial: false);
            }
            catch (Exception ex)
            {
                Log.Error($"Custom networks hot-reload failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void MarkReloadPending()
        {
            _reloadDebounceFrames = ReloadDebounceFrames;
            _reloadPending = true;
        }

        /// <summary>
        /// Cheap main-thread check: has the file changed since we last looked? Detects both
        /// modifications (write time / size) and removal (so the default is re-seeded).
        /// </summary>
        private static bool DetectFileChange()
        {
            try
            {
                var path = GetConfigFilePath();

                if (!File.Exists(path))
                {
                    // File was removed since the last check: reload so EnsureDefaultConfigExists re-seeds it.
                    if (_lastFileSize != -1L || _lastWriteUtc != DateTime.MinValue)
                    {
                        _lastFileSize = -1L;
                        _lastWriteUtc = DateTime.MinValue;
                        Log.Warning($"Custom networks file '{path}' was removed; recreating it from the embedded default.");
                        return true;
                    }

                    return false;
                }

                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc != _lastWriteUtc || info.Length != _lastFileSize)
                {
                    _lastWriteUtc = info.LastWriteTimeUtc;
                    _lastFileSize = info.Length;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Custom networks poll failed: {ex.GetType().Name}: {ex.Message}");
            }

            return false;
        }

        private static void UpdateFileBaseline()
        {
            try
            {
                var path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    _lastWriteUtc = DateTime.MinValue;
                    _lastFileSize = -1L;
                    return;
                }

                var info = new FileInfo(path);
                _lastWriteUtc = info.LastWriteTimeUtc;
                _lastFileSize = info.Length;
            }
            catch (Exception ex)
            {
                Log.Debug($"Custom networks baseline failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(Paths.ConfigPath, Mod.Info.Name, ConfigFileName);
        }

        /// <summary>Default JSON baked into the assembly (see csproj EmbeddedResource).</summary>
        private static string ReadEmbeddedTemplate()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var resName in asm.GetManifestResourceNames())
                {
                    if (!resName.EndsWith(ConfigFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (var s = asm.GetManifestResourceStream(resName))
                    {
                        if (s == null)
                        {
                            continue;
                        }

                        using (var r = new StreamReader(s, Utf8NoBom))
                        {
                            return r.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to read embedded `{ConfigFileName}` from assembly: {ex.GetType().Name}: {ex.Message}");
            }

            return null;
        }

        private static void EnsureDefaultConfigExists()
        {
            var path = GetConfigFilePath();
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(path))
                {
                    return;
                }

                var templateText = ReadEmbeddedTemplate();
                if (!string.IsNullOrEmpty(templateText))
                {
                    File.WriteAllText(path, templateText, Utf8NoBom);
                    Log.Info($"Created `{path}` from embedded `{ConfigFileName}`.");
                    return;
                }

                Log.Error($"Embedded default template `{ConfigFileName}` not found; cannot create `{path}`.");
            }
            catch (Exception ex)
            {
                Log.Error($"Could not create default custom networks file: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the config file from disk. Returns false if the file does not exist (not an error).
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
                Log.Error($"Custom networks JSON: unexpected failure while scanning `{ConfigFileName}`: {ex.GetType().Name}: {ex.Message}");
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
                    $"Custom networks JSON: no valid entries found in `{ConfigFileName}`. Expected objects like \"id\": 1, \"name\": \"...\" (optionally with \"allow_list\": [\"...\"]) with ids in {MinId}–{MaxId}. Check the file format.");
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

        private static bool IsOurConfigFile(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static void OnWatcherRenamed(object sender, RenamedEventArgs e)
        {
            if (IsOurConfigFile(e.Name))
            {
                MarkReloadPending();
            }
        }

        private static void OnWatcherEvent(object sender, FileSystemEventArgs e)
        {
            if (IsOurConfigFile(e.Name))
            {
                MarkReloadPending();
            }
        }

        private static void ReloadFromDiskAndBroadcast(bool isInitial)
        {
            lock (ReloadGate)
            {
                if (!TryReadConfigFile(out var text, out var readError))
                {
                    if (!readError)
                    {
                        EnsureDefaultConfigExists();
                    }

                    if (!TryReadConfigFile(out text, out readError) || text == null)
                    {
                        if (readError)
                        {
                            Log.Error(
                                $"Keeping the previous custom network list; fix `{GetConfigFilePath()}` and save, or restart the server after correcting the file.");
                            return;
                        }

                        text = string.Empty;
                    }
                }

                var parsed = ParseConfigJson(text ?? string.Empty);
                ServerSetFromParsed(parsed);
                MigrateInvalidNetworks();

                if (!isInitial)
                {
                    BroadcastToAllPeers();
                }

                if (!isInitial)
                {
                    Log.Info("Custom networks file reloaded and pushed to clients.");
                }

                if (!Environment.IsHeadless)
                {
                    NotifyListChangedLocal();
                }

                // Re-baseline so the file we just read (or re-seeded) isn't flagged again next poll.
                UpdateFileBaseline();
            }
        }

        #endregion
    }
}
