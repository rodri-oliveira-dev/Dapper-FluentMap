using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap
{
    internal static class MappingConfigurationValidator
    {
        internal static void ValidateEntityMap(Type entityType, IEntityMap entityMap)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (entityMap == null)
            {
                throw new ArgumentNullException(nameof(entityMap));
            }

            var maps = GetEntityMapDescriptors(entityType, entityMap).ToList();
            ValidateDuplicateMemberPaths(entityType, maps, "entity map", entityMap.GetType());
            ValidateColumnConflicts(entityType, maps, "entity map", entityMap.GetType());
            ValidateNestedMaterializationPaths(entityType, maps, "entity map", entityMap.GetType());
        }

        internal static void ValidateComposedEntityMap(Type entityType, IEntityMap entityMap, IList<IPropertyMap> propertyMaps)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (entityMap == null)
            {
                throw new ArgumentNullException(nameof(entityMap));
            }

            if (propertyMaps == null)
            {
                throw new ArgumentNullException(nameof(propertyMaps));
            }

            var maps = GetEntityMapDescriptors(entityType, propertyMaps, entityMap.GetType(), "composed entity map").ToList();
            ValidateColumnConflicts(entityType, maps, "composed entity map", entityMap.GetType());
            ValidateNestedMaterializationPaths(entityType, maps, "composed entity map", entityMap.GetType());
        }

        internal static void ValidateConvention(Type entityType, Convention convention)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (convention == null)
            {
                throw new ArgumentNullException(nameof(convention));
            }

            var maps = GetConventionMapDescriptors(entityType, convention).ToList();
            ValidateDuplicateMemberPaths(entityType, maps, "convention", convention.GetType());
            ValidateColumnConflicts(entityType, maps, "convention", convention.GetType());
        }

        internal static void ValidateConventionConfiguration(Type entityType, Convention convention, PropertyConventionConfiguration configuration)
        {
            if (configuration.PropertyConfiguration == null)
            {
                throw new FluentMapConfigurationException(
                    $"Convention '{FormatType(convention.GetType())}' has a matching property rule without configuration for entity '{FormatType(entityType)}'. Call Configure(...) and choose a column name, prefix or transformer.");
            }
        }

        internal static void ValidateConventionColumn(Type entityType, Convention convention, PropertyMap propertyMap)
        {
            if (string.IsNullOrEmpty(propertyMap.ColumnName))
            {
                throw new FluentMapConfigurationException(
                    $"Convention '{FormatType(convention.GetType())}' produced an empty column name for property path '{PropertyMapIdentity.GetMemberPath(propertyMap)}' on entity '{FormatType(entityType)}'.");
            }
        }

        private static IEnumerable<MapDescriptor> GetEntityMapDescriptors(Type entityType, IEntityMap entityMap)
        {
            if (entityMap.PropertyMaps == null)
            {
                throw new FluentMapConfigurationException(
                    $"Entity map '{FormatType(entityMap.GetType())}' for entity '{FormatType(entityType)}' returned a null property map collection.");
            }

            return GetEntityMapDescriptors(entityType, entityMap.PropertyMaps, entityMap.GetType(), "entity map");
        }

        private static IEnumerable<MapDescriptor> GetEntityMapDescriptors(Type entityType, IEnumerable<IPropertyMap> propertyMaps, Type sourceType, string sourceKind)
        {
            foreach (var map in propertyMaps)
            {
                yield return CreateDescriptor(entityType, map, sourceType, sourceKind, requireEntityCompatibility: true);
            }
        }

        private static IEnumerable<MapDescriptor> GetConventionMapDescriptors(Type entityType, Convention convention)
        {
            foreach (var map in convention.PropertyMaps)
            {
                if (map == null)
                {
                    throw new FluentMapConfigurationException(
                        $"Convention '{FormatType(convention.GetType())}' for entity '{FormatType(entityType)}' contains a null property map.");
                }

                if (!IsMapForEntity(entityType, map))
                {
                    continue;
                }

                yield return CreateDescriptor(entityType, map, convention.GetType(), "convention", requireEntityCompatibility: false);
            }
        }

        private static MapDescriptor CreateDescriptor(Type entityType, IPropertyMap map, Type sourceType, string sourceKind, bool requireEntityCompatibility)
        {
            if (map == null)
            {
                throw new FluentMapConfigurationException(
                    $"The {sourceKind} '{FormatType(sourceType)}' for entity '{FormatType(entityType)}' contains a null property map.");
            }

            if (map.PropertyInfo == null)
            {
                throw new FluentMapConfigurationException(
                    $"The {sourceKind} '{FormatType(sourceType)}' for entity '{FormatType(entityType)}' contains a property map without metadata.");
            }

            var memberPath = PropertyMapIdentity.GetMemberPath(map);
            if (requireEntityCompatibility && !IsMemberPathCompatible(entityType, memberPath))
            {
                throw new FluentMapConfigurationException(
                    $"Property path '{memberPath}' is not compatible with entity '{FormatType(entityType)}'. The first property is declared by '{FormatType(memberPath.Properties[0].DeclaringType)}'.");
            }

            if (string.IsNullOrEmpty(map.ColumnName))
            {
                throw new FluentMapConfigurationException(
                    $"Property path '{memberPath}' on entity '{FormatType(entityType)}' has an empty column name.");
            }

            ValidatePersistenceMetadata(entityType, map, memberPath, sourceKind, sourceType);
            ValidateConversionMetadata(entityType, map, memberPath, sourceKind, sourceType);

            return new MapDescriptor(map, memberPath);
        }

        private static void ValidateConversionMetadata(Type entityType, IPropertyMap map, MemberPath memberPath, string sourceKind, Type sourceType)
        {
            var conversion = PropertyMapConversion.GetConversion(map);
            if (conversion == null)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Conversion metadata cannot be null.");
            }

            if (conversion.HasReadConverter)
            {
                ValidateConverterDescriptor(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    conversion.ReadConverter,
                    PropertyConversionDirection.Read);
            }

            if (conversion.HasWriteConverter)
            {
                ValidateConverterDescriptor(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    conversion.WriteConverter,
                    PropertyConversionDirection.Write);
            }

            var persistence = PropertyMapPersistence.GetPersistence(map);
            if (persistence == null)
            {
                return;
            }

            if (persistence.IgnoredByFluentMap && conversion.HasReadConverter)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Ignored properties are never materialized by FluentMap and cannot use read converters.");
            }

            if (conversion.HasWriteConverter &&
                persistence.IgnoredByFluentMap)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Ignored properties are never persisted and cannot use write converters.");
            }

            if (conversion.HasWriteConverter &&
                !persistence.ParticipatesInInsert &&
                !persistence.ParticipatesInUpdate &&
                !persistence.IsKey)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "The write converter is configured for a property that never participates in insert, update or key persistence.");
            }
        }

        private static void ValidateConverterDescriptor(
            Type entityType,
            MemberPath memberPath,
            string sourceKind,
            Type sourceType,
            PropertyConverterMetadata converter,
            PropertyConversionDirection expectedDirection)
        {
            if (converter == null)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    $"The {expectedDirection.ToString().ToLowerInvariant()} converter descriptor cannot be null.");
            }

            if (converter.Direction != expectedDirection)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    $"The converter descriptor direction is '{converter.Direction}' but '{expectedDirection}' was expected.");
            }

            if (converter.ConverterType == null ||
                converter.DatabaseType == null ||
                converter.PropertyType == null)
            {
                throw InvalidConversionMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Converter type, database type and property type must all be present.");
            }
        }

        private static FluentMapConfigurationException InvalidConversionMetadata(Type entityType, MemberPath memberPath, string sourceKind, Type sourceType, string reason)
        {
            return new FluentMapConfigurationException(
                $"Property path '{memberPath}' on entity '{FormatType(entityType)}' has invalid conversion metadata in {sourceKind} '{FormatType(sourceType)}'. {reason}");
        }

        private static void ValidatePersistenceMetadata(Type entityType, IPropertyMap map, MemberPath memberPath, string sourceKind, Type sourceType)
        {
            var persistence = PropertyMapPersistence.GetPersistence(map);
            if (persistence == null)
            {
                throw InvalidPersistenceMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Persistence metadata cannot be null.");
            }

            if (map.Ignored != persistence.IgnoredByFluentMap)
            {
                throw InvalidPersistenceMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "The Ignored flag and persistence metadata disagree.");
            }

            if (persistence.IgnoredByFluentMap)
            {
                if (persistence.ParticipatesInMaterialization ||
                    persistence.ParticipatesInInsert ||
                    persistence.ParticipatesInUpdate ||
                    persistence.IsKey ||
                    persistence.IsIdentity ||
                    persistence.IsGenerated ||
                    persistence.IsComputed ||
                    persistence.HasDatabaseDefaultOnInsert)
                {
                    throw InvalidPersistenceMetadata(
                        entityType,
                        memberPath,
                        sourceKind,
                        sourceType,
                        "Ignored properties cannot participate in materialization, insert, update, key or generated persistence behavior.");
                }

                return;
            }

            if (!persistence.ParticipatesInMaterialization)
            {
                throw InvalidPersistenceMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Only ignored properties may opt out of FluentMap materialization.");
            }

            if (persistence.IsComputed)
            {
                if (!persistence.IsGenerated ||
                    persistence.ParticipatesInInsert ||
                    persistence.ParticipatesInUpdate ||
                    persistence.HasDatabaseDefaultOnInsert ||
                    persistence.IsKey ||
                    persistence.IsIdentity)
                {
                    throw InvalidPersistenceMetadata(
                        entityType,
                        memberPath,
                        sourceKind,
                        sourceType,
                        "Computed properties must be generated read-only values and cannot also be key, identity or database-default properties.");
                }
            }

            if (persistence.IsIdentity)
            {
                if (!persistence.IsGenerated ||
                    !persistence.IsKey ||
                    persistence.ParticipatesInInsert ||
                    persistence.ParticipatesInUpdate ||
                    persistence.HasDatabaseDefaultOnInsert)
                {
                    throw InvalidPersistenceMetadata(
                        entityType,
                        memberPath,
                        sourceKind,
                        sourceType,
                        "Identity properties must be generated keys and cannot participate in insert, update or database-default-on-insert behavior.");
                }
            }

            if (persistence.IsKey && persistence.ParticipatesInUpdate)
            {
                throw InvalidPersistenceMetadata(
                    entityType,
                    memberPath,
                    sourceKind,
                    sourceType,
                    "Key properties cannot participate in generated UPDATE SET behavior.");
            }

            if (persistence.HasDatabaseDefaultOnInsert)
            {
                if (!persistence.IsGenerated ||
                    persistence.ParticipatesInInsert ||
                    persistence.IsComputed ||
                    persistence.IsIdentity)
                {
                    throw InvalidPersistenceMetadata(
                        entityType,
                        memberPath,
                        sourceKind,
                        sourceType,
                        "Database-default-on-insert properties must be generated values omitted from insert and cannot also be computed or identity properties.");
                }
            }
        }

        private static FluentMapConfigurationException InvalidPersistenceMetadata(Type entityType, MemberPath memberPath, string sourceKind, Type sourceType, string reason)
        {
            return new FluentMapConfigurationException(
                $"Property path '{memberPath}' on entity '{FormatType(entityType)}' has invalid persistence metadata in {sourceKind} '{FormatType(sourceType)}'. {reason}");
        }

        private static void ValidateDuplicateMemberPaths(Type entityType, IList<MapDescriptor> maps, string sourceKind, Type sourceType)
        {
            for (var i = 0; i < maps.Count; i++)
            {
                for (var j = i + 1; j < maps.Count; j++)
                {
                    if (!maps[i].MemberPath.Equals(maps[j].MemberPath))
                    {
                        continue;
                    }

                    throw new FluentMapConfigurationException(
                        $"Property path '{maps[i].MemberPath}' is already mapped for entity '{FormatType(entityType)}' in {sourceKind} '{FormatType(sourceType)}'. Existing column: '{maps[i].Map.ColumnName}'; duplicate column: '{maps[j].Map.ColumnName}'.");
                }
            }
        }

        private static void ValidateColumnConflicts(Type entityType, IList<MapDescriptor> maps, string sourceKind, Type sourceType)
        {
            for (var i = 0; i < maps.Count; i++)
            {
                for (var j = i + 1; j < maps.Count; j++)
                {
                    if (!ShouldValidateColumnConflict(maps[i].Map, maps[j].Map))
                    {
                        continue;
                    }

                    if (!ColumnNamesOverlap(maps[i].Map, maps[j].Map))
                    {
                        continue;
                    }

                    var caseSensitivity = maps[i].Map.CaseSensitive == maps[j].Map.CaseSensitive
                        ? string.Empty
                        : " The mappings use different case sensitivity settings.";

                    throw new FluentMapConfigurationException(
                        $"Column '{maps[i].Map.ColumnName}' is configured for more than one property path on entity '{FormatType(entityType)}' in {sourceKind} '{FormatType(sourceType)}': '{maps[i].MemberPath}' and '{maps[j].MemberPath}'.{caseSensitivity}");
                }
            }
        }

        private static bool ColumnNamesOverlap(IPropertyMap left, IPropertyMap right)
        {
            if (string.Equals(left.ColumnName, right.ColumnName, StringComparison.Ordinal))
            {
                return true;
            }

            if (!left.CaseSensitive || !right.CaseSensitive)
            {
                return string.Equals(left.ColumnName, right.ColumnName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool ShouldValidateColumnConflict(IPropertyMap left, IPropertyMap right)
        {
            return left.GetType() == typeof(PropertyMap) &&
                   right.GetType() == typeof(PropertyMap);
        }

        private static void ValidateNestedMaterializationPaths(Type entityType, IList<MapDescriptor> maps, string sourceKind, Type sourceType)
        {
            var activeMaps = maps
                .Where(map => !map.Map.Ignored)
                .ToList();

            foreach (var map in activeMaps.Where(map => map.MemberPath.IsNested))
            {
                ValidateNestedMaterializationPath(entityType, map.MemberPath, sourceKind, sourceType);
            }

            for (var i = 0; i < activeMaps.Count; i++)
            {
                for (var j = i + 1; j < activeMaps.Count; j++)
                {
                    if (!IsPathPrefix(activeMaps[i].MemberPath, activeMaps[j].MemberPath) &&
                        !IsPathPrefix(activeMaps[j].MemberPath, activeMaps[i].MemberPath))
                    {
                        continue;
                    }

                    throw new FluentMapConfigurationException(
                        $"Property path '{activeMaps[i].MemberPath}' conflicts with property path '{activeMaps[j].MemberPath}' for entity '{FormatType(entityType)}' in {sourceKind} '{FormatType(sourceType)}'. Nested materialization cannot map both a path and one of its descendants.");
                }
            }
        }

        private static void ValidateNestedMaterializationPath(Type entityType, MemberPath memberPath, string sourceKind, Type sourceType)
        {
            var properties = memberPath.Properties;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                if (property.GetIndexParameters().Length != 0)
                {
                    throw UnsupportedNestedPath(entityType, memberPath, sourceKind, sourceType, $"Property '{property.Name}' is an indexer.");
                }

                if (IsStatic(property))
                {
                    throw UnsupportedNestedPath(entityType, memberPath, sourceKind, sourceType, $"Property '{property.Name}' is static.");
                }

                if (!CanRead(property))
                {
                    throw UnsupportedNestedPath(entityType, memberPath, sourceKind, sourceType, $"Property '{property.Name}' must have a public getter.");
                }

                if (i == properties.Count - 1)
                {
                    continue;
                }

                var propertyType = property.PropertyType;
                if (IsUnsupportedIntermediateType(propertyType))
                {
                    throw UnsupportedNestedPath(entityType, memberPath, sourceKind, sourceType, $"Intermediate property '{property.Name}' has unsupported type '{FormatType(propertyType)}'. Collections and scalar values cannot appear in the middle of a nested path.");
                }

            }
        }

        private static bool IsPathPrefix(MemberPath prefix, MemberPath path)
        {
            if (prefix.Properties.Count >= path.Properties.Count)
            {
                return false;
            }

            for (var i = 0; i < prefix.Properties.Count; i++)
            {
                if (!Equals(prefix.Properties[i], path.Properties[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanRead(PropertyInfo property)
        {
            var getter = property.GetGetMethod();
            return getter != null && !getter.IsStatic;
        }

        private static bool IsStatic(PropertyInfo property)
        {
            var getter = property.GetGetMethod();
            var setter = property.GetSetMethod();
            return (getter != null && getter.IsStatic) || (setter != null && setter.IsStatic);
        }

        private static bool IsUnsupportedIntermediateType(Type type)
        {
            if (!type.IsClass || type == typeof(string))
            {
                return true;
            }

            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static FluentMapConfigurationException UnsupportedNestedPath(Type entityType, MemberPath memberPath, string sourceKind, Type sourceType, string reason)
        {
            return new FluentMapConfigurationException(
                $"Property path '{memberPath}' is not supported for nested materialization on entity '{FormatType(entityType)}' in {sourceKind} '{FormatType(sourceType)}'. {reason}");
        }

        private static bool IsMapForEntity(Type entityType, IPropertyMap map)
        {
#if NETSTANDARD1_3
            return map.PropertyInfo.DeclaringType == entityType;
#else
            return map.PropertyInfo.ReflectedType == entityType;
#endif
        }

        private static bool IsMemberPathCompatible(Type entityType, MemberPath memberPath)
        {
            var declaringType = memberPath.Properties[0].DeclaringType;
            return declaringType != null && declaringType.IsAssignableFrom(entityType);
        }

        private static string FormatType(Type type)
        {
            return type == null ? "<unknown>" : type.FullName;
        }

        private sealed class MapDescriptor
        {
            internal MapDescriptor(IPropertyMap map, MemberPath memberPath)
            {
                Map = map;
                MemberPath = memberPath;
            }

            internal IPropertyMap Map { get; }

            internal MemberPath MemberPath { get; }
        }
    }
}
