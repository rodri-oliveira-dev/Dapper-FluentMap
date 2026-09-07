using Dapper.FluentMap.Mapping;

Console.WriteLine(typeof(InvalidCustomerMap).Name);

public sealed class InvalidCustomer
{
    public string Name { get; set; } = string.Empty;

    public string GetName()
    {
        return Name;
    }
}

public sealed class InvalidCustomerMap : EntityMap<InvalidCustomer>
{
    public InvalidCustomerMap()
    {
        Map(customer => customer.GetName()).ToColumn("customer_name");
    }
}
