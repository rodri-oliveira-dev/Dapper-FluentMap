using System;
using System.Linq;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
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

        private sealed class DefaultSplitOrderMap : EntityMap<DefaultSplitOrder>
        {
            public DefaultSplitOrderMap()
            {
                Map(order => order.Total).ToColumn("total");
            }
        }
    }
}
