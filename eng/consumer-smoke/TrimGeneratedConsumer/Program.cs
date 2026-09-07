using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var customer = connection.QueryMappedSingle<TrimGeneratedCustomer>(
    "SELECT 32 AS customer_id, 'trim-generated' AS customer_name;");

if (customer.Id != 32 || customer.Name != "trim-generated")
{
    throw new InvalidOperationException("Trimmed generated consumer did not materialize the expected row.");
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
