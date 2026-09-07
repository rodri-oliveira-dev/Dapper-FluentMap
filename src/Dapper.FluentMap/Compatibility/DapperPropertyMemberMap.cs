using System;
using System.Reflection;

namespace Dapper.FluentMap.Compatibility
{
    internal sealed class DapperPropertyMemberMap : SqlMapper.IMemberMap
    {
        internal DapperPropertyMemberMap(string columnName, PropertyInfo property)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }

        public string ColumnName { get; }

        public Type MemberType => Property.PropertyType;

        public PropertyInfo Property { get; }

        public FieldInfo Field => null;

        public ParameterInfo Parameter => null;
    }
}
