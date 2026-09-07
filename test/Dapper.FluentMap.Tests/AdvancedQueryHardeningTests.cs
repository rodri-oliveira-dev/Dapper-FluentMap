using System;
using System.Collections.Generic;
using System.Data;
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
    public class AdvancedQueryHardeningTests
    {
        [Fact]
        public void ReadMappedShouldMaterializeRepresentativeDataTypesFromProviderIndependentReader()
        {
            PreTest(typeof(RepresentativeRecord));

            try
            {
                var id = new Guid("42f74f8f-2e12-4ca7-9c0f-46973f89dd65");
                var createdAt = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

                FluentMapper.Initialize(configuration => configuration.AddMap(new RepresentativeRecordMap()));

                using (var reader = CreateReader(CreateTable(
                    new[]
                    {
                        "record_id",
                        "display_name",
                        "optional_count",
                        "missing_count",
                        "created_at",
                        "external_id",
                        "amount",
                        "status",
                        "email"
                    },
                    new object[] { 42, "Ada", 7, DBNull.Value, createdAt, id, 123.45m, 2, "ada@example.com" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var row = multi.ReadMappedSingle<RepresentativeRecord>();

                    Assert.Equal(42, row.Id);
                    Assert.Equal("Ada", row.Name);
                    Assert.Equal(7, row.OptionalCount);
                    Assert.Null(row.MissingCount);
                    Assert.Equal(createdAt, row.CreatedAt);
                    Assert.Equal(id, row.ExternalId);
                    Assert.Equal(123.45m, row.Amount);
                    Assert.Equal(RepresentativeStatus.Active, row.Status);
                    Assert.Equal(new RepresentativeEmail("ada@example.com"), row.Email);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(RepresentativeRecord));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldMaterializeRepresentativeDataTypesWithSqliteProvider()
        {
            PreTest(typeof(RepresentativeRecord));

            try
            {
                var id = new Guid("d9112374-bc21-4396-a7c8-d4f2d1212f47");

                FluentMapper.Initialize(configuration => configuration.AddMap(new RepresentativeRecordMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var row = connection.QueryMappedSingle<RepresentativeRecord>(
                        @"SELECT
                            42 AS record_id,
                            'Ada' AS display_name,
                            7 AS optional_count,
                            NULL AS missing_count,
                            '2024-05-06T07:08:09' AS created_at,
                            'd9112374-bc21-4396-a7c8-d4f2d1212f47' AS external_id,
                            123.45 AS amount,
                            2 AS status,
                            'ada@example.com' AS email;");

                    Assert.Equal(42, row.Id);
                    Assert.Equal("Ada", row.Name);
                    Assert.Equal(7, row.OptionalCount);
                    Assert.Null(row.MissingCount);
                    Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), row.CreatedAt);
                    Assert.Equal(id, row.ExternalId);
                    Assert.Equal(123.45m, row.Amount);
                    Assert.Equal(RepresentativeStatus.Active, row.Status);
                    Assert.Equal(new RepresentativeEmail("ada@example.com"), row.Email);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(RepresentativeRecord));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedRuntimeFallbackShouldRemainStableAcrossParallelConnections()
        {
            PreTest(typeof(ConcurrentCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ConcurrentCustomerMap()));

                var results = Enumerable.Range(0, 40)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = new SqliteConnection("Data Source=:memory:"))
                        {
                            var customer = connection.QueryMappedSingle<ConcurrentCustomer>(
                                $"SELECT 'customer-{index}' AS customer_name, {index} AS customer_id;");

                            return customer.Id == index && customer.Name == $"customer-{index}";
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
                Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                PreTest(typeof(ConcurrentCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task QueryMappedUnbufferedAsyncShouldRemainStableAcrossParallelProfileStreams()
        {
            PreTest(typeof(ConcurrentCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddProfile<ConcurrentCustomerProfileMap>());

                var tasks = Enumerable.Range(0, 24)
                    .Select(index => MaterializeProfileStreamAsync(index, TestContext.Current.CancellationToken))
                    .ToArray();

                var results = await Task.WhenAll(tasks);

                Assert.All(results, Assert.True);
                Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                PreTest(typeof(ConcurrentCustomer));
            }
        }

        [Fact]
        public void QueryMultipleMappedShouldUseGeneratedAndRuntimeMaterializersOnIndependentParallelReaders()
        {
            PreTest(typeof(ConcurrentCustomer));

            try
            {
                var generatedRows = 0;

                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new ConcurrentCustomerMap());
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("customer_id", nameof(ConcurrentCustomer.Id)),
                            GeneratedMaterializerColumn.Map("customer_name", nameof(ConcurrentCustomer.Name))
                        },
                        record =>
                        {
                            Interlocked.Increment(ref generatedRows);
                            return new ConcurrentCustomer
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                Name = Convert.ToString(record.GetValue(1))
                            };
                        });
                });

                var results = Enumerable.Range(0, 30)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var reader = CreateReader(
                            CreateTable(
                                new[] { "customer_id", "customer_name" },
                                new object[] { index, "generated-" + index }),
                            CreateTable(
                                new[] { "customer_name", "customer_id" },
                                new object[] { "runtime-" + index, index })))
                        using (var multi = new MappedGridReader(reader))
                        {
                            var generated = multi.ReadMappedSingle<ConcurrentCustomer>();
                            var runtime = multi.ReadMappedSingle<ConcurrentCustomer>();

                            return generated.Id == index &&
                                   generated.Name == "generated-" + index &&
                                   runtime.Id == index &&
                                   runtime.Name == "runtime-" + index &&
                                   multi.IsConsumed;
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
                Assert.Equal(30, generatedRows);
                Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                PreTest(typeof(ConcurrentCustomer));
            }
        }

        private static async Task<bool> MaterializeProfileStreamAsync(int index, CancellationToken cancellationToken)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var rows = new List<ConcurrentCustomer>();

                await foreach (var customer in connection.QueryMappedUnbufferedAsync<ConcurrentCustomer, ConcurrentProfile>(
                    $"SELECT {index} AS legacy_id, 'legacy-{index}' AS legal_name;",
                    cancellationToken))
                {
                    rows.Add(customer);
                }

                var row = Assert.Single(rows);
                return row.Id == index &&
                       row.Name == $"legacy-{index}" &&
                       connection.State == ConnectionState.Closed;
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

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class RepresentativeRecord
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public int? OptionalCount { get; set; }

            public int? MissingCount { get; set; }

            public DateTime CreatedAt { get; set; }

            public Guid ExternalId { get; set; }

            public decimal Amount { get; set; }

            public RepresentativeStatus Status { get; set; }

            public RepresentativeEmail Email { get; set; }
        }

        private sealed record RepresentativeEmail(string Value);

        private enum RepresentativeStatus
        {
            Unknown = 0,
            Draft = 1,
            Active = 2
        }

        private sealed class RepresentativeRecordMap : EntityMap<RepresentativeRecord>
        {
            public RepresentativeRecordMap()
            {
                Map(record => record.Id).ToColumn("record_id");
                Map(record => record.Name).ToColumn("display_name");
                Map(record => record.OptionalCount).ToColumn("optional_count");
                Map(record => record.MissingCount).ToColumn("missing_count");
                Map(record => record.CreatedAt).ToColumn("created_at");
                Map(record => record.ExternalId).ToColumn("external_id");
                Map(record => record.Amount).ToColumn("amount");
                Map(record => record.Status).ToColumn("status");
                Map(record => record.Email.Value).ToColumn("email");
            }
        }

        private sealed class ConcurrentProfile : IMappingProfile
        {
        }

        private sealed class ConcurrentCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class ConcurrentCustomerMap : EntityMap<ConcurrentCustomer>
        {
            public ConcurrentCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class ConcurrentCustomerProfileMap : EntityMap<ConcurrentCustomer>, IProfileMap<ConcurrentProfile>
        {
            public ConcurrentCustomerProfileMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legal_name");
            }
        }
    }
}
