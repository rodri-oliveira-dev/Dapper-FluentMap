using System;
using System.Reflection;

namespace Dapper.FluentMap.TypeMaps
{
    internal sealed class ConstructorParameterMap : SqlMapper.IMemberMap
    {
        internal ConstructorParameterMap(string columnName, ParameterInfo parameter)
        {
            if (columnName == null)
            {
                throw new ArgumentNullException(nameof(columnName));
            }

            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            ColumnName = columnName;
            Parameter = parameter;
        }

        public string ColumnName { get; }

        public Type MemberType => Parameter.ParameterType;

        public PropertyInfo Property => null;

        public FieldInfo Field => null;

        public ParameterInfo Parameter { get; }
    }
}
