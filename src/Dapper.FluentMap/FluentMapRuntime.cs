using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Represents a thread-safe FluentMap runtime bound to one immutable mapping configuration.
    /// </summary>
    /// <remarks>
    /// Runtime instances own the caches derived from their configuration. They do not own connections,
    /// transactions, commands or database resources and do not require disposal.
    /// </remarks>
    public sealed class FluentMapRuntime
    {
        private const DynamicallyAccessedMemberTypes EntityMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapRuntime"/> class.
        /// </summary>
        /// <param name="configuration">The immutable configuration used by this runtime.</param>
        public FluentMapRuntime(ImmutableFluentMapConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Registry = RuntimeConfigurationRegistryFactory.Create(configuration);
        }

        internal FluentMapRuntime(MappingRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Gets the immutable configuration used by this runtime, or <see langword="null"/> for the legacy global runtime.
        /// </summary>
        public ImmutableFluentMapConfiguration Configuration { get; }

        internal MappingRegistry Registry { get; }

        internal int CacheEntryCount => Registry.CacheEntryCount;

        internal int MaterializationPlanCacheEntryCount => Registry.MaterializationPlanCacheEntryCount;

        internal int GeneratedMaterializerCount => Registry.GeneratedMaterializerCount;

        internal bool StrictGeneratedMaterialization =>
            Configuration != null && Configuration.StrictGeneratedMaterialization;

        /// <summary>
        /// Validates this runtime's effective configuration without accessing global FluentMap state.
        /// </summary>
        public void Validate()
        {
            Registry.ValidateConfiguration();
        }

        /// <summary>
        /// Explains the effective mapping configuration for the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to explain.</typeparam>
        /// <returns>A structured explanation of configured mappings, conventions and fallback mappings.</returns>
        public MappingExplanation Explain<
            [DynamicallyAccessedMembers(EntityMemberTypes)]
            TEntity>()
        {
            return Registry.Explain(typeof(TEntity));
        }

        /// <summary>
        /// Explains the effective mapping configuration for the specified entity type and mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to explain.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to explain.</typeparam>
        /// <returns>A structured explanation of configured mappings, conventions and fallback mappings.</returns>
        public MappingExplanation Explain<
            [DynamicallyAccessedMembers(EntityMemberTypes)]
            TEntity,
            TProfile>()
            where TProfile : IMappingProfile
        {
            return Registry.Explain(typeof(TEntity), typeof(TProfile));
        }

        /// <summary>
        /// Executes a query and materializes rows using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMapped<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                profileType: null,
                runtime: this);
        }

        /// <summary>
        /// Executes a query and materializes rows using this runtime and mapping profile.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMapped<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                typeof(TProfile),
                this);
        }

        /// <summary>
        /// Executes a query and materializes exactly one row using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public TEntity QueryMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            return QueryMapped<TEntity>(connection, sql, param, transaction, commandTimeout, commandType).Single();
        }

        /// <summary>
        /// Executes a query and materializes rows only through registered generated materializers.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized rows.</returns>
        /// <exception cref="T:Dapper.FluentMap.FluentMapConfigurationException">
        /// Thrown when the runtime cannot resolve a generated materializer for the entity, profile and result-column shape.
        /// </exception>
        public IEnumerable<TEntity> QueryGeneratedMapped<TEntity>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            if (param != null)
            {
                throw new NotSupportedException("Strict generated materialization does not support dynamic parameter objects. Use a parameterless command or a non-strict query API.");
            }

            return QueryMappedExtensions.ExecuteGeneratedMapped<TEntity>(
                connection,
                sql,
                transaction,
                commandTimeout,
                commandType,
                profileType: null,
                runtime: this);
        }

        /// <summary>
        /// Executes a query and materializes rows for the specified profile only through registered generated materializers.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized rows.</returns>
        /// <exception cref="T:Dapper.FluentMap.FluentMapConfigurationException">
        /// Thrown when the runtime cannot resolve a generated materializer for the entity, profile and result-column shape.
        /// </exception>
        public IEnumerable<TEntity> QueryGeneratedMapped<TEntity, TProfile>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            if (param != null)
            {
                throw new NotSupportedException("Strict generated materialization does not support dynamic parameter objects. Use a parameterless command or a non-strict query API.");
            }

            return QueryMappedExtensions.ExecuteGeneratedMapped<TEntity>(
                connection,
                sql,
                transaction,
                commandTimeout,
                commandType,
                typeof(TProfile),
                this);
        }

        /// <summary>
        /// Executes a query and materializes exactly one row only through registered generated materializers.
        /// </summary>
        public TEntity QueryGeneratedMappedSingle<TEntity>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            return QueryGeneratedMapped<TEntity>(connection, sql, param, transaction, commandTimeout, commandType).Single();
        }

        /// <summary>
        /// Executes a query and materializes exactly one row for the specified profile only through registered generated materializers.
        /// </summary>
        public TEntity QueryGeneratedMappedSingle<TEntity, TProfile>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return QueryGeneratedMapped<TEntity, TProfile>(connection, sql, param, transaction, commandTimeout, commandType).Single();
        }

        /// <summary>
        /// Executes a query and materializes exactly one row using this runtime and mapping profile.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public TEntity QueryMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return QueryMapped<TEntity, TProfile>(connection, sql, param, transaction, commandTimeout, commandType).Single();
        }

        /// <summary>
        /// Executes a query, splits each row at <paramref name="splitOn"/>, materializes two segments using this runtime and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TSecond,
            TReturn>(
            IDbConnection connection,
            string sql,
            Func<TFirst, TSecond, TReturn> map,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            string splitOn = "Id")
            where TFirst : class
            where TSecond : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMapped<TFirst, TSecond, TReturn>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                map,
                splitOn,
                firstProfileType: null,
                secondProfileType: null,
                runtime: this);
        }

        /// <summary>
        /// Creates a lazy unbuffered query that materializes rows using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMappedUnbuffered<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None),
                profileType: null,
                runtime: this);
        }

        /// <summary>
        /// Creates a lazy unbuffered query that materializes rows using this runtime and mapping profile.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMappedUnbuffered<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None),
                typeof(TProfile),
                this);
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered query that materializes rows using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            DbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            var command = new CommandDefinition(
                sql,
                param,
                transaction,
                commandTimeout,
                commandType,
                CommandFlags.None,
                cancellationToken);

            return QueryMappedExtensions.ExecuteMappedUnbufferedAsync<TEntity>(
                connection,
                command,
                profileType: null,
                runtime: this,
                cancellationToken);
        }

        /// <summary>
        /// Executes a command and returns a reader for sequential materialization using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public MappedGridReader QueryMultipleMapped(IDbConnection connection, string sql, object param = null)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return new MappedGridReader(
                SqlMapper.ExecuteReader(connection, new CommandDefinition(sql, param)),
                this);
        }

        /// <summary>
        /// Asynchronously executes a command and returns a reader for sequential materialization using this runtime.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public Task<MappedGridReader> QueryMultipleMappedAsync(
            DbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            CancellationToken cancellationToken = default)
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedExtensions.ExecuteMultipleMappedAsync(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None, cancellationToken),
                this);
        }
    }
}
