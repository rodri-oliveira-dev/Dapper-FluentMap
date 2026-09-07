using System;
using Dapper.FluentMap.Compatibility;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.TypeMaps
{
    internal sealed class FluentMapTypeMap : MultiTypeMap
    {
        internal FluentMapTypeMap(Type entityType)
            : base(
                new FluentConstructorTypeMap(entityType, GetPropertyMap),
                new DapperFluentPropertyTypeMap(entityType, GetPropertyMap),
                new DefaultTypeMap(entityType))
        {
        }

        private static IPropertyMap GetPropertyMap(Type type, string columnName)
        {
            return FluentMapper.Registry.GetFluentPropertyMap(type, columnName);
        }

    }
}
