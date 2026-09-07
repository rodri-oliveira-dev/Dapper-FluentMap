using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Mapping;
using Dommel;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Dommel.Tests
{
    public class DommelPersistenceIntegrationTests
    {
        [Fact]
        public void InsertUpdateAndSelectShouldHonorPropertyPersistenceMetadata()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(config =>
            {
                config.AddMap(new PersistenceBaseEntityMap());
                config.AddMap(new PersistenceEntityMap());
                config.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE persistence_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    normal TEXT NOT NULL,
    ignored TEXT DEFAULT 'ignored-default',
    read_only TEXT DEFAULT 'read-only-default',
    insert_excluded TEXT DEFAULT 'insert-excluded-default',
    update_excluded TEXT DEFAULT 'update-excluded-default',
    default_value TEXT DEFAULT 'default-value-default',
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    inherited_value TEXT DEFAULT 'inherited-default',
    computed TEXT GENERATED ALWAYS AS (normal || '-computed') STORED
);");

                var logs = CaptureDommelLogs();
                try
                {
                    var entity = new PersistenceEntity
                    {
                        Normal = "normal-insert",
                        Ignored = "ignored-insert",
                        ReadOnly = "read-only-insert",
                        InsertExcluded = "insert-excluded-insert",
                        UpdateExcluded = "update-excluded-insert",
                        DefaultValue = "default-value-insert",
                        CreatedAt = new DateTime(2000, 1, 1),
                        InheritedValue = "inherited-insert",
                        Computed = "computed-insert"
                    };

                    var id = Convert.ToInt32(connection.Insert(entity));
                    var inserted = SelectPersistenceRow(connection, id);

                    Assert.Equal("normal-insert", inserted.Normal);
                    Assert.Equal("ignored-default", inserted.Ignored);
                    Assert.Equal("read-only-default", inserted.ReadOnly);
                    Assert.Equal("insert-excluded-default", inserted.InsertExcluded);
                    Assert.Equal("update-excluded-insert", inserted.UpdateExcluded);
                    Assert.Equal("default-value-default", inserted.DefaultValue);
                    Assert.NotEqual(new DateTime(2000, 1, 1), inserted.CreatedAt);
                    Assert.Equal("inherited-default", inserted.InheritedValue);
                    Assert.Equal("normal-insert-computed", inserted.Computed);

                    var insertSql = logs.Last(log => log.IndexOf("insert into", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(insertSql, "normal", "update_excluded");
                    AssertSqlDoesNotContain(insertSql, "\"id\"", "ignored", "read_only", "insert_excluded", "default_value", "created_at", "inherited_value", "computed");

                    entity.Id = id;
                    entity.Normal = "normal-update";
                    entity.Ignored = "ignored-update";
                    entity.ReadOnly = "read-only-update";
                    entity.InsertExcluded = "insert-excluded-update";
                    entity.UpdateExcluded = "update-excluded-update";
                    entity.DefaultValue = "default-value-update";
                    entity.CreatedAt = new DateTime(2001, 2, 3, 4, 5, 6);
                    entity.InheritedValue = "inherited-update";
                    entity.Computed = "computed-update";

                    Assert.True(connection.Update(entity));

                    var updated = SelectPersistenceRow(connection, id);
                    Assert.Equal("normal-update", updated.Normal);
                    Assert.Equal("ignored-default", updated.Ignored);
                    Assert.Equal("read-only-default", updated.ReadOnly);
                    Assert.Equal("insert-excluded-update", updated.InsertExcluded);
                    Assert.Equal("update-excluded-insert", updated.UpdateExcluded);
                    Assert.Equal("default-value-update", updated.DefaultValue);
                    Assert.Equal(new DateTime(2001, 2, 3, 4, 5, 6), updated.CreatedAt);
                    Assert.Equal("inherited-update", updated.InheritedValue);
                    Assert.Equal("normal-update-computed", updated.Computed);

                    var updateSql = logs.Last(log => log.IndexOf("update ", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(updateSql, "normal", "insert_excluded", "default_value", "created_at", "inherited_value");
                    AssertSqlDoesNotContain(updateSql, "ignored", "read_only", "update_excluded", "computed");
                    Assert.DoesNotContain("set \"id\"", updateSql, StringComparison.OrdinalIgnoreCase);

                    var loaded = connection.Get<PersistenceEntity>(id);
                    Assert.Equal("read-only-default", loaded.ReadOnly);
                    Assert.Equal("insert-excluded-update", loaded.InsertExcluded);
                    Assert.Equal("default-value-update", loaded.DefaultValue);
                    Assert.Equal(new DateTime(2001, 2, 3, 4, 5, 6), loaded.CreatedAt);
                    Assert.Equal("normal-update-computed", loaded.Computed);
                    Assert.Null(loaded.Ignored);
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        public void NonIdentityKeyShouldParticipateInInsertAndStayOutOfUpdateSet()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(config =>
            {
                config.AddMap(new AssignedKeyEntityMap());
                config.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE assigned_key_entities (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    update_excluded TEXT
);");

                var logs = CaptureDommelLogs();
                try
                {
                    var entity = new AssignedKeyEntity
                    {
                        Code = "A-001",
                        Name = "inserted",
                        UpdateExcluded = "insert-write"
                    };

                    connection.Insert(entity);

                    var inserted = connection.QuerySingle<AssignedKeyRow>(
                        "SELECT code AS Code, name AS Name, update_excluded AS UpdateExcluded FROM assigned_key_entities WHERE code = 'A-001';");
                    Assert.Equal("A-001", inserted.Code);
                    Assert.Equal("inserted", inserted.Name);
                    Assert.Equal("insert-write", inserted.UpdateExcluded);

                    var insertSql = logs.Last(log => log.IndexOf("insert into", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(insertSql, "code", "name", "update_excluded");

                    entity.Name = "updated";
                    entity.UpdateExcluded = "update-write";
                    Assert.True(connection.Update(entity));

                    var updated = connection.QuerySingle<AssignedKeyRow>(
                        "SELECT code AS Code, name AS Name, update_excluded AS UpdateExcluded FROM assigned_key_entities WHERE code = 'A-001';");
                    Assert.Equal("A-001", updated.Code);
                    Assert.Equal("updated", updated.Name);
                    Assert.Equal("insert-write", updated.UpdateExcluded);

                    var updateSql = logs.Last(log => log.IndexOf("update ", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(updateSql, "name", "where", "code");
                    Assert.DoesNotContain("set \"code\"", updateSql, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("update_excluded", updateSql, StringComparison.OrdinalIgnoreCase);
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        public void CompositeNonIdentityKeyShouldParticipateInInsertAndStayOutOfUpdateSet()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(config =>
            {
                config.AddMap(new CompositePersistenceEntityMap());
                config.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE composite_persistence_entities (
    key_part_one INTEGER NOT NULL,
    key_part_two INTEGER NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (key_part_one, key_part_two)
);");

                var logs = CaptureDommelLogs();
                try
                {
                    var entity = new CompositePersistenceEntity
                    {
                        KeyPartOne = 10,
                        KeyPartTwo = 20,
                        Value = "inserted"
                    };

                    connection.Insert(entity);

                    var inserted = connection.QuerySingle<string>(
                        "SELECT value FROM composite_persistence_entities WHERE key_part_one = 10 AND key_part_two = 20;");
                    Assert.Equal("inserted", inserted);

                    var insertSql = logs.Last(log => log.IndexOf("insert into", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(insertSql, "key_part_one", "key_part_two", "value");

                    entity.Value = "updated";
                    Assert.True(connection.Update(entity));

                    var updated = connection.QuerySingle<string>(
                        "SELECT value FROM composite_persistence_entities WHERE key_part_one = 10 AND key_part_two = 20;");
                    Assert.Equal("updated", updated);

                    var updateSql = logs.Last(log => log.IndexOf("update ", StringComparison.OrdinalIgnoreCase) >= 0);
                    AssertSqlContains(updateSql, "value", "where", "key_part_one", "key_part_two");
                    Assert.DoesNotContain("set \"key_part_one\"", updateSql, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("set \"key_part_two\"", updateSql, StringComparison.OrdinalIgnoreCase);
                }
                finally
                {
                    DommelMapper.LogReceived = null;
                }
            }
        }

        [Fact]
        public void RepeatedOperationsForDifferentEntitiesShouldKeepIndependentPersistenceMetadata()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(config =>
            {
                config.AddMap(new AssignedKeyEntityMap());
                config.AddMap(new CompositePersistenceEntityMap());
                config.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE assigned_key_entities (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    update_excluded TEXT
);
CREATE TABLE composite_persistence_entities (
    key_part_one INTEGER NOT NULL,
    key_part_two INTEGER NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (key_part_one, key_part_two)
);");

                connection.Insert(new AssignedKeyEntity
                {
                    Code = "A-002",
                    Name = "assigned",
                    UpdateExcluded = "assigned-excluded"
                });

                connection.Insert(new CompositePersistenceEntity
                {
                    KeyPartOne = 30,
                    KeyPartTwo = 40,
                    Value = "composite"
                });

                Assert.Equal("assigned", connection.QuerySingle<string>("SELECT name FROM assigned_key_entities WHERE code = 'A-002';"));
                Assert.Equal("composite", connection.QuerySingle<string>("SELECT value FROM composite_persistence_entities WHERE key_part_one = 30 AND key_part_two = 40;"));
            }
        }

        [Fact]
        public void InsertAndUpdateShouldNotExecuteWriteConvertersWithoutDommelParameterHook()
        {
            PreTest();
            SQLitePCL.Batteries_V2.Init();

            FluentMapper.Initialize(config =>
            {
                config.AddMap(new WriteConversionBoundaryEntityMap());
                config.ForDommel();
            });

            using (var connection = OpenConnection())
            {
                connection.Execute(@"
CREATE TABLE write_conversion_boundary_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL
);");

                var entity = new WriteConversionBoundaryEntity
                {
                    Code = "original-insert"
                };

                var id = Convert.ToInt32(connection.Insert(entity));
                var inserted = connection.QuerySingle<string>(
                    "SELECT code FROM write_conversion_boundary_entities WHERE id = @id;",
                    new { id });

                Assert.Equal("original-insert", inserted);

                entity.Id = id;
                entity.Code = "original-update";

                Assert.True(connection.Update(entity));

                var updated = connection.QuerySingle<string>(
                    "SELECT code FROM write_conversion_boundary_entities WHERE id = @id;",
                    new { id });

                Assert.Equal("original-update", updated);
            }
        }

        private static void PreTest()
        {
            FluentMapper.EntityMaps.Clear();
            FluentMapper.TypeConventions.Clear();
            FluentMapper.Initialize(_ => { });
            DommelMapper.LogReceived = null;
        }

        private static List<string> CaptureDommelLogs()
        {
            var logs = new List<string>();
            DommelMapper.LogReceived = logs.Add;
            return logs;
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static PersistenceRow SelectPersistenceRow(SqliteConnection connection, int id)
        {
            return connection.QuerySingle<PersistenceRow>(@"
SELECT
    id AS Id,
    normal AS Normal,
    ignored AS Ignored,
    read_only AS ReadOnly,
    insert_excluded AS InsertExcluded,
    update_excluded AS UpdateExcluded,
    default_value AS DefaultValue,
    created_at AS CreatedAt,
    inherited_value AS InheritedValue,
    computed AS Computed
FROM persistence_entities
WHERE id = @id;", new { id });
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

        private class PersistenceBaseEntity
        {
            public string InheritedValue { get; set; }
        }

        private sealed class PersistenceEntity : PersistenceBaseEntity
        {
            public int Id { get; set; }

            public string Normal { get; set; }

            public string Ignored { get; set; }

            public string ReadOnly { get; set; }

            public string InsertExcluded { get; set; }

            public string UpdateExcluded { get; set; }

            public string DefaultValue { get; set; }

            public DateTime CreatedAt { get; set; }

            public string Computed { get; set; }
        }

        private sealed class PersistenceBaseEntityMap : DommelEntityMap<PersistenceBaseEntity>
        {
            public PersistenceBaseEntityMap()
            {
                Map(entity => entity.InheritedValue).ToColumn("inherited_value").DatabaseDefaultOnInsert();
            }
        }

        private sealed class PersistenceEntityMap : DommelEntityMap<PersistenceEntity>
        {
            public PersistenceEntityMap()
            {
                ToTable("persistence_entities");
                IncludeBase<PersistenceBaseEntity>();
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Normal).ToColumn("normal");
                Map(entity => entity.Ignored).ToColumn("ignored").Ignore();
                Map(entity => entity.ReadOnly).ToColumn("read_only").ReadOnly();
                Map(entity => entity.InsertExcluded).ToColumn("insert_excluded").ExcludeFromInsert();
                Map(entity => entity.UpdateExcluded).ToColumn("update_excluded").ExcludeFromUpdate();
                Map(entity => entity.DefaultValue).ToColumn("default_value").DatabaseDefaultOnInsert();
                Map(entity => entity.CreatedAt).ToColumn("created_at").DatabaseDefaultOnInsert();
                Map(entity => entity.Computed).ToColumn("computed").Computed();
            }
        }

        private sealed class PersistenceRow
        {
            public int Id { get; set; }

            public string Normal { get; set; }

            public string Ignored { get; set; }

            public string ReadOnly { get; set; }

            public string InsertExcluded { get; set; }

            public string UpdateExcluded { get; set; }

            public string DefaultValue { get; set; }

            public DateTime CreatedAt { get; set; }

            public string InheritedValue { get; set; }

            public string Computed { get; set; }
        }

        private sealed class AssignedKeyEntity
        {
            public string Code { get; set; }

            public string Name { get; set; }

            public string UpdateExcluded { get; set; }
        }

        private sealed class AssignedKeyEntityMap : DommelEntityMap<AssignedKeyEntity>
        {
            public AssignedKeyEntityMap()
            {
                ToTable("assigned_key_entities");
                Map(entity => entity.Code).ToColumn("code").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(entity => entity.Name).ToColumn("name");
                Map(entity => entity.UpdateExcluded).ToColumn("update_excluded").ExcludeFromUpdate();
            }
        }

        private sealed class AssignedKeyRow
        {
            public string Code { get; set; }

            public string Name { get; set; }

            public string UpdateExcluded { get; set; }
        }

        private sealed class CompositePersistenceEntity
        {
            public int KeyPartOne { get; set; }

            public int KeyPartTwo { get; set; }

            public string Value { get; set; }
        }

        private sealed class CompositePersistenceEntityMap : DommelEntityMap<CompositePersistenceEntity>
        {
            public CompositePersistenceEntityMap()
            {
                ToTable("composite_persistence_entities");
                Map(entity => entity.KeyPartOne).ToColumn("key_part_one").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(entity => entity.KeyPartTwo).ToColumn("key_part_two").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(entity => entity.Value).ToColumn("value");
            }
        }

        private sealed class WriteConversionBoundaryEntity
        {
            public int Id { get; set; }

            public string Code { get; set; }
        }

        private sealed class WriteConversionBoundaryEntityMap : DommelEntityMap<WriteConversionBoundaryEntity>
        {
            public WriteConversionBoundaryEntityMap()
            {
                ToTable("write_conversion_boundary_entities");
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Code)
                    .ToColumn("code")
                    .ConvertToDatabaseUsing<ThrowingWriteConverter, string>();
            }
        }

        private sealed class ThrowingWriteConverter : IWritePropertyConverter<string, string>
        {
            public string ConvertToDatabase(string value)
            {
                throw new InvalidOperationException("Dommel does not execute property write converters in this stage.");
            }
        }
    }
}
