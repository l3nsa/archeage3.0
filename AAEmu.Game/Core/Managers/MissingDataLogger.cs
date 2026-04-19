using System;
using System.Collections.Concurrent;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    /// <summary>
    /// Collects cases where the server could not fulfil a client request because
    /// of incomplete data (missing template id, unhandled opcode, empty SQL result,
    /// missing SQLite table/column, etc.). Deduplicates by key and writes to a
    /// dedicated log target (see NLog.config, logger name "MissingData").
    ///
    /// Usage:
    ///   MissingDataLogger.Instance.ReportTemplate("item_templates", id, "ItemManager.GetTemplate");
    ///   MissingDataLogger.Instance.ReportPacket(type, level, "GameProtocolHandler");
    ///   MissingDataLogger.Instance.ReportSqlite(tableOrFile, ex.Message, "SQLite.CreateConnection");
    /// </summary>
    public sealed class MissingDataLogger
    {
        private static readonly Lazy<MissingDataLogger> _instance = new(() => new MissingDataLogger());
        public static MissingDataLogger Instance => _instance.Value;

        private static readonly Logger _log = LogManager.GetLogger("MissingData");

        // key -> hit count. First occurrence is written as Warn; subsequent only every N hits as Debug.
        private readonly ConcurrentDictionary<string, long> _seen = new();

        private const long ReportEvery = 100; // re-log after every 100 additional hits

        private MissingDataLogger() { }

        public void ReportTemplate(string table, object key, string caller = null)
        {
            var k = $"TEMPLATE|{table}|{key}";
            Report(k, () => $"Missing template in '{table}' (key={key}){FormatCaller(caller)}");
        }

        public void ReportEmptyResult(string query, string caller = null)
        {
            var k = $"EMPTY|{query}";
            Report(k, () => $"Empty SQL result: {query}{FormatCaller(caller)}");
        }

        public void ReportPacket(uint type, byte level, string ip = null)
        {
            var k = $"PACKET|{level}|{type:X3}";
            Report(k, () => $"Unhandled client packet: level={level} type=0x{type:X3}{(ip != null ? $" from {ip}" : string.Empty)}");
        }

        public void ReportSqlite(string context, string message, string caller = null)
        {
            var k = $"SQLITE|{context}|{message}";
            Report(k, () => $"SQLite failure in {context}: {message}{FormatCaller(caller)}");
        }

        public void ReportGeneric(string category, string detail, string caller = null)
        {
            var k = $"{category}|{detail}";
            Report(k, () => $"[{category}] {detail}{FormatCaller(caller)}");
        }

        private void Report(string key, Func<string> messageFactory)
        {
            var count = _seen.AddOrUpdate(key, 1L, (_, v) => v + 1L);

            if (count == 1L)
            {
                _log.Warn(messageFactory());
            }
            else if (count % ReportEvery == 0L)
            {
                _log.Debug("{0} (hit #{1})", messageFactory(), count);
            }
        }

        private static string FormatCaller(string caller)
        {
            return string.IsNullOrEmpty(caller) ? string.Empty : $" [caller={caller}]";
        }

        /// <summary>Dump aggregated hit counts. Call e.g. on shutdown.</summary>
        public void DumpSummary()
        {
            _log.Info("=== MissingData summary: {0} distinct keys ===", _seen.Count);
            foreach (var kv in _seen)
            {
                _log.Info("  {0}  ×{1}", kv.Key, kv.Value);
            }
        }
    }
}
