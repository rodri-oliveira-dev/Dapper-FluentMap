using System;
using Dapper.FluentMap.Compatibility;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.TypeMaps
{
    /// <summary>
    /// Represents a Dapper type mapping strategy which first tries explicit fluent mappings,
    /// then configured conventions, and finally the <see cref="T:Dapper.DefaultTypeMap"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public class FluentMapTypeMap<TEntity> : MultiTypeMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Dapper.FluentMap.TypeMaps.FluentTypeMap"/> class
        /// which uses explicit fluent mappings, conventions and <see cref="T:Dapper.DefaultTypeMap"/>
        /// as mapping strategies.
        /// </summary>
        public FluentMapTypeMap()
            : base(
                new FluentConstructorTypeMap(typeof(TEntity), GetPropertyMap),
                new DapperFluentPropertyTypeMap(typeof(TEntity), GetPropertyMap),
                new DefaultTypeMap(typeof(TEntity)))
        {
        }

        private static IPropertyMap GetPropertyMap(Type type, string columnName)
        {
            return FluentMapper.Registry.GetFluentPropertyMap(type, columnName);
        }

    }
}
