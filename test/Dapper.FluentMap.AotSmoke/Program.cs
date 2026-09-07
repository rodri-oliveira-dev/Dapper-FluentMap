using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

#if AOT_SMOKE_GENERATED
const string scenario = "generated";
FluentMapper.Initialize(configuration =>
{
    configuration.AddGeneratedMappings();
    configuration.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<NamingCustomer>();
});

AssertMappedMember<Customer>("customer_id", nameof(Customer.Id));
AssertMappedMember<NamingCustomer>("created_at", nameof(NamingCustomer.CreatedAt));
AssertConstructorMapping();
AssertExplain();
AssertValueObjectExplain();
AssertProfileExplain();
AssertGeneratedQueryMappedMaterializer();
#elif AOT_SMOKE_DI_GENERATED
const string scenario = "di-generated";
using (var provider = new ServiceCollection()
    .AddFluentMap(builder =>
    {
        builder.Configure(configuration => configuration.AddGeneratedMappings());
        builder.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<NamingCustomer>();
    })
    .BuildServiceProvider())
{
    var runtime = provider.GetRequiredService<FluentMapRuntime>();
    AssertRuntimeMappedMember<Customer>(runtime, "customer_id", nameof(Customer.Id));
    AssertRuntimeMappedMember<NamingCustomer>(runtime, "created_at", nameof(NamingCustomer.CreatedAt));
    AssertRuntimeProfileExplain(runtime);
    AssertRuntimeGeneratedRegistration(runtime);
}
#elif AOT_SMOKE_DI_EXPLICIT
const string scenario = "di-explicit";
using (var provider = new ServiceCollection()
    .AddFluentMap(builder =>
    {
        builder.AddMap<CustomerMap>();
        builder.AddMap<ImmutableCustomerMap>();
        builder.AddMap<ValueObjectCustomerMap>();
        builder.AddProfile<LegacyCustomerMap>();
        builder.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<NamingCustomer>();
    })
    .BuildServiceProvider())
{
    var runtime = provider.GetRequiredService<FluentMapRuntime>();
    AssertRuntimeMappedMember<Customer>(runtime, "customer_id", nameof(Customer.Id));
    AssertRuntimeMappedMember<NamingCustomer>(runtime, "created_at", nameof(NamingCustomer.CreatedAt));
    AssertRuntimeProfileExplain(runtime);
}
#elif AOT_SMOKE_SCANNING
const string scenario = "scanning";
FluentMapper.Initialize(configuration => configuration.AddMapsFromAssemblyContaining<CustomerMap>());

AssertMappedMember<Customer>("customer_id", nameof(Customer.Id));
#else
const string scenario = "explicit";
FluentMapper.Initialize(configuration =>
{
    configuration.AddMap<CustomerMap>();
    configuration.AddMap<ImmutableCustomerMap>();
    configuration.AddMap<ValueObjectCustomerMap>();
    configuration.AddProfile<LegacyCustomerMap>();
    configuration.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<NamingCustomer>();
});

AssertMappedMember<Customer>("customer_id", nameof(Customer.Id));
AssertMappedMember<NamingCustomer>("created_at", nameof(NamingCustomer.CreatedAt));
AssertConstructorMapping();
AssertExplain();
AssertValueObjectExplain();
AssertProfileExplain();
#endif

#if AOT_SMOKE_DI_GENERATED || AOT_SMOKE_DI_EXPLICIT
static void AssertRuntimeMappedMember<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    TEntity>(
    FluentMapRuntime runtime,
    string columnName,
    string propertyName)
{
    var explanation = runtime.Explain<TEntity>();
    if (!explanation.Members.Any(member =>
            member.ColumnName == columnName &&
            member.MemberPath == propertyName))
    {
        throw new InvalidOperationException(
            $"Runtime explanation did not map column '{columnName}' to member '{propertyName}'.");
    }
}
#endif

Console.WriteLine(scenario + ":ok");

#if !AOT_SMOKE_DI_GENERATED && !AOT_SMOKE_DI_EXPLICIT
static void AssertMappedMember<TEntity>(string columnName, string propertyName)
{
    var member = SqlMapper.GetTypeMap(typeof(TEntity)).GetMember(columnName);
    if (member?.Property?.Name != propertyName)
    {
        throw new InvalidOperationException(
            $"Column '{columnName}' was not mapped to property '{propertyName}'.");
    }
}
#endif

#if !AOT_SMOKE_SCANNING && !AOT_SMOKE_DI_GENERATED && !AOT_SMOKE_DI_EXPLICIT
static void AssertConstructorMapping()
{
    var typeMap = SqlMapper.GetTypeMap(typeof(ImmutableCustomer));
    var constructor = typeMap.FindConstructor(
        new[] { "customer_id", "name" },
        new[] { typeof(int), typeof(string) });

    if (constructor == null)
    {
        throw new InvalidOperationException("Constructor mapping was not resolved.");
    }

    var member = typeMap.GetConstructorParameter(constructor, "customer_id");
    if (member?.Parameter?.Name != "id")
    {
        throw new InvalidOperationException("Constructor parameter mapping was not resolved.");
    }
}

