using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class QueryMappedUnbufferedAsyncTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task QueryMappedUnbufferedAsyncShouldMaterializeFlatEntity()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                        TestContext.Current.CancellationToken));

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
        public async Task QueryMappedUnbufferedAsyncShouldReturnEmptySequence()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name WHERE 1 = 0;",
                        TestContext.Current.CancellationToken));

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
        public async Task QueryMappedUnbufferedAsyncShouldMaterializeNestedObjectsAndValueObjects()
        {
            PreTest(typeof(ComplexCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ComplexCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<ComplexCustomer>(
                        "SELECT 13 AS customer_id, 'Sao Paulo' AS city, 'ada@example.com' AS email;",
                        TestContext.Current.CancellationToken))).Single();

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
        public async Task QueryMappedUnbufferedAsyncShouldUseProfile()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddProfile<LegacyCustomerMap>());

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer, LegacyProfile>(
                        "SELECT 7 AS legacy_id, 'Legacy Ltd.' AS legal_name;",
                        TestContext.Current.CancellationToken))).Single();

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
        public async Task QueryMappedUnbufferedAsyncShouldUseGeneratedMaterializerWhenRegistered()
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
                    var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 3 AS customer_id, 'Ada' AS customer_name;",
                        TestContext.Current.CancellationToken))).Single();

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
        public async Task QueryMappedUnbufferedAsyncShouldUseRuntimeFallbackWhenNoGeneratedMaterializerMatches()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 'Ada' AS customer_name, 1 AS customer_id;",
                        TestContext.Current.CancellationToken))).Single();

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
        public async Task QueryMappedUnbufferedAsyncShouldNotExecuteUntilEnumerated()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name;",
                        TestContext.Current.CancellationToken);

                    Assert.Equal(ConnectionState.Closed, connection.State);

                    await using (customers.GetAsyncEnumerator(TestContext.Current.CancellationToken))
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
        public async Task QueryMappedUnbufferedAsyncShouldCloseConnectionItOpenedAfterCompleteEnumeration()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customers = await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                        TestContext.Current.CancellationToken));

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
        public async Task QueryMappedUnbufferedAsyncShouldCloseConnectionItOpenedAfterPartialConsumption()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var seen = 0;

                    await foreach (var customer in connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                        TestContext.Current.CancellationToken))
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
        public async Task QueryMappedUnbufferedAsyncShouldKeepOpenConnectionOpenAfterPartialConsumption()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = OpenConnection())
                {
                    await foreach (var customer in connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                        TestContext.Current.CancellationToken))
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
        public async Task QueryMappedUnbufferedAsyncShouldPropagateParametersAndTransaction()
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

                        var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                            "SELECT customer_id, customer_name FROM customers WHERE customer_id = @id;",
                            new { id = 9 },
                            transaction,
                            cancellationToken: TestContext.Current.CancellationToken))).Single();

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
        public async Task QueryMappedUnbufferedAsyncShouldPropagateCancellationBeforeExecution()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();

                    var rows = connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                        "SELECT 1 AS customer_id, 'Ada' AS customer_name;",
                        cancellation.Token);

                    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    {
                        await foreach (var row in rows)
                        {
                            _ = row;
                        }
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
        public async Task QueryMappedUnbufferedAsyncShouldPropagateCancellationDuringEnumeration()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                using (var cancellation = new CancellationTokenSource())
                {
                    await using var enumerator = connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                            cancellation.Token)
                        .GetAsyncEnumerator(cancellation.Token);

                    Assert.True(await enumerator.MoveNextAsync());
                    Assert.Equal(1, enumerator.Current.Id);
                    Assert.Equal(ConnectionState.Open, connection.State);

                    cancellation.Cancel();

                    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    {
                        await enumerator.MoveNextAsync();
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
        public async Task QueryMappedUnbufferedAsyncShouldDisposeAfterCancellationFollowingPartialEnumeration()
        {
            PreTest(typeof(FlatCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FlatCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                using (var cancellation = new CancellationTokenSource())
                {
                    var enumerator = connection.QueryMappedUnbufferedAsync<FlatCustomer>(
                            "SELECT 1 AS customer_id, 'Ada' AS customer_name UNION ALL SELECT 2, 'Grace';",
                            cancellation.Token)
                        .GetAsyncEnumerator(cancellation.Token);

                    try
                    {
                        Assert.True(await enumerator.MoveNextAsync());
                        Assert.Equal(ConnectionState.Open, connection.State);

                        cancellation.Cancel();

                        await enumerator.DisposeAsync();

                        Assert.Equal(ConnectionState.Closed, connection.State);
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
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
        public async Task QueryMappedUnbufferedAsyncShouldDisposeReaderWhenMaterializationThrowsMidEnumeration()
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
                {
                    await using var enumerator = connection.QueryMappedUnbufferedAsync<ThrowingCustomer>(
                            "SELECT customer_id, cpf FROM customers ORDER BY customer_id;",
                            TestContext.Current.CancellationToken)
                        .GetAsyncEnumerator(TestContext.Current.CancellationToken);

                    Assert.True(await enumerator.MoveNextAsync());
                    Assert.Equal(1, enumerator.Current.Id);
                    Assert.Equal(ConnectionState.Open, connection.State);

                    var exception = await Assert.ThrowsAsync<FluentMapConfigurationException>(async () =>
                    {
                        await enumerator.MoveNextAsync();
                    });

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

        private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
        {
            var results = new List<T>();

            await foreach (var item in source)
            {
                results.Add(item);
            }

            return results;
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
