using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Naming;

namespace Dapper.FluentMap.Configuration
{
    /// <summary>
    /// Defines methods for configuring Dapper.FluentMap.
    /// </summary>
    public class FluentMapConfiguration
    {
        private const string AssemblyScanningRequiresUnreferencedCodeMessage =
            "Assembly scanning discovers entity maps by reflection. Register maps explicitly with AddMap<TMap>() when publishing trimmed or Native AOT applications.";

        private readonly MappingRegistry _registry;
        private readonly Action _ensureMutable;
        private readonly Action _afterMutation;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapConfiguration"/> class.
        /// </summary>
        public FluentMapConfiguration()
            : this(FluentMapper.ConfigurationRegistry, ensureMutable: null, afterMutation: FluentMapper.PublishConfigurationMutation)
        {
        }

        internal FluentMapConfiguration(MappingRegistry registry, Action ensureMutable)
            : this(registry, ensureMutable, afterMutation: null)
        {
        }

        internal FluentMapConfiguration(MappingRegistry registry, Action ensureMutable, Action afterMutation)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _ensureMutable = ensureMutable;
            _afterMutation = afterMutation;
        }

        /// <summary>
        /// Adds the specified <see cref="T:Dapper.FluentMap.Mapping.EntityMap"/> to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <typeparam name="TEntity">The type argument of the entity.</typeparam>
        /// <param name="mapper">
        /// An instance of the <see cref="T:Dapper.FluentMap.Mapping.IEntityMap"/> interface containing the
        /// entity mapping configuration.
        /// </param>
        public void AddMap<TEntity>(IEntityMap<TEntity> mapper) where TEntity : class
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            EnsureCanMutate();
            _registry.AddEntityMap(mapper);
            NotifyMutation();
        }

        /// <summary>
        /// Adds a new instance of the specified entity map type to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <typeparam name="TMap">The type of the entity map to create and register.</typeparam>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        public FluentMapConfiguration AddMap<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            TMap>()
            where TMap : IEntityMap, new()
        {
            var mapType = typeof(TMap);
            var entityType = GetMappedEntityType(mapType);
            var mapper = CreateEntityMap<TMap>();

            EnsureCanMutate();
            _registry.AddEntityMap(entityType, mapper);
            NotifyMutation();
            return this;
        }

        /// <summary>
        /// Adds a new instance of the specified entity map type as an explicitly selected mapping profile.
        /// </summary>
        /// <typeparam name="TMap">The profile entity map type to create and register.</typeparam>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        public FluentMapConfiguration AddProfile<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            TMap>()
            where TMap : IEntityMap, new()
        {
            var mapType = typeof(TMap);
            var entityType = GetMappedEntityType(mapType);
            var profileType = GetMappedProfileType(mapType);
            var mapper = CreateEntityMap<TMap>();

            EnsureCanMutate();
            _registry.AddProfileMap(entityType, profileType, mapper);
            NotifyMutation();
            return this;
        }

        /// <summary>
        /// Registers a generated materializer for the default mapping of the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        public FluentMapConfiguration AddGeneratedMaterializer<TEntity>(
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
            where TEntity : class
        {
            return AddGeneratedMaterializer(new GeneratedMaterializerDescriptor<TEntity>(columns, materializer));
        }

        /// <summary>
        /// Registers a generated materializer for the specified entity type and mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type used by the materializer.</typeparam>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        public FluentMapConfiguration AddGeneratedMaterializer<TEntity, TProfile>(
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return AddGeneratedMaterializer(new GeneratedMaterializerDescriptor<TEntity>(typeof(TProfile), columns, materializer));
        }

        /// <summary>
        /// Registers a generated materializer descriptor.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <param name="descriptor">The generated materializer descriptor.</param>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        public FluentMapConfiguration AddGeneratedMaterializer<TEntity>(
            GeneratedMaterializerDescriptor<TEntity> descriptor)
            where TEntity : class
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            EnsureCanMutate();
            _registry.AddGeneratedMaterializer(descriptor);
            NotifyMutation();
            return this;
        }

        /// <summary>
        /// Finds exported entity map types in the specified assembly and adds them to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <param name="assembly">The assembly to scan for entity maps.</param>
        /// <param name="namespaces">Optional namespaces used to filter discovered entity map types.</param>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        public FluentMapConfiguration AddMapsFromAssembly(Assembly assembly, params string[] namespaces)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            EnsureCanMutate();
            var definitions = FindEntityMapDefinitions(assembly, namespaces).ToList();
            EnsureNoDuplicateEntityMaps(definitions);

            var registrations = definitions
                .Select(definition => new EntityMapRegistration(
                    definition.MapType,
                    definition.EntityType,
                    CreateEntityMap(definition.MapType)))
                .ToList();

            foreach (var registration in OrderByIncludedBaseMaps(registrations))
            {
                _registry.AddEntityMap(registration.EntityType, registration.Map);
            }

            NotifyMutation();
            return this;
        }

        /// <summary>
        /// Finds exported entity map types in the assembly containing <typeparamref name="TMarker"/>
        /// and adds them to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <typeparam name="TMarker">A marker type from the assembly to scan.</typeparam>
        /// <param name="namespaces">Optional namespaces used to filter discovered entity map types.</param>
        /// <returns>The current instance of <see cref="T:Dapper.FluentMap.Configuration.FluentMapConfiguration"/>.</returns>
        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        public FluentMapConfiguration AddMapsFromAssemblyContaining<TMarker>(params string[] namespaces)
        {
            return AddMapsFromAssembly(typeof(TMarker).GetTypeInfo().Assembly, namespaces);
        }

        /// <summary>
        /// Adds the specified <see cref="T:Dapper.FluentMap.Conventions.Convention"/> to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <typeparam name="TConvention">The type of the convention.</typeparam>
        /// <returns>
        /// An instance of <see cref="T:Dapper.FluentMap.Configuration.FluentConventionConfiguration"/>
        /// which allows configuration of the convention.
        /// </returns>
        public FluentConventionConfiguration AddConvention<TConvention>() where TConvention : Convention, new()
        {
            EnsureCanMutate();
            return new FluentConventionConfiguration(new TConvention(), _registry, EnsureCanMutate, NotifyMutation);
        }

        /// <summary>
        /// Adds a naming policy to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <param name="namingPolicy">The naming policy used to transform member names into column names.</param>
        /// <param name="caseSensitive">A value indicating whether the generated column name mappings should be case sensitive.</param>
        /// <returns>
        /// An instance of <see cref="T:Dapper.FluentMap.Configuration.FluentConventionConfiguration"/>
        /// which allows configuration of the naming policy for entities.
        /// </returns>
        public FluentConventionConfiguration UseNamingPolicy(NamingPolicy namingPolicy, bool caseSensitive = true)
        {
            if (namingPolicy == null)
            {
                throw new ArgumentNullException(nameof(namingPolicy));
            }

            EnsureCanMutate();
            return new FluentConventionConfiguration(new NamingPolicyConvention(namingPolicy, caseSensitive), _registry, EnsureCanMutate, NotifyMutation);
        }

        /// <summary>
        /// Adds a custom naming policy to the configuration of Dapper.FluentMap.
        /// </summary>
        /// <param name="transformer">A function that receives a member name and returns a column name.</param>
        /// <param name="caseSensitive">A value indicating whether the generated column name mappings should be case sensitive.</param>
        /// <returns>
        /// An instance of <see cref="T:Dapper.FluentMap.Configuration.FluentConventionConfiguration"/>
        /// which allows configuration of the naming policy for entities.
        /// </returns>
        public FluentConventionConfiguration UseNamingPolicy(Func<string, string> transformer, bool caseSensitive = true)
        {
            if (transformer == null)
            {
                throw new ArgumentNullException(nameof(transformer));
            }

            return UseNamingPolicy(NamingPolicy.Custom(transformer), caseSensitive);
        }

        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        private static IEnumerable<EntityMapDefinition> FindEntityMapDefinitions(Assembly assembly, string[] namespaces)
        {
            return GetExportedTypes(assembly)
                .Where(IsConcreteEntityMapType)
                .Where(type => IsNamespaceMatch(type, namespaces))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
                .Select(type => new EntityMapDefinition(type, GetMappedEntityType(type)));
        }

        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                throw new FluentMapConfigurationException(
                    $"Cannot load exported types from assembly '{assembly.FullName}'.",
                    ex);
            }
        }

        private static bool IsConcreteEntityMapType(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return !typeInfo.IsAbstract &&
                   !typeInfo.IsInterface &&
                   !typeInfo.ContainsGenericParameters &&
                   typeof(IEntityMap).GetTypeInfo().IsAssignableFrom(typeInfo);
        }

        private static bool IsNamespaceMatch(Type type, string[] namespaces)
        {
            return namespaces == null ||
                   namespaces.Length == 0 ||
                   namespaces.Any(ns => string.Equals(ns, type.Namespace, StringComparison.Ordinal));
        }

        private static Type GetMappedEntityType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            Type mapType)
        {
            var entityMapInterfaces = mapType.GetInterfaces()
                .Where(type => type.GetTypeInfo().IsGenericType &&
                               type.GetGenericTypeDefinition() == typeof(IEntityMap<>))
                .ToList();

            if (entityMapInterfaces.Count != 1)
            {
                throw new FluentMapConfigurationException(
                    $"Entity map type '{mapType.FullName}' must implement exactly one closed IEntityMap<TEntity> interface.");
            }

            var entityType = entityMapInterfaces[0].GetGenericArguments()[0];
            if (!entityType.GetTypeInfo().IsClass)
            {
                throw new FluentMapConfigurationException(
                    $"Entity map type '{mapType.FullName}' targets '{entityType.FullName}', but entity maps must target class types.");
            }

            return entityType;
        }

        private static Type GetMappedProfileType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            Type mapType)
        {
            var profileInterfaces = mapType.GetInterfaces()
                .Where(type => type.GetTypeInfo().IsGenericType &&
                               type.GetGenericTypeDefinition() == typeof(IProfileMap<>))
                .ToList();

            if (profileInterfaces.Count != 1)
            {
                throw new FluentMapConfigurationException(
                    $"Profile entity map type '{mapType.FullName}' must implement exactly one closed IProfileMap<TProfile> interface.");
            }

            var profileType = profileInterfaces[0].GetGenericArguments()[0];
            if (!typeof(IMappingProfile).GetTypeInfo().IsAssignableFrom(profileType.GetTypeInfo()))
            {
                throw new FluentMapConfigurationException(
                    $"Profile entity map type '{mapType.FullName}' targets '{profileType.FullName}', but mapping profiles must implement IMappingProfile.");
            }

            return profileType;
        }

        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        private static IEntityMap CreateEntityMap(Type mapType)
        {
            try
            {
                return (IEntityMap)Activator.CreateInstance(mapType);
            }
            catch (Exception ex)
            {
                throw new FluentMapConfigurationException(
                    $"Entity map type '{mapType.FullName}' could not be created. Ensure it has a public parameterless constructor and the constructor completes successfully.",
                    ex);
            }
        }

        private static IEntityMap CreateEntityMap<TMap>()
            where TMap : IEntityMap, new()
        {
            try
            {
                return new TMap();
            }
            catch (Exception ex)
            {
                throw new FluentMapConfigurationException(
                    $"Entity map type '{typeof(TMap).FullName}' could not be created. Ensure it has a public parameterless constructor and the constructor completes successfully.",
                    ex);
            }
        }

        private void EnsureCanMutate()
        {
            _ensureMutable?.Invoke();
        }

        private void NotifyMutation()
        {
            _afterMutation?.Invoke();
        }

        private static void EnsureNoDuplicateEntityMaps(IList<EntityMapDefinition> definitions)
        {
            var duplicates = definitions
                .GroupBy(definition => definition.EntityType)
                .Where(group => group.Count() > 1)
                .ToList();

            if (duplicates.Count == 0)
            {
                return;
            }

            var duplicateDescriptions = duplicates
                .Select(group =>
                    $"entity '{group.Key.FullName}' mapped by {string.Join(", ", group.Select(definition => "'" + definition.MapType.FullName + "'"))}");

            throw new FluentMapConfigurationException(
                "Multiple entity maps were discovered for the same entity: " +
                string.Join("; ", duplicateDescriptions) + ".");
        }

        private static IList<EntityMapRegistration> OrderByIncludedBaseMaps(IList<EntityMapRegistration> registrations)
        {
            var ordered = new List<EntityMapRegistration>();
            var remaining = registrations.ToList();

            while (remaining.Count > 0)
            {
                var progressed = false;

                foreach (var registration in remaining.ToList())
                {
                    if (!HasPendingIncludedBaseMap(registration, remaining, ordered))
                    {
                        remaining.Remove(registration);
                        ordered.Add(registration);
                        progressed = true;
                    }
                }

                if (!progressed)
                {
                    throw new FluentMapConfigurationException(
                        "Entity maps discovered from assembly could not be ordered by included base mappings. Check for cyclic or invalid IncludeBase configuration.");
                }
            }

            return ordered;
        }

        private static bool HasPendingIncludedBaseMap(
            EntityMapRegistration registration,
            IList<EntityMapRegistration> remaining,
            IList<EntityMapRegistration> ordered)
        {
            foreach (var includedBaseType in GetIncludedBaseTypes(registration.Map))
            {
                if (ordered.Any(map => map.EntityType == includedBaseType))
                {
                    continue;
                }

                if (remaining.Any(map => map.EntityType == includedBaseType))
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<Type> GetIncludedBaseTypes(IEntityMap map)
        {
            var mapWithIncludedBases = map as IEntityMapWithIncludedBaseTypes;
            return mapWithIncludedBases == null
                ? new Type[0]
                : mapWithIncludedBases.IncludedBaseTypes;
        }

        private sealed class EntityMapDefinition
        {
            internal EntityMapDefinition(Type mapType, Type entityType)
            {
                MapType = mapType;
                EntityType = entityType;
            }

            internal Type MapType { get; }

            internal Type EntityType { get; }
        }

        private sealed class EntityMapRegistration
        {
            internal EntityMapRegistration(Type mapType, Type entityType, IEntityMap map)
            {
                MapType = mapType;
                EntityType = entityType;
                Map = map;
            }

            internal Type MapType { get; }

            internal Type EntityType { get; }

            internal IEntityMap Map { get; }
        }

        #region EditorBrowsableStates
        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Type GetType()
        {
            return base.GetType();
        }
        #endregion
    }
}
