using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Dommel.Resolvers;
using Dapper.FluentMap.Mapping;
using global::Dommel;

namespace Dapper.FluentMap.Dommel
{
    /// <summary>
    /// Provides Dommel-generated write operations that execute FluentMap property write converters.
    /// </summary>
    public static class DommelWriteExtensions
    {
        private static readonly ConcurrentDictionary<PropertyConverterMetadata, Func<object, object>> WriteConverterInvokers =
            new ConcurrentDictionary<PropertyConverterMetadata, Func<object, object>>();

        /// <summary>
        /// Inserts an entity using Dommel mapping and persistence metadata, applying configured property write converters.
        /// </summary>
        /// <typeparam name="TEntity">The mapped entity type.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="entity">The entity to insert.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <returns>The scalar value returned by the provider-specific Dommel insert statement.</returns>
        public static object InsertMapped<TEntity>(
            this IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction = null)
            where TEntity : class
        {
            var command = CreateInsertCommand(connection, entity, transaction, CancellationToken.None);
            return connection.ExecuteScalar(command);
        }

        /// <summary>
        /// Asynchronously inserts an entity using Dommel mapping and persistence metadata, applying configured property write converters.
        /// </summary>
        public static Task<object> InsertMappedAsync<TEntity>(
            this IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var command = CreateInsertCommand(connection, entity, transaction, cancellationToken);
            return connection.ExecuteScalarAsync<object>(command);
        }

        /// <summary>
        /// Updates an entity using Dommel mapping and persistence metadata, applying configured property write converters.
        /// </summary>
        /// <returns><see langword="true"/> when at least one row was updated.</returns>
        public static bool UpdateMapped<TEntity>(
            this IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction = null)
            where TEntity : class
        {
            var command = CreateUpdateCommand(connection, entity, transaction, CancellationToken.None);
            return connection.Execute(command) > 0;
        }

        /// <summary>
        /// Asynchronously updates an entity using Dommel mapping and persistence metadata, applying configured property write converters.
        /// </summary>
        /// <returns><see langword="true"/> when at least one row was updated.</returns>
        public static async Task<bool> UpdateMappedAsync<TEntity>(
            this IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var command = CreateUpdateCommand(connection, entity, transaction, cancellationToken);
            return await connection.ExecuteAsync(command).ConfigureAwait(false) > 0;
        }

        private static CommandDefinition CreateInsertCommand<TEntity>(
            IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            EnsureArguments(connection, entity);
            var type = typeof(TEntity);
            var sqlBuilder = DommelMapper.GetSqlBuilder(connection);
            var keyProperties = new DommelKeyPropertyResolver().ResolveKeyProperties(type);
            var generatedKeys = new HashSet<PropertyInfo>(
                keyProperties.Where(key => key.IsGenerated).Select(key => key.Property));
            var properties = (DommelPersistenceMetadata.ResolveInsertProperties(type) ??
                    new DommelPropertyResolver().ResolveProperties(type).Select(property => property.Property))
                .Where(property => !generatedKeys.Contains(property))
                .ToArray();
            var tableName = global::Dommel.Resolvers.Table(type, sqlBuilder);
            var columnNames = properties
                .Select(property => global::Dommel.Resolvers.Column(property, sqlBuilder, false))
                .ToArray();
            var parameterNames = properties.Select(property => sqlBuilder.PrefixParameter(property.Name)).ToArray();
            var sql = sqlBuilder.BuildInsert(type, tableName, columnNames, parameterNames);
            var parameters = CreateParameters(type, entity, properties, "INSERT");
            DommelMapper.LogReceived?.Invoke(sql);
            return new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        }

