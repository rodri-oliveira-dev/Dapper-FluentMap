using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var customer = connection.QueryMappedSingle<GeneratedCustomer>(
    "SELECT 42 AS customer_id, 'Ada' AS full_name;");
AssertEqual(42, customer.Id, "generated customer id");
AssertEqual("Ada", customer.Name, "generated customer name");

var nested = connection.QueryMappedSingle<GeneratedNestedCustomer>(
    "SELECT 'Curitiba' AS city;");
AssertEqual("Curitiba", nested.Address?.City, "generated nested city");

var valueObject = connection.QueryMappedSingle<GeneratedValueObjectCustomer>(
    "SELECT '98765432100' AS cpf;");
AssertEqual("98765432100", valueObject.Cpf.Number, "generated value object cpf");

var converted = connection.QueryMappedSingle<GeneratedConvertedCustomer>(
    "SELECT 'A' AS status;");
AssertEqual(AccountStatus.Active, converted.Status, "generated read converter");

if (!FluentMapper.Configuration.GeneratedMaterializers.Any(materializer =>
        materializer.EntityType == typeof(GeneratedCustomer)))
{
    throw new InvalidOperationException("Generated materializer registration metadata was not captured.");
}

Console.WriteLine("generator-analyzer-consumer:ok");

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

public sealed class GeneratedCustomer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class GeneratedCustomerMap : EntityMap<GeneratedCustomer>
{
    public GeneratedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("full_name");
    }
}

public sealed class GeneratedNestedCustomer
{
    public GeneratedAddress? Address { get; set; }
}

public sealed class GeneratedAddress
{
    public string City { get; set; } = string.Empty;
}

public sealed class GeneratedNestedCustomerMap : EntityMap<GeneratedNestedCustomer>
{
    public GeneratedNestedCustomerMap()
    {
        Map(customer => customer.Address.City).ToColumn("city");
    }
}

public sealed class GeneratedValueObjectCustomer
{
    public GeneratedValueObjectCustomer(GeneratedCpf cpf)
    {
        Cpf = cpf;
    }

    public GeneratedCpf Cpf { get; }
}

public sealed class GeneratedCpf
{
    public GeneratedCpf(string number)
    {
        Number = number;
    }

    public string Number { get; }
}

public sealed class GeneratedValueObjectCustomerMap : EntityMap<GeneratedValueObjectCustomer>
{
    public GeneratedValueObjectCustomerMap()
    {
        Map(customer => customer.Cpf.Number).ToColumn("cpf");
    }
}

public enum AccountStatus
{
    Unknown,
    Active
}

public sealed class GeneratedConvertedCustomer
{
    public AccountStatus Status { get; set; }
}

public sealed class GeneratedConvertedCustomerMap : EntityMap<GeneratedConvertedCustomer>
{
    public GeneratedConvertedCustomerMap()
    {
        Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<GeneratedStatusConverter, string>();
    }
}

public sealed class GeneratedStatusConverter : IReadPropertyConverter<string, AccountStatus>
{
    public AccountStatus ConvertFromDatabase(string value)
    {
        return value == "A" ? AccountStatus.Active : AccountStatus.Unknown;
    }
}
