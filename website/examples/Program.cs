using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

FluentMapper.Validate();

using var connection = new SqliteConnection("Data Source=:memory:");
_ = connection.QuerySingle<Customer>(
    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
    }
}
