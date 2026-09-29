using System.Diagnostics;
using System.Reflection;
using Vapok.Common.Abstractions;
using Vapok.Common.Managers;

namespace XPortalNetworks
{
    internal static class Log
    {
        private static ILogIt _logger;

        public static void Initialize(ILogIt logger)
        {
            _logger = logger;
        }

        private static ILogIt GetLogger()
        {
            if (_logger == null)
            {
                LogManager.Init(Mod.Info.GUID, out _logger);
            }
            return _logger;
        }

        public static void Debug(object message)
        {
            StackFrame frame = new StackTrace().GetFrame(1);
            MethodBase method = frame?.GetMethod();
            string callingClass = method?.DeclaringType?.Name ?? "Unknown";
            string callingMethod = method?.Name ?? "Unknown";
            GetLogger()?.Debug($"[{callingClass}.{callingMethod}]  {message}");
        }

        public static void Info(object message) => GetLogger()?.Info(message?.ToString() ?? string.Empty);
        public static void Message(object message) => GetLogger()?.Message(message?.ToString() ?? string.Empty);
        public static void Warning(object message) => GetLogger()?.Warning(message?.ToString() ?? string.Empty);
        public static void Error(object message) => GetLogger()?.Error(message?.ToString() ?? string.Empty);
        public static void Fatal(object message) => GetLogger()?.Fatal(message?.ToString() ?? string.Empty);
    }
}
