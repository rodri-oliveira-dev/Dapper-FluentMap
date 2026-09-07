using System;
using System.Reflection;

namespace Dapper.FluentMap.Compatibility
{
    internal sealed class DapperIgnoredMemberMap : SqlMapper.IMemberMap
    {
        internal DapperIgnoredMemberMap(string columnName)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        }

        public string ColumnName { get; }

        public Type MemberType => typeof(object);

        public PropertyInfo Property => null;

        public FieldInfo Field => null;

        public ParameterInfo Parameter => null;

        internal static bool IsIgnored(SqlMapper.IMemberMap memberMap)
        {
            return memberMap is DapperIgnoredMemberMap;
        }
    }
}
