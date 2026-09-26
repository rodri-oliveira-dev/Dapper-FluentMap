using System;
using System.Data;

namespace Dapper.FluentMap.Materialization
{
    internal sealed class OrdinalMappedDataRecord : IDataRecord
    {
        private readonly IDataRecord _inner;
        private readonly int[] _ordinalMap;

        internal OrdinalMappedDataRecord(IDataRecord inner, int[] ordinalMap)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _ordinalMap = ordinalMap ?? throw new ArgumentNullException(nameof(ordinalMap));
        }

        public int FieldCount => _ordinalMap.Length;

        public object this[int i] => _inner[MapOrdinal(i)];

        public object this[string name] => _inner[name];

        public bool GetBoolean(int i) => _inner.GetBoolean(MapOrdinal(i));

        public byte GetByte(int i) => _inner.GetByte(MapOrdinal(i));

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) =>
            _inner.GetBytes(MapOrdinal(i), fieldOffset, buffer, bufferoffset, length);

        public char GetChar(int i) => _inner.GetChar(MapOrdinal(i));

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) =>
            _inner.GetChars(MapOrdinal(i), fieldoffset, buffer, bufferoffset, length);

        public IDataReader GetData(int i) => _inner.GetData(MapOrdinal(i));

        public string GetDataTypeName(int i) => _inner.GetDataTypeName(MapOrdinal(i));

        public DateTime GetDateTime(int i) => _inner.GetDateTime(MapOrdinal(i));

        public decimal GetDecimal(int i) => _inner.GetDecimal(MapOrdinal(i));

        public double GetDouble(int i) => _inner.GetDouble(MapOrdinal(i));

        public Type GetFieldType(int i) => _inner.GetFieldType(MapOrdinal(i));

        public float GetFloat(int i) => _inner.GetFloat(MapOrdinal(i));

        public Guid GetGuid(int i) => _inner.GetGuid(MapOrdinal(i));

        public short GetInt16(int i) => _inner.GetInt16(MapOrdinal(i));

        public int GetInt32(int i) => _inner.GetInt32(MapOrdinal(i));

        public long GetInt64(int i) => _inner.GetInt64(MapOrdinal(i));

        public string GetName(int i) => _inner.GetName(MapOrdinal(i));

        public int GetOrdinal(string name) => _inner.GetOrdinal(name);

        public string GetString(int i) => _inner.GetString(MapOrdinal(i));

        public object GetValue(int i) => _inner.GetValue(MapOrdinal(i));

        public int GetValues(object[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
            {
                values[i] = GetValue(i);
            }

            return count;
        }

        public bool IsDBNull(int i) => _inner.IsDBNull(MapOrdinal(i));

        private int MapOrdinal(int ordinal)
        {
            if (ordinal < 0 || ordinal >= _ordinalMap.Length)
            {
                throw new IndexOutOfRangeException();
            }

            return _ordinalMap[ordinal];
        }
    }
}
