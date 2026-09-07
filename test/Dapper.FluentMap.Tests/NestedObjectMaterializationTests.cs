using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class NestedObjectMaterializationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeSimpleNestedObject()
        {
            PreTest(typeof(Customer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<Customer>(
                        "SELECT 7 AS customer_id, 'Sao Paulo' AS city;");

                    Assert.Equal(7, customer.Id);
                    Assert.NotNull(customer.Address);
                    Assert.Equal("Sao Paulo", customer.Address.City);
                }
            }
            finally
            {
                PreTest(typeof(Customer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeThreeLevelNestedObject()
        {
            PreTest(typeof(CustomerWithCountry));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithCountryMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithCountry>(
                        "SELECT 'Brazil' AS country_name;");

                    Assert.NotNull(customer.Address);
                    Assert.NotNull(customer.Address.Country);
                    Assert.Equal("Brazil", customer.Address.Country.Name);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithCountry));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldPreserveSameTerminalMemberPaths()
        {
            PreTest(typeof(SameTerminalCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new SameTerminalCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<SameTerminalCustomer>(
                        "SELECT 10 AS rank_level, 20 AS seniority_level;");

                    Assert.NotNull(customer.Rank);
                    Assert.NotNull(customer.Seniority);
                    Assert.Equal(10, customer.Rank.Level);
                    Assert.Equal(20, customer.Seniority.Level);
                }
            }
            finally
            {
                PreTest(typeof(SameTerminalCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyNamingPolicyToRootPropertiesAndExplicitNestedMappings()
        {
            PreTest(typeof(PolicyCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false).ForEntity<PolicyCustomer>();
                    c.AddMap(new PolicyCustomerMap());
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<PolicyCustomer>(
                        "SELECT 42 AS CUSTOMER_ID, 'Campinas' AS city;");

                    Assert.Equal(42, customer.CustomerId);
                    Assert.NotNull(customer.Address);
                    Assert.Equal("Campinas", customer.Address.City);
                }
            }
            finally
            {
                PreTest(typeof(PolicyCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyInheritedNestedMapping()
        {
            PreTest(typeof(BaseCustomer), typeof(DerivedCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new BaseCustomerMap());
                    c.AddMap(new DerivedCustomerMap());
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<DerivedCustomer>(
                        "SELECT 'Recife' AS city, 'vip' AS tier;");

                    Assert.NotNull(customer.Address);
                    Assert.Equal("Recife", customer.Address.City);
                    Assert.Equal("vip", customer.Tier);
                }
            }
            finally
            {
                PreTest(typeof(BaseCustomer), typeof(DerivedCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldKeepNestedObjectNullWhenAllNestedColumnsAreNull()
        {
            PreTest(typeof(Customer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<Customer>(
                        "SELECT 7 AS customer_id, NULL AS city;");

                    Assert.Equal(7, customer.Id);
                    Assert.Null(customer.Address);
                }
            }
            finally
            {
                PreTest(typeof(Customer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldCreateNestedObjectWhenSomeNestedColumnsAreNotNull()
        {
            PreTest(typeof(CustomerWithPostalCode));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithPostalCodeMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithPostalCode>(
                        "SELECT NULL AS city, '01000' AS postal_code;");

                    Assert.NotNull(customer.Address);
                    Assert.Null(customer.Address.City);
                    Assert.Equal("01000", customer.Address.PostalCode);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithPostalCode));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseExistingIntermediateObjectWhenAvailable()
        {
            PreTest(typeof(CustomerWithExistingAddress));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithExistingAddressMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithExistingAddress>(
                        "SELECT 'Niteroi' AS city;");

                    Assert.NotNull(customer.Address);
                    Assert.Equal("created by constructor", customer.Address.CreatedBy);
                    Assert.Equal("Niteroi", customer.Address.City);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithExistingAddress));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldClearExistingIntermediateObjectWhenAllNestedColumnsAreNull()
        {
            PreTest(typeof(CustomerWithExistingAddress));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerWithExistingAddressMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<CustomerWithExistingAddress>(
                        "SELECT NULL AS city;");

                    Assert.Null(customer.Address);
                }
            }
            finally
            {
                PreTest(typeof(CustomerWithExistingAddress));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeMultipleRows()
        {
            PreTest(typeof(Customer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customers = connection.QueryMapped<Customer>(
                            "SELECT 1 AS customer_id, 'Santos' AS city UNION ALL SELECT 2, 'Osasco';")
                        .ToList();

                    Assert.Collection(
                        customers,
                        first =>
                        {
                            Assert.Equal(1, first.Id);
                            Assert.Equal("Santos", first.Address.City);
                        },
                        second =>
                        {
                            Assert.Equal(2, second.Id);
                            Assert.Equal("Osasco", second.Address.City);
                        });
                }
            }
            finally
            {
                PreTest(typeof(Customer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldContinueMaterializingTraditionalPocoFallback()
        {
            PreTest(typeof(TraditionalCustomer));

            try
            {
                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<TraditionalCustomer>(
                        "SELECT 9 AS Id, 'Curitiba' AS Name;");

                    Assert.Equal(9, customer.Id);
                    Assert.Equal("Curitiba", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(TraditionalCustomer));
            }
        }

        [Fact]
        public void InitializeShouldRejectUnsupportedCollectionInNestedPath()
        {
            PreTest(typeof(CollectionPathCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c => c.AddMap(new CollectionPathCustomerMap())));

                Assert.Contains("collection", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Items.Value", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                PreTest(typeof(CollectionPathCustomer));
            }
        }

        [Fact]
        public void QueryMappedShouldRejectNestedTypeWithoutPublicParameterlessConstructor()
        {
            PreTest(typeof(NonConstructibleCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new NonConstructibleCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<NonConstructibleCustomer>("SELECT 'Sao Paulo' AS city;"));

                    Assert.Contains("No public constructor", exception.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("Address", exception.Message, StringComparison.Ordinal);
                }
            }
            finally
            {
                PreTest(typeof(NonConstructibleCustomer));
            }
        }

        [Fact]
        public void QueryMappedShouldRejectReadonlyNestedPathWithoutMatchingConstructor()
        {
            PreTest(typeof(ReadOnlyPathCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ReadOnlyPathCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<ReadOnlyPathCustomer>("SELECT 'Sao Paulo' AS city;"));

                    Assert.Contains("No public constructor", exception.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("Address", exception.Message, StringComparison.Ordinal);
                    Assert.Contains("city", exception.Message, StringComparison.Ordinal);
                }
            }
            finally
            {
                PreTest(typeof(ReadOnlyPathCustomer));
            }
        }

        [Fact]
        public void InitializeShouldRejectConflictingNestedPathPrefix()
        {
            PreTest(typeof(ConflictingPathCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c => c.AddMap(new ConflictingPathCustomerMap())));

                Assert.Contains("conflicts", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Address", exception.Message, StringComparison.Ordinal);
                Assert.Contains("Address.City", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                PreTest(typeof(ConflictingPathCustomer));
            }
        }

        [Fact]
        public void ExplainShouldDescribeNestedMaterialization()
        {
            PreTest(typeof(Customer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CustomerMap()));

                var explanation = FluentMapper.Explain<Customer>();
                var city = explanation.Members.Single(m => m.MemberPath == "Address.City");

                Assert.Equal("city", city.ColumnName);
                Assert.Equal(MappingSource.Explicit, city.Source);
                Assert.Equal(MappingMaterialization.Nested, city.Materialization);
            }
            finally
            {
                PreTest(typeof(Customer));
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

        private sealed class Customer
        {
            public int Id { get; set; }

            public Address Address { get; set; }
        }

        private sealed class CustomerMap : EntityMap<Customer>
        {
            public CustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class Address
        {
            public string City { get; set; }
        }

        private sealed class CustomerWithCountry
        {
            public AddressWithCountry Address { get; set; }
        }

        private sealed class CustomerWithCountryMap : EntityMap<CustomerWithCountry>
        {
            public CustomerWithCountryMap()
            {
                Map(customer => customer.Address.Country.Name).ToColumn("country_name");
            }
        }

        private sealed class AddressWithCountry
        {
            public Country Country { get; set; }
        }

        private sealed class Country
        {
            public string Name { get; set; }
        }

        private sealed class SameTerminalCustomer
        {
            public RankInfo Rank { get; set; }

            public SeniorityInfo Seniority { get; set; }
        }

        private sealed class RankInfo
        {
            public int Level { get; set; }
        }

        private sealed class SeniorityInfo
        {
            public int Level { get; set; }
        }

        private sealed class SameTerminalCustomerMap : EntityMap<SameTerminalCustomer>
        {
            public SameTerminalCustomerMap()
            {
                Map(customer => customer.Rank.Level).ToColumn("rank_level");
                Map(customer => customer.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private sealed class PolicyCustomer
        {
            public int CustomerId { get; set; }

            public PolicyAddress Address { get; set; }
        }

        private sealed class PolicyAddress
        {
            public string City { get; set; }
        }

        private sealed class PolicyCustomerMap : EntityMap<PolicyCustomer>
        {
            public PolicyCustomerMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private class BaseCustomer
        {
            public Address Address { get; set; }
        }

        private sealed class DerivedCustomer : BaseCustomer
        {
            public string Tier { get; set; }
        }

        private sealed class BaseCustomerMap : EntityMap<BaseCustomer>
        {
            public BaseCustomerMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class DerivedCustomerMap : EntityMap<DerivedCustomer>
        {
            public DerivedCustomerMap()
            {
                IncludeBase<BaseCustomer>();
                Map(customer => customer.Tier).ToColumn("tier");
            }
        }

        private sealed class CustomerWithPostalCode
        {
            public PostalAddress Address { get; set; }
        }

        private sealed class PostalAddress
        {
            public string City { get; set; }

            public string PostalCode { get; set; }
        }

        private sealed class CustomerWithPostalCodeMap : EntityMap<CustomerWithPostalCode>
        {
            public CustomerWithPostalCodeMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
                Map(customer => customer.Address.PostalCode).ToColumn("postal_code");
            }
        }

        private sealed class CustomerWithExistingAddress
        {
            public CustomerWithExistingAddress()
            {
                Address = new ExistingAddress { CreatedBy = "created by constructor" };
            }

            public ExistingAddress Address { get; set; }
        }

        private sealed class ExistingAddress
        {
            public string CreatedBy { get; set; }

            public string City { get; set; }
        }

        private sealed class CustomerWithExistingAddressMap : EntityMap<CustomerWithExistingAddress>
        {
            public CustomerWithExistingAddressMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class TraditionalCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class CollectionPathCustomer
        {
            public CollectionItem Items { get; set; }
        }

        private sealed class CollectionItem : List<CollectionLeaf>
        {
            public string Value { get; set; }
        }

        private sealed class CollectionLeaf
        {
            public string Value { get; set; }
        }

        private sealed class CollectionPathCustomerMap : EntityMap<CollectionPathCustomer>
        {
            public CollectionPathCustomerMap()
            {
                Map(customer => customer.Items.Value).ToColumn("value");
            }
        }

        private sealed class NonConstructibleCustomer
        {
            public NonConstructibleAddress Address { get; set; }
        }

        private sealed class NonConstructibleAddress
        {
            public NonConstructibleAddress(string seed)
            {
                City = seed;
            }

            public string City { get; set; }
        }

        private sealed class NonConstructibleCustomerMap : EntityMap<NonConstructibleCustomer>
        {
            public NonConstructibleCustomerMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class ReadOnlyPathCustomer
        {
            public ReadOnlyAddress Address { get; set; }
        }

        private sealed class ReadOnlyAddress
        {
            public string City { get; }
        }

        private sealed class ReadOnlyPathCustomerMap : EntityMap<ReadOnlyPathCustomer>
        {
            public ReadOnlyPathCustomerMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class ConflictingPathCustomer
        {
            public Address Address { get; set; }
        }

        private sealed class ConflictingPathCustomerMap : EntityMap<ConflictingPathCustomer>
        {
            public ConflictingPathCustomerMap()
            {
                Map(customer => customer.Address).ToColumn("address");
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }
    }
}
