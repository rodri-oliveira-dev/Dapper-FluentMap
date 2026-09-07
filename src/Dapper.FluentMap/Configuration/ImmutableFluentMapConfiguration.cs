using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;

namespace Dapper.FluentMap.Configuration
{
    /// <summary>
    /// Represents a read-only snapshot produced by <see cref="FluentMapConfigurationBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The snapshot is safe to share between threads. It does not expose the mutable map or convention
    /// collections used while the builder was being configured.
    /// </remarks>
    public sealed class ImmutableFluentMapConfiguration
    {
        private ImmutableFluentMapConfiguration(
            IReadOnlyDictionary<Type, EntityMappingConfiguration> entityMaps,
            IReadOnlyList<ProfileMappingConfiguration> profileMaps,
            IReadOnlyDictionary<Type, IReadOnlyList<ConventionMappingConfiguration>> typeConventions,
            IReadOnlyList<GeneratedMaterializerConfiguration> generatedMaterializers)
        {
            EntityMaps = entityMaps;
            ProfileMaps = profileMaps;
            TypeConventions = typeConventions;
            GeneratedMaterializers = generatedMaterializers;
        }

        /// <summary>
        /// Gets the configured default entity maps by entity type.
        /// </summary>
        public IReadOnlyDictionary<Type, EntityMappingConfiguration> EntityMaps { get; }

        /// <summary>
        /// Gets the configured mapping profiles.
        /// </summary>
        public IReadOnlyList<ProfileMappingConfiguration> ProfileMaps { get; }

        /// <summary>
        /// Gets the configured conventions and naming policies by entity type.
        /// </summary>
        public IReadOnlyDictionary<Type, IReadOnlyList<ConventionMappingConfiguration>> TypeConventions { get; }

        /// <summary>
        /// Gets the generated materializer registrations captured by this configuration.
        /// </summary>
        public IReadOnlyList<GeneratedMaterializerConfiguration> GeneratedMaterializers { get; }

        /// <summary>
        /// Creates a runtime that uses this immutable configuration and owns its derived caches.
        /// </summary>
        /// <returns>A FluentMap runtime bound to this configuration.</returns>
        public FluentMapRuntime CreateRuntime()
        {
            return new FluentMapRuntime(this);
        }

        internal static ImmutableFluentMapConfiguration Create(MappingRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var entityMaps = registry.EntityMaps
                .OrderBy(map => map.Key.FullName, StringComparer.Ordinal)
                .ToDictionary(
                    map => map.Key,
                    map => EntityMappingConfiguration.Create(map.Key, map.Value));

            var profileMaps = registry.ProfileMaps
                .OrderBy(map => map.Key.EntityType.FullName, StringComparer.Ordinal)
                .ThenBy(map => map.Key.ProfileType.FullName, StringComparer.Ordinal)
                .Select(map => ProfileMappingConfiguration.Create(map.Key.EntityType, map.Key.ProfileType, map.Value))
                .ToList();

            var conventions = registry.TypeConventions
                .OrderBy(map => map.Key.FullName, StringComparer.Ordinal)
                .ToDictionary(
                    map => map.Key,
                    map => (IReadOnlyList<ConventionMappingConfiguration>)new ReadOnlyCollection<ConventionMappingConfiguration>(
                        map.Value
                            .Select(convention => ConventionMappingConfiguration.Create(map.Key, convention))
                            .ToList()));

            var materializers = registry.GetGeneratedMaterializerSnapshots()
                .Select(GeneratedMaterializerConfiguration.Create)
                .ToList();

            return new ImmutableFluentMapConfiguration(
                new ReadOnlyDictionary<Type, EntityMappingConfiguration>(entityMaps),
                new ReadOnlyCollection<ProfileMappingConfiguration>(profileMaps),
                new ReadOnlyDictionary<Type, IReadOnlyList<ConventionMappingConfiguration>>(conventions),
                new ReadOnlyCollection<GeneratedMaterializerConfiguration>(materializers));
        }
    }

    /// <summary>
    /// Describes an entity map captured in an immutable FluentMap configuration.
    /// </summary>
    public sealed class EntityMappingConfiguration
    {
        private EntityMappingConfiguration(
            Type entityType,
            Type mapType,
            IReadOnlyList<PropertyMappingConfiguration> propertyMaps,
            IReadOnlyList<Type> includedBaseTypes)
        {
            EntityType = entityType;
            MapType = mapType;
            PropertyMaps = propertyMaps;
            IncludedBaseTypes = includedBaseTypes;
        }