static void AssertExplain()
{
    var explanation = FluentMapper.Explain<Customer>();
    if (!explanation.Members.Any(member =>
            member.MemberPath == nameof(Customer.Id) &&
            member.ColumnName == "customer_id"))
    {
        throw new InvalidOperationException("Explain did not include the explicit mapping.");
    }
}

static void AssertValueObjectExplain()
{
    var explanation = FluentMapper.Explain<ValueObjectCustomer>();
    if (!explanation.Members.Any(member =>
            member.MemberPath == "Cpf.Number" &&
            member.ColumnName == "cpf" &&
            member.Materialization == MappingMaterialization.ValueObject))
    {
        throw new InvalidOperationException("Explain did not include the value object mapping.");
    }
}

static void AssertProfileExplain()
{
    var explanation = FluentMapper.Explain<Customer, LegacyProfile>();
    if (explanation.ProfileType != typeof(LegacyProfile) ||
        !explanation.Members.Any(member =>
            member.MemberPath == nameof(Customer.Id) &&
            member.ColumnName == "legacy_id"))
    {
        throw new InvalidOperationException("Explain did not include the profile mapping.");
    }
}

#endif

#if AOT_SMOKE_DI_GENERATED || AOT_SMOKE_DI_EXPLICIT
static void AssertRuntimeProfileExplain(FluentMapRuntime runtime)
{
    var explanation = runtime.Explain<Customer, LegacyProfile>();
    if (explanation.ProfileType != typeof(LegacyProfile) ||
        !explanation.Members.Any(member =>
            member.MemberPath == nameof(Customer.Id) &&
            member.ColumnName == "legacy_id"))
    {
        throw new InvalidOperationException("Runtime Explain did not include the profile mapping.");
    }
}
#endif

#if AOT_SMOKE_DI_GENERATED
static void AssertRuntimeGeneratedRegistration(FluentMapRuntime runtime)
{
    if (!runtime.Configuration.GeneratedMaterializers.Any(materializer =>
            materializer.EntityType == typeof(Customer)))
    {
        throw new InvalidOperationException("DI runtime did not include generated materializer metadata.");
    }
}
#endif

#if AOT_SMOKE_GENERATED
static void AssertGeneratedQueryMappedMaterializer()
{
    SQLitePCL.Batteries_V2.Init();

    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();

    var customer = connection.QueryMappedSingle<Customer>(
        "SELECT 42 AS customer_id;");
    if (customer.Id != 42)
    {
        throw new InvalidOperationException("Generated flat QueryMapped materializer was not used correctly.");
    }

    var valueObjectCustomer = connection.QueryMappedSingle<ValueObjectCustomer>(
        "SELECT '12345678909' AS cpf;");
    if (valueObjectCustomer.Cpf?.Number != "12345678909")
    {
        throw new InvalidOperationException("Generated Value Object QueryMapped materializer was not used correctly.");
    }

    var converted = connection.QueryMappedSingle<ConvertedCustomer>(
        "SELECT 'A' AS status;");
    if (converted.Status != AccountStatus.Active)
    {
        throw new InvalidOperationException("Generated property converter materializer was not used correctly.");
    }
}
#endif

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
    }
}

public sealed class LegacyProfile : IMappingProfile
{
}

public sealed class LegacyCustomerMap : EntityMap<Customer>, IProfileMap<LegacyProfile>
{
    public LegacyCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("legacy_id");
    }
}

public sealed class NamingCustomer
{
    public DateTime CreatedAt { get; set; }
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
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("name");
    }
}

public sealed class ValueObjectCustomer
{
    public ValueObjectCustomer(Cpf cpf)
    {
        Cpf = cpf;
    }

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
        Map(customer => customer.Cpf.Number).ToColumn("cpf");
    }
}

public enum AccountStatus
{
    Unknown,
    Active
}

public sealed class ConvertedCustomer
{
    public AccountStatus Status { get; set; }
}

public sealed class ConvertedCustomerMap : EntityMap<ConvertedCustomer>
{
    public ConvertedCustomerMap()
    {
        Map(customer => customer.Status)
            .ToColumn("status")
            .ConvertFromDatabaseUsing<AccountStatusConverter, string>();
    }
}

public sealed class AccountStatusConverter : IReadPropertyConverter<string, AccountStatus>
{
    public AccountStatus ConvertFromDatabase(string value)
    {
        return value == "A" ? AccountStatus.Active : AccountStatus.Unknown;
    }
}
