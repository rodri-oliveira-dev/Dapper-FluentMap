using System;
using System.Data;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class QueryMultipleMappedTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMultipleMappedShouldCreateReaderAndReadFirstResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var connection = OpenConnection())
                using (var multi = connection.QueryMultipleMapped(
                    "SELECT 1 AS customer_id, 'Ada' AS customer_name;"))
                {
                    var customers = multi.ReadMapped<MappedCustomer>().ToList();

                    Assert.Collection(
                        customers,
                        customer =>
                        {
                            Assert.Equal(1, customer.Id);
                            Assert.Equal("Ada", customer.Name);
                        });
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldReadSequentialResultSets()
        {
            PreTest(typeof(MappedCustomer), typeof(MappedOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddMap(new MappedOrderMap());
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "customer_id", "customer_name" },
                        new object[] { 1, "Ada" },
                        new object[] { 2, "Grace" }),
                    CreateTable(
                        new[] { "order_id", "total" },
                        new object[] { 10, 12.5m })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customers = multi.ReadMapped<MappedCustomer>().ToList();
                    var orders = multi.ReadMapped<MappedOrder>().ToList();

                    Assert.Collection(
                        customers,
                        first =>
                        {
                            Assert.Equal(1, first.Id);
                            Assert.Equal("Ada", first.Name);
                        },
                        second =>
                        {
                            Assert.Equal(2, second.Id);
                            Assert.Equal("Grace", second.Name);
                        });
                    Assert.Collection(
                        orders,
                        order =>
                        {
                            Assert.Equal(10, order.Id);
                            Assert.Equal(12.5m, order.Total);
                        });
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer), typeof(MappedOrder));
            }
        }

        [Fact]
        public void ReadMappedSingleShouldMaterializeExactlyOneRowAndAdvanceResultSet()
        {
            PreTest(typeof(MappedCustomer), typeof(MappedOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddMap(new MappedOrderMap());
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "customer_id", "customer_name" },
                        new object[] { 1, "Ada" }),
                    CreateTable(
                        new[] { "order_id", "total" },
                        new object[] { 10, 12.5m })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<MappedCustomer>();
                    var order = multi.ReadMappedSingle<MappedOrder>();

                    Assert.Equal(1, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                    Assert.Equal(10, order.Id);
                    Assert.Equal(12.5m, order.Total);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer), typeof(MappedOrder));
            }
        }

        [Fact]
        public void ReadMappedSingleShouldUseProfileForCurrentResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddProfile<LegacyCustomerMap>());

                using (var reader = CreateReader(CreateTable(
                    new[] { "legacy_id", "legal_name" },
                    new object[] { 7, "Legacy Ltd." })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<MappedCustomer, LegacyProfile>();

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("Legacy Ltd.", customer.Name);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedSingleShouldThrowForEmptyResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var reader = CreateReader(CreateTable(new[] { "customer_id", "customer_name" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var exception = Assert.Throws<InvalidOperationException>(
                        () => multi.ReadMappedSingle<MappedCustomer>());

                    Assert.Contains("no elements", exception.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldUseProfileForCurrentResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddProfile<LegacyCustomerMap>());

                using (var reader = CreateReader(CreateTable(
                    new[] { "legacy_id", "legal_name" },
                    new object[] { 7, "Legacy Ltd." })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMapped<MappedCustomer, LegacyProfile>().Single();

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("Legacy Ltd.", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldKeepDefaultAndProfileResultSetsIsolated()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddProfile<LegacyCustomerMap>();
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "customer_id", "customer_name" },
                        new object[] { 1, "Default" }),
                    CreateTable(
                        new[] { "legacy_id", "legal_name" },
                        new object[] { 2, "Legacy" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var defaultCustomer = multi.ReadMappedSingle<MappedCustomer>();
                    var legacyCustomer = multi.ReadMappedSingle<MappedCustomer, LegacyProfile>();

                    Assert.Equal(1, defaultCustomer.Id);
                    Assert.Equal("Default", defaultCustomer.Name);
                    Assert.Equal(2, legacyCustomer.Id);
                    Assert.Equal("Legacy", legacyCustomer.Name);
                    Assert.Equal(2, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldApplyNamingPolicyAndConventionInCurrentResultSet()
        {
            PreTest(typeof(PolicyConventionCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false).ForEntity<PolicyConventionCustomer>();
                    configuration.AddConvention<LegalNameConvention>().ForEntity<PolicyConventionCustomer>();
                });

                using (var reader = CreateReader(CreateTable(
                    new[] { "CUSTOMER_ID", "legal_name" },
                    new object[] { 8, "Policy" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<PolicyConventionCustomer>();

                    Assert.Equal(8, customer.CustomerId);
                    Assert.Equal("Policy", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(PolicyConventionCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldMaterializeImmutableNestedObjectsAndValueObjects()
        {
            PreTest(typeof(ComplexCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ComplexCustomerMap()));

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "city", "email" },
                    new object[] { 13, "Sao Paulo", "ada@example.com" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<ComplexCustomer>();

                    Assert.Equal(13, customer.Id);
                    Assert.NotNull(customer.Address);
                    Assert.Equal("Sao Paulo", customer.Address.City);
                    Assert.Equal(new ComplexEmail("ada@example.com"), customer.Email);
                }
            }
            finally
            {
                PreTest(typeof(ComplexCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldPreserveNestedNullSemantics()
        {
            PreTest(typeof(ComplexCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ComplexCustomerMap()));

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "city", "email" },
                    new object[] { 14, DBNull.Value, DBNull.Value })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<ComplexCustomer>();

                    Assert.Equal(14, customer.Id);
                    Assert.Null(customer.Address);
                    Assert.Null(customer.Email);
                }
            }
            finally
            {
                PreTest(typeof(ComplexCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldReturnEmptyCollectionForEmptyResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var reader = CreateReader(CreateTable(new[] { "customer_id", "customer_name" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customers = multi.ReadMapped<MappedCustomer>().ToList();

                    Assert.Empty(customers);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldThrowAfterFinalResultSet()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "customer_name" },
                    new object[] { 1, "Ada" })))
                using (var multi = new MappedGridReader(reader))
                {
                    Assert.Single(multi.ReadMapped<MappedCustomer>());

                    var exception = Assert.Throws<InvalidOperationException>(
                        () => multi.ReadMapped<MappedCustomer>());

                    Assert.Contains("no remaining result sets", exception.Message, StringComparison.OrdinalIgnoreCase);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldThrowAfterDispose()
        {
            using (var reader = CreateReader(CreateTable(new[] { "Id" }, new object[] { 1 })))
            {
                var multi = new MappedGridReader(reader);

                multi.Dispose();

                Assert.Throws<ObjectDisposedException>(() => multi.ReadMapped<DefaultEntity>());
            }
        }

        [Fact]
        public void DisposeAfterPartialConsumptionShouldCloseReader()
        {
            PreTest(typeof(MappedCustomer), typeof(MappedOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddMap(new MappedOrderMap());
                });

                var reader = CreateReader(
                    CreateTable(
                        new[] { "customer_id", "customer_name" },
                        new object[] { 1, "Ada" }),
                    CreateTable(
                        new[] { "order_id", "total" },
                        new object[] { 10, 12.5m }));
                var multi = new MappedGridReader(reader);

                Assert.Single(multi.ReadMapped<MappedCustomer>());

                multi.Dispose();

                Assert.True(reader.IsClosed);
                Assert.True(multi.IsConsumed);
                Assert.Throws<ObjectDisposedException>(() => multi.ReadMapped<MappedOrder>());
            }
            finally
            {
                PreTest(typeof(MappedCustomer), typeof(MappedOrder));
            }
        }

        [Fact]
        public void ReadMappedShouldDisposeReaderWhenMaterializationThrows()
        {
            PreTest(typeof(ThrowingCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ThrowingCustomerMap()));

                var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "cpf" },
                    new object[] { 1, string.Empty }));
                var multi = new MappedGridReader(reader);

                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => multi.ReadMapped<ThrowingCustomer>());

                Assert.IsType<ArgumentException>(exception.InnerException);
                Assert.True(reader.IsClosed);
                Assert.True(multi.IsConsumed);
                Assert.Throws<ObjectDisposedException>(() => multi.ReadMapped<ThrowingCustomer>());
            }
            finally
            {
                PreTest(typeof(ThrowingCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldUseGeneratedMaterializerWhenRegistered()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(MappedCustomer.Id)),
                            GeneratedMaterializerColumn.Map("customer_name", nameof(MappedCustomer.Name))
                        },
                        record => new MappedCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "generated:" + Convert.ToString(record.GetValue(1))
                        });
                });

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "customer_name" },
                    new object[] { 3, "Ada" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMapped<MappedCustomer>().Single();

                    Assert.Equal(3, customer.Id);
                    Assert.Equal("generated:Ada", customer.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldUseGeneratedProfileMaterializersWithoutCollisions()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());
                    configuration.AddProfile<LegacyCustomerMap>();
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(MappedCustomer.Id)),
                            GeneratedMaterializerColumn.Map("customer_name", nameof(MappedCustomer.Name))
                        },
                        record => new MappedCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "default-generated:" + Convert.ToString(record.GetValue(1))
                        });
                    configuration.AddGeneratedMaterializer<MappedCustomer, LegacyProfile>(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("legacy_id", nameof(MappedCustomer.Id)),
                            GeneratedMaterializerColumn.Map("legal_name", nameof(MappedCustomer.Name))
                        },
                        record => new MappedCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "profile-generated:" + Convert.ToString(record.GetValue(1))
                        });
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "customer_id", "customer_name" },
                        new object[] { 3, "Default" }),
                    CreateTable(
                        new[] { "legacy_id", "legal_name" },
                        new object[] { 4, "Legacy" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var defaultCustomer = multi.ReadMappedSingle<MappedCustomer>();
                    var legacyCustomer = multi.ReadMappedSingle<MappedCustomer, LegacyProfile>();

                    Assert.Equal("default-generated:Default", defaultCustomer.Name);
                    Assert.Equal("profile-generated:Legacy", legacyCustomer.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void ReadMappedGeneratedAndRuntimeShouldReturnEquivalentResultsForSameShape()
        {
            var generated = MaterializeGeneratedCustomer(registerGeneratedMaterializer: true);
            var runtime = MaterializeGeneratedCustomer(registerGeneratedMaterializer: false);

            Assert.Equal(generated.Id, runtime.Id);
            Assert.Equal(generated.Name, runtime.Name);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedAndReadMappedShouldReturnEquivalentResults()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var connection = OpenConnection())
                {
                    const string sql = "SELECT 21 AS customer_id, 'Equivalent' AS customer_name;";

                    var queryMapped = connection.QueryMappedSingle<MappedCustomer>(sql);
                    using (var multi = connection.QueryMultipleMapped(sql))
                    {
                        var readMapped = multi.ReadMappedSingle<MappedCustomer>();

                        Assert.Equal(queryMapped.Id, readMapped.Id);
                        Assert.Equal(queryMapped.Name, readMapped.Name);
                    }
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        public void MappedConventionShouldApplyToTypedReadFromMultipleResults()
        {
            PreTest(typeof(ConventionCustomer), typeof(ConventionOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddConvention<ColumnPrefixConvention>().ForEntity<ConventionCustomer>();
                    configuration.AddConvention<ColumnPrefixConvention>().ForEntity<ConventionOrder>();
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "colId", "colName" },
                        new object[] { 1, "Ada" }),
                    CreateTable(
                        new[] { "colId", "colTotal" },
                        new object[] { 20, 99.5m })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<ConventionCustomer>();
                    var order = multi.ReadMappedSingle<ConventionOrder>();

                    Assert.Equal(1, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                    Assert.Equal(20, order.Id);
                    Assert.Equal(99.5m, order.Total);
                }
            }
            finally
            {
                PreTest(typeof(ConventionCustomer), typeof(ConventionOrder));
            }
        }

        [Fact]
        public void ExplicitMapShouldApplyToLaterTypedReadFromMultipleResults()
        {
            PreTest(typeof(HistoricalRow), typeof(HistoricalTotal), typeof(HistoricalColumn));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new HistoricalRowMap());
                    configuration.AddMap(new HistoricalTotalMap());
                    configuration.AddMap(new HistoricalColumnMap());
                });

                using (var reader = CreateReader(
                    CreateTable(
                        new[] { "row_no" },
                        new object[] { 1 }),
                    CreateTable(
                        new[] { "total" },
                        new object[] { 1 }),
                    CreateTable(
                        new[]
                        {
                            "column_prefix",
                            "column_name",
                            "display_order",
                            "can_be_ordered",
                            "can_be_filtered",
                            "column_width_in_pixels"
                        },
                        new object[] { "usr", "name", 2, true, false, 160 })))
                using (var multi = new MappedGridReader(reader))
                {
                    var row = multi.ReadMappedSingle<HistoricalRow>();
                    var total = multi.ReadMappedSingle<HistoricalTotal>();
                    var column = multi.ReadMappedSingle<HistoricalColumn>();

                    Assert.Equal(1, row.RowNo);
                    Assert.Equal(1, total.Total);
                    Assert.Equal("usr", column.Prefix);
                    Assert.Equal("name", column.Name);
                    Assert.Equal(2, column.DisplayOrder);
                    Assert.True(column.CanBeOrdered);
                    Assert.False(column.CanBeFiltered);
                    Assert.Equal(160, column.WidthInPixels);
                }
            }
            finally
            {
                PreTest(typeof(HistoricalRow), typeof(HistoricalTotal), typeof(HistoricalColumn));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMultipleMappedShouldPassCommandParameters()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var connection = OpenConnection())
                using (var multi = connection.QueryMultipleMapped(
                    "SELECT @id AS customer_id, @name AS customer_name;",
                    new { id = 5, name = "Katherine" }))
                {
                    var customer = multi.ReadMapped<MappedCustomer>().Single();

                    Assert.Equal(5, customer.Id);
                    Assert.Equal("Katherine", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMultipleMappedShouldPropagateTransaction()
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MappedCustomerMap()));

                using (var connection = OpenConnection())
                {
                    connection.Execute("CREATE TABLE customers (customer_id INTEGER NOT NULL, customer_name TEXT NOT NULL);");

                    using (var transaction = connection.BeginTransaction())
                    {
                        connection.Execute(
                            "INSERT INTO customers (customer_id, customer_name) VALUES (9, 'Transaction');",
                            transaction: transaction);

                        using (var multi = connection.QueryMultipleMapped(
                            "SELECT customer_id, customer_name FROM customers WHERE customer_id = @id;",
                            new { id = 9 },
                            transaction))
                        {
                            var customer = multi.ReadMapped<MappedCustomer>().Single();

                            Assert.Equal(9, customer.Id);
                            Assert.Equal("Transaction", customer.Name);
                        }

                        transaction.Rollback();
                    }
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMultipleMappedShouldCloseConnectionItOpened()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var multi = connection.QueryMultipleMapped("SELECT 1 AS Id;");

                Assert.Equal(ConnectionState.Open, connection.State);

                multi.Dispose();

                Assert.Equal(ConnectionState.Closed, connection.State);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMultipleMappedShouldKeepOpenConnectionOpenAfterDispose()
        {
            using (var connection = OpenConnection())
            {
                using (var multi = connection.QueryMultipleMapped("SELECT 1 AS Id;"))
                {
                    Assert.Equal(ConnectionState.Open, connection.State);
                }

                Assert.Equal(ConnectionState.Open, connection.State);
            }
        }

        private static DataTableReader CreateReader(params DataTable[] tables)
        {
            return new DataTableReader(tables);
        }

        private static DataTable CreateTable(string[] columns, params object[][] rows)
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

            return table;
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static MappedCustomer MaterializeGeneratedCustomer(bool registerGeneratedMaterializer)
        {
            PreTest(typeof(MappedCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MappedCustomerMap());

                    if (registerGeneratedMaterializer)
                    {
                        configuration.AddGeneratedMaterializer(
                            new[]
                            {
                                GeneratedMaterializerColumn.Map("customer_id", nameof(MappedCustomer.Id)),
                                GeneratedMaterializerColumn.Map("customer_name", nameof(MappedCustomer.Name))
                            },
                            record => new MappedCustomer
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                Name = Convert.ToString(record.GetValue(1))
                            });
                    }
                });

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "customer_name" },
                    new object[] { 31, "Same Shape" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<MappedCustomer>();
                    Assert.Equal(registerGeneratedMaterializer ? 0 : 1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                    return customer;
                }
            }
            finally
            {
                PreTest(typeof(MappedCustomer));
            }
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private sealed class DefaultEntity
        {
            public int Id { get; set; }
        }

        private sealed class MappedCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class MappedCustomerMap : EntityMap<MappedCustomer>
        {
            public MappedCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class LegacyCustomerMap : EntityMap<MappedCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legal_name");
            }
        }

        private sealed class MappedOrder
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }

        private sealed class MappedOrderMap : EntityMap<MappedOrder>
        {
            public MappedOrderMap()
            {
                Map(order => order.Id).ToColumn("order_id");
                Map(order => order.Total).ToColumn("total");
            }
        }

        private sealed class PolicyConventionCustomer
        {
            public int CustomerId { get; set; }

            public string Name { get; set; }
        }

        private sealed class LegalNameConvention : Convention
        {
            public LegalNameConvention()
            {
                Properties<string>()
                    .Where(property => property.Name == nameof(PolicyConventionCustomer.Name))
                    .Configure(property => property.HasColumnName("legal_name"));
            }
        }

        private sealed class ComplexCustomer
        {
            public ComplexCustomer(int id, ComplexAddress address, ComplexEmail email)
            {
                Id = id;
                Address = address;
                Email = email;
            }

            public int Id { get; }

            public ComplexAddress Address { get; }

            public ComplexEmail Email { get; }
        }

        private sealed class ComplexAddress
        {
            public ComplexAddress(string city)
            {
                City = city;
            }

            public string City { get; }
        }

        private sealed record ComplexEmail(string Value);

        private sealed class ComplexCustomerMap : EntityMap<ComplexCustomer>
        {
            public ComplexCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("city");
                Map(customer => customer.Email.Value).ToColumn("email");
            }
        }

        private sealed class ConventionCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class ConventionOrder
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }

        private sealed class ColumnPrefixConvention : Convention
        {
            public ColumnPrefixConvention()
            {
                Properties()
                    .Configure(property => property.HasPrefix("col"));
            }
        }

        private sealed class HistoricalRow
        {
            public int RowNo { get; set; }
        }

        private sealed class HistoricalRowMap : EntityMap<HistoricalRow>
        {
            public HistoricalRowMap()
            {
                Map(row => row.RowNo).ToColumn("row_no");
            }
        }

        private sealed class HistoricalTotal
        {
            public int Total { get; set; }
        }

        private sealed class HistoricalTotalMap : EntityMap<HistoricalTotal>
        {
            public HistoricalTotalMap()
            {
                Map(total => total.Total).ToColumn("total");
            }
        }

        private sealed class HistoricalColumn
        {
            public string Prefix { get; set; }

            public string Name { get; set; }

            public int DisplayOrder { get; set; }

            public bool CanBeOrdered { get; set; }

            public bool CanBeFiltered { get; set; }

            public int WidthInPixels { get; set; }
        }

        private sealed class HistoricalColumnMap : EntityMap<HistoricalColumn>
        {
            public HistoricalColumnMap()
            {
                Map(column => column.Prefix).ToColumn("column_prefix");
                Map(column => column.Name).ToColumn("column_name");
                Map(column => column.DisplayOrder).ToColumn("display_order");
                Map(column => column.CanBeOrdered).ToColumn("can_be_ordered");
                Map(column => column.CanBeFiltered).ToColumn("can_be_filtered");
                Map(column => column.WidthInPixels).ToColumn("column_width_in_pixels");
            }
        }

        private sealed class ThrowingCustomer
        {
            public ThrowingCustomer(int id, ThrowingCpf cpf)
            {
                Id = id;
                Cpf = cpf;
            }

            public int Id { get; }

            public ThrowingCpf Cpf { get; }
        }

        private sealed class ThrowingCustomerMap : EntityMap<ThrowingCustomer>
        {
            public ThrowingCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
            }
        }

        private sealed class ThrowingCpf
        {
            public ThrowingCpf(string number)
            {
                if (string.IsNullOrWhiteSpace(number))
                {
                    throw new ArgumentException("CPF cannot be empty.", nameof(number));
                }

                Number = number;
            }

            public string Number { get; }
        }
    }
}