        /// <summary>
        /// Gets the mapped entity type.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the concrete map type that produced this configuration.
        /// </summary>
        public Type MapType { get; }

        /// <summary>
        /// Gets the explicit property maps captured from the entity map.
        /// </summary>
        public IReadOnlyList<PropertyMappingConfiguration> PropertyMaps { get; }

        /// <summary>
        /// Gets the base entity types explicitly included by this map.
        /// </summary>
        public IReadOnlyList<Type> IncludedBaseTypes { get; }

        internal static EntityMappingConfiguration Create(Type entityType, IEntityMap map)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var includedBaseTypes = map as IEntityMapWithIncludedBaseTypes;
            return new EntityMappingConfiguration(
                entityType,
                map.GetType(),
                new ReadOnlyCollection<PropertyMappingConfiguration>(
                    map.PropertyMaps.Select(PropertyMappingConfiguration.Create).ToList()),
                new ReadOnlyCollection<Type>(
                    includedBaseTypes == null ? new List<Type>() : includedBaseTypes.IncludedBaseTypes.ToList()));
        }
    }

    /// <summary>
    /// Describes a profile map captured in an immutable FluentMap configuration.
    /// </summary>
    public sealed class ProfileMappingConfiguration
    {
        private ProfileMappingConfiguration(
            Type entityType,
            Type profileType,
            Type mapType,
            IReadOnlyList<PropertyMappingConfiguration> propertyMaps,
            IReadOnlyList<Type> includedBaseTypes)
        {
            EntityType = entityType;
            ProfileType = profileType;
            MapType = mapType;
            PropertyMaps = propertyMaps;
            IncludedBaseTypes = includedBaseTypes;
        }

        /// <summary>
        /// Gets the mapped entity type.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the selected mapping profile type.
        /// </summary>
        public Type ProfileType { get; }

        /// <summary>
        /// Gets the concrete map type that produced this profile configuration.
        /// </summary>
        public Type MapType { get; }

        /// <summary>
        /// Gets the explicit property maps captured from the profile map.
        /// </summary>
        public IReadOnlyList<PropertyMappingConfiguration> PropertyMaps { get; }

        /// <summary>
        /// Gets the base entity types explicitly included by this profile map.
        /// </summary>
        public IReadOnlyList<Type> IncludedBaseTypes { get; }

        internal static ProfileMappingConfiguration Create(Type entityType, Type profileType, IEntityMap map)
        {
            var entityMap = EntityMappingConfiguration.Create(entityType, map);
            return new ProfileMappingConfiguration(
                entityType,
                profileType,
                entityMap.MapType,
                entityMap.PropertyMaps,
                entityMap.IncludedBaseTypes);
        }
    }

    /// <summary>
    /// Describes a convention or naming policy captured in an immutable FluentMap configuration.
    /// </summary>
    public sealed class ConventionMappingConfiguration
    {
        private ConventionMappingConfiguration(
            Type entityType,
            Type conventionType,
            IReadOnlyList<PropertyMappingConfiguration> propertyMaps)
        {
            EntityType = entityType;
            ConventionType = conventionType;
            PropertyMaps = propertyMaps;
        }

        /// <summary>
        /// Gets the entity type to which the convention was applied.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the concrete convention type.
        /// </summary>
        public Type ConventionType { get; }

        /// <summary>
        /// Gets the property maps generated by the convention for the entity.
        /// </summary>
        public IReadOnlyList<PropertyMappingConfiguration> PropertyMaps { get; }

        internal static ConventionMappingConfiguration Create(Type entityType, Convention convention)
        {
            if (convention == null)
            {
                throw new ArgumentNullException(nameof(convention));
            }

            return new ConventionMappingConfiguration(
                entityType,
                convention.GetType(),
                new ReadOnlyCollection<PropertyMappingConfiguration>(
                    convention.PropertyMaps
                        .Where(map => IsMapForEntity(entityType, map))
                        .Select(PropertyMappingConfiguration.Create)
                        .ToList()));
        }

        private static bool IsMapForEntity(Type type, IPropertyMap map)
        {
#if NETSTANDARD1_3
            return map.PropertyInfo.DeclaringType == type;
#else
            return map.PropertyInfo.ReflectedType == type;
#endif
        }
    }

    /// <summary>
    /// Describes a property map captured in an immutable FluentMap configuration.
    /// </summary>
    public sealed class PropertyMappingConfiguration
    {
        private PropertyMappingConfiguration(
            string memberPath,
            IReadOnlyList<PropertyInfo> memberPathProperties,
            PropertyInfo propertyInfo,
            string columnName,
            bool caseSensitive,
            bool ignored,
            PropertyPersistenceMetadata persistence,
            PropertyConversionMetadata conversion)
        {
            MemberPath = memberPath;
            MemberPathProperties = memberPathProperties;
            PropertyInfo = propertyInfo;
            ColumnName = columnName;
            CaseSensitive = caseSensitive;
            Ignored = ignored;
            Persistence = persistence;
            Conversion = conversion;
        }

        /// <summary>
        /// Gets the mapped member path.
        /// </summary>
        public string MemberPath { get; }

        internal IReadOnlyList<PropertyInfo> MemberPathProperties { get; }

        /// <summary>
        /// Gets the terminal property metadata for the mapped member path.
        /// </summary>
        public PropertyInfo PropertyInfo { get; }

        /// <summary>
        /// Gets the configured column name.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets a value indicating whether column matching is case sensitive.
        /// </summary>
        public bool CaseSensitive { get; }

        /// <summary>
        /// Gets a value indicating whether FluentMap ignores this property.
        /// </summary>
        public bool Ignored { get; }

        /// <summary>
        /// Gets the persistence metadata captured for this property.
        /// </summary>
        public PropertyPersistenceMetadata Persistence { get; }

        /// <summary>
        /// Gets the conversion metadata captured for this property.
        /// </summary>
        public PropertyConversionMetadata Conversion { get; }

        internal static PropertyMappingConfiguration Create(IPropertyMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var memberPath = PropertyMapIdentity.GetMemberPath(map);
            return new PropertyMappingConfiguration(
                memberPath.ToString(),
                new ReadOnlyCollection<PropertyInfo>(memberPath.Properties.ToList()),
                map.PropertyInfo,
                map.ColumnName,
                map.CaseSensitive,
                map.Ignored,
                PropertyMapPersistence.GetPersistence(map),
                PropertyMapConversion.GetConversion(map));
        }
    }

    /// <summary>
    /// Describes a generated materializer captured in an immutable FluentMap configuration.
    /// </summary>
    public sealed class GeneratedMaterializerConfiguration
    {
        private GeneratedMaterializerConfiguration(
            Type entityType,
            Type profileType,
            IReadOnlyList<GeneratedMaterializerColumn> columns,
            Func<IDataRecord, object> materializer)
        {
            EntityType = entityType;
            ProfileType = profileType;
            Columns = columns;
            Materializer = materializer;
        }

        /// <summary>
        /// Gets the entity type produced by the generated materializer.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the mapping profile type, or <see langword="null"/> for the default map.
        /// </summary>
        public Type ProfileType { get; }

        /// <summary>
        /// Gets the ordered column shape expected by the generated materializer.
        /// </summary>
        public IReadOnlyList<GeneratedMaterializerColumn> Columns { get; }

        internal Func<IDataRecord, object> Materializer { get; }

        internal static GeneratedMaterializerConfiguration Create(GeneratedMaterializerRegistrationSnapshot snapshot)
        {
            return new GeneratedMaterializerConfiguration(
                snapshot.EntityType,
                snapshot.ProfileType,
                new ReadOnlyCollection<GeneratedMaterializerColumn>(snapshot.Columns.ToList()),
                snapshot.Materializer);
        }
    }

    internal sealed class GeneratedMaterializerRegistrationSnapshot
    {
        internal GeneratedMaterializerRegistrationSnapshot(
            Type entityType,
            Type profileType,
            IReadOnlyList<GeneratedMaterializerColumn> columns,
            Func<IDataRecord, object> materializer)
        {
            EntityType = entityType;
            ProfileType = profileType;
            Columns = columns;
            Materializer = materializer;
        }

        internal Type EntityType { get; }

        internal Type ProfileType { get; }

        internal IReadOnlyList<GeneratedMaterializerColumn> Columns { get; }

        internal Func<IDataRecord, object> Materializer { get; }
    }
}
