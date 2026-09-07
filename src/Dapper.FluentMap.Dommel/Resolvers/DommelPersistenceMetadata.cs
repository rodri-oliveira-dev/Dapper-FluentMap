using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Dommel.Resolvers
{
    internal static class DommelPersistenceMetadata
    {
        internal static bool TryGetPersistence(PropertyInfo property, out PropertyPersistenceMetadata persistence)
        {
            persistence = null;

            IEntityMap entityMap;
            if (!TryGetEntityMap(property, out entityMap))
            {
                return false;
            }

            var propertyMap = ResolvePropertyMap(property.ReflectedType ?? property.DeclaringType, entityMap, property.Name);
            if (propertyMap == null)
            {
                persistence = PropertyPersistenceMetadata.Default;
                return true;
            }

            var mapWithPersistence = propertyMap as IPropertyMapWithPersistenceMetadata;
            persistence = mapWithPersistence == null
                ? (propertyMap.Ignored ? PropertyPersistenceMetadata.Ignored : PropertyPersistenceMetadata.Default)
                : mapWithPersistence.Persistence;
            return true;
        }

        internal static IEnumerable<PropertyInfo> ResolveInsertProperties(Type type)
        {
            IEntityMap entityMap;
            if (!FluentMapper.EntityMaps.TryGetValue(type, out entityMap))
            {
                return null;
            }

            var propertyResolver = new DommelPropertyResolver();
            return propertyResolver
                .ResolveProperties(type)
                .Where(property =>
                {
                    PropertyPersistenceMetadata persistence;
                    return !TryGetPersistence(property.Property, out persistence) ||
                        persistence.ParticipatesInInsert;
                })
                .Select(property => property.Property);
        }

        internal static IPropertyMap ResolvePropertyMap(Type type, IEntityMap entityMap, string propertyName)
        {
            return ResolvePropertyMaps(type, entityMap).FirstOrDefault(map => map.PropertyInfo.Name == propertyName);
        }

        internal static IList<IPropertyMap> ResolvePropertyMaps(Type type, IEntityMap entityMap)
        {
            var propertyMaps = new List<IPropertyMap>();
            AddPropertyMapsWithOverride(propertyMaps, entityMap.PropertyMaps);

            foreach (var baseType in GetIncludedBaseTypes(entityMap))
            {
                IEntityMap baseMap;
                if (FluentMapper.EntityMaps.TryGetValue(baseType, out baseMap))
                {
                    AddPropertyMapsWithOverride(propertyMaps, ResolvePropertyMaps(baseType, baseMap));
                }
            }

            return propertyMaps;
        }

        private static bool TryGetEntityMap(PropertyInfo property, out IEntityMap entityMap)
        {
            entityMap = null;

            var reflectedType = property.ReflectedType;
            if (reflectedType != null && FluentMapper.EntityMaps.TryGetValue(reflectedType, out entityMap))
            {
                return true;
            }

            var declaringType = property.DeclaringType;
            return declaringType != null && FluentMapper.EntityMaps.TryGetValue(declaringType, out entityMap);
        }

        private static void AddPropertyMapsWithOverride(IList<IPropertyMap> target, IEnumerable<IPropertyMap> propertyMaps)
        {
            foreach (var propertyMap in propertyMaps)
            {
                if (target.Any(existing => existing.PropertyInfo.Name == propertyMap.PropertyInfo.Name))
                {
                    continue;
                }

                target.Add(propertyMap);
            }
        }

        private static IEnumerable<Type> GetIncludedBaseTypes(IEntityMap entityMap)
        {
            var includedBaseInterface = entityMap
                .GetType()
                .GetInterfaces()
                .FirstOrDefault(type => type.FullName == "Dapper.FluentMap.Mapping.IEntityMapWithIncludedBaseTypes");

            if (includedBaseInterface == null)
            {
                return new Type[0];
            }

            var property = includedBaseInterface.GetProperty("IncludedBaseTypes");
            return property == null
                ? new Type[0]
                : ((IEnumerable<Type>)property.GetValue(entityMap, null)).ToArray();
        }
    }
}
