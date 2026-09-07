using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapper.FluentMap.DependencyInjection.Tests
{
    public sealed class FluentMapServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddFluentMapShouldRegisterConfigurationAndRuntimeAsSingletons()
        {
            var services = new ServiceCollection();

            services.AddFluentMap(builder => builder.AddMap<CurrentCustomerMap>());

            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(ImmutableFluentMapConfiguration) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(FluentMapRuntime) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            using (var provider = services.BuildServiceProvider())
            {
                var configuration = provider.GetRequiredService<ImmutableFluentMapConfiguration>();
                var runtime = provider.GetRequiredService<FluentMapRuntime>();

                Assert.Same(configuration, provider.GetRequiredService<ImmutableFluentMapConfiguration>());
                Assert.Same(runtime, provider.GetRequiredService<FluentMapRuntime>());
                Assert.Same(configuration, runtime.Configuration);
                Assert.True(configuration.EntityMaps.ContainsKey(typeof(DiCustomer)));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ResolvedRuntimeShouldMaterializeRows()
        {
            using (var provider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap<CurrentCustomerMap>())
                .BuildServiceProvider())
            using (var connection = OpenConnection())
            {
                var runtime = provider.GetRequiredService<FluentMapRuntime>();
                var customer = runtime.QueryMappedSingle<DiCustomer>(
                    connection,
                    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");

                Assert.Equal(7, customer.Id);
                Assert.Equal("Ada", customer.Name);
            }
        }

        [Fact]
        public void AddFluentMapShouldFailFastForInvalidConfiguration()
        {
            var services = new ServiceCollection();
            var map = new InvalidAfterRegistrationMap();

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                services.AddFluentMap(builder =>
                {
                    builder.AddMap(map);
                    map.PropertyMaps.Add(null);
                }));

            Assert.Contains("configuration validation found", exception.Message);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void AddFluentMapShouldSupportExplicitInstanceRegistration()
        {
            using (var provider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap(new ExplicitInstanceCustomerMap()))
                .BuildServiceProvider())
            using (var connection = OpenConnection())
            {
                var customer = provider.GetRequiredService<FluentMapRuntime>()
                    .QueryMappedSingle<ExplicitInstanceCustomer>(
                        connection,
                        "SELECT 9 AS instance_id;");

                Assert.Equal(9, customer.Id);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void AddFluentMapShouldSupportProfiles()
        {
            using (var provider = new ServiceCollection()
                .AddFluentMap(builder =>
                {
                    builder.AddMap<CurrentCustomerMap>();
                    builder.AddProfile<LegacyCustomerMap>();
                })
                .BuildServiceProvider())
            using (var connection = OpenConnection())
            {
                var current = provider.GetRequiredService<FluentMapRuntime>()
                    .QueryMappedSingle<DiCustomer>(
                        connection,
                        "SELECT 1 AS customer_id, 'Current' AS customer_name;");
                var legacy = provider.GetRequiredService<FluentMapRuntime>()
                    .QueryMappedSingle<DiCustomer, LegacyProfile>(
                        connection,
                        "SELECT 2 AS legacy_id, 'Legacy' AS legacy_name;");

                Assert.Equal("Current", current.Name);
                Assert.Equal(2, legacy.Id);
                Assert.Equal("Legacy", legacy.Name);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void IndependentServiceProvidersShouldKeepIndependentConfigurations()
        {
            using (var currentProvider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap(new CurrentCustomerMap()))
                .BuildServiceProvider())
            using (var legacyProvider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap(new AlternateCustomerMap()))
                .BuildServiceProvider())
            using (var connection = OpenConnection())
            {
                var current = currentProvider.GetRequiredService<FluentMapRuntime>()
                    .QueryMappedSingle<DiCustomer>(
                        connection,
                        "SELECT 3 AS customer_id, 'Current' AS customer_name;");
                var legacy = legacyProvider.GetRequiredService<FluentMapRuntime>()
                    .QueryMappedSingle<DiCustomer>(
                        connection,
                        "SELECT 4 AS customer_id, 'Alternate' AS alternate_name;");

                Assert.Equal("Current", current.Name);
                Assert.Equal("Alternate", legacy.Name);
                Assert.NotSame(
                    currentProvider.GetRequiredService<FluentMapRuntime>(),
                    legacyProvider.GetRequiredService<FluentMapRuntime>());
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void SingletonRuntimeShouldSupportConcurrentQueries()
        {
            using (var provider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap<CurrentCustomerMap>())
                .BuildServiceProvider())
            {
                var runtime = provider.GetRequiredService<FluentMapRuntime>();

                var results = Enumerable.Range(0, 32)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            var customer = runtime.QueryMappedSingle<DiCustomer>(
                                connection,
                                $"SELECT {index} AS customer_id, 'customer-{index}' AS customer_name;");

                            return customer.Id == index && customer.Name == $"customer-{index}";
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task IndependentServiceProvidersShouldResolveIndependentRuntimesConcurrently()
        {
            using (var currentProvider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap(new CurrentCustomerMap()))
                .BuildServiceProvider())
            using (var legacyProvider = new ServiceCollection()
                .AddFluentMap(builder => builder.AddMap(new AlternateCustomerMap()))
                .BuildServiceProvider())
            {
                var start = new Barrier(2);
                var currentTask = Task.Run(() =>
                {
                    start.SignalAndWait();
                    using (var connection = OpenConnection())
                    {
                        return currentProvider.GetRequiredService<FluentMapRuntime>()
                            .QueryMappedSingle<DiCustomer>(
                                connection,
                                "SELECT 11 AS customer_id, 'Current' AS customer_name;");
                    }
                });
                var legacyTask = Task.Run(() =>
                {
                    start.SignalAndWait();
                    using (var connection = OpenConnection())
                    {
                        return legacyProvider.GetRequiredService<FluentMapRuntime>()
                            .QueryMappedSingle<DiCustomer>(
                                connection,
                                "SELECT 12 AS customer_id, 'Alternate' AS alternate_name;");
                    }
                });

                var customers = await Task.WhenAll(currentTask, legacyTask);

                Assert.Equal("Current", customers[0].Name);
                Assert.Equal("Alternate", customers[1].Name);
                Assert.NotSame(
                    currentProvider.GetRequiredService<FluentMapRuntime>(),
                    legacyProvider.GetRequiredService<FluentMapRuntime>());
            }
        }

        [Fact]
        public void AddFluentMapShouldRejectNullArguments()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null).AddFluentMap(_ => { }));
            Assert.Throws<ArgumentNullException>(() => services.AddFluentMap(null));
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private sealed class DiCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class CurrentCustomerMap : EntityMap<DiCustomer>
        {
            public CurrentCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class AlternateCustomerMap : EntityMap<DiCustomer>
        {
            public AlternateCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("alternate_name");
            }
        }

        private sealed class LegacyCustomerMap : EntityMap<DiCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legacy_name");
            }
        }

        private sealed class ExplicitInstanceCustomer
        {
            public int Id { get; set; }
        }

        private sealed class ExplicitInstanceCustomerMap : EntityMap<ExplicitInstanceCustomer>
        {
            public ExplicitInstanceCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("instance_id");
            }
        }

        private sealed class InvalidAfterRegistrationEntity
        {
            public int Id { get; set; }
        }

        private sealed class InvalidAfterRegistrationMap : EntityMap<InvalidAfterRegistrationEntity>
        {
            public InvalidAfterRegistrationMap()
            {
                Map(entity => entity.Id).ToColumn("invalid_id");
            }
        }
    }
}
