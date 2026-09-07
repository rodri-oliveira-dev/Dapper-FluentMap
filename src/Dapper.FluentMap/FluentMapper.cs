using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.TypeMaps;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Main entry point for Dapper.FluentMap configuration.
    /// </summary>
    public static class FluentMapper
    {
        private const DynamicallyAccessedMemberTypes EntityMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties;

        private static readonly object _syncRoot = new object();
        private static readonly MappingRegistry _builderRegistry = new MappingRegistry(installDapperTypeMaps: false);
        private static readonly FluentMapConfiguration _configuration = new FluentMapConfiguration(_builderRegistry, ensureMutable: null);
        private static volatile FluentMapRuntime _runtime = CreateRuntime(_builderRegistry);

        /// <summary>
        /// Gets the dictionary containing the entity mapping per entity type.
        /// </summary>
        /// <remarks>
        /// This mutable dictionary is preserved for source and binary compatibility. Prefer configuring maps
        /// through <see cref="Initialize(Action{FluentMapConfiguration})"/> and use <see cref="GetEntityMaps"/>
        /// for read-only inspection.
        /// </remarks>
        public static readonly ConcurrentDictionary<Type, IEntityMap> EntityMaps = _builderRegistry.EntityMaps;

        /// <summary>
        /// Gets the dictionary containing the conventions per entity type.
        /// </summary>
        /// <remarks>
        /// This mutable dictionary is preserved for source and binary compatibility. Prefer configuring conventions
        /// through <see cref="Initialize(Action{FluentMapConfiguration})"/> and use <see cref="GetTypeConventions"/>
        /// for read-only inspection.
        /// </remarks>
        public static readonly ConcurrentDictionary<Type, IList<Convention>> TypeConventions = _builderRegistry.TypeConventions;

        /// <summary>
        /// Gets the immutable configuration currently used by the default compatibility runtime.
        /// </summary>
        public static ImmutableFluentMapConfiguration Configuration => Runtime.Configuration;

        /// <summary>
        /// Gets the default compatibility runtime used by the historical static APIs.
        /// </summary>
        public static FluentMapRuntime Runtime => _runtime;

        internal static MappingRegistry Registry => Runtime.Registry;

        internal static MappingRegistry ConfigurationRegistry => _builderRegistry;

        internal static void PublishConfigurationMutation()
        {
            lock (_syncRoot)
            {
                PublishDefaultRuntime();
            }
        }

        /// <summary>
        /// Initializes Dapper.FluentMap with the specified configuration.
        /// This is method should be called when the application starts or when the first mapping is needed.
        /// </summary>
        /// <param name="configure">A callback containing the configuration of Dapper.FluentMap.</param>
        public static void Initialize(Action<FluentMapConfiguration> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            lock (_syncRoot)
            {
                try
                {
                    configure(_configuration);
                    PublishDefaultRuntime();
                }
                catch
                {
                    PublishDefaultRuntime();
                    throw;
                }
            }
        }

        /// <summary>
        /// Validates the current Dapper.FluentMap configuration.
        /// </summary>
        /// <exception cref="T:Dapper.FluentMap.FluentMapConfigurationException">
        /// when one or more configuration errors are found.
        /// </exception>
        public static void Validate()
        {
            _builderRegistry.ValidateConfiguration();
            Runtime.Validate();
        }

        /// <summary>
        /// Gets a read-only snapshot of the default entity maps currently registered in Dapper.FluentMap.
        /// </summary>
        /// <returns>A read-only snapshot of the registered default entity maps.</returns>
        public static IReadOnlyDictionary<Type, IEntityMap> GetEntityMaps()
        {
            return _builderRegistry.GetEntityMapsSnapshot();
        }

        /// <summary>
        /// Gets a read-only snapshot of the type conventions currently registered in Dapper.FluentMap.
        /// </summary>
        /// <returns>A read-only snapshot of the registered type conventions.</returns>
        public static IReadOnlyDictionary<Type, IReadOnlyList<Convention>> GetTypeConventions()
        {
            return _builderRegistry.GetTypeConventionsSnapshot();
        }

        /// <summary>
        /// Explains the effective mapping configuration for the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to explain.</typeparam>
        /// <returns>A structured explanation of configured mappings, conventions and fallback mappings.</returns>
        public static MappingExplanation Explain<
            [DynamicallyAccessedMembers(EntityMemberTypes)]
            TEntity>()
        {
            return Runtime.Explain<TEntity>();
        }

        /// <summary>
        /// Explains the effective mapping configuration for the specified entity type and mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to explain.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to explain.</typeparam>
        /// <returns>A structured explanation of configured mappings, conventions and fallback mappings.</returns>
        public static MappingExplanation Explain<
            [DynamicallyAccessedMembers(EntityMemberTypes)]
            TEntity,
            TProfile>()
            where TProfile : IMappingProfile
        {
            return Runtime.Explain<TEntity, TProfile>();
        }

        /// <summary>
        /// Registers a Dapper type map using fluent mapping for the specified <typeparamref name="TEntity"/>.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        internal static void AddTypeMap<TEntity>()
        {
            SetDapperTypeMap(typeof(TEntity));
        }

        /// <summary>
        /// Registers a Dapper type map using fluent mapping for the specified <paramref name="entityType"/>.
        /// </summary>
        /// <param name="entityType">The type of the entity.</param>
        internal static void AddTypeMap(Type entityType)
        {
            SetDapperTypeMap(entityType);
        }

        /// <summary>
        /// Registers a Dapper type map using conventions for the specified <typeparamref name="TEntity"/>.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        internal static void AddConventionTypeMap<TEntity>()
        {
            AddTypeMap<TEntity>();
        }

        /// <summary>
        /// Registers a Dapper type map using conventions for the specified <paramref name="entityType"/>.
        /// </summary>
        /// <param name="entityType">The type of the entity.</param>
        internal static void AddConventionTypeMap(Type entityType)
        {
            AddTypeMap(entityType);
        }

        internal static void Reset(params Type[] dapperTypes)
        {
            lock (_syncRoot)
            {
                _builderRegistry.Reset(dapperTypes);
                _runtime = CreateRuntime(_builderRegistry);
            }
        }

        private static FluentMapRuntime CreateRuntime(MappingRegistry registry)
        {
            var configuration = ImmutableFluentMapConfiguration.Create(registry);
            return configuration.CreateRuntime();
        }

        private static void PublishDefaultRuntime()
        {
            var runtime = CreateRuntime(_builderRegistry);
            _runtime = runtime;
            InstallDefaultDapperTypeMaps(runtime.Configuration);
        }

        private static void InstallDefaultDapperTypeMaps(ImmutableFluentMapConfiguration configuration)
        {
            foreach (var entityType in GetDefaultDapperMappedTypes(configuration))
            {
                SetDapperTypeMap(entityType);
            }
        }

        private static IEnumerable<Type> GetDefaultDapperMappedTypes(ImmutableFluentMapConfiguration configuration)
        {
            return configuration.EntityMaps.Keys
                .Concat(configuration.TypeConventions.Keys)
                .Distinct();
        }

        private static void SetDapperTypeMap(Type entityType)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            SqlMapper.SetTypeMap(entityType, new FluentMapTypeMap(entityType));
        }
    }
}
