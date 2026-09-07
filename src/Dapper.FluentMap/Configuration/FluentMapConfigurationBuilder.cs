using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Naming;

namespace Dapper.FluentMap.Configuration
{
    /// <summary>
    /// Builds an immutable FluentMap configuration from the existing registration DSL.
    /// </summary>
    /// <remarks>
    /// The builder is mutable and intended for startup/composition-root use. After <see cref="Build"/>
    /// is called, further mutation through the builder is rejected and subsequent calls return the same
    /// immutable configuration instance.
    /// </remarks>
    public sealed class FluentMapConfigurationBuilder
    {
        private const string AssemblyScanningRequiresUnreferencedCodeMessage =
            "Assembly scanning discovers entity maps by reflection. Register maps explicitly with AddMap<TMap>() when publishing trimmed or Native AOT applications.";

        private readonly MappingRegistry _registry;
        private readonly FluentMapConfiguration _configuration;
        private ImmutableFluentMapConfiguration _builtConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapConfigurationBuilder"/> class.
        /// </summary>
        public FluentMapConfigurationBuilder()
        {
            _registry = new MappingRegistry(installDapperTypeMaps: false);
            _configuration = new FluentMapConfiguration(_registry, EnsureNotBuilt);
        }

        /// <summary>
        /// Applies existing FluentMap registration extensions to this builder.
        /// </summary>
        /// <param name="configure">The registration callback that uses the historical configuration DSL.</param>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder Configure(Action<FluentMapConfiguration> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EnsureNotBuilt();
            configure(_configuration);
            return this;
        }

        /// <summary>
        /// Adds the specified entity map to the configuration.
        /// </summary>
        /// <typeparam name="TEntity">The mapped entity type.</typeparam>
        /// <param name="mapper">The entity map instance.</param>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddMap<TEntity>(IEntityMap<TEntity> mapper)
            where TEntity : class
        {
            EnsureNotBuilt();
            _configuration.AddMap(mapper);
            return this;
        }

        /// <summary>
        /// Adds a new instance of the specified entity map type to the configuration.
        /// </summary>
        /// <typeparam name="TMap">The entity map type to create and register.</typeparam>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddMap<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            TMap>()
            where TMap : IEntityMap, new()
        {
            EnsureNotBuilt();
            _configuration.AddMap<TMap>();
            return this;
        }

        /// <summary>
        /// Adds a new instance of the specified entity map type as an explicitly selected mapping profile.
        /// </summary>
        /// <typeparam name="TMap">The profile entity map type to create and register.</typeparam>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddProfile<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
            TMap>()
            where TMap : IEntityMap, new()
        {
            EnsureNotBuilt();
            _configuration.AddProfile<TMap>();
            return this;
        }

        /// <summary>
        /// Registers a generated materializer for the default mapping of the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddGeneratedMaterializer<TEntity>(
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
            where TEntity : class
        {
            EnsureNotBuilt();
            _configuration.AddGeneratedMaterializer(columns, materializer);
            return this;
        }

        /// <summary>
        /// Registers a generated materializer for the specified entity type and mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type used by the materializer.</typeparam>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddGeneratedMaterializer<TEntity, TProfile>(
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            EnsureNotBuilt();
            _configuration.AddGeneratedMaterializer<TEntity, TProfile>(columns, materializer);
            return this;
        }

        /// <summary>
        /// Registers a generated materializer descriptor.
        /// </summary>
        /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
        /// <param name="descriptor">The generated materializer descriptor.</param>
        /// <returns>The current builder.</returns>
        public FluentMapConfigurationBuilder AddGeneratedMaterializer<TEntity>(
            GeneratedMaterializerDescriptor<TEntity> descriptor)
            where TEntity : class
        {
            EnsureNotBuilt();
            _configuration.AddGeneratedMaterializer(descriptor);
            return this;
        }

        /// <summary>
        /// Finds exported entity map types in the specified assembly and adds them to the configuration.
        /// </summary>
        /// <param name="assembly">The assembly to scan for entity maps.</param>
        /// <param name="namespaces">Optional namespaces used to filter discovered entity map types.</param>
        /// <returns>The current builder.</returns>
        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        public FluentMapConfigurationBuilder AddMapsFromAssembly(Assembly assembly, params string[] namespaces)
        {
            EnsureNotBuilt();
            _configuration.AddMapsFromAssembly(assembly, namespaces);
            return this;
        }

        /// <summary>
        /// Finds exported entity map types in the assembly containing <typeparamref name="TMarker"/>
        /// and adds them to the configuration.
        /// </summary>
        /// <typeparam name="TMarker">A marker type from the assembly to scan.</typeparam>
        /// <param name="namespaces">Optional namespaces used to filter discovered entity map types.</param>
        /// <returns>The current builder.</returns>
        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        public FluentMapConfigurationBuilder AddMapsFromAssemblyContaining<TMarker>(params string[] namespaces)
        {
            EnsureNotBuilt();
            _configuration.AddMapsFromAssemblyContaining<TMarker>(namespaces);
            return this;
        }

        /// <summary>
        /// Adds the specified convention to the configuration.
        /// </summary>
        /// <typeparam name="TConvention">The convention type.</typeparam>
        /// <returns>A convention configuration object that writes to this builder.</returns>
        public FluentConventionConfiguration AddConvention<TConvention>()
            where TConvention : Convention, new()
        {
            EnsureNotBuilt();
            return _configuration.AddConvention<TConvention>();
        }

        /// <summary>
        /// Adds a naming policy to the configuration.
        /// </summary>
        /// <param name="namingPolicy">The naming policy used to transform member names into column names.</param>
        /// <param name="caseSensitive">A value indicating whether generated column mappings are case sensitive.</param>
        /// <returns>A convention configuration object that writes to this builder.</returns>
        public FluentConventionConfiguration UseNamingPolicy(NamingPolicy namingPolicy, bool caseSensitive = true)
        {
            EnsureNotBuilt();
            return _configuration.UseNamingPolicy(namingPolicy, caseSensitive);
        }

        /// <summary>
        /// Adds a custom naming policy to the configuration.
        /// </summary>
        /// <param name="transformer">A function that receives a member name and returns a column name.</param>
        /// <param name="caseSensitive">A value indicating whether generated column mappings are case sensitive.</param>
        /// <returns>A convention configuration object that writes to this builder.</returns>
        public FluentConventionConfiguration UseNamingPolicy(Func<string, string> transformer, bool caseSensitive = true)
        {
            EnsureNotBuilt();
            return _configuration.UseNamingPolicy(transformer, caseSensitive);
        }

        /// <summary>
        /// Validates the current mutable configuration using the same runtime validator as <see cref="FluentMapper.Validate"/>.
        /// </summary>
        public void Validate()
        {
            _registry.ValidateConfiguration();
        }

        /// <summary>
        /// Validates the mutable registrations and returns an immutable configuration snapshot.
        /// </summary>
        /// <returns>The immutable FluentMap configuration.</returns>
        public ImmutableFluentMapConfiguration Build()
        {
            if (_builtConfiguration != null)
            {
                return _builtConfiguration;
            }

            _registry.ValidateConfiguration();
            _builtConfiguration = ImmutableFluentMapConfiguration.Create(_registry);
            return _builtConfiguration;
        }

        private void EnsureNotBuilt()
        {
            if (_builtConfiguration != null)
            {
                throw new InvalidOperationException("The FluentMap configuration builder cannot be mutated after Build() has been called.");
            }
        }
    }
}
