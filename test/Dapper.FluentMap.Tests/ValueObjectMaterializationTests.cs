using System;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ValueObjectMaterializationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeSimpleValueObjectThroughConstructor()
        {
            PreTest(typeof(CustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCpfMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithCpf>(
                        "SELECT 1 AS customer_id, '12345678909' AS cpf;");

                    Assert.Equal(1, customer.Id);
                    Assert.NotNull(customer.Cpf);
                    Assert.Equal("12345678909", customer.Cpf.Number);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeSingleValueRecordThroughConstructor()
        {
            PreTest(typeof(CustomerWithEmail));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithEmailMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithEmail>(
                        "SELECT 2 AS customer_id, 'ada@example.com' AS email;");

                    Assert.Equal(2, customer.Id);
                    Assert.Equal(new Email("ada@example.com"), customer.Email);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithEmail));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeMultiComponentValueObjectThroughConstructor()
        {
            PreTest(typeof(CustomerWithMoney));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithMoneyMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithMoney>(
                        "SELECT 12.50 AS amount, 'BRL' AS currency;");

                    Assert.NotNull(customer.Balance);
                    Assert.Equal(12.50m, customer.Balance.Amount);
                    Assert.Equal("BRL", customer.Balance.Currency);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithMoney));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldPassNullForNullableValueObjectWhenSqlValueIsNull()
        {
            PreTest(typeof(CustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCpfMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithCpf>(
                        "SELECT 3 AS customer_id, NULL AS cpf;");

                    Assert.Equal(3, customer.Id);
                    Assert.Null(customer.Cpf);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldWrapDomainExceptionWithMappingContext()
        {
            PreTest(typeof(CustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCpfMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<CustomerWithCpf>(
                            "SELECT 4 AS customer_id, '' AS cpf;"));

                    Assert.IsType<ArgumentException>(exception.InnerException);
                    Assert.Contains(typeof(CustomerWithCpf).FullName, exception.Message);
                    Assert.Contains(typeof(Cpf).FullName, exception.Message);
                    Assert.Contains("Cpf", exception.Message);
                    Assert.Contains("cpf", exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeNestedImmutableObject()
        {
            PreTest(typeof(CustomerWithImmutableAddress));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithImmutableAddressMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithImmutableAddress>(
                        "SELECT 'Sao Paulo' AS city;");

                    Assert.NotNull(customer.Address);
                    Assert.Equal("Sao Paulo", customer.Address.City);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithImmutableAddress));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeTwoValueObjectsInSameEntity()
        {
            PreTest(typeof(CustomerWithTwoCpfs));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithTwoCpfsMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithTwoCpfs>(
                        "SELECT '11111111111' AS cpf, '22222222222' AS backup_cpf;");

                    Assert.Equal("11111111111", customer.Cpf.Number);
                    Assert.Equal("22222222222", customer.BackupCpf.Number);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithTwoCpfs));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldPreserveSameTerminalInImmutablePaths()
        {
            PreTest(typeof(ImmutableSameTerminalCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ImmutableSameTerminalCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ImmutableSameTerminalCustomer>(
                        "SELECT 5 AS rank_level, 9 AS seniority_level;");

                    Assert.Equal(5, customer.Rank.Level);
                    Assert.Equal(9, customer.Seniority.Level);
                }
            }
            finally
            {
                PreTest(typeof(ImmutableSameTerminalCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyNamingPolicyToImmutableRootConstructor()
        {
            PreTest(typeof(PolicyValueObjectCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false).ForEntity<PolicyValueObjectCustomer>();
                    c.AddMap(new PolicyValueObjectCustomerMap());
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<PolicyValueObjectCustomer>(
                        "SELECT 6 AS CUSTOMER_ID, 'grace@example.com' AS email;");

                    Assert.Equal(6, customer.CustomerId);
                    Assert.Equal("grace@example.com", customer.Email.Value);
                }
            }
            finally
            {
                PreTest(typeof(PolicyValueObjectCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyInheritedValueObjectMapping()
        {
            PreTest(typeof(BaseCustomerWithCpf), typeof(DerivedCustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new BaseCustomerWithCpfMap());
                    c.AddMap(new DerivedCustomerWithCpfMap());
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<DerivedCustomerWithCpf>(
                        "SELECT '33333333333' AS cpf, 'vip' AS tier;");

                    Assert.Equal("33333333333", customer.Cpf.Number);
                    Assert.Equal("vip", customer.Tier);
                }
            }
            finally
            {
                PreTest(typeof(BaseCustomerWithCpf), typeof(DerivedCustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeSimpleImmutableConstructorMapping()
        {
            PreTest(typeof(SimpleImmutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new SimpleImmutableCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<SimpleImmutableCustomer>(
                        "SELECT 7 AS customer_id, 'Katherine Johnson' AS full_name;");

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("Katherine Johnson", customer.FullName);
                }
            }
            finally
            {
                PreTest(typeof(SimpleImmutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeDefaultImmutableConstructorMapping()
        {
            PreTest(typeof(DefaultImmutableCustomer));

            try
            {
                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<DefaultImmutableCustomer>(
                        "SELECT 10 AS Id, 'Dorothy Vaughan' AS Name;");

                    Assert.Equal(10, customer.Id);
                    Assert.Equal("Dorothy Vaughan", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(DefaultImmutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeValueObjectsAcrossMultipleRows()
        {
            PreTest(typeof(CustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCpfMap()));

                using (var connection = OpenConnection())
                {
                    var customers = connection.QueryMapped<CustomerWithCpf>(
                            "SELECT 8 AS customer_id, '44444444444' AS cpf UNION ALL SELECT 9, '55555555555';")
                        .ToList();

                    Assert.Collection(
                        customers,
                        first =>
                        {
                            Assert.Equal(8, first.Id);
                            Assert.Equal("44444444444", first.Cpf.Number);
                        },
                        second =>
                        {
                            Assert.Equal(9, second.Id);
                            Assert.Equal("55555555555", second.Cpf.Number);
                        });
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseDapperTypeHandlerForScalarValueObjectProperty()
        {
            PreTest(typeof(HandlerCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new CpfTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new HandlerCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<HandlerCustomer>(
                        "SELECT '66666666666' AS cpf;");

                    Assert.NotNull(customer.Cpf);
                    Assert.Equal("66666666666", customer.Cpf.Number);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(HandlerCustomer));
            }
        }

        [Fact]
        public void QueryMappedShouldRejectMissingConstructorParameterColumn()
        {
            PreTest(typeof(CustomerWithIncompleteValueObject));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithIncompleteValueObjectMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<CustomerWithIncompleteValueObject>(
                            "SELECT '77777777777' AS cpf;"));

                    Assert.Contains("No public constructor", exception.Message);
                    Assert.Contains("IncompleteCpf", exception.Message);
                    Assert.Contains("cpf", exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithIncompleteValueObject));
            }
        }

        [Fact]
        public void QueryMappedShouldRejectAmbiguousValueObjectConstructors()
        {
            PreTest(typeof(CustomerWithAmbiguousValueObject));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithAmbiguousValueObjectMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<CustomerWithAmbiguousValueObject>(
                            "SELECT 'abc' AS code;"));

                    Assert.Contains("multiple public constructors", exception.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("AmbiguousCode", exception.Message);
                    Assert.Contains("Code", exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithAmbiguousValueObject));
            }
        }

        [Fact]
        public void ExplainShouldDescribeValueObjectMaterialization()
        {
            PreTest(typeof(CustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCpfMap()));

                var explanation = FluentMapper.Explain<CustomerWithCpf>();
                var cpf = explanation.Members.Single(m => m.MemberPath == "Cpf.Number");

                Assert.Equal("cpf", cpf.ColumnName);
                Assert.Equal(MappingSource.Explicit, cpf.Source);
                Assert.Equal(MappingMaterialization.ValueObject, cpf.Materialization);
            }
            finally
            {
                PreTest(typeof(CustomerWithCpf));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class CustomerWithCpf
        {
            public CustomerWithCpf(int id, Cpf cpf)
            {
                Id = id;
                Cpf = cpf;
            }

            public int Id { get; }

            public Cpf Cpf { get; }
        }

        private sealed class CustomerWithCpfMap : EntityMap<CustomerWithCpf>
        {
            public CustomerWithCpfMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
            }
        }

        private sealed class Cpf
        {
            public Cpf(string number)
            {
                if (string.IsNullOrWhiteSpace(number))
                {
                    throw new ArgumentException("CPF cannot be empty.", nameof(number));
                }

                Number = number;
            }

            public string Number { get; }
        }

        private sealed record Email(string Value);

        private sealed class CustomerWithEmail
        {
            public CustomerWithEmail(int id, Email email)
            {
                Id = id;
                Email = email;
            }

            public int Id { get; }

            public Email Email { get; }
        }

        private sealed class CustomerWithEmailMap : EntityMap<CustomerWithEmail>
        {
            public CustomerWithEmailMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Email.Value).ToColumn("email");
            }
        }

        private sealed class Money
        {
            public Money(decimal amount, string currency)
            {
                Amount = amount;
                Currency = currency;
            }

            public decimal Amount { get; }

            public string Currency { get; }
        }

        private sealed class CustomerWithMoney
        {
            public CustomerWithMoney(Money balance)
            {
                Balance = balance;
            }

            public Money Balance { get; }
        }

        private sealed class CustomerWithMoneyMap : EntityMap<CustomerWithMoney>
        {
            public CustomerWithMoneyMap()
            {
                Map(customer => customer.Balance.Amount).ToColumn("amount");
                Map(customer => customer.Balance.Currency).ToColumn("currency");
            }
        }

        private sealed class CustomerWithImmutableAddress
        {
            public CustomerWithImmutableAddress(ImmutableAddress address)
            {
                Address = address;
            }

            public ImmutableAddress Address { get; }
        }

        private sealed class ImmutableAddress
        {
            public ImmutableAddress(string city)
            {
                City = city;
            }

            public string City { get; }
        }

        private sealed class CustomerWithImmutableAddressMap : EntityMap<CustomerWithImmutableAddress>
        {
            public CustomerWithImmutableAddressMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class CustomerWithTwoCpfs
        {
            public CustomerWithTwoCpfs(Cpf cpf, Cpf backupCpf)
            {
                Cpf = cpf;
                BackupCpf = backupCpf;
            }

            public Cpf Cpf { get; }

            public Cpf BackupCpf { get; }
        }

        private sealed class CustomerWithTwoCpfsMap : EntityMap<CustomerWithTwoCpfs>
        {
            public CustomerWithTwoCpfsMap()
            {
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
                Map(customer => customer.BackupCpf.Number).ToColumn("backup_cpf");
            }
        }

        private sealed class ImmutableSameTerminalCustomer
        {
            public ImmutableSameTerminalCustomer(ImmutableRank rank, ImmutableSeniority seniority)
            {
                Rank = rank;
                Seniority = seniority;
            }

            public ImmutableRank Rank { get; }

            public ImmutableSeniority Seniority { get; }
        }

        private sealed class ImmutableRank
        {
            public ImmutableRank(int level)
            {
                Level = level;
            }

            public int Level { get; }
        }

        private sealed class ImmutableSeniority
        {
            public ImmutableSeniority(int level)
            {
                Level = level;
            }

            public int Level { get; }
        }

        private sealed class ImmutableSameTerminalCustomerMap : EntityMap<ImmutableSameTerminalCustomer>
        {
            public ImmutableSameTerminalCustomerMap()
            {
                Map(customer => customer.Rank.Level).ToColumn("rank_level");
                Map(customer => customer.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private sealed class PolicyValueObjectCustomer
        {
            public PolicyValueObjectCustomer(int customerId, Email email)
            {
                CustomerId = customerId;
                Email = email;
            }

            public int CustomerId { get; }

            public Email Email { get; }
        }

        private sealed class PolicyValueObjectCustomerMap : EntityMap<PolicyValueObjectCustomer>
        {
            public PolicyValueObjectCustomerMap()
            {
                Map(customer => customer.Email.Value).ToColumn("email");
            }
        }

        private class BaseCustomerWithCpf
        {
            public BaseCustomerWithCpf(Cpf cpf)
            {
                Cpf = cpf;
            }

            public Cpf Cpf { get; }
        }

        private sealed class DerivedCustomerWithCpf : BaseCustomerWithCpf
        {
            public DerivedCustomerWithCpf(Cpf cpf, string tier)
                : base(cpf)
            {
                Tier = tier;
            }

            public string Tier { get; }
        }

        private sealed class BaseCustomerWithCpfMap : EntityMap<BaseCustomerWithCpf>
        {
            public BaseCustomerWithCpfMap()
            {
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
            }
        }

        private sealed class DerivedCustomerWithCpfMap : EntityMap<DerivedCustomerWithCpf>
        {
            public DerivedCustomerWithCpfMap()
            {
                IncludeBase<BaseCustomerWithCpf>();
                Map(customer => customer.Tier).ToColumn("tier");
            }
        }

        private sealed class SimpleImmutableCustomer
        {
            public SimpleImmutableCustomer(int id, string fullName)
            {
                Id = id;
                FullName = fullName;
            }

            public int Id { get; }

            public string FullName { get; }
        }

        private sealed class SimpleImmutableCustomerMap : EntityMap<SimpleImmutableCustomer>
        {
            public SimpleImmutableCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.FullName).ToColumn("full_name");
            }
        }

        private sealed class DefaultImmutableCustomer
        {
            public DefaultImmutableCustomer(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; }

            public string Name { get; }
        }

        private sealed class HandlerCustomer
        {
            public Cpf Cpf { get; set; }
        }

        private sealed class HandlerCustomerMap : EntityMap<HandlerCustomer>
        {
            public HandlerCustomerMap()
            {
                Map(customer => customer.Cpf).ToColumn("cpf");
            }
        }

        private sealed class CpfTypeHandler : SqlMapper.TypeHandler<Cpf>
        {
            public override Cpf Parse(object value)
            {
                return new Cpf((string)value);
            }

            public override void SetValue(System.Data.IDbDataParameter parameter, Cpf value)
            {
                parameter.Value = value == null ? DBNull.Value : value.Number;
            }
        }

        private sealed class CustomerWithIncompleteValueObject
        {
            public CustomerWithIncompleteValueObject(IncompleteCpf cpf)
            {
                Cpf = cpf;
            }

            public IncompleteCpf Cpf { get; }
        }

        private sealed class IncompleteCpf
        {
            public IncompleteCpf(string number, string kind)
            {
                Number = number;
                Kind = kind;
            }

            public string Number { get; }

            public string Kind { get; }
        }

        private sealed class CustomerWithIncompleteValueObjectMap : EntityMap<CustomerWithIncompleteValueObject>
        {
            public CustomerWithIncompleteValueObjectMap()
            {
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
            }
        }

        private sealed class CustomerWithAmbiguousValueObject
        {
            public CustomerWithAmbiguousValueObject(AmbiguousCode code)
            {
                Code = code;
            }

            public AmbiguousCode Code { get; }
        }

        private sealed class AmbiguousCode
        {
            public AmbiguousCode(object value)
            {
                Value = (string)value;
            }

            public AmbiguousCode(IComparable value)
            {
                Value = value.ToString();
            }

            public string Value { get; }
        }

        private sealed class CustomerWithAmbiguousValueObjectMap : EntityMap<CustomerWithAmbiguousValueObject>
        {
            public CustomerWithAmbiguousValueObjectMap()
            {
                Map(customer => customer.Code.Value).ToColumn("code");
            }
        }
    }
}