        private static CommandDefinition CreateUpdateCommand<TEntity>(
            IDbConnection connection,
            TEntity entity,
            IDbTransaction transaction,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            EnsureArguments(connection, entity);
            var type = typeof(TEntity);
            var sqlBuilder = DommelMapper.GetSqlBuilder(connection);
            var keyProperties = new DommelKeyPropertyResolver().ResolveKeyProperties(type);
            var keySet = new HashSet<PropertyInfo>(keyProperties.Select(key => key.Property));
            var updateProperties = new DommelPropertyResolver()
                .ResolveProperties(type)
                .Where(property => !property.IsGenerated && !keySet.Contains(property.Property))
                .Select(property => property.Property)
                .ToArray();
            var tableName = global::Dommel.Resolvers.Table(type, sqlBuilder);
            var assignments = updateProperties.Select(property =>
                global::Dommel.Resolvers.Column(property, sqlBuilder, false) + " = " +
                sqlBuilder.PrefixParameter(property.Name));
            var predicates = keyProperties.Select(key =>
                global::Dommel.Resolvers.Column(key.Property, sqlBuilder, false) + " = " +
                sqlBuilder.PrefixParameter(key.Property.Name));
            var sql = "update " + tableName + " set " + string.Join(", ", assignments) +
                      " where " + string.Join(" and ", predicates);
            var parameterProperties = updateProperties.Concat(keyProperties.Select(key => key.Property)).Distinct().ToArray();
            var parameters = CreateParameters(type, entity, parameterProperties, "UPDATE");
            DommelMapper.LogReceived?.Invoke(sql);
            return new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        }

        private static DynamicParameters CreateParameters<TEntity>(
            Type entityType,
            TEntity entity,
            IEnumerable<PropertyInfo> properties,
            string operation)
            where TEntity : class
        {
            var parameters = new DynamicParameters();
            FluentMapper.EntityMaps.TryGetValue(entityType, out var entityMap);

            foreach (var property in properties)
            {
                var value = property.GetValue(entity, null);
                var propertyMap = entityMap == null
                    ? null
                    : DommelPersistenceMetadata.ResolvePropertyMap(entityType, entityMap, property.Name);
                var conversion = propertyMap == null
                    ? PropertyConversionMetadata.Default
                    : PropertyMapConversion.GetConversion(propertyMap);

                if (conversion.HasWriteConverter && value != null)
                {
                    try
                    {
                        value = InvokeWriteConverter(conversion.WriteConverter, value);
                    }
                    catch (Exception exception)
                    {
                        throw new FluentMapConfigurationException(
                            $"Write converter failed during Dommel {operation} for entity '{entityType.FullName}', property '{property.Name}', column '{propertyMap.ColumnName}', converter '{conversion.WriteConverter.ConverterType.FullName}'. The original property value was not written.",
                            exception);
                    }
                }

                parameters.Add(property.Name, value);
            }

            return parameters;
        }

        private static object InvokeWriteConverter(PropertyConverterMetadata converter, object value)
        {
            return WriteConverterInvokers.GetOrAdd(converter, CreateWriteConverterInvoker)(value);
        }

        private static Func<object, object> CreateWriteConverterInvoker(PropertyConverterMetadata converter)
        {
            var input = Expression.Parameter(typeof(object), "value");
            var converterValue = Expression.Constant(converter.Converter);
            Expression call;

            if (converter.Converter is Delegate)
            {
                call = Expression.Invoke(
                    Expression.Convert(converterValue, converter.Converter.GetType()),
                    Expression.Convert(input, converter.PropertyType));
            }
            else
            {
                var interfaceType = typeof(IWritePropertyConverter<,>).MakeGenericType(
                    converter.PropertyType,
                    converter.DatabaseType);
                call = Expression.Call(
                    Expression.Convert(converterValue, interfaceType),
                    interfaceType.GetMethod(nameof(IWritePropertyConverter<object, object>.ConvertToDatabase)),
                    Expression.Convert(input, converter.PropertyType));
            }

            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(call, typeof(object)),
                input).Compile();
        }

        private static void EnsureArguments<TEntity>(IDbConnection connection, TEntity entity)
            where TEntity : class
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
        }
    }
}
