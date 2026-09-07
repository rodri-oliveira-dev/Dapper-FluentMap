using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.TypeMaps;

namespace Dapper.FluentMap
{
    internal sealed class MappingRegistry
    {
        private readonly bool _installDapperTypeMaps;

        private readonly ConcurrentDictionary<MappingCacheKey, MappingCacheEntry> _propertyMapCache =
            new ConcurrentDictionary<MappingCacheKey, MappingCacheEntry>();

        private readonly ConcurrentDictionary<MaterializationPlanCacheKey, NestedMaterializationPlan> _materializationPlanCache =
            new ConcurrentDictionary<MaterializationPlanCacheKey, NestedMaterializationPlan>();

        private readonly ConcurrentDictionary<MaterializationPlanCacheKey, GeneratedMaterializerEntry> _generatedMaterializers =
            new ConcurrentDictionary<MaterializationPlanCacheKey, GeneratedMaterializerEntry>();

        internal ConcurrentDictionary<Type, IEntityMap> EntityMaps { get; } =
            new ConcurrentDictionary<Type, IEntityMap>();

        internal ConcurrentDictionary<MappingProfileKey, IEntityMap> ProfileMaps { get; } =
            new ConcurrentDictionary<MappingProfileKey, IEntityMap>();

        internal ConcurrentDictionary<Type, IList<Convention>> TypeConventions { get; } =
            new ConcurrentDictionary<Type, IList<Convention>>();

        internal MappingRegistry(bool installDapperTypeMaps = true)
        {
            _installDapperTypeMaps = installDapperTypeMaps;
        }

        internal int CacheEntryCount => _propertyMapCache.Count;

        internal int MaterializationPlanCacheEntryCount => _materializationPlanCache.Count;

        internal int GeneratedMaterializerCount => _generatedMaterializers.Count;

        internal IReadOnlyDictionary<Type, IEntityMap> GetEntityMapsSnapshot()
        {
            var snapshot = EntityMaps
                .OrderBy(map => map.Key.FullName, StringComparer.Ordinal)
                .ToDictionary(map => map.Key, map => map.Value);

            return new ReadOnlyDictionary<Type, IEntityMap>(snapshot);
        }

        internal IReadOnlyDictionary<Type, IReadOnlyList<Convention>> GetTypeConventionsSnapshot()
        {
            var snapshot = TypeConventions
                .OrderBy(conventions => conventions.Key.FullName, StringComparer.Ordinal)
                .ToDictionary(
                    conventions => conventions.Key,
                    conventions => (IReadOnlyList<Convention>)new ReadOnlyCollection<Convention>(conventions.Value.ToList()));

            return new ReadOnlyDictionary<Type, IReadOnlyList<Convention>>(snapshot);
        }

        internal IReadOnlyList<GeneratedMaterializerRegistrationSnapshot> GetGeneratedMaterializerSnapshots()
        {
            return _generatedMaterializers
                .OrderBy(materializer => materializer.Key.Type.FullName, StringComparer.Ordinal)
                .ThenBy(materializer => materializer.Key.ProfileType == null ? string.Empty : materializer.Key.ProfileType.FullName, StringComparer.Ordinal)
                .ThenBy(materializer => string.Join("|", materializer.Key.ColumnNames), StringComparer.Ordinal)
                .Select(materializer => new GeneratedMaterializerRegistrationSnapshot(
                    materializer.Key.Type,
                    materializer.Key.ProfileType,
                    materializer.Value.Columns,
                    materializer.Value.Materialize))
                .ToList();
        }

        internal void AddEntityMap<TEntity>(IEntityMap<TEntity> mapper)
            where TEntity : class
        {
            AddEntityMap(typeof(TEntity), mapper);
        }

        internal void AddEntityMap(Type type, IEntityMap mapper)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            if (EntityMaps.ContainsKey(type))
            {
                throw new FluentMapConfigurationException($"Entity '{type}' already has a configured entity map. Current entity maps: " + string.Join(", ", EntityMaps.Select(e => e.Key.ToString())));
            }

            MappingConfigurationValidator.ValidateEntityMap(type, mapper);
            ValidateIncludedBaseMaps(type, mapper, profileType: null);
            MappingConfigurationValidator.ValidateComposedEntityMap(type, mapper, ComposeExplicitPropertyMaps(type, mapper, profileType: null));

