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
        /// Executes a query, splits each row into three FluentMap-controlled segments and composes the return value.
        /// </summary>
        /// <remarks>
        /// <paramref name="splitOn"/> must contain two comma-separated, unique column names in result order.
        /// An all-database-null second or third segment is passed to <paramref name="map"/> as <see langword="null"/>.
        /// This API composes one row at a time and does not aggregate one-to-many graphs.
        /// </remarks>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn>(
            this IDbConnection connection,
            string sql,
            Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            string splitOn = null)
            where TFirst : class
            where TSecond : class
            where TThird : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMapped<TFirst, TSecond, TThird, TReturn>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                map,
                splitOn);
        }

        /// <summary>
        /// Executes a command, splits each row into three FluentMap-controlled segments and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TThird, TReturn> map,
            string splitOn = null)
            where TFirst : class
            where TSecond : class
            where TThird : class
        {
            return ExecuteMapped<TFirst, TSecond, TThird, TReturn>(
                connection, command, map, splitOn, null, null, null, FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a query and materializes three segments with an explicit mapping profile per segment.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn,
            TFirstProfile,
            TSecondProfile,
            TThirdProfile>(
            this IDbConnection connection,
            string sql,
            Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            string splitOn = null)
            where TFirst : class
            where TSecond : class
            where TThird : class
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
            where TThirdProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return ExecuteMapped<TFirst, TSecond, TThird, TReturn>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                map,
                splitOn,
                typeof(TFirstProfile),
                typeof(TSecondProfile),
                typeof(TThirdProfile),
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a command and materializes three segments with an explicit mapping profile per segment.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn,
            TFirstProfile,
            TSecondProfile,
            TThirdProfile>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TThird, TReturn> map,
            string splitOn)
            where TFirst : class
            where TSecond : class
            where TThird : class
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
            where TThirdProfile : IMappingProfile
        {
            return ExecuteMapped<TFirst, TSecond, TThird, TReturn>(
                connection,
                command,
                map,
                splitOn,
                typeof(TFirstProfile),
                typeof(TSecondProfile),
                typeof(TThirdProfile),
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Asynchronously executes a query, materializes three FluentMap-controlled segments and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TReturn>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn>(
            this IDbConnection connection,
            string sql,
            Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            string splitOn = null,
            CancellationToken cancellationToken = default)
            where TFirst : class
            where TSecond : class
            where TThird : class
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegmentsAsync(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.Buffered, cancellationToken),
                new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) },
                new Type[] { null, null, null },
                values => map((TFirst)values[0], (TSecond)values[1], (TThird)values[2]),
                splitOn,
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Asynchronously executes a command, materializes three FluentMap-controlled segments and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TReturn>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TThird, TReturn> map,
            string splitOn)
            where TFirst : class
            where TSecond : class
            where TThird : class
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegmentsAsync(
                connection,
                command,
                new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) },
                new Type[] { null, null, null },
                values => map((TFirst)values[0], (TSecond)values[1], (TThird)values[2]),
                splitOn,
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Asynchronously executes a query and materializes three segments with an explicit mapping profile per segment.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TReturn>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn,
            TFirstProfile,
            TSecondProfile,
            TThirdProfile>(
            this IDbConnection connection,
            string sql,
            Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            string splitOn = null,
            CancellationToken cancellationToken = default)
            where TFirst : class
            where TSecond : class
            where TThird : class
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
            where TThirdProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegmentsAsync(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType, CommandFlags.Buffered, cancellationToken),
                new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) },
                new[] { typeof(TFirstProfile), typeof(TSecondProfile), typeof(TThirdProfile) },
                values => map((TFirst)values[0], (TSecond)values[1], (TThird)values[2]),
                splitOn,
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Asynchronously executes a command and materializes three segments with an explicit mapping profile per segment.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static Task<IEnumerable<TReturn>> QueryMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn,
            TFirstProfile,
            TSecondProfile,
            TThirdProfile>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TThird, TReturn> map,
            string splitOn)
            where TFirst : class
            where TSecond : class
            where TThird : class
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
            where TThirdProfile : IMappingProfile
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegmentsAsync(
                connection,
                command,
                new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) },
                new[] { typeof(TFirstProfile), typeof(TSecondProfile), typeof(TThirdProfile) },
                values => map((TFirst)values[0], (TSecond)values[1], (TThird)values[2]),
                splitOn,
                FluentMapper.Runtime);
        }

        /// <summary>
        /// Executes a query, splits each row at <paramref name="splitOn"/>, materializes two profiled FluentMap-controlled segments and composes the return value.
        /// </summary>
        /// <typeparam name="TFirst">The entity type materialized from columns before the split boundary.</typeparam>
        /// <typeparam name="TSecond">The entity type materialized from columns starting at the split boundary.</typeparam>
        /// <typeparam name="TReturn">The projected return type.</typeparam>
        /// <typeparam name="TFirstProfile">The mapping profile marker type used for the first segment.</typeparam>
        /// <typeparam name="TSecondProfile">The mapping profile marker type used for the second segment.</typeparam>
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
            TReturn,
            TFirstProfile,
            TSecondProfile>(
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
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            return QueryMapped<TFirst, TSecond, TReturn, TFirstProfile, TSecondProfile>(
                connection,
                new CommandDefinition(sql, param, transaction, commandTimeout, commandType),
                map,
                splitOn);
        }

        /// <summary>
        /// Executes a command, splits each row at <paramref name="splitOn"/>, materializes two profiled FluentMap-controlled segments and composes the return value.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public static IEnumerable<TReturn> QueryMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TSecond,
            TReturn,
            TFirstProfile,
            TSecondProfile>(
            this IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TReturn> map,
            string splitOn = "Id")
            where TFirst : class
            where TSecond : class
            where TFirstProfile : IMappingProfile
            where TSecondProfile : IMappingProfile
        {
            return ExecuteMapped<TFirst, TSecond, TReturn>(
                connection,
                command,
                map,
                splitOn,
                firstProfileType: typeof(TFirstProfile),
                secondProfileType: typeof(TSecondProfile),
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
            GeneratedParameters parameters,
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

                    parameters?.AddParameters(command);

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
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegments(
                connection,
                command,
                new[] { typeof(TFirst), typeof(TSecond) },
                new[] { firstProfileType, secondProfileType },
                values => map((TFirst)values[0], (TSecond)values[1]),
                splitOn,
                runtime);
        }

        internal static IEnumerable<TReturn> ExecuteMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TFirst,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TSecond,
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)] TThird,
            TReturn>(
            IDbConnection connection,
            CommandDefinition command,
            Func<TFirst, TSecond, TThird, TReturn> map,
            string splitOn,
            Type firstProfileType,
            Type secondProfileType,
            Type thirdProfileType,
            FluentMapRuntime runtime)
            where TFirst : class
            where TSecond : class
            where TThird : class
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return ExecuteMappedSegments(
                connection,
                command,
                new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) },
                new[] { firstProfileType, secondProfileType, thirdProfileType },
                values => map((TFirst)values[0], (TSecond)values[1], (TThird)values[2]),
                splitOn,
                runtime);
        }

        private static IEnumerable<TReturn> ExecuteMappedSegments<TReturn>(
            IDbConnection connection,
            CommandDefinition command,
            Type[] entityTypes,
            Type[] profileTypes,
            Func<object[], TReturn> map,
            string splitOn,
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

            using (var reader = SqlMapper.ExecuteReader(connection, command))
            {
                var segments = CreateSegments(reader, splitOn, entityTypes.Length);
                var materializers = CreateSegmentMaterializers(segments, entityTypes, profileTypes, runtime);
                var results = new List<TReturn>();

                while (reader.Read())
                {
                    results.Add(map(MaterializeSegments(segments, materializers)));
                }

                return results;
            }
        }

        internal static async Task<IEnumerable<TReturn>> ExecuteMappedSegmentsAsync<TReturn>(
            IDbConnection connection,
            CommandDefinition command,
            Type[] entityTypes,
            Type[] profileTypes,
            Func<object[], TReturn> map,
            string splitOn,
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

            using (var reader = await SqlMapper.ExecuteReaderAsync(connection, command).ConfigureAwait(false))
            {
                var segments = CreateSegments(reader, splitOn, entityTypes.Length);
                var materializers = CreateSegmentMaterializers(segments, entityTypes, profileTypes, runtime);
                var results = new List<TReturn>();

                while (reader.Read())
                {
                    results.Add(map(MaterializeSegments(segments, materializers)));
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

        private static SegmentDataRecord[] CreateSegments(IDataRecord reader, string splitOn, int segmentCount)
        {
            if (string.IsNullOrWhiteSpace(splitOn))
            {
                throw new ArgumentException("One splitOn column name is required for each segment after the first.", nameof(splitOn));
            }

            var boundaries = splitOn.Split(',').Select(boundary => boundary.Trim()).ToArray();
            if (boundaries.Any(string.IsNullOrEmpty))
            {
                throw new ArgumentException("splitOn cannot contain an empty boundary.", nameof(splitOn));
            }

            if (boundaries.Length != segmentCount - 1)
            {
                throw new ArgumentException(
                    $"Multi-mapping with {segmentCount} segments requires exactly {segmentCount - 1} splitOn boundaries; {boundaries.Length} were provided.",
                    nameof(splitOn));
            }

            var duplicateBoundary = boundaries
                .GroupBy(boundary => boundary, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateBoundary != null)
            {
                throw new InvalidOperationException(
                    "The splitOn boundary '" + duplicateBoundary.Key + "' is duplicated and ambiguous.");
            }

            var indexes = new List<int> { 0 };
            foreach (var boundary in boundaries)
            {
                var matches = Enumerable.Range(0, reader.FieldCount)
                    .Where(index => string.Equals(reader.GetName(index), boundary, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (matches.Length == 0)
                {
                    throw new InvalidOperationException("The splitOn column '" + boundary + "' was not found in the result set.");
                }

                if (matches.Length > 1)
                {
                    throw new InvalidOperationException("The splitOn column '" + boundary + "' is ambiguous in the result set.");
                }

                if (matches[0] <= indexes[indexes.Count - 1])
                {
                    throw new InvalidOperationException(
                        "The splitOn column '" + boundary + "' is out of order or produced an empty row segment.");
                }

                indexes.Add(matches[0]);
            }

            indexes.Add(reader.FieldCount);
            return Enumerable.Range(0, segmentCount)
                .Select(index => new SegmentDataRecord(
                    reader,
                    indexes[index],
                    indexes[index + 1] - indexes[index]))
                .ToArray();
        }

        private static Func<IDataRecord, object>[] CreateSegmentMaterializers(
            SegmentDataRecord[] segments,
            Type[] entityTypes,
            Type[] profileTypes,
            FluentMapRuntime runtime)
        {
            return Enumerable.Range(0, segments.Length)
                .Select(index => MappedRowMaterializer.CreateMaterializer(
                    segments[index], entityTypes[index], profileTypes[index], runtime))
                .ToArray();
        }

        private static object[] MaterializeSegments(
            SegmentDataRecord[] segments,
            Func<IDataRecord, object>[] materializers)
        {
            var values = new object[segments.Length];
            for (var index = 0; index < segments.Length; index++)
            {
                values[index] = index > 0 && IsAllNull(segments[index])
                    ? null
                    : materializers[index](segments[index]);
            }

            return values;
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
