using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();

FluentMapper.Initialize(configuration => configuration.AddMap<TrimExplicitCustomerMap>());

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var customer = connection.QueryMappedSingle<TrimExplicitCustomer>(
    "SELECT 31 AS customer_id, 'trim-explicit' AS customer_name;");

if (customer.Id != 31 || customer.Name != "trim-explicit")
{
    throw new InvalidOperationException("Trimmed explicit consumer did not materialize the expected row.");
}

Console.WriteLine("trim-explicit-consumer:ok");

public sealed class TrimExplicitCustomer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class TrimExplicitCustomerMap : EntityMap<TrimExplicitCustomer>
{
    public TrimExplicitCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
    }
}