            if (!EntityMaps.TryAdd(type, mapper))
            {
                throw new FluentMapConfigurationException($"Entity '{type}' already has a configured entity map. Current entity maps: " + string.Join(", ", EntityMaps.Select(e => e.Key.ToString())));
            }

            InvalidateType(type);
            SetDapperTypeMap(type);
        }

        internal void AddProfileMap(Type type, Type profileType, IEntityMap mapper)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (profileType == null)
            {
                throw new ArgumentNullException(nameof(profileType));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            var key = new MappingProfileKey(type, profileType);
            if (ProfileMaps.ContainsKey(key))
            {
                throw new FluentMapConfigurationException(
                    $"Entity '{type}' already has a configured mapping profile '{profileType}'.");
            }

            MappingConfigurationValidator.ValidateEntityMap(type, mapper);
            ValidateIncludedBaseMaps(type, mapper, profileType);
            MappingConfigurationValidator.ValidateComposedEntityMap(
                type,
                mapper,
                ComposeExplicitPropertyMaps(type, mapper, profileType));

            if (!ProfileMaps.TryAdd(key, mapper))
            {
                throw new FluentMapConfigurationException(
                    $"Entity '{type}' already has a configured mapping profile '{profileType}'.");
            }

            InvalidateType(type);
        }

        internal void AddConvention(Type type, Convention convention)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (convention == null)
            {
                throw new ArgumentNullException(nameof(convention));
            }

            MappingConfigurationValidator.ValidateConvention(type, convention);

            TypeConventions.AddOrUpdate(
                type,
                _ => new List<Convention> { convention },
                (_, current) =>
                {
                    var updated = current.ToList();
                    updated.Add(convention);
                    return updated;
                });

