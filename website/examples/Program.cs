using System.Data;
using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;

FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

FluentMapper.Validate();

IDbConnection connection = null!;
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
