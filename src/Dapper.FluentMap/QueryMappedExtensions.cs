using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Provides opt-in query helpers for FluentMap-controlled materialization.
    /// </summary>
    public static class QueryMappedExtensions
    {
        /// <summary>
        /// Executes a query and materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized rows.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMapped<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType));
        }

        /// <summary>
        /// Executes a query and materializes rows using the specified FluentMap mapping profile.
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
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
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

            return QueryMapped<TEntity, TProfile>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType));
        }

        /// <summary>
        /// Executes a command and materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The materialized rows.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this IDbConnection connection,
            CommandDefinition command)
            where TEntity : class
        {
            return ExecuteMapped<TEntity>(connection, command, profileType: null, FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a command and materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The materialized rows.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
            CommandDefinition command)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ExecuteMapped<TEntity>(connection, command, typeof(TProfile), FluentMapper.Runtime);
        }

        /// <summary>
        /// Creates a lazy unbuffered query that materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>A lazy sequence that keeps the underlying reader open until enumeration completes or the enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedUnbuffered<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None));
        }

        /// <summary>
        /// Creates a lazy unbuffered query that materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>A lazy sequence that keeps the underlying reader open until enumeration completes or the enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedUnbuffered<TEntity, TProfile>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None));
        }

        /// <summary>
        /// Creates a lazy unbuffered command that materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A lazy sequence that keeps the underlying reader open until enumeration completes or the enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this IDbConnection connection,
            CommandDefinition command)
            where TEntity : class
        {
            return ExecuteMappedUnbuffered<TEntity>(connection, command, profileType: null, FluentMapper.Runtime);
        }

        /// <summary>
        /// Creates a lazy unbuffered command that materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A lazy sequence that keeps the underlying reader open until enumeration completes or the enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TEntity> QueryMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
            CommandDefinition command)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ExecuteMappedUnbuffered<TEntity>(connection, command, typeof(TProfile), FluentMapper.Runtime);
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered query that materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="cancellationToken">A token to cancel asynchronous execution or enumeration.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this DbConnection connection,
            string sql,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            return QueryMappedUnbufferedAsync<TEntity>(
                connection,
                sql,
                param: null,
                transaction: null,
                commandTimeout: null,
                commandType: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered query that materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <param name="cancellationToken">A token to cancel asynchronous execution or enumeration.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this DbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedUnbufferedAsync<TEntity>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None, cancellationToken));
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered query that materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="cancellationToken">A token to cancel asynchronous execution or enumeration.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this DbConnection connection,
            string sql,
            CancellationToken cancellationToken)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return QueryMappedUnbufferedAsync<TEntity, TProfile>(
                connection,
                sql,
                param: null,
                transaction: null,
                commandTimeout: null,
                commandType: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered query that materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <param name="cancellationToken">A token to cancel asynchronous execution or enumeration.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this DbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMappedUnbufferedAsync<TEntity, TProfile>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None, cancellationToken));
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered command that materializes rows using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this DbConnection connection,
            CommandDefinition command)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            return ExecuteMappedUnbufferedAsync<TEntity>(connection, command, profileType: null, FluentMapper.Runtime, command.CancellationToken);
        }

        /// <summary>
        /// Creates a lazy asynchronous unbuffered command that materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A lazy asynchronous sequence that keeps the underlying reader open until enumeration completes or the async enumerator is disposed.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TEntity> QueryMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this DbConnection connection,
            CommandDefinition command)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            return ExecuteMappedUnbufferedAsync<TEntity>(connection, command, typeof(TProfile), FluentMapper.Runtime, command.CancellationToken);
        }

        /// <summary>
        /// Executes a query and materializes exactly one row using FluentMap's opt-in nested object materializer.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized row.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static TEntity QueryMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            this IDbConnection connection,
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
        /// Executes a query and materializes exactly one row using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized row.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static TEntity QueryMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
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
        /// Executes a query asynchronously and materializes rows using the specified FluentMap mapping profile.
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
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TEntity>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
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

            return QueryMappedAsync<TEntity, TProfile>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType));
        }

        /// <summary>
        /// Executes a command asynchronously and materializes rows using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The materialized rows.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TEntity>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
            CommandDefinition command)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ExecuteMappedAsync<TEntity>(connection, command, typeof(TProfile), FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a query asynchronously and materializes exactly one row using the specified FluentMap mapping profile.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>The materialized row.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static async Task<TEntity> QueryMappedSingleAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            var rows = await QueryMappedAsync<TEntity, TProfile>(
                connection,
                sql,
                param,
                transaction,
                commandTimeout,
                commandType).ConfigureAwait(false);

            return rows.Single();
        }

        /// <summary>
        /// Executes a query, splits each row at <paramref name="splitOn"/>, materializes two FluentMap-controlled segments and composes the return value.
        /// </summary>
        /// <typeparam name="TFirst">The entity type materialized from columns before the split boundary.</typeparam>
        /// <typeparam name="TSecond">The entity type materialized from columns starting at the split boundary.</typeparam>
        /// <typeparam name="TReturn">The projected return type.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="map">The composition delegate called for each materialized row.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <param name="splitOn">The column name where the second row segment begins. The default is <c>Id</c>.</param>
        /// <returns>The composed rows.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TSecond,
            TReturn>(
            this IDbConnection connection,
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

            return QueryMapped<TFirst, TSecond, TReturn>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                map,
                splitOn);
        }

        /// <summary>
        /// Executes a command, splits each row at <paramref name="splitOn"/>, materializes two FluentMap-controlled segments and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TSecond,
            TReturn>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TReturn> map,
            string splitOn = "Id")
            where TFirst : class
            where TSecond : class
        {
            return ExecuteMapped<TFirst, TSecond, TReturn>(
                connection,
                command,
                map,
                splitOn,
                firstProfileType: null,
                secondProfileType: null,
                runtime: FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a query and returns a reader for sequential FluentMap-controlled materialization of multiple result sets.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="sql">The SQL command to execute.</param>
        /// <param name="param">Optional query parameters.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="commandTimeout">Optional command timeout.</param>
        /// <param name="commandType">Optional command type.</param>
        /// <returns>A disposable mapped multiple result reader.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static MappedGridReader QueryMultipleMapped(
            this IDbConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMultipleMapped(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType));
        }

        /// <summary>
        /// Executes a command and returns a reader for sequential FluentMap-controlled materialization of multiple result sets.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>A disposable mapped multiple result reader.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static MappedGridReader QueryMultipleMapped(
            this IDbConnection connection,
            CommandDefinition command)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            return new MappedGridReader(SqlMapper.ExecuteReader(connection, command), FluentMapper.Runtime);
        }

        /// <summary>
        /// Asynchronously executes a query and returns a reader for sequential FluentMap-controlled materialization of multiple result sets.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<MappedGridReader> QueryMultipleMappedAsync(
            this DbConnection connection,
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

            return QueryMultipleMappedAsync(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.None, cancellationToken));
        }

        /// <summary>
        /// Asynchronously executes a command and returns a reader for sequential FluentMap-controlled materialization of multiple result sets.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<MappedGridReader> QueryMultipleMappedAsync(
            this DbConnection connection,
            CommandDefinition command)
        {
            return ExecuteMultipleMappedAsync(connection, command, FluentMapper.Runtime);
        }

        internal static IEnumerable<TEntity> ExecuteMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            CommandDefinition command,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            using (var reader = SqlMapper.ExecuteReader(connection, command))
            {
                return MappedRowMaterializer.Materialize<TEntity>(reader, profileType, runtime);
            }
        }

        internal static IEnumerable<TEntity> ExecuteGeneratedMapped<TEntity>(
            IDbConnection connection,
            string sql,
            IDbTransaction transaction,
            int? commandTimeout,
            CommandType? commandType,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            var openedHere = connection.State == ConnectionState.Closed;
            if (openedHere)
            {
                connection.Open();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Transaction = transaction;
                    if (commandTimeout.HasValue)
                    {
                        command.CommandTimeout = commandTimeout.Value;
                    }

                    if (commandType.HasValue)
                    {
                        command.CommandType = commandType.Value;
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        var results = new List<TEntity>();
                        var materializer = MappedRowMaterializer.CreateGeneratedMaterializer<TEntity>(reader, profileType, runtime);

                        while (reader.Read())
                        {
                            results.Add(materializer(reader));
                        }

                        return results;
                    }
                }
            }
            finally
            {
                if (openedHere)
                {
                    connection.Close();
                }
            }
        }

        internal static IEnumerable<TReturn> ExecuteMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TSecond,
            TReturn>(
            IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TReturn> map,
            string splitOn,
            Type firstProfileType,
            Type secondProfileType,
            FluentMapRuntime runtime)
            where TFirst : class
            where TSecond : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (string.IsNullOrWhiteSpace(splitOn))
            {
                throw new ArgumentException("A splitOn column name is required.", nameof(splitOn));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            using (var reader = SqlMapper.ExecuteReader(connection, command))
            {
                var splitIndex = GetSplitIndex(reader, splitOn);
                var firstSegment = new SegmentDataRecord(reader, 0, splitIndex);
                var secondSegment = new SegmentDataRecord(reader, splitIndex, reader.FieldCount - splitIndex);
                var firstMaterializer = MappedRowMaterializer.CreateMaterializer<TFirst>(firstSegment, firstProfileType, runtime);
                var secondMaterializer = MappedRowMaterializer.CreateMaterializer<TSecond>(secondSegment, secondProfileType, runtime);
                var results = new List<TReturn>();

                while (reader.Read())
                {
                    var first = firstMaterializer(firstSegment);
                    var second = IsAllNull(secondSegment) ? null : secondMaterializer(secondSegment);
                    results.Add(map(first, second));
                }

                return results;
            }
        }

        internal static async Task<MappedGridReader> ExecuteMultipleMappedAsync(
            DbConnection connection,
            CommandDefinition command,
            FluentMapRuntime runtime)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            var reader = await SqlMapper.ExecuteReaderAsync(connection, command).ConfigureAwait(false);
            return new MappedGridReader(reader, runtime);
        }

        private static int GetSplitIndex(IDataRecord reader, string splitOn)
        {
            var splitIndex = -1;
            for (var i = 1; i < reader.FieldCount; i++)
            {
                if (!string.Equals(reader.GetName(i), splitOn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (splitIndex >= 0)
                {
                    throw new InvalidOperationException("The splitOn column '" + splitOn + "' is ambiguous in the result set.");
                }

                splitIndex = i;
            }

            if (splitIndex < 0)
            {
                throw new InvalidOperationException("The splitOn column '" + splitOn + "' was not found in the result set.");
            }

            if (splitIndex == 0 || splitIndex >= reader.FieldCount)
            {
                throw new InvalidOperationException("The splitOn column '" + splitOn + "' produced an empty row segment.");
            }

            return splitIndex;
        }

        private static bool IsAllNull(IDataRecord record)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (!record.IsDBNull(i) && record.GetValue(i) != null)
                {
                    return false;
                }
            }

            return true;
        }

        internal static IEnumerable<TEntity> ExecuteMappedUnbuffered<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            CommandDefinition command,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            return ExecuteMappedUnbufferedIterator<TEntity>(connection, command, profileType, runtime);
        }

        private static IEnumerable<TEntity> ExecuteMappedUnbufferedIterator<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            CommandDefinition command,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            using (var reader = SqlMapper.ExecuteReader(connection, command))
            {
                var materializer = MappedRowMaterializer.CreateMaterializer<TEntity>(reader, profileType, runtime);

                while (reader.Read())
                {
                    yield return materializer(reader);
                }
            }
        }

        private static async Task<IEnumerable<TEntity>> ExecuteMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDbConnection connection,
            CommandDefinition command,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            using (var reader = await SqlMapper.ExecuteReaderAsync(connection, command).ConfigureAwait(false))
            {
                return MappedRowMaterializer.Materialize<TEntity>(reader, profileType, runtime);
            }
        }

        internal static async IAsyncEnumerable<TEntity> ExecuteMappedUnbufferedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            DbConnection connection,
            CommandDefinition command,
            Type profileType,
            FluentMapRuntime runtime,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            DbDataReader reader = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var effectiveCommand = WithCancellation(command, cancellationToken);
                reader = await SqlMapper.ExecuteReaderAsync(connection, effectiveCommand).ConfigureAwait(false);
                var materializer = MappedRowMaterializer.CreateMaterializer<TEntity>(reader, profileType, runtime);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        yield break;
                    }

                    yield return materializer(reader);
                }
            }
            finally
            {
                if (reader != null)
                {
                    await DisposeReaderAsync(reader).ConfigureAwait(false);
                }
            }
        }

        private static CommandDefinition WithCancellation(CommandDefinition command, CancellationToken cancellationToken)
        {
            return new CommandDefinition(
                command.CommandText,
                command.Parameters,
                command.Transaction,
                command.CommandTimeout,
                command.CommandType,
                command.Flags,
                cancellationToken);
        }

        private static ValueTask DisposeReaderAsync(DbDataReader reader)
        {
            if (reader is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            reader.Dispose();
            return default;
        }
    }
}
