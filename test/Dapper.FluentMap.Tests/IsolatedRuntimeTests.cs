using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class IsolatedRuntimeTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldMaterializeSameEntityWithIndependentConfigurations()
        {
            var current = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));
            var legacy = CreateRuntime(builder => builder.AddMap(new LegacyCustomerMap()));

            using (var connection = OpenConnection())
            {
                var currentCustomer = current.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name;");
                var legacyCustomer = legacy.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 2 AS customer_id, 'Grace' AS legacy_name;");

                Assert.Equal(1, currentCustomer.Id);
                Assert.Equal("Ada", currentCustomer.Name);
                Assert.Equal(2, legacyCustomer.Id);
                Assert.Equal("Grace", legacyCustomer.Name);
                Assert.Equal(1, current.MaterializationPlanCacheEntryCount);
                Assert.Equal(1, legacy.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldKeepSameProfileTypeIsolatedAcrossConfigurations()
        {
            var first = CreateRuntime(builder => builder.AddProfile<FirstProfileCustomerMap>());
            var second = CreateRuntime(builder => builder.AddProfile<SecondProfileCustomerMap>());

            using (var connection = OpenConnection())
            {
                var firstCustomer = first.QueryMappedSingle<RuntimeCustomer, RuntimeLegacyProfile>(
                    connection,
                    "SELECT 7 AS legacy_id, 'First' AS legacy_name;");
                var secondCustomer = second.QueryMappedSingle<RuntimeCustomer, RuntimeLegacyProfile>(
                    connection,
                    "SELECT 8 AS profile_id, 'Second' AS profile_name;");

                Assert.Equal(7, firstCustomer.Id);
                Assert.Equal("First", firstCustomer.Name);
                Assert.Equal(8, secondCustomer.Id);
                Assert.Equal("Second", secondCustomer.Name);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldScopeGeneratedMaterializersToConfiguration()
        {
            var generatedA = CreateRuntime(builder =>
            {
                builder.AddMap(new CurrentCustomerMap());
                builder.AddGeneratedMaterializer(
                    CustomerGeneratedColumns(),
                    record => new RuntimeCustomer
                    {
                        Id = Convert.ToInt32(record.GetValue(0)),
                        Name = "A:" + Convert.ToString(record.GetValue(1))
                    });
            });
            var generatedB = CreateRuntime(builder =>
            {
                builder.AddMap(new CurrentCustomerMap());
                builder.AddGeneratedMaterializer(
                    CustomerGeneratedColumns(),
                    record => new RuntimeCustomer
                    {
                        Id = Convert.ToInt32(record.GetValue(0)),
                        Name = "B:" + Convert.ToString(record.GetValue(1))
                    });
            });

            using (var connection = OpenConnection())
            {
                var first = generatedA.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name;");
                var second = generatedB.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name;");

                Assert.Equal("A:Ada", first.Name);
                Assert.Equal("B:Ada", second.Name);
                Assert.Equal(0, generatedA.MaterializationPlanCacheEntryCount);
                Assert.Equal(0, generatedB.MaterializationPlanCacheEntryCount);
                Assert.Equal(1, generatedA.GeneratedMaterializerCount);
                Assert.Equal(1, generatedB.GeneratedMaterializerCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldScopeConvertersToConfiguration()
        {
            var upper = CreateRuntime(builder => builder.AddMap(new UpperConverterCustomerMap()));
            var bracket = CreateRuntime(builder => builder.AddMap(new BracketConverterCustomerMap()));

            using (var connection = OpenConnection())
            {
                var upperCustomer = upper.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'ada' AS customer_name;");
                var bracketCustomer = bracket.QueryMappedSingle<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'ada' AS customer_name;");

                Assert.Equal("ADA", upperCustomer.Name);
                Assert.Equal("[ada]", bracketCustomer.Name);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldMaterializeNestedMappingsFromItsConfiguration()
        {
            var current = CreateRuntime(builder => builder.AddMap(new CurrentNestedCustomerMap()));
            var legacy = CreateRuntime(builder => builder.AddMap(new LegacyNestedCustomerMap()));

            using (var connection = OpenConnection())
            {
                var currentCustomer = current.QueryMappedSingle<NestedRuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Sao Paulo' AS city;");
                var legacyCustomer = legacy.QueryMappedSingle<NestedRuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Campinas' AS legacy_city;");

                Assert.NotNull(currentCustomer.Address);
                Assert.NotNull(legacyCustomer.Address);
                Assert.Equal("Sao Paulo", currentCustomer.Address.City);
                Assert.Equal("Campinas", legacyCustomer.Address.City);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldUseConfigurationScopedMaterializationPlanCache()
        {
            var nameRuntime = CreateRuntime(builder => builder.AddMap(new SharedShapeNameMap()));
            var legacyRuntime = CreateRuntime(builder => builder.AddMap(new SharedShapeLegacyNameMap()));

            using (var connection = OpenConnection())
            {
                var name = nameRuntime.QueryMappedSingle<SharedShapeCustomer>(
                    connection,
                    "SELECT 'value' AS shared_name;");
                var legacy = legacyRuntime.QueryMappedSingle<SharedShapeCustomer>(
                    connection,
                    "SELECT 'value' AS shared_name;");

                Assert.Equal("value", name.Name);
                Assert.Null(name.LegacyName);
                Assert.Null(legacy.Name);
                Assert.Equal("value", legacy.LegacyName);
                Assert.Equal(1, nameRuntime.MaterializationPlanCacheEntryCount);
                Assert.Equal(1, legacyRuntime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldIntegrateWithReadMappedAndUnbufferedQueries()
        {
            var runtime = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));

            using (var connection = OpenConnection())
            using (var multi = runtime.QueryMultipleMapped(
                connection,
                "SELECT 1 AS customer_id, 'Ada' AS customer_name;"))
            {
                var first = multi.ReadMappedSingle<RuntimeCustomer>();
                var second = runtime.QueryMappedUnbuffered<RuntimeCustomer>(
                        connection,
                        "SELECT 3 AS customer_id, 'Katherine' AS customer_name;")
                    .Single();

                Assert.Equal("Ada", first.Name);
                Assert.Equal("Katherine", second.Name);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task RuntimeShouldIntegrateWithAsyncStreaming()
        {
            var runtime = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));

            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var customers = await ToListAsync(runtime.QueryMappedUnbufferedAsync<RuntimeCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2 AS customer_id, 'Grace' AS customer_name;",
                    cancellationToken: TestContext.Current.CancellationToken));

                Assert.Collection(
                    customers,
                    customer => Assert.Equal("Ada", customer.Name),
                    customer => Assert.Equal("Grace", customer.Name));
                Assert.Equal(ConnectionState.Closed, connection.State);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeShouldSupportConcurrentQueriesUsingSameImmutableConfiguration()
        {
            var runtime = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));

            var results = Enumerable.Range(0, 32)
                .AsParallel()
                .Select(index =>
                {
                    using (var connection = OpenConnection())
                    {
                        var customer = runtime.QueryMappedSingle<RuntimeCustomer>(
                            connection,
                            $"SELECT {index} AS customer_id, 'customer-{index}' AS customer_name;");

                        return customer.Id == index && customer.Name == $"customer-{index}";
                    }
                })
                .ToList();

            Assert.All(results, Assert.True);
            Assert.Equal(1, runtime.MaterializationPlanCacheEntryCount);
        }

        [Fact]
        public void RuntimeDiagnosticsShouldUseItsConfigurationWithoutGlobalState()
        {
            var current = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));
            var legacy = CreateRuntime(builder => builder.AddMap(new LegacyCustomerMap()));

            current.Validate();
            legacy.Validate();

            var currentName = current.Explain<RuntimeCustomer>()
                .Members
                .Single(member => member.MemberPath == nameof(RuntimeCustomer.Name));
            var legacyName = legacy.Explain<RuntimeCustomer>()
                .Members
                .Single(member => member.MemberPath == nameof(RuntimeCustomer.Name));

            Assert.Equal("customer_name", currentName.ColumnName);
            Assert.Equal("legacy_name", legacyName.ColumnName);
        }

        private static FluentMapRuntime CreateRuntime(Action<FluentMapConfigurationBuilder> configure)
        {
            var builder = new FluentMapConfigurationBuilder();
            configure(builder);
            return builder.Build().CreateRuntime();
        }

        private static GeneratedMaterializerColumn[] CustomerGeneratedColumns()
        {
            return new[]
            {
                GeneratedMaterializerColumn.Map("customer_id", nameof(RuntimeCustomer.Id)),
                GeneratedMaterializerColumn.Map("customer_name", nameof(RuntimeCustomer.Name))
            };
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
        {
            var results = new List<T>();

            await foreach (var item in source)
            {
                results.Add(item);
            }

            return results;
        }

        private sealed class RuntimeLegacyProfile : IMappingProfile
        {
        }

        private sealed class RuntimeCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class CurrentCustomerMap : EntityMap<RuntimeCustomer>
        {
            public CurrentCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class LegacyCustomerMap : EntityMap<RuntimeCustomer>
        {
            public LegacyCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("legacy_name");
            }
        }

        private sealed class FirstProfileCustomerMap :
            EntityMap<RuntimeCustomer>,
            IProfileMap<RuntimeLegacyProfile>
        {
            public FirstProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legacy_name");
            }
        }

        private sealed class SecondProfileCustomerMap :
            EntityMap<RuntimeCustomer>,
            IProfileMap<RuntimeLegacyProfile>
        {
            public SecondProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("profile_id");
                Map(customer => customer.Name).ToColumn("profile_name");
            }
        }

        private sealed class UpperConverterCustomerMap : EntityMap<RuntimeCustomer>
        {
            public UpperConverterCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name").ConvertFromDatabaseUsing<UpperNameConverter, string>();
            }
        }

        private sealed class BracketConverterCustomerMap : EntityMap<RuntimeCustomer>
        {
            public BracketConverterCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name").ConvertFromDatabaseUsing<BracketNameConverter, string>();
            }
        }

        private sealed class UpperNameConverter : IReadPropertyConverter<string, string>
        {
            public string ConvertFromDatabase(string value)
            {
                return value.ToUpperInvariant();
            }
        }

        private sealed class BracketNameConverter : IReadPropertyConverter<string, string>
        {
            public string ConvertFromDatabase(string value)
            {
                return "[" + value + "]";
            }
        }

        private sealed class NestedRuntimeCustomer
        {
            public int Id { get; set; }

            public RuntimeAddress Address { get; set; }
        }

        private sealed class RuntimeAddress
        {
            public string City { get; set; }
        }

        private sealed class CurrentNestedCustomerMap : EntityMap<NestedRuntimeCustomer>
        {
            public CurrentNestedCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class LegacyNestedCustomerMap : EntityMap<NestedRuntimeCustomer>
        {
            public LegacyNestedCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("legacy_city");
            }
        }

        private sealed class SharedShapeCustomer
        {
            public string Name { get; set; }

            public string LegacyName { get; set; }
        }

        private sealed class SharedShapeNameMap : EntityMap<SharedShapeCustomer>
        {
            public SharedShapeNameMap()
            {
                Map(customer => customer.Name).ToColumn("shared_name");
            }
        }

        private sealed class SharedShapeLegacyNameMap : EntityMap<SharedShapeCustomer>
        {
            public SharedShapeLegacyNameMap()
            {
                Map(customer => customer.LegacyName).ToColumn("shared_name");
            }
        }
    }
}
