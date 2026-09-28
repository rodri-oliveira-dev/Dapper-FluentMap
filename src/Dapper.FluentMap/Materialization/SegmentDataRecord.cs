using System;
using System.Data;

namespace Dapper.FluentMap.Materialization
{
    internal sealed class SegmentDataRecord : IDataRecord
    {
        private readonly IDataRecord _inner;
        private readonly int _start;

        internal SegmentDataRecord(IDataRecord inner, int start, int length)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (start < 0 || length < 0 || start + length > inner.FieldCount)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "The segment must be within the data record.");
            }

            _start = start;
            FieldCount = length;
        }

        public int FieldCount { get; }

        public object this[int i] => _inner[ToInnerOrdinal(i)];

        public object this[string name] => GetValue(GetOrdinal(name));

        public bool GetBoolean(int i) => _inner.GetBoolean(ToInnerOrdinal(i));

        public byte GetByte(int i) => _inner.GetByte(ToInnerOrdinal(i));

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => _inner.GetBytes(ToInnerOrdinal(i), fieldOffset, buffer, bufferoffset, length);

        public char GetChar(int i) => _inner.GetChar(ToInnerOrdinal(i));

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => _inner.GetChars(ToInnerOrdinal(i), fieldoffset, buffer, bufferoffset, length);

        public IDataReader GetData(int i) => _inner.GetData(ToInnerOrdinal(i));

        public string GetDataTypeName(int i) => _inner.GetDataTypeName(ToInnerOrdinal(i));

        public DateTime GetDateTime(int i) => _inner.GetDateTime(ToInnerOrdinal(i));

        public decimal GetDecimal(int i) => _inner.GetDecimal(ToInnerOrdinal(i));

        public double GetDouble(int i) => _inner.GetDouble(ToInnerOrdinal(i));

        public Type GetFieldType(int i) => _inner.GetFieldType(ToInnerOrdinal(i));

        public float GetFloat(int i) => _inner.GetFloat(ToInnerOrdinal(i));

        public Guid GetGuid(int i) => _inner.GetGuid(ToInnerOrdinal(i));

        public short GetInt16(int i) => _inner.GetInt16(ToInnerOrdinal(i));

        public int GetInt32(int i) => _inner.GetInt32(ToInnerOrdinal(i));

        public long GetInt64(int i) => _inner.GetInt64(ToInnerOrdinal(i));

        public string GetName(int i) => _inner.GetName(ToInnerOrdinal(i));

        public int GetOrdinal(string name)
        {
            for (var i = 0; i < FieldCount; i++)
            {
                if (string.Equals(GetName(i), name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            throw new IndexOutOfRangeException("Column '" + name + "' was not found in the current split segment.");
        }

        public string GetString(int i) => _inner.GetString(ToInnerOrdinal(i));

        public object GetValue(int i) => _inner.GetValue(ToInnerOrdinal(i));

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

        public bool IsDBNull(int i) => _inner.IsDBNull(ToInnerOrdinal(i));

        private int ToInnerOrdinal(int ordinal)
        {
            if (ordinal < 0 || ordinal >= FieldCount)
            {
                throw new IndexOutOfRangeException("Column ordinal " + ordinal + " is outside the current split segment.");
            }

            return _start + ordinal;
        }
    }
}
