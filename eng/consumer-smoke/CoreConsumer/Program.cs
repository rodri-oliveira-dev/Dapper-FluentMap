using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

FluentMapper.Initialize(configuration =>
{
    configuration.AddMap<CoreCustomerMap>();
    configuration.AddMap<ImmutableCustomerMap>();
    configuration.AddMap<NestedCustomerMap>();
    configuration.AddMap<ValueObjectCustomerMap>();
    configuration.AddMap<ConvertedCustomerMap>();
    configuration.AddProfile<LegacyCustomerMap>();
});

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var explicitCustomer = connection.QuerySingle<CoreCustomer>(
    "SELECT 1 AS customer_id, 'Ada' AS full_name;");
AssertEqual(1, explicitCustomer.Id, "explicit column id");
AssertEqual("Ada", explicitCustomer.Name, "explicit column name");

var mappedCustomers = connection.QueryMapped<CoreCustomer>(
    "SELECT 2 AS customer_id, 'Grace' AS full_name UNION ALL SELECT 3 AS customer_id, 'Linus' AS full_name;")
    .ToList();
AssertEqual(2, mappedCustomers.Count, "QueryMapped count");
AssertEqual(3, mappedCustomers[1].Id, "QueryMapped row");

var immutable = connection.QuerySingle<ImmutableCustomer>(
    "SELECT 4 AS immutable_id, 'Constructor' AS name;");
AssertEqual(4, immutable.Id, "constructor id");
AssertEqual("Constructor", immutable.Name, "constructor name");

var nested = connection.QueryMappedSingle<NestedCustomer>(
    "SELECT 5 AS customer_id, 'Sao Paulo' AS city;");
AssertEqual(5, nested.Id, "nested id");
AssertEqual("Sao Paulo", nested.Address?.City, "nested city");

var valueObject = connection.QueryMappedSingle<ValueObjectCustomer>(
    "SELECT 6 AS customer_id, '12345678909' AS cpf;");
AssertEqual(6, valueObject.Id, "value object id");
AssertEqual("12345678909", valueObject.Cpf.Number, "value object cpf");

var profiled = connection.QueryMappedSingle<CoreCustomer, LegacyProfile>(
    "SELECT 7 AS legacy_id, 'Legacy Ltd.' AS legal_name;");
AssertEqual(7, profiled.Id, "profile id");
AssertEqual("Legacy Ltd.", profiled.Name, "profile name");

var converted = connection.QueryMappedSingle<ConvertedCustomer>(
    "SELECT 8 AS customer_id, 'A' AS status;");
AssertEqual(AccountStatus.Active, converted.Status, "read converter");

if (!FluentMapper.EntityMaps.ContainsKey(typeof(CoreCustomer)))
{
    throw new InvalidOperationException("Legacy EntityMaps API did not expose the configured map.");
}

if (!FluentMapper.GetEntityMaps().ContainsKey(typeof(CoreCustomer)))
{
    throw new InvalidOperationException("Read-only EntityMaps snapshot did not expose the configured map.");
}

AssertIsolatedRuntime(connection);

Console.WriteLine("core-consumer:ok");

static void AssertIsolatedRuntime(SqliteConnection connection)
{
    var firstRuntime = new FluentMapConfigurationBuilder()
        .AddMap<FirstIsolatedCustomerMap>()
        .Build()
        .CreateRuntime();
    var secondRuntime = new FluentMapConfigurationBuilder()
        .AddMap<SecondIsolatedCustomerMap>()
        .Build()
        .CreateRuntime();

    var first = firstRuntime.QueryMappedSingle<IsolatedCustomer>(
        connection,
        "SELECT 10 AS first_id;");
    var second = secondRuntime.QueryMappedSingle<IsolatedCustomer>(
        connection,
        "SELECT 20 AS second_id;");

    AssertEqual(10, first.Id, "first isolated runtime");
    AssertEqual(20, second.Id, "second isolated runtime");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

public sealed class CoreCustomer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class CoreCustomerMap : EntityMap<CoreCustomer>
{
    public CoreCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("full_name");
    }
}

public sealed class ImmutableCustomer
{
    public ImmutableCustomer(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }
}

public sealed class ImmutableCustomerMap : EntityMap<ImmutableCustomer>
{
    public ImmutableCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("immutable_id");
        Map(customer => customer.Name).ToColumn("name");
    }
}

public sealed class NestedCustomer
{
    public int Id { get; set; }

    public Address? Address { get; set; }
}

public sealed class Address
{
    public string City { get; set; } = string.Empty;
}

public sealed class NestedCustomerMap : EntityMap<NestedCustomer>
{
    public NestedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Address.City).ToColumn("city");
    }
}

public sealed class ValueObjectCustomer
{
    public ValueObjectCustomer(int id, Cpf cpf)
    {
        Id = id;
        Cpf = cpf;
    }

    public int Id { get; }

    public Cpf Cpf { get; }
}

public sealed class Cpf
{
    public Cpf(string number)
    {
        Number = number;
    }

    public string Number { get; }
}

public sealed class ValueObjectCustomerMap : EntityMap<ValueObjectCustomer>
{
    public ValueObjectCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Cpf.Number).ToColumn("cpf");
    }
}

public sealed class LegacyProfile : IMappingProfile
{
}

public sealed class LegacyCustomerMap : EntityMap<CoreCustomer>, IProfileMap<LegacyProfile>
{
    public LegacyCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("legacy_id");
        Map(customer => customer.Name).ToColumn("legal_name");
    }
}

public enum AccountStatus
{
    Unknown,
    Active
}

public sealed class ConvertedCustomer
{
    public int Id { get; set; }

    public AccountStatus Status { get; set; }
}

public sealed class ConvertedCustomerMap : EntityMap<ConvertedCustomer>
{
    public ConvertedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<StatusConverter, string>();
    }
}

public sealed class StatusConverter : IReadPropertyConverter<string, AccountStatus>
{
    public AccountStatus ConvertFromDatabase(string value)
    {
        return value == "A" ? AccountStatus.Active : AccountStatus.Unknown;
    }
}

public sealed class IsolatedCustomer
{
    public int Id { get; set; }
}

public sealed class FirstIsolatedCustomerMap : EntityMap<IsolatedCustomer>
{
    public FirstIsolatedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("first_id");
    }
}

public sealed class SecondIsolatedCustomerMap : EntityMap<IsolatedCustomer>
{
    public SecondIsolatedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("second_id");
    }
}
