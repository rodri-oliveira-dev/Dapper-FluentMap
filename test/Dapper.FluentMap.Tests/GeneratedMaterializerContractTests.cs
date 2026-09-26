using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class GeneratedMaterializerContractTests
    {
        [Fact]
        public void RegistryShouldResolveRegisteredGeneratedMaterializer()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer);
                });

                var found = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(GeneratedContractCustomer),
                    profileType: null,
                    columnNames: new[] { "customer_id", "full_name" },
                    out var materializer);

                using (var reader = CreateReader(
                    new[] { "customer_id", "full_name" },
                    new object[] { 11, "Ada" }))
                {
                    Assert.True(found);
                    Assert.NotNull(materializer);
                    Assert.True(reader.Read());

                    var customer = Assert.IsType<GeneratedContractCustomer>(materializer(reader));
                    Assert.Equal(11, customer.Id);
                    Assert.Equal("generated:Ada", customer.Name);
                    Assert.Equal(1, FluentMapper.Registry.GeneratedMaterializerCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void RegistryShouldReturnFalseWhenGeneratedMaterializerIsMissing()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new GeneratedContractCustomerMap()));

                var found = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(GeneratedContractCustomer),
                    profileType: null,
                    columnNames: new[] { "customer_id", "full_name" },
                    out var materializer);

                Assert.False(found);
                Assert.Null(materializer);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void RegistryShouldResolveGeneratedMaterializerByProfile()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddProfile<GeneratedContractCustomerLegacyMap>();
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer);
                    configuration.AddGeneratedMaterializer<GeneratedContractCustomer, GeneratedLegacyProfile>(
                        LegacyColumns(),
                        ReadLegacyGeneratedCustomer);
                });

                var defaultFound = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(GeneratedContractCustomer),
                    profileType: null,
                    columnNames: new[] { "customer_id", "full_name" },
                    out var defaultMaterializer);
                var profileFound = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(GeneratedContractCustomer),
                    typeof(GeneratedLegacyProfile),
                    new[] { "legacy_id", "legal_name" },
                    out var profileMaterializer);

                Assert.True(defaultFound);
                Assert.True(profileFound);
                Assert.NotSame(defaultMaterializer, profileMaterializer);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void RegistryShouldRejectDuplicateGeneratedMaterializerForSameShape()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(configuration =>
                    {
                        configuration.AddGeneratedMaterializer(
                            DefaultColumns(),
                            ReadDefaultGeneratedCustomer);
                        configuration.AddGeneratedMaterializer(
                            DefaultColumns(),
                            ReadDefaultGeneratedCustomer);
                    }));

                Assert.Contains("already has a generated materializer", exception.Message);
                Assert.Equal(1, FluentMapper.Registry.GeneratedMaterializerCount);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void RegistryShouldIgnoreGeneratedMaterializerWhenContractDoesNotMatchEffectiveMapping()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(GeneratedContractCustomer.Name)),
                            GeneratedMaterializerColumn.Map("full_name", nameof(GeneratedContractCustomer.Name))
                        },
                        ReadDefaultGeneratedCustomer);
                });

                var found = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(GeneratedContractCustomer),
                    profileType: null,
                    columnNames: new[] { "customer_id", "full_name" },
                    out var materializer);

                Assert.False(found);
                Assert.Null(materializer);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void DescriptorShouldRejectInvalidContracts()
        {
            var columns = DefaultColumns();

            Assert.Throws<ArgumentException>(() => GeneratedMaterializerColumn.Map(" ", nameof(GeneratedContractCustomer.Id)));
            Assert.Throws<ArgumentException>(() => GeneratedMaterializerColumn.Map("customer_id", string.Empty));
            Assert.Throws<ArgumentNullException>(() => new GeneratedMaterializerDescriptor<GeneratedContractCustomer>(columns, null));
            Assert.Throws<ArgumentException>(() => new GeneratedMaterializerDescriptor<GeneratedContractCustomer>(
                typeof(GeneratedContractCustomer),
                columns,
                ReadDefaultGeneratedCustomer));
            Assert.Throws<ArgumentException>(() => new GeneratedMaterializerDescriptor<GeneratedContractCustomer>(
                new GeneratedMaterializerColumn[] { null },
                ReadDefaultGeneratedCustomer));
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseGeneratedMaterializerWhenRegistered()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer);
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 31 AS customer_id, 'Ada' AS full_name;");

                    Assert.Equal(31, customer.Id);
                    Assert.Equal("generated:Ada", customer.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldFallBackToRuntimeWhenGeneratedMaterializerIsMissing()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new GeneratedContractCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 41 AS customer_id, 'Runtime' AS full_name;");

                    Assert.Equal(41, customer.Id);
                    Assert.Equal("Runtime", customer.Name);
                    Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldUseRegisteredGeneratedMaterializer()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer)
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var customer = runtime.QueryMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 81 AS customer_id, 'Strict' AS full_name;");

                Assert.Equal(81, customer.Id);
                Assert.Equal("generated:Strict", customer.Name);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryGeneratedMappedShouldUseRegisteredGeneratedMaterializerWithoutRuntimeFallback()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer)
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var customer = runtime.QueryGeneratedMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 86 AS customer_id, 'GeneratedOnly' AS full_name;");

                Assert.Equal(86, customer.Id);
                Assert.Equal("generated:GeneratedOnly", customer.Name);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldRejectMissingGeneratedMaterializer()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    runtime.QueryMappedSingle<GeneratedContractCustomer>(
                        connection,
                        "SELECT 91 AS customer_id, 'Missing' AS full_name;"));

                Assert.Contains("Strict generated materialization is enabled", exception.Message);
                Assert.Contains(typeof(GeneratedContractCustomer).FullName, exception.Message);
                Assert.Contains("no registered generated materializer", exception.Message);
                Assert.Contains("['customer_id', 'full_name']", exception.Message);
                Assert.DoesNotContain("SELECT", exception.Message);
                Assert.DoesNotContain("Data Source", exception.Message);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldRejectUnsupportedColumnShape()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer)
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    runtime.QueryMappedSingle<GeneratedContractCustomer>(
                        connection,
                        "SELECT 92 AS customer_id;"));

                Assert.Contains("none match requested result columns", exception.Message);
                Assert.Contains("['customer_id']", exception.Message);
                Assert.Contains("['customer_id', 'full_name']", exception.Message);
                Assert.DoesNotContain("SELECT", exception.Message);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldUseGeneratedMaterializerForReorderedColumns()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer)
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var customer = runtime.QueryMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 'Reordered' AS full_name, 94 AS customer_id;");

                Assert.Equal(94, customer.Id);
                Assert.Equal("generated:Reordered", customer.Name);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldRejectIncompatibleGeneratedContract()
        {
            var runtime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(GeneratedContractCustomer.Name)),
                            GeneratedMaterializerColumn.Map("full_name", nameof(GeneratedContractCustomer.Name))
                        },
                        ReadDefaultGeneratedCustomer)
                    .UseStrictGeneratedMaterialization();
            });

            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    runtime.QueryMappedSingle<GeneratedContractCustomer>(
                        connection,
                        "SELECT 93 AS customer_id, 'Mismatch' AS full_name;"));

                Assert.Contains("member/converter contract does not match", exception.Message);
                Assert.Contains("['customer_id', 'full_name']", exception.Message);
                Assert.DoesNotContain("SELECT", exception.Message);
                Assert.Equal(0, runtime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldRemainIsolatedFromLegacyFallbackRuntime()
        {
            var strictRuntime = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .UseStrictGeneratedMaterialization();
            });
            var fallbackRuntime = CreateRuntime(builder => builder.AddMap(new GeneratedContractCustomerMap()));

            using (var connection = OpenConnection())
            {
                Assert.Throws<FluentMapConfigurationException>(() =>
                    strictRuntime.QueryMappedSingle<GeneratedContractCustomer>(
                        connection,
                        "SELECT 101 AS customer_id, 'Strict' AS full_name;"));

                var customer = fallbackRuntime.QueryMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 102 AS customer_id, 'Fallback' AS full_name;");

                Assert.Equal(102, customer.Id);
                Assert.Equal("Fallback", customer.Name);
                Assert.Equal(0, strictRuntime.MaterializationPlanCacheEntryCount);
                Assert.Equal(1, fallbackRuntime.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseGeneratedProfileMaterializerWhenRegistered()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddProfile<GeneratedContractCustomerLegacyMap>();
                    configuration.AddGeneratedMaterializer<GeneratedContractCustomer, GeneratedLegacyProfile>(
                        LegacyColumns(),
                        ReadLegacyGeneratedCustomer);
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<GeneratedContractCustomer, GeneratedLegacyProfile>(
                        "SELECT 51 AS legacy_id, 'Ada' AS legal_name;");

                    Assert.Equal(51, customer.Id);
                    Assert.Equal("legacy:Ada", customer.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldFallBackToRuntimeWhenGeneratedContractDoesNotMatchEffectiveMapping()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(GeneratedContractCustomer.Name)),
                            GeneratedMaterializerColumn.Map("full_name", nameof(GeneratedContractCustomer.Name))
                        },
                        ReadDefaultGeneratedCustomer);
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 61 AS customer_id, 'Runtime' AS full_name;");

                    Assert.Equal(61, customer.Id);
                    Assert.Equal("Runtime", customer.Name);
                    Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedGeneratedMaterializerShouldSupportReorderedColumnsWithoutRuntimeFallback()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        record => new GeneratedContractCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = Convert.ToString(record.GetValue(1))
                        });
                });

                using (var connection = OpenConnection())
                {
                    var generated = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 71 AS customer_id, 'Equivalent' AS full_name;");
                    var reordered = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 'Equivalent' AS full_name, 71 AS customer_id;");
                    var repeatedReordered = connection.QueryMappedSingle<GeneratedContractCustomer>(
                        "SELECT 'Equivalent' AS full_name, 71 AS customer_id;");

                    Assert.Equal(generated.Id, reordered.Id);
                    Assert.Equal(generated.Name, reordered.Name);
                    Assert.Equal(reordered.Id, repeatedReordered.Id);
                    Assert.Equal(reordered.Name, repeatedReordered.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ReorderedGeneratedMaterializerResolutionShouldRemainRuntimeScoped()
        {
            var first = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        record => new GeneratedContractCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "first:" + Convert.ToString(record.GetValue(1))
                        });
            });
            var second = CreateRuntime(builder =>
            {
                builder
                    .AddMap(new GeneratedContractCustomerMap())
                    .AddGeneratedMaterializer(
                        DefaultColumns(),
                        record => new GeneratedContractCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "second:" + Convert.ToString(record.GetValue(1))
                        });
            });

            using (var connection = OpenConnection())
            {
                var firstCustomer = first.QueryMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 'Scoped' AS full_name, 111 AS customer_id;");
                var secondCustomer = second.QueryMappedSingle<GeneratedContractCustomer>(
                    connection,
                    "SELECT 'Scoped' AS full_name, 112 AS customer_id;");

                Assert.Equal("first:Scoped", firstCustomer.Name);
                Assert.Equal("second:Scoped", secondCustomer.Name);
                Assert.Equal(0, first.MaterializationPlanCacheEntryCount);
                Assert.Equal(0, second.MaterializationPlanCacheEntryCount);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedGeneratedMaterializerShouldRemainStableUnderConcurrentQueries()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                var materializedRows = 0;
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        record =>
                        {
                            Interlocked.Increment(ref materializedRows);
                            return new GeneratedContractCustomer
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                Name = Convert.ToString(record.GetValue(1))
                            };
                        });
                });

                var results = Enumerable.Range(0, 50)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            var customer = connection.QueryMappedSingle<GeneratedContractCustomer>(
                                $"SELECT {index} AS customer_id, 'generated-{index}' AS full_name;");

                            return customer.Id == index && customer.Name == $"generated-{index}";
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
                Assert.Equal(50, materializedRows);
                Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        [Fact]
        public void GeneratedLookupShouldRemainStableUnderConcurrentReads()
        {
            PreTest(typeof(GeneratedContractCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new GeneratedContractCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        DefaultColumns(),
                        ReadDefaultGeneratedCustomer);
                });

                var results = new bool[100];
                Parallel.For(0, results.Length, index =>
                {
                    results[index] = FluentMapper.Registry.TryGetGeneratedMaterializer(
                        typeof(GeneratedContractCustomer),
                        profileType: null,
                        columnNames: new[] { "customer_id", "full_name" },
                        out var materializer) && materializer != null;
                });

                Assert.All(results, Assert.True);
                Assert.Equal(1, FluentMapper.Registry.GeneratedMaterializerCount);
                Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                PreTest(typeof(GeneratedContractCustomer));
            }
        }

        private static GeneratedMaterializerColumn[] DefaultColumns()
        {
            return new[]
            {
                GeneratedMaterializerColumn.Map("customer_id", nameof(GeneratedContractCustomer.Id)),
                GeneratedMaterializerColumn.Map("full_name", nameof(GeneratedContractCustomer.Name))
            };
        }

        private static GeneratedMaterializerColumn[] LegacyColumns()
        {
            return new[]
            {
                GeneratedMaterializerColumn.Map("legacy_id", nameof(GeneratedContractCustomer.Id)),
                GeneratedMaterializerColumn.Map("legal_name", nameof(GeneratedContractCustomer.Name))
            };
        }

        private static GeneratedContractCustomer ReadDefaultGeneratedCustomer(IDataRecord record)
        {
            return new GeneratedContractCustomer
            {
                Id = Convert.ToInt32(record.GetValue(0)),
                Name = "generated:" + Convert.ToString(record.GetValue(1))
            };
        }

        private static GeneratedContractCustomer ReadLegacyGeneratedCustomer(IDataRecord record)
        {
            return new GeneratedContractCustomer
            {
                Id = Convert.ToInt32(record.GetValue(0)),
                Name = "legacy:" + Convert.ToString(record.GetValue(1))
            };
        }

        private static IDataReader CreateReader(string[] columns, params object[][] rows)
        {
            var table = new DataTable();
            foreach (var column in columns)
            {
                table.Columns.Add(column, typeof(object));
            }

            foreach (var row in rows)
            {
                table.Rows.Add(row);
            }

            return table.CreateDataReader();
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static FluentMapRuntime CreateRuntime(Action<FluentMapConfigurationBuilder> configure)
        {
            var builder = new FluentMapConfigurationBuilder();
            configure(builder);
            return builder.Build().CreateRuntime();
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class GeneratedLegacyProfile : IMappingProfile
        {
        }

        private sealed class GeneratedContractCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class GeneratedContractCustomerMap : EntityMap<GeneratedContractCustomer>
        {
            public GeneratedContractCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("full_name");
            }
        }

        private sealed class GeneratedContractCustomerLegacyMap :
            EntityMap<GeneratedContractCustomer>,
            IProfileMap<GeneratedLegacyProfile>
        {
            public GeneratedContractCustomerLegacyMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legal_name");
            }
        }
    }
}
