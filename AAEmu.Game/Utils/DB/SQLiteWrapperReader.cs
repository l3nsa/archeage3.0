using System;
using System.Collections.Generic;

using AAEmu.Game.Core.Managers;

using Microsoft.Data.Sqlite;

namespace AAEmu.Game.Utils.DB
{
    /// <summary>
    /// Wrapper around <see cref="SqliteDataReader"/> that is tolerant of schema drift
    /// (columns present in code but missing from this particular version of compact.sqlite3).
    /// Missing columns are reported once via <see cref="MissingDataLogger"/> and then
    /// every subsequent getter call returns a sensible default, so the server keeps
    /// running instead of crashing and we accumulate the list of gaps for later.
    /// </summary>
    public class SQLiteWrapperReader : IDisposable
    {
        private const int MissingOrdinal = -1;

        private readonly SqliteDataReader _reader;
        private readonly Dictionary<string, int> _ordinal;

        public SQLiteWrapperReader(SqliteDataReader reader)
        {
            _reader = reader;
            _ordinal = new Dictionary<string, int>();
        }

        public bool Read() => _reader.Read();

        // ------------------------------------------------------------------
        // Ordinal lookup
        // ------------------------------------------------------------------

        public int GetOrdinal(string column)
        {
            if (_ordinal.TryGetValue(column, out var cached))
                return cached;

            int ordinal;
            try
            {
                ordinal = _reader.GetOrdinal(column);
            }
            catch (Exception)
            {
                ordinal = MissingOrdinal;
                // Try to include table name hint — SqliteDataReader does not expose it directly,
                // so we only record the column name. Good enough for the gap inventory.
                MissingDataLogger.Instance.ReportGeneric("SQLITE_COLUMN", column, "SQLiteWrapperReader.GetOrdinal");
            }

            _ordinal[column] = ordinal;
            return ordinal;
        }

        public bool IsDBNull(string column)
        {
            var ord = GetOrdinal(column);
            return ord == MissingOrdinal || _reader.IsDBNull(ord);
        }

        // ------------------------------------------------------------------
        // Primitive getters — all return a default for missing columns.
        // ------------------------------------------------------------------

        public object GetValue(string column)
        {
            var ord = GetOrdinal(column);
            return ord == MissingOrdinal ? null : _reader.GetValue(ord);
        }

        public bool GetBoolean(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return false;
            return _reader.GetBoolean(ord);
        }

        public bool GetBoolean(string column, bool fromString)
        {
            if (fromString)
            {
                if (IsDBNull(column))
                    return false;

                var value = GetString(column);
                return value == "t" || value == "1";
            }

            return GetBoolean(column);
        }

        public byte GetByte(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0;
            return _reader.GetByte(ord);
        }

        public byte GetByte(string column, byte defaultValue)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return defaultValue;
            return _reader.GetByte(ord);
        }

        public long GetBytes(string column, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal)
                return 0L;
            return _reader.GetBytes(ord, fieldOffset, buffer, bufferOffset, length);
        }

        public char GetChar(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return '\0';
            return _reader.GetChar(ord);
        }

        public long GetChars(string column, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal)
                return 0L;
            return _reader.GetChars(ord, fieldOffset, buffer, bufferOffset, length);
        }

        public Guid GetGuid(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return Guid.Empty;
            return _reader.GetGuid(ord);
        }

        public short GetInt16(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0;
            return unchecked((short)_reader.GetInt64(ord));
        }

        public ushort GetUInt16(string column) => unchecked((ushort)GetInt16(column));

        public int GetInt32(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0;
            // SQLite stores INTEGER as 8-byte; widen to Int64 and unchecked-cast to avoid
            // OverflowException for values outside Int32 range.
            return unchecked((int)_reader.GetInt64(ord));
        }

        public int GetInt32(string column, int defaultValue)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return defaultValue;
            return unchecked((int)_reader.GetInt64(ord));
        }

        public uint GetUInt32(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0u;
            return unchecked((uint)_reader.GetInt64(ord));
        }

        public uint GetUInt32(string column, uint defaultValue)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return defaultValue;
            return unchecked((uint)_reader.GetInt64(ord));
        }

        public long GetInt64(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0L;
            return _reader.GetInt64(ord);
        }

        public ulong GetUInt64(string column) => unchecked((ulong)GetInt64(column));

        public float GetFloat(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0f;
            return _reader.GetFloat(ord);
        }

        public float GetFloat(string column, float defaultValue)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return defaultValue;
            return _reader.GetFloat(ord);
        }

        public double GetDouble(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0d;
            return _reader.GetDouble(ord);
        }

        public string GetString(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return string.Empty;
            return _reader.GetString(ord);
        }

        public string GetString(string column, string defaultValue)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return defaultValue;
            return _reader.GetString(ord);
        }

        public decimal GetDecimal(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return 0m;
            return _reader.GetDecimal(ord);
        }

        public DateTime GetDateTime(string column)
        {
            var ord = GetOrdinal(column);
            if (ord == MissingOrdinal || _reader.IsDBNull(ord))
                return DateTime.MinValue;
            return _reader.GetDateTime(ord);
        }

        public void Dispose()
        {
            _ordinal.Clear();
            _reader.Dispose();
        }
    }
}
