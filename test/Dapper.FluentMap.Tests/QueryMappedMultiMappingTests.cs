using System;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class QueryMappedMultiMappingTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeTwoSegmentsAndComposeRows()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var rows = connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total " +
                            "UNION ALL SELECT 2 AS customer_id, 'Grace' AS customer_name, 11 AS order_id, 15.75 AS total;",
                            (customer, order) => new CustomerOrder(customer, order),
                            splitOn: "order_id")
                        .ToList();

                    Assert.Collection(
                        rows,
                        row =>
                        {
                            Assert.Equal(1, row.Customer.Id);
                            Assert.Equal("Ada", row.Customer.Name);
                            Assert.Equal(10, row.Order.Id);
                            Assert.Equal(12.5m, row.Order.Total);
                        },
                        row =>
                        {
                            Assert.Equal(2, row.Customer.Id);
                            Assert.Equal("Grace", row.Customer.Name);
                            Assert.Equal(11, row.Order.Id);
                            Assert.Equal(15.75m, row.Order.Total);
                        });
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseDefaultSplitOnId()
        {
            PreTest(typeof(JoinCustomer), typeof(DefaultSplitOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new DefaultSplitOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, DefaultSplitOrder, Tuple<JoinCustomer, DefaultSplitOrder>>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS Id, 12.5 AS total;",
                            Tuple.Create)
                        .Single();

                    Assert.Equal(1, row.Item1.Id);
                    Assert.Equal(10, row.Item2.Id);
                    Assert.Equal(12.5m, row.Item2.Total);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(DefaultSplitOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldReturnNullSecondSegmentWhenEverySecondColumnIsNull()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, NULL AS order_id, NULL AS total;",
                            (customer, order) => new CustomerOrder(customer, order),
                            splitOn: "order_id")
                        .Single();

                    Assert.Equal(1, row.Customer.Id);
                    Assert.Null(row.Order);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldFailForMissingSplitOn()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                                "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total;",
                                (customer, order) => new CustomerOrder(customer, order),
                                splitOn: "missing")
                            .ToList());

                    Assert.Contains("splitOn", exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldRejectSplitOnInFirstColumn()
        {
            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    connection.QueryMapped<JoinCustomer, JoinOrder, Tuple<JoinCustomer, JoinOrder>>(
                        "SELECT 1 AS order_id, 2 AS customer_id;",
                        (customer, order) => Tuple.Create(customer, order),
                        splitOn: "order_id")
                    .ToList());

                Assert.Contains("empty row segment", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldRejectAmbiguousSplitOnIncludingFirstColumn()
        {
            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    connection.QueryMapped<JoinCustomer, JoinOrder, Tuple<JoinCustomer, JoinOrder>>(
                        "SELECT 1 AS order_id, 2 AS customer_id, 3 AS ORDER_ID;",
                        (customer, order) => Tuple.Create(customer, order),
                        splitOn: "Order_Id")
                    .ToList());

                Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldFailForAmbiguousSplitOn()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                                "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 11 AS order_id, 12.5 AS total;",
                                (customer, order) => new CustomerOrder(customer, order),
                                splitOn: "order_id")
                            .ToList());

                    Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeQueryMappedShouldUseIsolatedConfiguration()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddMap<JoinCustomerMap>()
                .AddMap<JoinOrderMap>()
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var row = runtime.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                        connection,
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total;",
                        (customer, order) => new CustomerOrder(customer, order),
                        splitOn: "order_id")
                    .Single();

                Assert.Equal(1, row.Customer.Id);
                Assert.Equal(10, row.Order.Id);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldRejectEmptySplitOn()
        {
            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<ArgumentException>(() =>
                    connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                            "SELECT 1 AS customer_id, 10 AS order_id;",
                            (customer, order) => new CustomerOrder(customer, order),
                            splitOn: " ")
                        .ToList());

                Assert.Equal("splitOn", exception.ParamName);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeImmutableChildWhenNullablePropertyIsNull()
        {
            PreTest(typeof(JoinCustomer), typeof(ImmutableOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new ImmutableOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, ImmutableOrder, Tuple<JoinCustomer, ImmutableOrder>>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, NULL AS order_note;",
                            Tuple.Create,
                            splitOn: "order_id")
                        .Single();

                    Assert.Equal(1, row.Item1.Id);
                    Assert.NotNull(row.Item2);
                    Assert.Equal(10, row.Item2.Id);
                    Assert.Null(row.Item2.Note);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(ImmutableOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyProfilesToEachSegment()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddProfile<LegacyJoinCustomerMap>();
                    configuration.AddProfile<ArchivedJoinOrderMap>();
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<
                            JoinCustomer,
                            JoinOrder,
                            CustomerOrder,
                            LegacyProfile,
                            ArchiveProfile>(
                            "SELECT 7 AS legacy_customer_id, 'Legacy' AS legal_name, 70 AS archived_order_id, 42.25 AS archived_total;",
                            (customer, order) => new CustomerOrder(customer, order),
                            splitOn: "archived_order_id")
                        .Single();

                    Assert.Equal(7, row.Customer.Id);
                    Assert.Equal("Legacy", row.Customer.Name);
                    Assert.Equal(70, row.Order.Id);
                    Assert.Equal(42.25m, row.Order.Total);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeQueryMappedShouldApplyProfiledIsolatedConfiguration()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddProfile<LegacyJoinCustomerMap>()
                .AddProfile<ArchivedJoinOrderMap>()
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var row = runtime.QueryMapped<
                        JoinCustomer,
                        JoinOrder,
                        CustomerOrder,
                        LegacyProfile,
                        ArchiveProfile>(
                        connection,
                        "SELECT 8 AS legacy_customer_id, 'Runtime Legacy' AS legal_name, 80 AS archived_order_id, 55.5 AS archived_total;",
                        (customer, order) => new CustomerOrder(customer, order),
                        splitOn: "archived_order_id")
                    .Single();

                Assert.Equal(8, row.Customer.Id);
                Assert.Equal("Runtime Legacy", row.Customer.Name);
                Assert.Equal(80, row.Order.Id);
                Assert.Equal(55.5m, row.Order.Total);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeNestedValueObjectsAndConvertersWithinSegments()
        {
            PreTest(typeof(ComplexCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new ComplexCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<ComplexCustomer, JoinOrder, Tuple<ComplexCustomer, JoinOrder>>(
                            "SELECT 3 AS customer_id, 'Sao Paulo' AS city, 'ada@example.com' AS email, 'A' AS status, " +
                            "30 AS order_id, 11.75 AS total;",
                            Tuple.Create,
                            splitOn: "order_id")
                        .Single();

                    Assert.Equal(3, row.Item1.Id);
                    Assert.NotNull(row.Item1.Address);
                    Assert.Equal("Sao Paulo", row.Item1.Address.City);
                    Assert.Equal(new ComplexEmail("ada@example.com"), row.Item1.Email);
                    Assert.Equal(AccountStatus.Active, row.Item1.Status);
                    Assert.Equal(30, row.Item2.Id);
                    Assert.Equal(11.75m, row.Item2.Total);
                }
            }
            finally
            {
                PreTest(typeof(ComplexCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseDapperTypeHandlerWithinSegment()
        {
            PreTest(typeof(JoinCustomer), typeof(TypeHandlerOrder));

            try
            {
                SqlMapper.AddTypeHandler(new CpfTypeHandler());
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new TypeHandlerOrderMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, TypeHandlerOrder, Tuple<JoinCustomer, TypeHandlerOrder>>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 99 AS order_id, '12345678909' AS tax_id;",
                            Tuple.Create,
                            splitOn: "order_id")
                        .Single();

                    Assert.Equal(99, row.Item2.Id);
                    Assert.NotNull(row.Item2.TaxId);
                    Assert.Equal("12345678909", row.Item2.TaxId.Number);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(JoinCustomer), typeof(TypeHandlerOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedGeneratedAndRuntimeFallbackShouldReturnEquivalentResults()
        {
            var generated = MaterializeCustomerOrder(registerGeneratedMaterializers: true);
            var runtime = MaterializeCustomerOrder(registerGeneratedMaterializers: false);

            Assert.Equal(runtime.Customer.Id, generated.Customer.Id);
            Assert.Equal(runtime.Customer.Name, generated.Customer.Name);
            Assert.Equal(runtime.Order.Id, generated.Order.Id);
            Assert.Equal(runtime.Order.Total, generated.Order.Total);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldCloseOwnedConnectionWhenProjectionThrows()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                });

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                                "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total;",
                                (customer, order) => throw new InvalidOperationException("projection failed"),
                                splitOn: "order_id")
                            .ToList());

                    Assert.Equal("projection failed", exception.Message);
                    Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeThreeSegmentsThroughSharedPipeline()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                    configuration.AddMap(new JoinShipmentMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total, 100 AS shipment_id, 'sent' AS shipment_status;",
                            (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                            splitOn: "order_id,shipment_id")
                        .Single();

                    Assert.Equal(1, row.Customer.Id);
                    Assert.Equal(10, row.Order.Id);
                    Assert.Equal(100, row.Shipment.Id);
                    Assert.Equal("sent", row.Shipment.Status);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async System.Threading.Tasks.Task QueryMappedAsyncShouldMaterializeThreeSegments()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                    configuration.AddMap(new JoinShipmentMap());
                });

                using (var connection = OpenConnection())
                {
                    var rows = await connection.QueryMappedAsync<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                        "SELECT 2 AS customer_id, 'Grace' AS customer_name, 20 AS order_id, 25.5 AS total, 200 AS shipment_id, 'ready' AS shipment_status;",
                        (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                        splitOn: "order_id,shipment_id",
                        cancellationToken: TestContext.Current.CancellationToken);
                    var row = rows.Single();

                    Assert.Equal(2, row.Customer.Id);
                    Assert.Equal(20, row.Order.Id);
                    Assert.Equal(200, row.Shipment.Id);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ThreeSegmentQueryShouldPassAllNullChildrenAsNull()
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());
                    configuration.AddMap(new JoinShipmentMap());
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                            "SELECT 3 AS customer_id, 'Null children' AS customer_name, NULL AS order_id, NULL AS total, NULL AS shipment_id, NULL AS shipment_status;",
                            (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                            splitOn: "order_id,shipment_id")
                        .Single();

                    Assert.NotNull(row.Customer);
                    Assert.Null(row.Order);
                    Assert.Null(row.Shipment);
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder), typeof(JoinShipment));
            }
        }

        [Theory]
        [InlineData("order_id")]
        [InlineData("order_id,")]
        [InlineData("order_id,order_id")]
        [Trait("Category", "Integration")]
        public void ThreeSegmentQueryShouldValidateEverySplitBoundary(string splitOn)
        {
            using (var connection = OpenConnection())
            {
                var exception = Assert.ThrowsAny<Exception>(() =>
                    connection.QueryMapped<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                            "SELECT 1 AS customer_id, 2 AS order_id, 3 AS shipment_id;",
                            (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                            splitOn: splitOn)
                        .ToList());

                Assert.Contains("splitOn", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ThreeSegmentQueryShouldHonorPerSegmentProfilesAndIsolatedRuntime()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddProfile<LegacyJoinCustomerMap>()
                .AddProfile<ArchivedJoinOrderMap>()
                .AddProfile<TrackedJoinShipmentMap>()
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var row = runtime.QueryMapped<
                        JoinCustomer,
                        JoinOrder,
                        JoinShipment,
                        CustomerOrderShipment,
                        LegacyProfile,
                        ArchiveProfile,
                        TrackingProfile>(
                        connection,
                        "SELECT 4 AS legacy_customer_id, 'Profiled' AS legal_name, 40 AS archived_order_id, 44.5 AS archived_total, 400 AS tracked_shipment_id, 'tracked' AS tracked_status;",
                        (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                        splitOn: "archived_order_id,tracked_shipment_id")
                    .Single();

                Assert.Equal(4, row.Customer.Id);
                Assert.Equal(40, row.Order.Id);
                Assert.Equal(400, row.Shipment.Id);
                Assert.Equal("tracked", row.Shipment.Status);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ThreeSegmentGeneratedAndRuntimeMaterializationShouldBeEquivalent()
        {
            var generatedRuntime = CreateThreeSegmentRuntime(registerGenerated: true);
            var reflectionRuntime = CreateThreeSegmentRuntime(registerGenerated: false);

            using (var connection = OpenConnection())
            {
                const string sql = "SELECT 5 AS customer_id, 'Equivalent' AS customer_name, 50 AS order_id, 50.5 AS total, 500 AS shipment_id, 'equivalent' AS shipment_status;";
                var generated = generatedRuntime.QueryMapped<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                    connection,
                    sql,
                    (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                    splitOn: "order_id,shipment_id").Single();
                var reflection = reflectionRuntime.QueryMapped<JoinCustomer, JoinOrder, JoinShipment, CustomerOrderShipment>(
                    connection,
                    sql,
                    (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
                    splitOn: "order_id,shipment_id").Single();

                Assert.Equal(reflection.Customer.Id, generated.Customer.Id);
                Assert.Equal(reflection.Customer.Name, generated.Customer.Name);
                Assert.Equal(reflection.Order.Id, generated.Order.Id);
                Assert.Equal(reflection.Order.Total, generated.Order.Total);
                Assert.Equal(reflection.Shipment.Id, generated.Shipment.Id);
                Assert.Equal(reflection.Shipment.Status, generated.Shipment.Status);
            }
        }

        private static FluentMapRuntime CreateThreeSegmentRuntime(bool registerGenerated)
        {
            var builder = new FluentMapConfigurationBuilder()
                .AddMap(new JoinCustomerMap())
                .AddMap(new JoinOrderMap())
                .AddMap(new JoinShipmentMap());

            if (registerGenerated)
            {
                builder
                    .AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(JoinCustomer.Id)),
                            GeneratedMaterializerColumn.Map("customer_name", nameof(JoinCustomer.Name))
                        },
                        record => new JoinCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = Convert.ToString(record.GetValue(1))
                        })
                    .AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("order_id", nameof(JoinOrder.Id)),
                            GeneratedMaterializerColumn.Map("total", nameof(JoinOrder.Total))
                        },
                        record => new JoinOrder
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Total = Convert.ToDecimal(record.GetValue(1))
                        })
                    .AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("shipment_id", nameof(JoinShipment.Id)),
                            GeneratedMaterializerColumn.Map("shipment_status", nameof(JoinShipment.Status))
                        },
                        record => new JoinShipment
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Status = Convert.ToString(record.GetValue(1))
                        });
            }

            return builder.Build().CreateRuntime();
        }

        private static CustomerOrder MaterializeCustomerOrder(bool registerGeneratedMaterializers)
        {
            PreTest(typeof(JoinCustomer), typeof(JoinOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new JoinCustomerMap());
                    configuration.AddMap(new JoinOrderMap());

                    if (registerGeneratedMaterializers)
                    {
                        configuration.AddGeneratedMaterializer(
                            new[]
                            {
                                GeneratedMaterializerColumn.Map("customer_id", nameof(JoinCustomer.Id)),
                                GeneratedMaterializerColumn.Map("customer_name", nameof(JoinCustomer.Name))
                            },
                            record => new JoinCustomer
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                Name = Convert.ToString(record.GetValue(1))
                            });
                        configuration.AddGeneratedMaterializer(
                            new[]
                            {
                                GeneratedMaterializerColumn.Map("order_id", nameof(JoinOrder.Id)),
                                GeneratedMaterializerColumn.Map("total", nameof(JoinOrder.Total))
                            },
                            record => new JoinOrder
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                Total = Convert.ToDecimal(record.GetValue(1))
                            });
                    }
                });

                using (var connection = OpenConnection())
                {
                    var row = connection.QueryMapped<JoinCustomer, JoinOrder, CustomerOrder>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name, 10 AS order_id, 12.5 AS total;",
                            (customer, order) => new CustomerOrder(customer, order),
                            splitOn: "order_id")
                        .Single();

                    Assert.Equal(registerGeneratedMaterializers ? 0 : 2, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                    return row;
                }
            }
            finally
            {
                PreTest(typeof(JoinCustomer), typeof(JoinOrder));
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

        private sealed class CustomerOrder
        {
            public CustomerOrder(JoinCustomer customer, JoinOrder order)
            {
                Customer = customer;
                Order = order;
            }

            public JoinCustomer Customer { get; }

            public JoinOrder Order { get; }
        }

        private sealed class CustomerOrderShipment
        {
            public CustomerOrderShipment(JoinCustomer customer, JoinOrder order, JoinShipment shipment)
            {
                Customer = customer;
                Order = order;
                Shipment = shipment;
            }

            public JoinCustomer Customer { get; }

            public JoinOrder Order { get; }

            public JoinShipment Shipment { get; }
        }

        private sealed class JoinCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class JoinOrder
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }

        private sealed class JoinShipment
        {
            public int Id { get; set; }

            public string Status { get; set; }
        }

        private sealed class DefaultSplitOrder
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }

        private sealed class JoinCustomerMap : EntityMap<JoinCustomer>
        {
            public JoinCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class JoinOrderMap : EntityMap<JoinOrder>
        {
            public JoinOrderMap()
            {
                Map(order => order.Id).ToColumn("order_id");
                Map(order => order.Total).ToColumn("total");
            }
        }

        private sealed class JoinShipmentMap : EntityMap<JoinShipment>
        {
            public JoinShipmentMap()
            {
                Map(shipment => shipment.Id).ToColumn("shipment_id");
                Map(shipment => shipment.Status).ToColumn("shipment_status");
            }
        }

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private sealed class ArchiveProfile : IMappingProfile
        {
        }

        private sealed class TrackingProfile : IMappingProfile
        {
        }

        private sealed class LegacyJoinCustomerMap : EntityMap<JoinCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyJoinCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_customer_id");
                Map(customer => customer.Name).ToColumn("legal_name");
            }
        }

        private sealed class ArchivedJoinOrderMap : EntityMap<JoinOrder>, IProfileMap<ArchiveProfile>
        {
            public ArchivedJoinOrderMap()
            {
                Map(order => order.Id).ToColumn("archived_order_id");
                Map(order => order.Total).ToColumn("archived_total");
            }
        }

        private sealed class TrackedJoinShipmentMap : EntityMap<JoinShipment>, IProfileMap<TrackingProfile>
        {
            public TrackedJoinShipmentMap()
            {
                Map(shipment => shipment.Id).ToColumn("tracked_shipment_id");
                Map(shipment => shipment.Status).ToColumn("tracked_status");
            }
        }

        private sealed class DefaultSplitOrderMap : EntityMap<DefaultSplitOrder>
        {
            public DefaultSplitOrderMap()
            {
                Map(order => order.Total).ToColumn("total");
            }
        }

        private sealed class ImmutableOrder
        {
            public ImmutableOrder(int id, string note)
            {
                Id = id;
                Note = note;
            }

            public int Id { get; }

            public string Note { get; }
        }

        private sealed class ImmutableOrderMap : EntityMap<ImmutableOrder>
        {
            public ImmutableOrderMap()
            {
                Map(order => order.Id).ToColumn("order_id");
                Map(order => order.Note).ToColumn("order_note");
            }
        }

        private enum AccountStatus
        {
            Unknown,
            Active,
            Inactive
        }

        private sealed class ComplexCustomer
        {
            public ComplexCustomer(int id, ComplexAddress address, ComplexEmail email, AccountStatus status)
            {
                Id = id;
                Address = address;
                Email = email;
                Status = status;
            }

            public int Id { get; }

            public ComplexAddress Address { get; }

            public ComplexEmail Email { get; }

            public AccountStatus Status { get; }
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
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<AccountStatusConverter, string>();
            }
        }

        private sealed class AccountStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return value == "A" ? AccountStatus.Active : AccountStatus.Inactive;
            }
        }

        private sealed class TypeHandlerOrder
        {
            public int Id { get; set; }

            public Cpf TaxId { get; set; }
        }

        private sealed class TypeHandlerOrderMap : EntityMap<TypeHandlerOrder>
        {
            public TypeHandlerOrderMap()
            {
                Map(order => order.Id).ToColumn("order_id");
                Map(order => order.TaxId).ToColumn("tax_id");
            }
        }

        private sealed class Cpf
        {
            public Cpf(string number)
            {
                Number = number;
            }

            public string Number { get; }
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
    }
}
