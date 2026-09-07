using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Dommel;
using Dapper.FluentMap.Dommel.Mapping;
using Dommel;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

FluentMapper.Initialize(configuration =>
{
    configuration.AddMap(new DommelSmokeEntityMap());
    configuration.AddMap(new AssignedKeyEntityMap());
    configuration.ForDommel();
});

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
connection.Execute("""
CREATE TABLE dommel_smoke_entities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    normal TEXT NOT NULL,
    ignored TEXT DEFAULT 'ignored-default',
    read_only TEXT DEFAULT 'read-only-default',
    database_default TEXT DEFAULT 'default-from-db',
    computed TEXT GENERATED ALWAYS AS (normal || '-computed') STORED
);

CREATE TABLE assigned_key_entities (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    update_excluded TEXT
);
""");

var identityEntity = new DommelSmokeEntity
{
    Normal = "inserted",
    Ignored = "ignored-write",
    ReadOnly = "read-only-write",
    DatabaseDefault = "default-write",
    Computed = "computed-write"
};

var id = Convert.ToInt32(connection.Insert(identityEntity));
if (id <= 0)
{
    throw new InvalidOperationException("Dommel identity insert did not return a generated key.");
}

identityEntity.Id = id;
var inserted = SelectDommelSmokeRow(connection, id);
AssertEqual("inserted", inserted.Normal, "insert normal");
AssertEqual("ignored-default", inserted.Ignored, "insert ignore default");
AssertEqual("read-only-default", inserted.ReadOnly, "insert read-only default");
AssertEqual("default-from-db", inserted.DatabaseDefault, "insert database default");
AssertEqual("inserted-computed", inserted.Computed, "insert computed");

identityEntity.Normal = "updated";
identityEntity.Ignored = "ignored-update";
identityEntity.ReadOnly = "read-only-update";
identityEntity.DatabaseDefault = "default-update";
identityEntity.Computed = "computed-update";

if (!connection.Update(identityEntity))
{
    throw new InvalidOperationException("Dommel update returned false.");
}

var updated = SelectDommelSmokeRow(connection, id);
AssertEqual("updated", updated.Normal, "update normal");
AssertEqual("ignored-default", updated.Ignored, "update ignore preserved");
AssertEqual("read-only-default", updated.ReadOnly, "update read-only preserved");
AssertEqual("default-update", updated.DatabaseDefault, "update database-default participates");
AssertEqual("updated-computed", updated.Computed, "update computed");

var loaded = connection.Get<DommelSmokeEntity>(id);
AssertEqual(id, loaded!.Id, "Dommel Get id");
AssertEqual("updated", loaded.Normal, "Dommel Get normal");
AssertEqual(null, loaded.Ignored, "Dommel Get ignored");
AssertEqual("read-only-default", loaded.ReadOnly, "Dommel Get read-only");
AssertEqual("default-update", loaded.DatabaseDefault, "Dommel Get database default");
AssertEqual("updated-computed", loaded.Computed, "Dommel Get computed");

var assigned = new AssignedKeyEntity
{
    Code = "A-001",
    Name = "assigned-insert",
    UpdateExcluded = "insert-only"
};
connection.Insert(assigned);

var assignedInserted = connection.QuerySingle<AssignedKeyRow>(
    "SELECT code AS Code, name AS Name, update_excluded AS UpdateExcluded FROM assigned_key_entities WHERE code = 'A-001';");
AssertEqual("A-001", assignedInserted.Code, "assigned key insert code");
AssertEqual("assigned-insert", assignedInserted.Name, "assigned key insert name");
AssertEqual("insert-only", assignedInserted.UpdateExcluded, "assigned key insert update excluded");

assigned.Name = "assigned-update";
assigned.UpdateExcluded = "should-not-update";
if (!connection.Update(assigned))
{
    throw new InvalidOperationException("Dommel non-identity key update returned false.");
}

var assignedUpdated = connection.QuerySingle<AssignedKeyRow>(
    "SELECT code AS Code, name AS Name, update_excluded AS UpdateExcluded FROM assigned_key_entities WHERE code = 'A-001';");
AssertEqual("A-001", assignedUpdated.Code, "assigned key update code");
AssertEqual("assigned-update", assignedUpdated.Name, "assigned key update name");
AssertEqual("insert-only", assignedUpdated.UpdateExcluded, "assigned key update excluded preserved");

Console.WriteLine("dommel-consumer:ok");

static DommelSmokeRow SelectDommelSmokeRow(SqliteConnection connection, int id)
{
    return connection.QuerySingle<DommelSmokeRow>(
        """
        SELECT
            id AS Id,
            normal AS Normal,
            ignored AS Ignored,
            read_only AS ReadOnly,
            database_default AS DatabaseDefault,
            computed AS Computed
        FROM dommel_smoke_entities
        WHERE id = @id;
        """,
        new { id });
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

public sealed class DommelSmokeEntity
{
    public int Id { get; set; }

    public string Normal { get; set; } = string.Empty;

    public string? Ignored { get; set; }

    public string? ReadOnly { get; set; }

    public string? DatabaseDefault { get; set; }

    public string? Computed { get; set; }
}

public sealed class DommelSmokeEntityMap : DommelEntityMap<DommelSmokeEntity>
{
    public DommelSmokeEntityMap()
    {
        ToTable("dommel_smoke_entities");
        Map(entity => entity.Id).ToColumn("id").IsIdentity();
        Map(entity => entity.Normal).ToColumn("normal");
        Map(entity => entity.Ignored).ToColumn("ignored").Ignore();
        Map(entity => entity.ReadOnly).ToColumn("read_only").ReadOnly();
        Map(entity => entity.DatabaseDefault).ToColumn("database_default").DatabaseDefaultOnInsert();
        Map(entity => entity.Computed).ToColumn("computed").Computed();
    }
}

public sealed class DommelSmokeRow
{
    public int Id { get; set; }

    public string Normal { get; set; } = string.Empty;

    public string? Ignored { get; set; }

    public string? ReadOnly { get; set; }

    public string? DatabaseDefault { get; set; }

    public string? Computed { get; set; }
}

public sealed class AssignedKeyEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? UpdateExcluded { get; set; }
}

public sealed class AssignedKeyEntityMap : DommelEntityMap<AssignedKeyEntity>
{
    public AssignedKeyEntityMap()
    {
        ToTable("assigned_key_entities");
        Map(entity => entity.Code).ToColumn("code").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
        Map(entity => entity.Name).ToColumn("name");
        Map(entity => entity.UpdateExcluded).ToColumn("update_excluded").ExcludeFromUpdate();
    }
}

public sealed class AssignedKeyRow
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? UpdateExcluded { get; set; }
}
