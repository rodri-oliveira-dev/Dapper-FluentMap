using System;
using System.Data;
using Dapper.FluentMap.Materialization;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class DataRecordAdapterTests
    {
        [Fact]
        public void SegmentDataRecord_ForwardsEveryIDataRecordMemberWithinSegment()
        {
            var inner = new TrackingDataRecord();
            var record = new SegmentDataRecord(inner, 2, 2);

            Assert.Equal(2, record.FieldCount);
            Assert.Equal("value:2", record[0]);
            Assert.Equal("value:3", record["column3"]);
            Assert.True(record.GetBoolean(0));
            Assert.Equal((byte)2, record.GetByte(0));
            Assert.Equal(2, record.GetBytes(0, 0, new byte[1], 0, 1));
            Assert.Equal('2', record.GetChar(0));
            Assert.Equal(2, record.GetChars(0, 0, new char[1], 0, 1));
            Assert.Same(inner.Reader, record.GetData(0));
            Assert.Equal("type:2", record.GetDataTypeName(0));
            Assert.Equal(new DateTime(2002, 1, 1), record.GetDateTime(0));
            Assert.Equal(2m, record.GetDecimal(0));
            Assert.Equal(2d, record.GetDouble(0));
            Assert.Equal(typeof(int), record.GetFieldType(0));
            Assert.Equal(2f, record.GetFloat(0));
            Assert.Equal(TrackingDataRecord.GuidFor(2), record.GetGuid(0));
            Assert.Equal((short)2, record.GetInt16(0));
            Assert.Equal(2, record.GetInt32(0));
            Assert.Equal(2L, record.GetInt64(0));
            Assert.Equal("column2", record.GetName(0));
            Assert.Equal(1, record.GetOrdinal("COLUMN3"));
            Assert.Equal("string:2", record.GetString(0));
            Assert.Equal("value:2", record.GetValue(0));
            Assert.False(record.IsDBNull(0));

            var values = new object[3];
            Assert.Equal(2, record.GetValues(values));
            Assert.Equal(new object[] { "value:2", "value:3", null }, values);
        }

        [Fact]
        public void SegmentDataRecord_ValidatesArgumentsAndLookups()
        {
            var inner = new TrackingDataRecord();

            Assert.Throws<ArgumentNullException>(() => new SegmentDataRecord(null, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentDataRecord(inner, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentDataRecord(inner, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentDataRecord(inner, 3, 2));

            var record = new SegmentDataRecord(inner, 1, 2);
            Assert.Throws<IndexOutOfRangeException>(() => record.GetValue(-1));
            Assert.Throws<IndexOutOfRangeException>(() => record.GetValue(2));
            Assert.Throws<IndexOutOfRangeException>(() => record.GetOrdinal("missing"));
            Assert.Throws<ArgumentNullException>(() => record.GetValues(null));
        }

        [Fact]
        public void OrdinalMappedDataRecord_ForwardsEveryIDataRecordMemberUsingMappedOrdinals()
        {
            var inner = new TrackingDataRecord();
            var record = new OrdinalMappedDataRecord(inner, new[] { 3, 1 });

            Assert.Equal(2, record.FieldCount);
            Assert.Equal("value:3", record[0]);
            Assert.Equal("value:2", record["column2"]);
            Assert.True(record.GetBoolean(0));
            Assert.Equal((byte)3, record.GetByte(0));
            Assert.Equal(3, record.GetBytes(0, 0, new byte[1], 0, 1));
            Assert.Equal('3', record.GetChar(0));
            Assert.Equal(3, record.GetChars(0, 0, new char[1], 0, 1));
            Assert.Same(inner.Reader, record.GetData(0));
            Assert.Equal("type:3", record.GetDataTypeName(0));
            Assert.Equal(new DateTime(2003, 1, 1), record.GetDateTime(0));
            Assert.Equal(3m, record.GetDecimal(0));
            Assert.Equal(3d, record.GetDouble(0));
            Assert.Equal(typeof(int), record.GetFieldType(0));
            Assert.Equal(3f, record.GetFloat(0));
            Assert.Equal(TrackingDataRecord.GuidFor(3), record.GetGuid(0));
            Assert.Equal((short)3, record.GetInt16(0));
            Assert.Equal(3, record.GetInt32(0));
            Assert.Equal(3L, record.GetInt64(0));
            Assert.Equal("column3", record.GetName(0));
            Assert.Equal(2, record.GetOrdinal("column2"));
            Assert.Equal("string:3", record.GetString(0));
            Assert.Equal("value:3", record.GetValue(0));
            Assert.False(record.IsDBNull(0));

            var values = new object[3];
            Assert.Equal(2, record.GetValues(values));
            Assert.Equal(new object[] { "value:3", "value:1", null }, values);
        }

        [Fact]
        public void OrdinalMappedDataRecord_ValidatesArgumentsAndOrdinals()
        {
            var inner = new TrackingDataRecord();

            Assert.Throws<ArgumentNullException>(() => new OrdinalMappedDataRecord(null, Array.Empty<int>()));
            Assert.Throws<ArgumentNullException>(() => new OrdinalMappedDataRecord(inner, null));

            var record = new OrdinalMappedDataRecord(inner, new[] { 0 });
            Assert.Throws<IndexOutOfRangeException>(() => record.GetValue(-1));
            Assert.Throws<IndexOutOfRangeException>(() => record.GetValue(1));
            Assert.Throws<ArgumentNullException>(() => record.GetValues(null));
        }

        private sealed class TrackingDataRecord : IDataRecord
        {
            internal IDataReader Reader { get; } = new DataTable().CreateDataReader();

            public int FieldCount => 4;
            public object this[int i] => GetValue(i);
            public object this[string name] => GetValue(GetOrdinal(name));
            public bool GetBoolean(int i) => true;
            public byte GetByte(int i) => (byte)i;
            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => i;
            public char GetChar(int i) => i.ToString()[0];
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => i;
            public IDataReader GetData(int i) => Reader;
            public string GetDataTypeName(int i) => "type:" + i;
            public DateTime GetDateTime(int i) => new DateTime(2000 + i, 1, 1);
            public decimal GetDecimal(int i) => i;
            public double GetDouble(int i) => i;
            public Type GetFieldType(int i) => typeof(int);
            public float GetFloat(int i) => i;
            public Guid GetGuid(int i) => GuidFor(i);
            public short GetInt16(int i) => (short)i;
            public int GetInt32(int i) => i;
            public long GetInt64(int i) => i;
            public string GetName(int i) => "column" + i;
            public int GetOrdinal(string name) => int.Parse(name.Substring("column".Length));
            public string GetString(int i) => "string:" + i;
            public object GetValue(int i) => "value:" + i;
            public int GetValues(object[] values) => 0;
            public bool IsDBNull(int i) => false;

            internal static Guid GuidFor(int value) => new Guid(value, 0, 0, new byte[8]);
        }
    }
}
