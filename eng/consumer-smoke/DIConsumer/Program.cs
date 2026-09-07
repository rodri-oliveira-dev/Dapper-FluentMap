using Dapper.FluentMap;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

SQLitePCL.Batteries_V2.Init();

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var explicitProvider = new ServiceCollection()
    .AddFluentMap(builder => builder.AddMap<ExplicitDiCustomerMap>())
    .BuildServiceProvider())
{
    var configuration = explicitProvider.GetRequiredService<ImmutableFluentMapConfiguration>();
    var runtime = explicitProvider.GetRequiredService<FluentMapRuntime>();
    var customer = runtime.QueryMappedSingle<ExplicitDiCustomer>(
        connection,
        "SELECT 11 AS customer_id;");

    AssertEqual(1, configuration.EntityMaps.Count, "explicit DI map count");
    AssertEqual(11, customer.Id, "explicit DI query");
}

using (var generatedProvider = new ServiceCollection()
    .AddFluentMap(builder => builder.Configure(configuration => configuration.AddGeneratedMappings()))
    .BuildServiceProvider())
{
    var runtime = generatedProvider.GetRequiredService<FluentMapRuntime>();
    var customer = runtime.QueryMappedSingle<GeneratedDiCustomer>(
        connection,
        "SELECT 12 AS generated_id;");

    if (!runtime.Configuration.GeneratedMaterializers.Any(materializer =>
            materializer.EntityType == typeof(GeneratedDiCustomer)))
    {
        throw new InvalidOperationException("Generated DI registration did not capture a materializer.");
    }

    AssertEqual(12, customer.Id, "generated DI query");
}

using (var firstProvider = new ServiceCollection()
    .AddFluentMap(builder => builder.AddMap<FirstIsolatedDiCustomerMap>())
    .BuildServiceProvider())
using (var secondProvider = new ServiceCollection()
    .AddFluentMap(builder => builder.AddMap<SecondIsolatedDiCustomerMap>())
    .BuildServiceProvider())
{
    var firstRuntime = firstProvider.GetRequiredService<FluentMapRuntime>();
    var secondRuntime = secondProvider.GetRequiredService<FluentMapRuntime>();

    var first = firstRuntime.QueryMappedSingle<FirstIsolatedDiCustomer>(
        connection,
        "SELECT 21 AS first_id;");
    var second = secondRuntime.QueryMappedSingle<SecondIsolatedDiCustomer>(
        connection,
        "SELECT 22 AS second_id;");

    AssertEqual(21, first.Id, "first isolated DI runtime");
    AssertEqual(22, second.Id, "second isolated DI runtime");
}

Console.WriteLine("di-consumer:ok");

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

public sealed class ExplicitDiCustomer
{
    public int Id { get; set; }
}

public sealed class ExplicitDiCustomerMap : EntityMap<ExplicitDiCustomer>
{
    public ExplicitDiCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
    }
}

public sealed class GeneratedDiCustomer
{
    public int Id { get; set; }
}

public sealed class GeneratedDiCustomerMap : EntityMap<GeneratedDiCustomer>
{
    public GeneratedDiCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("generated_id");
    }
}

public sealed class FirstIsolatedDiCustomer
{
    public int Id { get; set; }
}

public sealed class FirstIsolatedDiCustomerMap : EntityMap<FirstIsolatedDiCustomer>
{
    public FirstIsolatedDiCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("first_id");
    }
}

public sealed class SecondIsolatedDiCustomer
{
    public int Id { get; set; }
}

public sealed class SecondIsolatedDiCustomerMap : EntityMap<SecondIsolatedDiCustomer>
{
    public SecondIsolatedDiCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("second_id");
    }
}
