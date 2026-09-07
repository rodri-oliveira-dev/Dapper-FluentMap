using System;
using System.Linq;
using Dommel;

namespace Dapper.FluentMap.Dommel.Resolvers
{
    internal sealed class DommelPersistenceSqlBuilder : ISqlBuilder
    {
        private readonly ISqlBuilder inner;

        private DommelPersistenceSqlBuilder(ISqlBuilder inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal static void RegisterDefaults()
        {
            DommelMapper.AddSqlBuilder("sqlconnection", new DommelPersistenceSqlBuilder(new SqlServerSqlBuilder()));
            DommelMapper.AddSqlBuilder("sqlceconnection", new DommelPersistenceSqlBuilder(new SqlServerCeSqlBuilder()));
            DommelMapper.AddSqlBuilder("sqliteconnection", new DommelPersistenceSqlBuilder(new SqliteSqlBuilder()));
            DommelMapper.AddSqlBuilder("npgsqlconnection", new DommelPersistenceSqlBuilder(new PostgresSqlBuilder()));
            DommelMapper.AddSqlBuilder("mysqlconnection", new DommelPersistenceSqlBuilder(new MySqlSqlBuilder()));
        }

        public string PrefixParameter(string paramName)
        {
            return inner.PrefixParameter(paramName);
        }

        public string BuildInsert(Type type, string tableName, string[] columnNames, string[] paramNames)
        {
            var insertProperties = DommelPersistenceMetadata.ResolveInsertProperties(type);
            if (insertProperties == null)
            {
                return inner.BuildInsert(type, tableName, columnNames, paramNames);
            }

            var properties = insertProperties.ToArray();
            var persistenceColumnNames = properties.Select(property => global::Dommel.Resolvers.Column(property, this, false)).ToArray();
            var persistenceParamNames = properties.Select(property => PrefixParameter(property.Name)).ToArray();
            return inner.BuildInsert(type, tableName, persistenceColumnNames, persistenceParamNames);
        }

        public string BuildPaging(string orderBy, int pageNumber, int pageSize)
        {
            return inner.BuildPaging(orderBy, pageNumber, pageSize);
        }

        public string QuoteIdentifier(string identifier)
        {
            return inner.QuoteIdentifier(identifier);
        }

        public string LimitClause(int count)
        {
            return inner.LimitClause(count);
        }

        public string LikeExpression(string columnName, string parameterName)
        {
            return inner.LikeExpression(columnName, parameterName);
        }
    }
}
