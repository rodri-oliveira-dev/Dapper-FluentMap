using System;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class QueryMappedUnbufferedTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldMaterializeFlatEntity()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';")
                        .ToList();

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
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldMaterializeNestedObjectsAndValueObjects()
        {
            PreTest(typeof(ComplexCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ComplexCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = connection.QueryMappedUnbuffered<ComplexCustomer>(
                            "SELECT 13 AS customer_id, 'Sao Paulo' AS city, 'ada@example.com' AS email;")
                        .Single();

                    Assert.Equal(13, customer.Id);
                    Assert.NotNull(customer.Address);
                    Assert.Equal("Sao Paulo", customer.Address.City);
                    Assert.Equal(new ComplexEmail("ada@example.com"), customer.Email);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(ComplexCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldUseProfile()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddProfile<LegacyCustomerMap>());

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = connection.QueryMappedUnbuffered<FlatCustomer, LegacyProfile>(
                            "SELECT 7 AS legacy_id, 'Legacy Ltd.' AS legal_name;")
                        .Single();

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("Legacy Ltd.", customer.Name);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldUseGeneratedMaterializerWhenRegistered()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new FlatCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(FlatCustomer.Id)),
                            GeneratedMaterializerColumn.Map("customer_name", nameof(FlatCustomer.Name))
                        },
                        record => new FlatCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "generated:" + Convert.ToString(record.GetValue(1))
                        });
                });

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 3 AS customer_id, 'Ada' AS customer_name;")
                        .Single();

                    Assert.Equal(3, customer.Id);
                    Assert.Equal("generated:Ada", customer.Name);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldUseRuntimeFallbackWhenNoGeneratedMaterializerMatches()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 'Ada' AS customer_name, 1 AS customer_id;")
                        .Single();

                    Assert.Equal(1, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                    Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldNotExecuteUntilEnumerated()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = connection.QueryMappedUnbuffered<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name;");

                    Assert.Equal(ConnectionState.Closed, connection.State);

                    using (customers.GetEnumerator())
                    {
                        Assert.Equal(ConnectionState.Closed, connection.State);
                    }
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldCloseConnectionItOpenedAfterCompleteEnumeration()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';")
                        .ToList();

                    Assert.Equal(2, customers.Count);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldCloseConnectionItOpenedAfterEarlyBreak()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var seen = 0;

                    foreach (var customer in connection.QueryMappedUnbuffered<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';"))
                    {
                        Assert.Equal(1, customer.Id);
                        seen++;
                        break;
                    }

                    Assert.Equal(1, seen);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldCloseConnectionItOpenedWhenEnumeratorIsDisposed()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var enumerator = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';")
                        .GetEnumerator();

                    Assert.True(enumerator.MoveNext());
                    Assert.Equal(ConnectionState.Open, connection.State);

                    enumerator.Dispose();

                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldKeepOpenConnectionOpenAfterEarlyBreak()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = OpenConnection())
                {
                    foreach (var customer in connection.QueryMappedUnbuffered<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';"))
                    {
                        Assert.Equal(1, customer.Id);
                        break;
                    }

                    Assert.Equal(ConnectionState.Open, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldPropagateTransaction()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = OpenConnection())
                {
                    connection.Execute("CREATE TABLE customers (customer_id INTEGER NOT NULL, customer_name TEXT NOT NULL);");

                    using (var transaction = connection.BeginTransaction())
                    {
                        connection.Execute(
                            "INSERT INTO customers (customer_id, customer_name) VALUES (9, 'Transaction');",
                            transaction: transaction);

                        var customer = connection.QueryMappedUnbuffered<FlatCustomer>(
                                "SELECT customer_id, customer_name FROM customers WHERE customer_id = @id;",
                                new { id = 9 },
                                transaction)
                            .Single();

                        Assert.Equal(9, customer.Id);
                        Assert.Equal("Transaction", customer.Name);
                        Assert.Equal(ConnectionState.Open, connection.State);

                        transaction.Rollback();
                    }
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldDisposeReaderWhenMaterializationThrowsMidEnumeration()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), "DapperFluentMap-" + Guid.NewGuid().ToString("N") + ".db");

            PreTest(typeof(ThrowingCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ThrowingCustomerMap()));

                using (var setup = new SqliteConnection("Data Source=" + databasePath))
                {
                    setup.Open();
                    setup.Execute("CREATE TABLE customers (customer_id INTEGER NOT NULL, cpf TEXT NOT NULL);");
                    setup.Execute("INSERT INTO customers (customer_id, cpf) VALUES (1, '12345678909'), (2, ''), (3, '98765432100');");
                }

                using (var connection = new SqliteConnection("Data Source=" + databasePath))
                using (var enumerator = connection.QueryMappedUnbuffered<ThrowingCustomer>(
                    "SELECT customer_id, cpf FROM customers ORDER BY customer_id;").GetEnumerator())
                {
                    Assert.True(enumerator.MoveNext());
                    Assert.Equal(1, enumerator.Current.Id);
                    Assert.Equal(ConnectionState.Open, connection.State);

                    var exception = Assert.Throws<FluentMapConfigurationException>(() => enumerator.MoveNext());

                    Assert.IsType<ArgumentException>(exception.InnerException);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(ThrowingCustomer));
                SqliteConnection.ClearAllPools();

                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldReturnEmptySequence()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = connection.QueryMappedUnbuffered<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name WHERE 1 = 0;")
                        .ToList();

                    Assert.Empty(customers);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldProcessLargeSequenceWithOneMaterializerLookup()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var count = 0;
                    var lastId = 0;

                    foreach (var customer in connection.QueryMappedUnbuffered<FlatCustomer>(
                        @"WITH RECURSIVE numbers(Value) AS (
                            SELECT 1
                            UNION ALL
                            SELECT Value + 1 FROM numbers WHERE Value < 5000
                        )
                        SELECT Value AS customer_id, 'Customer ' || Value AS customer_name FROM numbers;"))
                    {
                        count++;
                        lastId = customer.Id;
                    }

                    Assert.Equal(5000, count);
                    Assert.Equal(5000, lastId);
                    Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(FlatCustomer));
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

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private sealed class FlatCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class FlatCustomerMap : EntityMap<FlatCustomer>
        {
            public FlatCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class LegacyCustomerMap : EntityMap<FlatCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legal_name");
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
