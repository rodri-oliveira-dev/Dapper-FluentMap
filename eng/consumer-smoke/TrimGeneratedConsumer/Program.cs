using Dapper.FluentMap;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

var runtime = new FluentMapConfigurationBuilder()
    .Configure(configuration => configuration.AddGeneratedMappings())
    .UseStrictGeneratedMaterialization()
    .Build()
    .CreateRuntime();

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var customer = runtime.QueryGeneratedMappedSingle<TrimGeneratedCustomer>(
    connection,
    "SELECT 32 AS customer_id, 'trim-generated' AS customer_name;");

if (customer.Id != 32 || customer.Name != "trim-generated")
{
    throw new InvalidOperationException("Trimmed generated consumer did not materialize the expected row.");
}

try
{
    runtime.QueryGeneratedMappedSingle<TrimGeneratedCustomer>(
        connection,
        "SELECT 33 AS customer_id;");
    throw new InvalidOperationException("Strict generated consumer did not reject the unsupported row shape.");
}
catch (FluentMapConfigurationException exception)
{
    if (!exception.Message.Contains("Strict generated materialization", StringComparison.Ordinal))
    {
        throw;
    }
}

Console.WriteLine("trim-generated-consumer:ok");

public sealed class TrimGeneratedCustomer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class TrimGeneratedCustomerMap : EntityMap<TrimGeneratedCustomer>
{
    public TrimGeneratedCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
    }
}
