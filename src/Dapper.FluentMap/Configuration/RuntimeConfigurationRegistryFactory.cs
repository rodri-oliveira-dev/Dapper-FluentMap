using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Configuration
{
    internal static class RuntimeConfigurationRegistryFactory
    {
        internal static MappingRegistry Create(ImmutableFluentMapConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var registry = new MappingRegistry(installDapperTypeMaps: false);

            foreach (var map in configuration.EntityMaps.Values)
            {
                registry.EntityMaps.TryAdd(map.EntityType, SnapshotEntityMap.Create(map));
            }

            foreach (var profile in configuration.ProfileMaps)
            {
                registry.ProfileMaps.TryAdd(
                    new MappingProfileKey(profile.EntityType, profile.ProfileType),
                    SnapshotEntityMap.Create(profile));
            }

            foreach (var conventions in configuration.TypeConventions)
            {
                registry.TypeConventions.TryAdd(
                    conventions.Key,
                    conventions.Value
                        .Select(convention => (Convention)SnapshotConvention.Create(convention))
                        .ToList());
            }

            foreach (var materializer in configuration.GeneratedMaterializers)
            {
                registry.AddGeneratedMaterializer(
                    materializer.EntityType,
                    materializer.ProfileType,
                    materializer.Columns,
                    materializer.Materializer);
            }

            registry.ValidateConfiguration();
            return registry;
        }

        private sealed class SnapshotEntityMap :
            IEntityMap,
            IEntityMapWithIncludedBaseTypes,
            IRuntimeEntityMapMetadata
        {
            private SnapshotEntityMap(Type mapType, IList<IPropertyMap> propertyMaps, IList<Type> includedBaseTypes)
            {
                MapType = mapType;
                PropertyMaps = propertyMaps;
                IncludedBaseTypes = includedBaseTypes;
            }

            public IList<IPropertyMap> PropertyMaps { get; }

            public IList<Type> IncludedBaseTypes { get; }

            public Type MapType { get; }

            internal static SnapshotEntityMap Create(EntityMappingConfiguration map)
            {
                return new SnapshotEntityMap(
                    map.MapType,
                    map.PropertyMaps.Select(SnapshotPropertyMap.Create).Cast<IPropertyMap>().ToList(),
                    map.IncludedBaseTypes.ToList());
            }

            internal static SnapshotEntityMap Create(ProfileMappingConfiguration map)
            {
                return new SnapshotEntityMap(
                    map.MapType,
                    map.PropertyMaps.Select(SnapshotPropertyMap.Create).Cast<IPropertyMap>().ToList(),
                    map.IncludedBaseTypes.ToList());
            }
        }

        private sealed class SnapshotPropertyMap :
            IPropertyMap,
            IPropertyMapWithMemberPath,
            IPropertyMapWithPersistenceMetadata,
            IPropertyMapWithConversionMetadata
        {
            private SnapshotPropertyMap(PropertyMappingConfiguration propertyMap)
            {
                ColumnName = propertyMap.ColumnName;
                PropertyInfo = propertyMap.PropertyInfo;
                CaseSensitive = propertyMap.CaseSensitive;
                Ignored = propertyMap.Ignored;
                Persistence = propertyMap.Persistence;
                Conversion = propertyMap.Conversion;
                MemberPath = MemberPath.FromProperties(propertyMap.MemberPathProperties);
            }

            public string ColumnName { get; }

            public System.Reflection.PropertyInfo PropertyInfo { get; }

            public bool CaseSensitive { get; }

            public bool Ignored { get; }

            public PropertyPersistenceMetadata Persistence { get; }

            public PropertyConversionMetadata Conversion { get; }

            public MemberPath MemberPath { get; private set; }

            void IPropertyMapWithMemberPath.SetMemberPath(MemberPath memberPath)
            {
                MemberPath = memberPath;
            }

            internal static SnapshotPropertyMap Create(PropertyMappingConfiguration propertyMap)
            {
                return new SnapshotPropertyMap(propertyMap);
            }
        }

        private sealed class SnapshotConvention :
            Convention,
            IRuntimeConventionMetadata
        {
            private SnapshotConvention(ConventionMappingConfiguration convention)
            {
                ConventionType = convention.ConventionType;

                foreach (var propertyMap in convention.PropertyMaps)
                {
                    var snapshotMap = new PropertyMap(
                        propertyMap.PropertyInfo,
                        propertyMap.ColumnName,
                        propertyMap.CaseSensitive);

                    PropertyMapIdentity.SetMemberPath(
                        snapshotMap,
                        MemberPath.FromProperties(propertyMap.MemberPathProperties));

                    if (propertyMap.Ignored)
                    {
                        snapshotMap.Ignore();
                    }

                    PropertyMaps.Add(snapshotMap);
                }
            }

            public Type ConventionType { get; }

            internal static SnapshotConvention Create(ConventionMappingConfiguration convention)
            {
                return new SnapshotConvention(convention);
            }
        }
    }

    internal interface IRuntimeEntityMapMetadata
    {
        Type MapType { get; }
    }

    internal interface IRuntimeConventionMetadata
    {
        Type ConventionType { get; }
    }
}
