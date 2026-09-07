using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public sealed class ConfigurationIsolationHardeningTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task IsolatedRuntimesShouldMaterializeSameEntityWithDifferentMappingsConcurrently()
        {
            var current = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));
            var legacy = CreateRuntime(builder => builder.AddMap(new LegacyCustomerMap()));
            var start = new Barrier(2);

            var currentTask = Task.Run(() =>
            {
                start.SignalAndWait();
                using (var connection = OpenConnection())
                {
                    return current.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name;");
                }
            });

            var legacyTask = Task.Run(() =>
            {
                start.SignalAndWait();
                using (var connection = OpenConnection())
                {
                    return legacy.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 2 AS customer_id, 'Grace' AS legacy_name;");
                }
            });

            var customers = await Task.WhenAll(currentTask, legacyTask);

            Assert.Equal(1, customers[0].Id);
            Assert.Equal("Ada", customers[0].Name);
            Assert.Equal(2, customers[1].Id);
            Assert.Equal("Grace", customers[1].Name);
            Assert.Equal(1, current.MaterializationPlanCacheEntryCount);
            Assert.Equal(1, legacy.MaterializationPlanCacheEntryCount);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SameRuntimeShouldMaterializeConcurrentReadersThroughOneScopedCache()
        {
            var runtime = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));
            var start = new Barrier(8);

            var tasks = Enumerable.Range(0, 8)
                .Select(index => Task.Run(() =>
                {
                    start.SignalAndWait();
                    using (var connection = OpenConnection())
                    {
                        return runtime.QueryMappedSingle<IsolationCustomer>(
                            connection,
                            $"SELECT {index} AS customer_id, 'customer-{index}' AS customer_name;");
                    }
                }))
                .ToArray();

            var customers = await Task.WhenAll(tasks);

            Assert.Equal(
                Enumerable.Range(0, 8),
                customers.Select(customer => customer.Id).OrderBy(id => id));
            Assert.All(customers, customer => Assert.Equal("customer-" + customer.Id, customer.Name));
            Assert.Equal(1, runtime.MaterializationPlanCacheEntryCount);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GeneratedMaterializersShouldRemainConfigurationScopedUnderConcurrentMaterialization()
        {
            var generatedA = CreateRuntime(builder =>
            {
                builder.AddMap(new CurrentCustomerMap());
                builder.AddGeneratedMaterializer(CustomerGeneratedColumns(), record => ReadGeneratedCustomer(record, "A"));
            });
            var generatedB = CreateRuntime(builder =>
            {
                builder.AddMap(new CurrentCustomerMap());
                builder.AddGeneratedMaterializer(CustomerGeneratedColumns(), record => ReadGeneratedCustomer(record, "B"));
            });
            var start = new Barrier(2);

            var firstTask = Task.Run(() => QueryGeneratedCustomer(generatedA, start));
            var secondTask = Task.Run(() => QueryGeneratedCustomer(generatedB, start));

            var customers = await Task.WhenAll(firstTask, secondTask);

            Assert.Equal("A:Ada", customers[0].Name);
            Assert.Equal("B:Ada", customers[1].Name);
            Assert.Equal(0, generatedA.MaterializationPlanCacheEntryCount);
            Assert.Equal(0, generatedB.MaterializationPlanCacheEntryCount);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SameConverterTypeShouldRemainScopedToDifferentRuntimeMappings()
        {
            var current = CreateRuntime(builder => builder.AddMap(new UpperConverterCustomerMap()));
            var legacy = CreateRuntime(builder => builder.AddMap(new LegacyUpperConverterCustomerMap()));
            var start = new Barrier(2);

            var currentTask = Task.Run(() =>
            {
                start.SignalAndWait();
                using (var connection = OpenConnection())
                {
                    return current.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 1 AS customer_id, 'ada' AS customer_name;");
                }
            });
            var legacyTask = Task.Run(() =>
            {
                start.SignalAndWait();
                using (var connection = OpenConnection())
                {
                    return legacy.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 2 AS customer_id, 'grace' AS legacy_name;");
                }
            });

            var customers = await Task.WhenAll(currentTask, legacyTask);

            Assert.Equal("ADA", customers[0].Name);
            Assert.Equal("GRACE", customers[1].Name);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task ProfilesConvertersAndDiagnosticsShouldStayIsolatedAcrossConcurrentRuntimes()
        {
            var upper = CreateRuntime(builder =>
            {
                builder.AddProfile<UpperProfileCustomerMap>();
                builder.AddMap(new UpperConverterCustomerMap());
            });
            var bracket = CreateRuntime(builder =>
            {
                builder.AddProfile<BracketProfileCustomerMap>();
                builder.AddMap(new BracketConverterCustomerMap());
            });
            var start = new Barrier(4);

            var upperProfileTask = Task.Run(() => QueryProfileCustomer<RuntimeProfile>(upper, start, "profile_id", "ada"));
            var bracketProfileTask = Task.Run(() => QueryProfileCustomer<RuntimeProfile>(bracket, start, "profile_id", "grace"));
            var upperDiagnosticTask = Task.Run(() => ExplainNameColumn(upper, start));
            var bracketDiagnosticTask = Task.Run(() => ExplainNameColumn(bracket, start));

            var upperProfile = await upperProfileTask;
            var bracketProfile = await bracketProfileTask;
            var diagnosticColumns = await Task.WhenAll(upperDiagnosticTask, bracketDiagnosticTask);

            Assert.Equal("ADA", upperProfile.Name);
            Assert.Equal("[grace]", bracketProfile.Name);
            Assert.Equal(new[] { "customer_name", "alternate_name" }, diagnosticColumns);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldUseOnlyThePublishedGlobalTypeMapForSameEntity()
        {
            ResetMapper(typeof(IsolationCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new CurrentCustomerMap()));
                var isolatedLegacy = CreateRuntime(builder => builder.AddMap(new LegacyCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var dapper = connection.QuerySingle<IsolationCustomer>(
                        "SELECT 9 AS customer_id, 'Legacy' AS legacy_name;");
                    var fluentMap = isolatedLegacy.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 9 AS customer_id, 'Legacy' AS legacy_name;");

                    Assert.Equal(9, dapper.Id);
                    Assert.Null(dapper.Name);
                    Assert.Equal("Legacy", fluentMap.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(IsolationCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void LegacyDefaultRuntimeShouldNotContaminatePreviouslyCreatedIsolatedRuntime()
        {
            ResetMapper(typeof(IsolationCustomer));

            try
            {
                var isolatedLegacy = CreateRuntime(builder => builder.AddMap(new LegacyCustomerMap()));
                FluentMapper.Initialize(configuration => configuration.AddMap(new CurrentCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var legacy = isolatedLegacy.QueryMappedSingle<IsolationCustomer>(
                        connection,
                        "SELECT 4 AS customer_id, 'Legacy' AS legacy_name;");
                    var current = connection.QueryMappedSingle<IsolationCustomer>(
                        "SELECT 5 AS customer_id, 'Current' AS customer_name;");

                    Assert.Equal("Legacy", legacy.Name);
                    Assert.Equal("Current", current.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(IsolationCustomer));
            }
        }

        [Fact]
        public void InvalidConfigurationShouldNotPoisonIndependentValidConfiguration()
        {
            var invalidMap = new InvalidAfterRegistrationMap();
            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
            {
                var invalidBuilder = new FluentMapConfigurationBuilder();
                invalidBuilder.AddMap(invalidMap);
                invalidMap.PropertyMaps.Add(null);
                invalidBuilder.Build();
            });

            var validRuntime = CreateRuntime(builder => builder.AddMap(new CurrentCustomerMap()));

            Assert.Contains("configuration validation found", exception.Message);
            validRuntime.Validate();
            Assert.Equal("customer_name", ExplainNameColumn(validRuntime));
        }

        private static FluentMapRuntime CreateRuntime(Action<FluentMapConfigurationBuilder> configure)
        {
            var builder = new FluentMapConfigurationBuilder();
            configure(builder);
            return builder.Build().CreateRuntime();
        }

        private static IsolationCustomer QueryGeneratedCustomer(FluentMapRuntime runtime, Barrier start)
        {
            start.SignalAndWait();
            using (var connection = OpenConnection())
            {
                return runtime.QueryMappedSingle<IsolationCustomer>(
                    connection,
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name;");
            }
        }

        private static IsolationCustomer QueryProfileCustomer<TProfile>(
            FluentMapRuntime runtime,
            Barrier start,
            string idColumn,
            string name)
            where TProfile : IMappingProfile
        {
            start.SignalAndWait();
            using (var connection = OpenConnection())
            {
                return runtime.QueryMappedSingle<IsolationCustomer, TProfile>(
                    connection,
                    $"SELECT 7 AS {idColumn}, '{name}' AS profile_name;");
            }
        }

        private static string ExplainNameColumn(FluentMapRuntime runtime, Barrier start = null)
        {
            if (start != null)
            {
                start.SignalAndWait();
            }

            return runtime.Explain<IsolationCustomer>()
                .Members
                .Single(member => member.MemberPath == nameof(IsolationCustomer.Name))
                .ColumnName;
        }

        private static GeneratedMaterializerColumn[] CustomerGeneratedColumns()
        {
            return new[]
            {
                GeneratedMaterializerColumn.Map("customer_id", nameof(IsolationCustomer.Id)),
                GeneratedMaterializerColumn.Map("customer_name", nameof(IsolationCustomer.Name))
            };
        }

        private static IsolationCustomer ReadGeneratedCustomer(IDataRecord record, string prefix)
        {
            return new IsolationCustomer
            {
                Id = Convert.ToInt32(record.GetValue(0)),
                Name = prefix + ":" + Convert.ToString(record.GetValue(1))
            };
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void ResetMapper(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class RuntimeProfile : IMappingProfile
        {
        }

        private sealed class IsolationCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class CurrentCustomerMap : EntityMap<IsolationCustomer>
        {
            public CurrentCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class LegacyCustomerMap : EntityMap<IsolationCustomer>
        {
            public LegacyCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("legacy_name");
            }
        }

        private sealed class UpperProfileCustomerMap :
            EntityMap<IsolationCustomer>,
            IProfileMap<RuntimeProfile>
        {
            public UpperProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("profile_id");
                Map(customer => customer.Name)
                    .ToColumn("profile_name")
                    .ConvertFromDatabaseUsing<UpperNameConverter, string>();
            }
        }

        private sealed class BracketProfileCustomerMap :
            EntityMap<IsolationCustomer>,
            IProfileMap<RuntimeProfile>
        {
            public BracketProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("profile_id");
                Map(customer => customer.Name)
                    .ToColumn("profile_name")
                    .ConvertFromDatabaseUsing<BracketNameConverter, string>();
            }
        }

        private sealed class UpperConverterCustomerMap : EntityMap<IsolationCustomer>
        {
            public UpperConverterCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name)
                    .ToColumn("customer_name")
                    .ConvertFromDatabaseUsing<UpperNameConverter, string>();
            }
        }

        private sealed class LegacyUpperConverterCustomerMap : EntityMap<IsolationCustomer>
        {
            public LegacyUpperConverterCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name)
                    .ToColumn("legacy_name")
                    .ConvertFromDatabaseUsing<UpperNameConverter, string>();
            }
        }

        private sealed class BracketConverterCustomerMap : EntityMap<IsolationCustomer>
        {
            public BracketConverterCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name)
                    .ToColumn("alternate_name")
                    .ConvertFromDatabaseUsing<BracketNameConverter, string>();
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
