using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Dommel.Mapping;
using Dommel;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Dommel.Tests.HistoricalRegression
{
    public class DommelHistoricalRegressionTests
    {
        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void ReadOnlyPropertyShouldBeMaterializedButExcludedFromWrites()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(configuration =>
            {
                configuration.AddMap(new ReadOnlyEntityMap());
                configuration.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE historical_readonly_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    read_only_value TEXT DEFAULT 'server-read'
);");

                var logs = CaptureDommelLogs();
                try
                {
                    // Historical issue #94.
                    var entity = new ReadOnlyEntity
                    {
                        Name = "inserted",
                        ReadOnlyValue = "client-insert"
                    };

                    var id = Convert.ToInt32(connection.Insert(entity));
                    var inserted = connection.Get<ReadOnlyEntity>(id);

                    Assert.Equal("inserted", inserted.Name);
                    Assert.Equal("server-read", inserted.ReadOnlyValue);

                    var insertSql = LastSql(logs, "insert into");
                    AssertSqlContains(insertSql, "name");
                    AssertSqlDoesNotContain(insertSql, "read_only_value");

                    entity.Id = id;
                    entity.Name = "updated";
                    entity.ReadOnlyValue = "client-update";

                    Assert.True(connection.Update(entity));

                    var updated = connection.Get<ReadOnlyEntity>(id);
                    Assert.Equal("updated", updated.Name);
                    Assert.Equal("server-read", updated.ReadOnlyValue);

                    var updateSql = LastSql(logs, "update ");
                    AssertSqlContains(updateSql, "name", "where", "id");
                    AssertSqlDoesNotContain(updateSql, "read_only_value");
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void NonIdentityKeyShouldBeInsertedAndOnlyUsedForUpdateWhereClause()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(configuration =>
            {
                configuration.AddMap(new NonIdentityKeyEntityMap());
                configuration.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE historical_assigned_key_entities (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL
);");

                var logs = CaptureDommelLogs();
                try
                {
                    // Historical issue #122.
                    var entity = new NonIdentityKeyEntity
                    {
                        Code = "A-001",
                        Name = "inserted"
                    };

                    connection.Insert(entity);

                    var inserted = connection.QuerySingle<string>(
                        "SELECT name FROM historical_assigned_key_entities WHERE code = 'A-001';");

                    Assert.Equal("inserted", inserted);

                    var insertSql = LastSql(logs, "insert into");
                    AssertSqlContains(insertSql, "code", "name");

                    entity.Name = "updated";
                    Assert.True(connection.Update(entity));

                    var updated = connection.QuerySingle<string>(
                        "SELECT name FROM historical_assigned_key_entities WHERE code = 'A-001';");

                    Assert.Equal("updated", updated);

                    var updateSql = LastSql(logs, "update ");
                    AssertSqlContains(updateSql, "name", "where", "code");
                    Assert.DoesNotContain("set \"code\"", updateSql, StringComparison.OrdinalIgnoreCase);
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void ComputedPropertyShouldBeReadButExcludedFromInsertAndUpdate()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(configuration =>
            {
                configuration.AddMap(new ComputedEntityMap());
                configuration.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE historical_computed_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    computed_value TEXT GENERATED ALWAYS AS (name || '-computed') STORED
);");

                var logs = CaptureDommelLogs();
                try
                {
                    // Historical issue #123.
                    var entity = new ComputedEntity
                    {
                        Name = "inserted",
                        ComputedValue = "client-computed"
                    };

                    var id = Convert.ToInt32(connection.Insert(entity));
                    var inserted = connection.Get<ComputedEntity>(id);

                    Assert.Equal("inserted-computed", inserted.ComputedValue);

                    var insertSql = LastSql(logs, "insert into");
                    AssertSqlContains(insertSql, "name");
                    AssertSqlDoesNotContain(insertSql, "computed_value");

                    entity.Id = id;
                    entity.Name = "updated";
                    entity.ComputedValue = "client-update";

                    Assert.True(connection.Update(entity));

                    var updated = connection.Get<ComputedEntity>(id);
                    Assert.Equal("updated-computed", updated.ComputedValue);

                    var updateSql = LastSql(logs, "update ");
                    AssertSqlContains(updateSql, "name");
                    AssertSqlDoesNotContain(updateSql, "computed_value");
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void DatabaseDefaultOnInsertShouldOmitInsertColumnAndReadDatabaseValue()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(configuration =>
            {
                configuration.AddMap(new DefaultValueEntityMap());
                configuration.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE historical_default_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    created_at TEXT DEFAULT '2026-07-28 09:30:00'
);");

                var logs = CaptureDommelLogs();
                try
                {
                    // Historical issue #130.
                    var entity = new DefaultValueEntity
                    {
                        Name = "inserted",
                        CreatedAt = new DateTime(2000, 1, 1)
                    };

                    var id = Convert.ToInt32(connection.Insert(entity));
                    var loaded = connection.Get<DefaultValueEntity>(id);

                    Assert.Equal("inserted", loaded.Name);
                    Assert.Equal(new DateTime(2026, 7, 28, 9, 30, 0), loaded.CreatedAt);
                    Assert.NotEqual(default, loaded.CreatedAt);

                    var insertSql = LastSql(logs, "insert into");
                    AssertSqlContains(insertSql, "name");
                    AssertSqlDoesNotContain(insertSql, "created_at");
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        private static void PreTest()
        {
            FluentMapper.EntityMaps.Clear();
            FluentMapper.TypeConventions.Clear();
            FluentMapper.Initialize(_ => { });
            DommelMapper.LogReceived = null;
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static List<string> CaptureDommelLogs()
        {
            var logs = new List<string>();
            DommelMapper.LogReceived = logs.Add;
            return logs;
        }

        private static string LastSql(List<string> logs, string fragment)
        {
            return logs.Last(log => log.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AssertSqlContains(string sql, params string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                Assert.Contains(fragment, sql, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void AssertSqlDoesNotContain(string sql, params string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                Assert.DoesNotContain(fragment, sql, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class ReadOnlyEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public string ReadOnlyValue { get; set; }
        }

        private sealed class ReadOnlyEntityMap : DommelEntityMap<ReadOnlyEntity>
        {
            public ReadOnlyEntityMap()
            {
                ToTable("historical_readonly_entities");
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Name).ToColumn("name");
                Map(entity => entity.ReadOnlyValue).ToColumn("read_only_value").ReadOnly();
            }
        }

        private sealed class NonIdentityKeyEntity
        {
            public string Code { get; set; }

            public string Name { get; set; }
        }

        private sealed class NonIdentityKeyEntityMap : DommelEntityMap<NonIdentityKeyEntity>
        {
            public NonIdentityKeyEntityMap()
            {
                ToTable("historical_assigned_key_entities");
                Map(entity => entity.Code).ToColumn("code").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(entity => entity.Name).ToColumn("name");
            }
        }

        private sealed class ComputedEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public string ComputedValue { get; set; }
        }

        private sealed class ComputedEntityMap : DommelEntityMap<ComputedEntity>
        {
            public ComputedEntityMap()
            {
                ToTable("historical_computed_entities");
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Name).ToColumn("name");
                Map(entity => entity.ComputedValue).ToColumn("computed_value").SetGeneratedOption(DatabaseGeneratedOption.Computed);
            }
        }

        private sealed class DefaultValueEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public DateTime CreatedAt { get; set; }
        }

        private sealed class DefaultValueEntityMap : DommelEntityMap<DefaultValueEntity>
        {
            public DefaultValueEntityMap()
            {
                ToTable("historical_default_entities");
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Name).ToColumn("name");
                Map(entity => entity.CreatedAt).ToColumn("created_at").DatabaseDefaultOnInsert();
            }
        }
    }
}
