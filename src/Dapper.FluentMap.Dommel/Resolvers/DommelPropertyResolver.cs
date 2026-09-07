using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Mapping;
using Dommel;

namespace Dapper.FluentMap.Dommel.Resolvers
{
    /// <summary>
    /// Implements the <see cref="IPropertyResolver"/> interface by using the configured mapping.
    /// </summary>
    public class DommelPropertyResolver : DefaultPropertyResolver
    {
        private static readonly IPropertyResolver DefaultResolver = new DefaultPropertyResolver();

        /// <inheritdoc/>
        protected override IEnumerable<PropertyInfo> FilterComplexTypes(IEnumerable<PropertyInfo> properties)
        {
            foreach (var propertyInfo in properties)
            {
                var type = propertyInfo.PropertyType;
                type = Nullable.GetUnderlyingType(type) ?? type;

                if (type.GetTypeInfo().IsPrimitive || type.GetTypeInfo().IsEnum || PrimitiveTypes.Contains(type))
                {
                    yield return propertyInfo;
                }
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<ColumnPropertyInfo> ResolveProperties(Type type)
        {
            IEntityMap entityMap;
            if (FluentMapper.EntityMaps.TryGetValue(type, out entityMap))
            {
                foreach (var property in FilterComplexTypes(type.GetProperties()))
                {
                    // Determine whether the property should be ignored.
                    var propertyMap = DommelPersistenceMetadata.ResolvePropertyMap(type, entityMap, property.Name);
                    if (propertyMap == null || !propertyMap.Ignored)
                    {
                        var dommelPropertyMap = propertyMap as DommelPropertyMap;
                        if (dommelPropertyMap != null)
                        {
                            yield return new ColumnPropertyInfo(property, dommelPropertyMap.EffectiveUpdateGeneratedOption);
                        }
                        else
                        {
                            var mapWithPersistence = propertyMap as IPropertyMapWithPersistenceMetadata;
                            yield return mapWithPersistence == null
                                ? new ColumnPropertyInfo(property)
                                : new ColumnPropertyInfo(property, ResolveGeneratedOption(mapWithPersistence.Persistence));
                        }
                    }
                }
            }
            else
            {
                foreach (var property in DefaultResolver.ResolveProperties(type))
                {
                    yield return property;
                }
            }
        }

        private static DatabaseGeneratedOption ResolveGeneratedOption(PropertyPersistenceMetadata persistence)
        {
            if (persistence.IsIdentity)
            {
                return DatabaseGeneratedOption.Identity;
            }

            if (!persistence.ParticipatesInUpdate)
            {
                return DatabaseGeneratedOption.Computed;
            }

            return DatabaseGeneratedOption.None;
        }
    }
}