            InvalidateType(type);
            SetDapperTypeMap(type);
        }

        internal void ResetDapperTypeMap<TEntity>()
        {
            SetDapperTypeMap(typeof(TEntity));
        }

        internal void ResetDapperTypeMap(Type type)
        {
            SetDapperTypeMap(type);
        }

        internal PropertyInfo GetFluentPropertyInfo(Type type, string columnName)
        {
            var cacheKey = MappingCacheKey.FluentMap(type, columnName);
            return _propertyMapCache
                .GetOrAdd(cacheKey, _ => new MappingCacheEntry(ResolveFluentPropertyMap(type, columnName)))
                .PropertyInfo;
        }

        internal PropertyInfo GetConventionPropertyInfo(Type type, string columnName)
        {
            var cacheKey = MappingCacheKey.ConventionOnly(type, columnName);
            return _propertyMapCache
                .GetOrAdd(cacheKey, _ => new MappingCacheEntry(ResolveConventionPropertyMap(type, columnName)))
                .PropertyInfo;
        }

        internal IPropertyMap GetFluentPropertyMap(Type type, string columnName)
        {
            var cacheKey = MappingCacheKey.FluentMap(type, columnName);
            return _propertyMapCache
                .GetOrAdd(cacheKey, _ => new MappingCacheEntry(ResolveFluentPropertyMap(type, columnName)))
                .PropertyMap;
        }

        internal IPropertyMap GetProfilePropertyMap(Type type, Type profileType, string columnName)
        {
            if (profileType == null)
            {
                return GetFluentPropertyMap(type, columnName);
            }

            var cacheKey = MappingCacheKey.ProfileMap(type, profileType, columnName);
            return _propertyMapCache
                .GetOrAdd(cacheKey, _ => new MappingCacheEntry(ResolveProfilePropertyMap(type, profileType, columnName)))
                .PropertyMap;
        }

        internal IPropertyMap GetConventionPropertyMap(Type type, string columnName)
        {
            var cacheKey = MappingCacheKey.ConventionOnly(type, columnName);
            return _propertyMapCache
                .GetOrAdd(cacheKey, _ => new MappingCacheEntry(ResolveConventionPropertyMap(type, columnName)))
                .PropertyMap;
        }

        internal NestedMaterializationPlan GetMaterializationPlan(Type type, string[] columnNames)
        {
            return GetMaterializationPlan(type, null, columnNames);
        }

        internal NestedMaterializationPlan GetMaterializationPlan(Type type, Type profileType, string[] columnNames)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (columnNames == null)
            {
                throw new ArgumentNullException(nameof(columnNames));
            }

            EnsureProfileRegistered(type, profileType);

            var cacheKey = new MaterializationPlanCacheKey(type, profileType, columnNames);
            return _materializationPlanCache.GetOrAdd(
                cacheKey,
                key => NestedMaterializationPlan.Create(key.Type, key.ProfileType, key.ColumnNames, this));
        }

        internal void AddGeneratedMaterializer<TEntity>(GeneratedMaterializerDescriptor<TEntity> descriptor)
            where TEntity : class
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var key = new MaterializationPlanCacheKey(
                descriptor.EntityType,
                descriptor.ProfileType,
                descriptor.Columns.Select(column => column.ColumnName));
            var entry = GeneratedMaterializerEntry.Create(descriptor);

            if (!_generatedMaterializers.TryAdd(key, entry))
            {
                var profileContext = descriptor.ProfileType == null
                    ? string.Empty
                    : $" and profile '{descriptor.ProfileType.FullName}'";

                throw new FluentMapConfigurationException(
                    $"Entity '{descriptor.EntityType.FullName}' already has a generated materializer registered for the same column shape{profileContext}.");
            }
        }

        internal void AddGeneratedMaterializer(
            Type entityType,
            Type profileType,
            IReadOnlyList<GeneratedMaterializerColumn> columns,
            Func<IDataRecord, object> materializer)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            if (materializer == null)
            {
                throw new ArgumentNullException(nameof(materializer));
            }

            var key = new MaterializationPlanCacheKey(
                entityType,
                profileType,
                columns.Select(column => column.ColumnName));
            var entry = GeneratedMaterializerEntry.Create(columns, materializer);

            if (!_generatedMaterializers.TryAdd(key, entry))
            {
                var profileContext = profileType == null
                    ? string.Empty
                    : $" and profile '{profileType.FullName}'";

                throw new FluentMapConfigurationException(
                    $"Entity '{entityType.FullName}' already has a generated materializer registered for the same column shape{profileContext}.");
            }
        }

        internal bool TryGetGeneratedMaterializer(
            Type type,
            Type profileType,
            string[] columnNames,
            out Func<IDataRecord, object> materializer)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (columnNames == null)
            {
                throw new ArgumentNullException(nameof(columnNames));
            }

            EnsureProfileRegistered(type, profileType);

            var cacheKey = new MaterializationPlanCacheKey(type, profileType, columnNames);
            GeneratedMaterializerEntry entry;
            if (!_generatedMaterializers.TryGetValue(cacheKey, out entry) ||
                !GeneratedMaterializerMatchesEffectiveMapping(type, profileType, entry.Columns))
            {
                materializer = null;
                return false;
            }

            materializer = entry.Materialize;
            return true;
        }

        internal void ValidateConfiguration()
        {
            var errors = new List<string>();

            foreach (var entityMap in EntityMaps.OrderBy(e => e.Key.FullName))
            {
                try
                {
                    MappingConfigurationValidator.ValidateEntityMap(entityMap.Key, entityMap.Value);
                    ValidateIncludedBaseMaps(entityMap.Key, entityMap.Value, profileType: null);
                    MappingConfigurationValidator.ValidateComposedEntityMap(
                        entityMap.Key,
                        entityMap.Value,
                        ComposeExplicitPropertyMaps(entityMap.Key, entityMap.Value, profileType: null));
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            foreach (var profileMap in ProfileMaps.OrderBy(p => p.Key.EntityType.FullName).ThenBy(p => p.Key.ProfileType.FullName))
            {
                try
                {
                    MappingConfigurationValidator.ValidateEntityMap(profileMap.Key.EntityType, profileMap.Value);
                    ValidateIncludedBaseMaps(profileMap.Key.EntityType, profileMap.Value, profileMap.Key.ProfileType);
                    MappingConfigurationValidator.ValidateComposedEntityMap(
                        profileMap.Key.EntityType,
                        profileMap.Value,
                        ComposeExplicitPropertyMaps(profileMap.Key.EntityType, profileMap.Value, profileMap.Key.ProfileType));
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
            }

            foreach (var typeConventions in TypeConventions.OrderBy(c => c.Key.FullName))
            {
                foreach (var convention in typeConventions.Value)
                {
                    try
                    {
                        MappingConfigurationValidator.ValidateConvention(typeConventions.Key, convention);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception.Message);
                    }
                }
            }

            if (errors.Count == 0)
            {
                return;
            }

            var message = new StringBuilder()
                .Append("Dapper.FluentMap configuration validation found ")
                .Append(errors.Count)
                .Append(errors.Count == 1 ? " error:" : " errors:");

            foreach (var error in errors)
            {
                message.AppendLine().Append("- ").Append(error);
            }

            throw new FluentMapConfigurationException(message.ToString());
        }

        internal MappingExplanation Explain(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
            Type type)
        {
            return Explain(type, profileType: null);
        }

        internal MappingExplanation Explain(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
            Type type,
            Type profileType)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var diagnostics = new List<string>();
            var members = new List<MemberMappingExplanation>();
            var configuredPaths = new List<MemberPath>();
            var entityMapType = default(Type);

            IEntityMap entityMap;
            var hasEntityMap = profileType == null
                ? EntityMaps.TryGetValue(type, out entityMap)
                : ProfileMaps.TryGetValue(new MappingProfileKey(type, profileType), out entityMap);

            if (hasEntityMap)
            {
                entityMapType = GetEntityMapType(entityMap);

                foreach (var descriptor in ComposeExplicitPropertyMapDescriptors(type, entityMap, profileType))
                {
                    AddMemberExplanation(type, members, configuredPaths, descriptor);
                }
            }

            var conventionTypes = GetConventionTypes(type).ToList();
            foreach (var descriptor in GetConventionPropertyMapDescriptors(type, configuredPaths))
            {
                AddMemberExplanation(type, members, configuredPaths, descriptor);
            }

            AddDapperDefaultExplanations(type, members, configuredPaths);

            if (entityMapType == null && profileType != null)
            {
                diagnostics.Add($"No FluentMap mapping profile '{profileType.FullName}' is registered for this entity. Dapper default mapping is used.");
            }
            else if (entityMapType == null && conventionTypes.Count == 0)
            {
                diagnostics.Add("No FluentMap entity map or convention is registered for this entity. Dapper default mapping is used.");
            }

            var generatedMaterializerCount = CountGeneratedMaterializers(type, profileType);
            if (generatedMaterializerCount > 0)
            {
                diagnostics.Add(
                    generatedMaterializerCount == 1
                        ? "One generated QueryMapped materializer descriptor is registered for this entity/profile. QueryMapped selects it only when the reader column order and effective mapping still match; otherwise it uses the runtime materializer fallback."
                        : generatedMaterializerCount + " generated QueryMapped materializer descriptors are registered for this entity/profile. QueryMapped selects one only when the reader column order and effective mapping still match; otherwise it uses the runtime materializer fallback.");
            }

            return new MappingExplanation(
                type,
                profileType,
                entityMapType,
                conventionTypes,
                members.OrderBy(m => m.MemberPath, StringComparer.Ordinal).ThenBy(m => m.ColumnName, StringComparer.Ordinal),
                diagnostics);
        }

        private int CountGeneratedMaterializers(Type type, Type profileType)
        {
            return _generatedMaterializers.Keys.Count(key => key.Type == type && key.ProfileType == profileType);
        }

        internal void Reset(params Type[] dapperTypes)
        {
            EntityMaps.Clear();
            ProfileMaps.Clear();
            TypeConventions.Clear();
            _propertyMapCache.Clear();
            _materializationPlanCache.Clear();
            _generatedMaterializers.Clear();

            if (dapperTypes == null)
            {
                return;
            }

            foreach (var type in dapperTypes)
            {
                SqlMapper.SetTypeMap(type, null);
            }
        }

        private void SetDapperTypeMap(Type type)
        {
            if (!_installDapperTypeMaps)
            {
                return;
            }

            var instance = new FluentMapTypeMap(type);
            SqlMapper.SetTypeMap(type, instance);
        }

        private void EnsureProfileRegistered(Type type, Type profileType)
        {
            if (profileType != null && !ProfileMaps.ContainsKey(new MappingProfileKey(type, profileType)))
            {
                throw new FluentMapConfigurationException(
                    $"Entity '{type.FullName}' does not have a registered mapping profile '{profileType.FullName}'.");
            }
        }

        private bool GeneratedMaterializerMatchesEffectiveMapping(
            Type type,
            Type profileType,
            IReadOnlyList<GeneratedMaterializerColumn> columns)
        {
            var defaultTypeMap = new DefaultTypeMap(type);

            foreach (var column in columns)
            {
                var fluentMap = GetProfilePropertyMap(type, profileType, column.ColumnName);
                if (fluentMap != null)
                {
                    if (column.Ignored)
                    {
                        if (!fluentMap.Ignored)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (fluentMap.Ignored)
                    {
                        return false;
                    }

                    if (!GeneratedReadConverterMatchesEffectiveMapping(
                        PropertyMapConversion.GetConversion(fluentMap),
                        column))
                    {
                        return false;
                    }

                    var memberPath = PropertyMapIdentity.GetMemberPath(fluentMap).ToString();
                    if (!string.Equals(memberPath, column.MemberPath, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    continue;
                }

                if (column.Ignored)
                {
                    return false;
                }

                var defaultMember = defaultTypeMap.GetMember(column.ColumnName);
                var defaultMemberPath = defaultMember == null
                    ? null
                    : defaultMember.Property != null
                        ? defaultMember.Property.Name
                        : defaultMember.Field != null
                            ? defaultMember.Field.Name
                            : null;

                if (!string.Equals(defaultMemberPath, column.MemberPath, StringComparison.Ordinal))
                {
                    return false;
                }

                if (column.ReadConverterType != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool GeneratedReadConverterMatchesEffectiveMapping(
            PropertyConversionMetadata conversion,
            GeneratedMaterializerColumn column)
        {
            if (conversion == null || !conversion.HasReadConverter)
            {
                return column.ReadConverterType == null &&
                       column.ReadConverterDatabaseType == null &&
                       column.ReadConverterPropertyType == null;
            }

            var readConverter = conversion.ReadConverter;
            return column.ReadConverterType == readConverter.ConverterType &&
                   column.ReadConverterDatabaseType == readConverter.DatabaseType &&
                   column.ReadConverterPropertyType == readConverter.PropertyType;
        }

        private void InvalidateType(Type type)
        {
            foreach (var key in _propertyMapCache.Keys.Where(k => k.Type == type))
            {
                _propertyMapCache.TryRemove(key, out _);
            }

            foreach (var key in _materializationPlanCache.Keys.Where(k => k.Type == type))
            {
                _materializationPlanCache.TryRemove(key, out _);
            }
        }

        private IPropertyMap ResolveFluentPropertyMap(Type type, string columnName)
        {
            var explicitPropertyMaps = GetExplicitPropertyMaps(type, profileType: null);
            var explicitPropertyMap = explicitPropertyMaps.FirstOrDefault(m => MatchColumnNames(m, columnName));

            if (explicitPropertyMap != null)
            {
                return explicitPropertyMap;
            }

            return ResolveConventionPropertyMap(type, columnName, explicitPropertyMaps);
        }

        private IPropertyMap ResolveProfilePropertyMap(Type type, Type profileType, string columnName)
        {
            var explicitPropertyMaps = GetExplicitPropertyMaps(type, profileType);
            var explicitPropertyMap = explicitPropertyMaps.FirstOrDefault(m => MatchColumnNames(m, columnName));

            if (explicitPropertyMap != null)
            {
                return explicitPropertyMap;
            }

            return ResolveConventionPropertyMap(type, columnName, explicitPropertyMaps);
        }

        private IList<IPropertyMap> GetExplicitPropertyMaps(Type type, Type profileType)
        {
            if (profileType == null)
            {
                if (EntityMaps.TryGetValue(type, out var entityMap))
                {
                    return ComposeExplicitPropertyMaps(type, entityMap, profileType: null);
                }

                return new IPropertyMap[0];
            }

            if (ProfileMaps.TryGetValue(new MappingProfileKey(type, profileType), out var profileMap))
            {
                return ComposeExplicitPropertyMaps(type, profileMap, profileType);
            }

            return new IPropertyMap[0];
        }

        private IEnumerable<Type> GetConventionTypes(Type type)
        {
            if (!TypeConventions.TryGetValue(type, out var conventions))
            {
                return new Type[0];
            }

            return conventions.Select(GetConventionType).ToList();
        }

        private void ValidateIncludedBaseMaps(Type type, IEntityMap entityMap, Type profileType)
        {
            foreach (var baseType in GetIncludedBaseTypes(entityMap))
            {
                if (baseType == type || !baseType.IsClass || !baseType.IsAssignableFrom(type))
                {
                    throw new FluentMapConfigurationException(
                        $"Type '{baseType.FullName}' cannot be included as a base mapping for entity '{type.FullName}'. The included type must be a base class of the entity.");
                }

                var hasBaseMap = profileType == null
                    ? EntityMaps.ContainsKey(baseType)
                    : ProfileMaps.ContainsKey(new MappingProfileKey(baseType, profileType));

                if (!hasBaseMap)
                {
                    var profileContext = profileType == null
                        ? string.Empty
                        : $" for mapping profile '{profileType.FullName}'";

                    throw new FluentMapConfigurationException(
                        $"Entity '{type.FullName}' includes base mapping '{baseType.FullName}'{profileContext}, but no entity map has been registered for the base type. Register the base map before the derived map.");
                }
            }
        }

        private IList<IPropertyMap> ComposeExplicitPropertyMaps(Type type, IEntityMap entityMap, Type profileType)
        {
            return ComposeExplicitPropertyMapDescriptors(type, entityMap, profileType)
                .Select(d => d.Map)
                .ToList();
        }

        private IList<MappingDiagnosticDescriptor> ComposeExplicitPropertyMapDescriptors(Type type, IEntityMap entityMap, Type profileType)
        {
            var propertyMaps = new List<MappingDiagnosticDescriptor>();
            AddPropertyMapsWithOverride(
                propertyMaps,
                entityMap.PropertyMaps.Select(m => MappingDiagnosticDescriptor.Explicit(m)));

            foreach (var baseType in GetIncludedBaseTypes(entityMap))
            {
                IEntityMap baseMap;
                var hasBaseMap = profileType == null
                    ? EntityMaps.TryGetValue(baseType, out baseMap)
                    : ProfileMaps.TryGetValue(new MappingProfileKey(baseType, profileType), out baseMap);

                if (!hasBaseMap)
                {
                    var profileContext = profileType == null
                        ? string.Empty
                        : $" for mapping profile '{profileType.FullName}'";

                    throw new FluentMapConfigurationException(
                        $"Entity '{type.FullName}' includes base mapping '{baseType.FullName}'{profileContext}, but no entity map has been registered for the base type. Register the base map before the derived map.");
                }

                AddPropertyMapsWithOverride(
                    propertyMaps,
                    ComposeExplicitPropertyMapDescriptors(baseType, baseMap, profileType)
                        .Select(d => d.AsInheritedFrom(baseType)));
            }

            return propertyMaps;
        }

        private static void AddPropertyMapsWithOverride(IList<MappingDiagnosticDescriptor> target, IEnumerable<MappingDiagnosticDescriptor> maps)
        {
            foreach (var descriptor in maps)
            {
                var memberPath = PropertyMapIdentity.GetMemberPath(descriptor.Map);
                if (target.Any(existingMap => PropertyMapIdentity.GetMemberPath(existingMap.Map).Equals(memberPath)))
                {
                    continue;
                }

                target.Add(descriptor);
            }
        }

        private static IList<Type> GetIncludedBaseTypes(IEntityMap entityMap)
        {
            var mapWithIncludedBases = entityMap as IEntityMapWithIncludedBaseTypes;
            if (mapWithIncludedBases == null)
            {
                return new Type[0];
            }

            return mapWithIncludedBases.IncludedBaseTypes;
        }

        private static Type GetEntityMapType(IEntityMap entityMap)
        {
            var runtimeMetadata = entityMap as IRuntimeEntityMapMetadata;
            return runtimeMetadata == null ? entityMap.GetType() : runtimeMetadata.MapType;
        }

        private static Type GetConventionType(Convention convention)
        {
            var runtimeMetadata = convention as IRuntimeConventionMetadata;
            return runtimeMetadata == null ? convention.GetType() : runtimeMetadata.ConventionType;
        }

        private static bool IsNamingPolicyConvention(Convention convention)
        {
            if (convention is NamingPolicyConvention)
            {
                return true;
            }

            var runtimeMetadata = convention as IRuntimeConventionMetadata;
            return runtimeMetadata != null && runtimeMetadata.ConventionType == typeof(NamingPolicyConvention);
        }

        private IEnumerable<MappingDiagnosticDescriptor> GetConventionPropertyMapDescriptors(Type type, IList<MemberPath> configuredPaths)
        {
            if (!TypeConventions.TryGetValue(type, out var conventions))
            {
                yield break;
            }

            foreach (var convention in conventions)
            {
                foreach (var map in convention.PropertyMaps)
                {
                    if (!IsMapForEntity(type, map))
                    {
                        continue;
                    }

                    var memberPath = PropertyMapIdentity.GetMemberPath(map);
                    if (configuredPaths.Any(path => path.Equals(memberPath)))
                    {
                        continue;
                    }

                    yield return MappingDiagnosticDescriptor.Convention(map, convention);
                }
            }
        }

        private void AddDapperDefaultExplanations(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
            Type type,
            IList<MemberMappingExplanation> members,
            IList<MemberPath> configuredPaths)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                         .Where(p => p.GetIndexParameters().Length == 0))
            {
                var memberPath = MemberPath.ForProperty(property);
                if (configuredPaths.Any(path => path.Equals(memberPath)))
                {
                    continue;
                }

                var constructorParameters = GetConstructorParameters(type, property).ToList();
                members.Add(new MemberMappingExplanation(
                    memberPath.ToString(),
                    property,
                    property.Name,
                    MappingSource.DapperDefault,
                    caseSensitive: false,
                    ignored: false,
                    inheritedFrom: null,
                    conventionType: null,
                    constructorParameters: constructorParameters,
                    materialization: MappingMaterialization.Dapper,
                    persistence: PropertyPersistenceMetadata.Default,
                    conversion: PropertyConversionMetadata.Default));
                configuredPaths.Add(memberPath);
            }
        }

        private void AddMemberExplanation(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type entityType,
            IList<MemberMappingExplanation> members,
            IList<MemberPath> configuredPaths,
            MappingDiagnosticDescriptor descriptor)
        {
            var memberPath = PropertyMapIdentity.GetMemberPath(descriptor.Map);
            var constructorParameters = descriptor.Map.Ignored || memberPath.IsNested
                ? new ConstructorParameterExplanation[0]
                : GetConstructorParameters(entityType, descriptor.Map.PropertyInfo);
            var materialization = GetMaterialization(memberPath);

            members.Add(new MemberMappingExplanation(
                memberPath.ToString(),
                descriptor.Map.PropertyInfo,
                descriptor.Map.ColumnName,
                descriptor.Source,
                descriptor.Map.CaseSensitive,
                descriptor.Map.Ignored,
                descriptor.InheritedFrom,
                descriptor.ConventionType,
                constructorParameters,
                materialization,
                PropertyMapPersistence.GetPersistence(descriptor.Map),
                PropertyMapConversion.GetConversion(descriptor.Map)));
            configuredPaths.Add(memberPath);
        }

        private static MappingMaterialization GetMaterialization(MemberPath memberPath)
        {
            if (!memberPath.IsNested)
            {
                return MappingMaterialization.Dapper;
            }

            return RequiresConstructorMaterialization(memberPath)
                ? MappingMaterialization.ValueObject
                : MappingMaterialization.Nested;
        }

        private static bool RequiresConstructorMaterialization(MemberPath memberPath)
        {
            var properties = memberPath.Properties;
            for (var i = 0; i < properties.Count; i++)
            {
                if (!CanWrite(properties[i]))
                {
                    return true;
                }

            }

            return false;
        }

        private static bool CanWrite(PropertyInfo property)
        {
            var setter = property.GetSetMethod();
            return setter != null && !setter.IsStatic;
        }

        private static IEnumerable<ConstructorParameterExplanation> GetConstructorParameters(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type entityType,
            PropertyInfo property)
        {
            foreach (var constructor in entityType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameter = FluentConstructorTypeMap.MatchParameter(constructor.GetParameters(), property.Name);
                if (parameter != null)
                {
                    yield return new ConstructorParameterExplanation(constructor, parameter);
                }
            }
        }

        private IPropertyMap ResolveConventionPropertyMap(Type type, string columnName)
        {
            return ResolveConventionPropertyMap(type, columnName, new IPropertyMap[0]);
        }

        private IPropertyMap ResolveConventionPropertyMap(Type type, string columnName, IList<IPropertyMap> explicitPropertyMaps)
        {
            if (!TypeConventions.TryGetValue(type, out var conventions))
            {
                return null;
            }

            foreach (var convention in conventions)
            {
                var maps = convention.PropertyMaps
#if NETSTANDARD1_3
                                     .Where(map => map.PropertyInfo.DeclaringType == type &&
                                                   !IsExplicitlyMapped(map, explicitPropertyMaps) &&
                                                   MatchColumnNames(map, columnName))
#else
                                     .Where(map => map.PropertyInfo.ReflectedType == type &&
                                                   !IsExplicitlyMapped(map, explicitPropertyMaps) &&
                                                   MatchColumnNames(map, columnName))
#endif
                                     .ToList();

                if (maps.Count > 1)
                {
                    const string msg = "Column '{0}' matched more than one convention property map for entity '{1}' in convention '{2}'. The convention should be more specific.";
                    throw new FluentMapConfigurationException(string.Format(msg, columnName, type, convention.GetType()));
                }

                if (maps.Count == 0)
                {
                    continue;
                }

                return maps[0];
            }

            return null;
        }

        private static bool IsExplicitlyMapped(IPropertyMap conventionMap, IList<IPropertyMap> explicitPropertyMaps)
        {
            var conventionPath = PropertyMapIdentity.GetMemberPath(conventionMap);
            return explicitPropertyMaps.Any(map => PropertyMapIdentity.GetMemberPath(map).Equals(conventionPath));
        }

        private static bool IsMapForEntity(Type type, IPropertyMap map)
        {
#if NETSTANDARD1_3
            return map.PropertyInfo.DeclaringType == type;
#else
            return map.PropertyInfo.ReflectedType == type;
#endif
        }

        private static bool MatchColumnNames(IPropertyMap map, string columnName)
        {
            var comparison = map.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return string.Equals(map.ColumnName, columnName, comparison);
        }

        private sealed class MappingCacheEntry
        {
            internal MappingCacheEntry(IPropertyMap propertyMap)
            {
                PropertyMap = propertyMap;

                if (propertyMap == null)
                {
                    return;
                }

                if (!propertyMap.Ignored)
                {
                    var memberPath = PropertyMapIdentity.GetMemberPath(propertyMap);
                    PropertyInfo = memberPath.IsNested ? null : propertyMap.PropertyInfo;
                    return;
                }
            }

            internal IPropertyMap PropertyMap { get; }

            internal PropertyInfo PropertyInfo { get; }
        }

        private sealed class GeneratedMaterializerEntry
        {
            private GeneratedMaterializerEntry(
                IReadOnlyList<GeneratedMaterializerColumn> columns,
                Func<IDataRecord, object> materialize)
            {
                Columns = columns;
                Materialize = materialize;
            }

            internal static GeneratedMaterializerEntry Create<TEntity>(GeneratedMaterializerDescriptor<TEntity> descriptor)
                where TEntity : class
            {
                return new GeneratedMaterializerEntry(
                    descriptor.Columns,
                    record => descriptor.Materializer(record));
            }

            internal static GeneratedMaterializerEntry Create(
                IReadOnlyList<GeneratedMaterializerColumn> columns,
                Func<IDataRecord, object> materialize)
            {
                return new GeneratedMaterializerEntry(columns, materialize);
            }

            internal IReadOnlyList<GeneratedMaterializerColumn> Columns { get; }

            internal Func<IDataRecord, object> Materialize { get; }
        }

        private sealed class MappingDiagnosticDescriptor
        {
            private MappingDiagnosticDescriptor(IPropertyMap map, MappingSource source, Type inheritedFrom, Type conventionType)
            {
                Map = map;
                Source = source;
                InheritedFrom = inheritedFrom;
                ConventionType = conventionType;
            }

            internal IPropertyMap Map { get; }

            internal MappingSource Source { get; }

            internal Type InheritedFrom { get; }

            internal Type ConventionType { get; }

            internal static MappingDiagnosticDescriptor Explicit(IPropertyMap map)
            {
                return new MappingDiagnosticDescriptor(map, MappingSource.Explicit, null, null);
            }

            internal static MappingDiagnosticDescriptor Convention(IPropertyMap map, Convention convention)
            {
                var source = IsNamingPolicyConvention(convention)
                    ? MappingSource.NamingPolicy
                    : MappingSource.Convention;

                return new MappingDiagnosticDescriptor(map, source, null, GetConventionType(convention));
            }

            internal MappingDiagnosticDescriptor AsInheritedFrom(Type baseType)
            {
                return new MappingDiagnosticDescriptor(
                    Map,
                    MappingSource.Inherited,
                    InheritedFrom ?? baseType,
                    ConventionType);
            }
        }
    }
}
