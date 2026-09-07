using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.FluentMap.Dommel;
using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Mapping;
using Dommel;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using Xunit;

namespace Dapper.FluentMap.ProviderCompatibility.Tests
{
    public class ProviderCompatibilityTests
    {
        public static IEnumerable<object[]> Providers()
        {
            yield return new object[] { ProviderCase.Sqlite() };
            yield return new object[] { ProviderCase.SqlServer() };
            yield return new object[] { ProviderCase.PostgreSql() };
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void BasicReadShouldMaterializeProviderValues(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            PreTest(typeof(BasicProviderCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new BasicProviderCustomerMap()));

                using (var connection = provider.OpenConnection())
                {
                    var tableName = provider.CreateTableName("basic");
                    provider.DropTable(connection, tableName);
                    provider.Execute(connection, provider.CreateBasicTableSql(tableName));

                    var expectedGuid = Guid.Parse("84e705f9-81a7-4c92-bf35-16310e29c5f2");
                    var expectedDate = new DateTime(2026, 7, 29, 12, 30, 45);
                    var expectedBalance = 1234.56m;

                    connection.Execute(
                        provider.InsertBasicSql(tableName),
                        new
                        {
                            CustomerId = 42,
                            OptionalName = (string)null,
                            ExternalId = provider.GuidParameter(expectedGuid),
                            CreatedAt = expectedDate,
                            Balance = expectedBalance
                        });

                    var customer = connection.QueryMappedSingle<BasicProviderCustomer>(
                        provider.SelectBasicSql(tableName));

                    Assert.Equal(42, customer.Id);
                    Assert.Null(customer.OptionalName);
                    Assert.Equal(expectedGuid, customer.ExternalId);
                    Assert.Equal(expectedDate, customer.CreatedAt);
                    Assert.Equal(expectedBalance, customer.Balance);
                }
            }
            finally
            {
                PreTest(typeof(BasicProviderCustomer));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void AdvancedReadShouldMaterializeConstructorNestedValueObjectProfileAndConverter(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            PreTest(typeof(AdvancedProviderCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new AdvancedProviderCustomerMap());
                    configuration.AddProfile<LegacyAdvancedProviderCustomerMap>();
                });

                using (var connection = provider.OpenConnection())
                {
                    var current = connection.QueryMappedSingle<AdvancedProviderCustomer>(
                        provider.SelectAdvancedSql(
                            "customer_id",
                            "city",
                            "email",
                            "status",
                            7,
                            "Sao Paulo",
                            "ada@example.com",
                            "A"));
                    var legacy = connection.QueryMappedSingle<AdvancedProviderCustomer, LegacyProfile>(
                        provider.SelectAdvancedSql(
                            "legacy_id",
                            "legacy_city",
                            "legacy_email",
                            "legacy_status",
                            8,
                            "Porto",
                            "legacy@example.com",
                            "I"));

                    Assert.Equal(7, current.Id);
                    Assert.NotNull(current.Address);
                    Assert.Equal("Sao Paulo", current.Address.City);
                    Assert.Equal(new ProviderEmail("ada@example.com"), current.Email);
                    Assert.Equal(AccountStatus.Active, current.Status);

                    Assert.Equal(8, legacy.Id);
                    Assert.NotNull(legacy.Address);
                    Assert.Equal("Porto", legacy.Address.City);
                    Assert.Equal(new ProviderEmail("legacy@example.com"), legacy.Email);
                    Assert.Equal(AccountStatus.Inactive, legacy.Status);
                }
            }
            finally
            {
                PreTest(typeof(AdvancedProviderCustomer));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void QueryMultipleMappedShouldReadSequentialProviderResultSets(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            provider.SkipIfMultipleResultsUnsupported();
            PreTest(typeof(MultipleCustomer), typeof(MultipleOrder));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new MultipleCustomerMap());
                    configuration.AddMap(new MultipleOrderMap());
                });

                using (var connection = provider.OpenConnection())
                using (var multi = connection.QueryMultipleMapped(provider.MultipleResultsSql()))
                {
                    var customer = multi.ReadMappedSingle<MultipleCustomer>();
                    var order = multi.ReadMappedSingle<MultipleOrder>();

                    Assert.Equal(11, customer.Id);
                    Assert.Equal("Multiple", customer.Name);
                    Assert.Equal(99, order.Id);
                    Assert.Equal(12.34m, order.Total);
                    Assert.True(multi.IsConsumed);
                }
            }
            finally
            {
                PreTest(typeof(MultipleCustomer), typeof(MultipleOrder));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void UnbufferedStreamingShouldKeepReaderOpenAndReleaseOnEarlyTermination(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            PreTest(typeof(StreamCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new StreamCustomerMap()));

                using (var connection = provider.CreateClosedConnection())
                using (var enumerator = connection.QueryMappedUnbuffered<StreamCustomer>(
                    provider.StreamingRowsSql()).GetEnumerator())
                {
                    Assert.True(enumerator.MoveNext());
                    Assert.Equal(1, enumerator.Current.Id);
                    Assert.Equal(ConnectionState.Open, connection.State);

                    enumerator.Dispose();

                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(StreamCustomer));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public async Task AsyncStreamingShouldPropagateCancellationAndReleaseReader(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            PreTest(typeof(StreamCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new StreamCustomerMap()));

                using (var connection = provider.CreateClosedConnection())
                using (var cancellation = new CancellationTokenSource())
                {
                    await using var enumerator = connection.QueryMappedUnbufferedAsync<StreamCustomer>(
                            provider.StreamingRowsSql(),
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
                PreTest(typeof(StreamCustomer));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void DommelPersistenceShouldHonorGeneratedDefaultsAndReadOnlyMetadata(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            provider.SkipIfPersistenceUnsupported();
            PreTest(typeof(ProviderPersistenceEntity));

            try
            {
                provider.InitializeNativeProvider();
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new ProviderPersistenceEntityMap());
                    configuration.ForDommel();
                });

                using (var connection = provider.OpenConnection())
                {
                    provider.DropTable(connection, ProviderPersistenceEntityMap.MappedTableName);
                    provider.Execute(connection, provider.CreatePersistenceTableSql(ProviderPersistenceEntityMap.MappedTableName));

                    var entity = new ProviderPersistenceEntity
                    {
                        Normal = "inserted",
                        ReadOnly = "client-read-only",
                        DefaultValue = "client-default",
                        Computed = "client-computed"
                    };

                    var id = Convert.ToInt32(connection.Insert(entity));
                    var inserted = connection.Get<ProviderPersistenceEntity>(id);

                    Assert.Equal("inserted", inserted.Normal);
                    Assert.Equal("read-only-default", inserted.ReadOnly);
                    Assert.Equal("default-value-default", inserted.DefaultValue);
                    Assert.Equal("inserted-computed", inserted.Computed);

                    entity.Id = id;
                    entity.Normal = "updated";
                    entity.ReadOnly = "updated-read-only";
                    entity.DefaultValue = "updated-default";
                    entity.Computed = "updated-computed";

                    Assert.True(connection.Update(entity));

                    var updated = connection.Get<ProviderPersistenceEntity>(id);
                    Assert.Equal("updated", updated.Normal);
                    Assert.Equal("read-only-default", updated.ReadOnly);
                    Assert.Equal("updated-default", updated.DefaultValue);
                    Assert.Equal("updated-computed", updated.Computed);
                }
            }
            finally
            {
                PreTest(typeof(ProviderPersistenceEntity));
            }
        }

        [Theory]
        [MemberData(nameof(Providers))]
        [Trait("Category", "ProviderCompatibility")]
        public void DommelPersistenceShouldInsertNonIdentityKeyAndKeepItOutOfUpdateSet(ProviderCase provider)
        {
            provider.SkipIfUnavailable();
            provider.SkipIfPersistenceUnsupported();
            PreTest(typeof(ProviderAssignedKeyEntity));

            try
            {
                provider.InitializeNativeProvider();
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new ProviderAssignedKeyEntityMap());
                    configuration.ForDommel();
                });

                using (var connection = provider.OpenConnection())
                {
                    provider.DropTable(connection, ProviderAssignedKeyEntityMap.MappedTableName);
                    provider.Execute(connection, provider.CreateAssignedKeyTableSql(ProviderAssignedKeyEntityMap.MappedTableName));

                    var entity = new ProviderAssignedKeyEntity
                    {
                        Code = "A-001",
                        Name = "inserted",
                        UpdateExcluded = "insert-write"
                    };

                    connection.Insert(entity);

                    var inserted = connection.QuerySingle<ProviderAssignedKeyEntity>(
                        provider.SelectAssignedKeySql(ProviderAssignedKeyEntityMap.MappedTableName),
                        new { entity.Code });

                    Assert.Equal("A-001", inserted.Code);
                    Assert.Equal("inserted", inserted.Name);
                    Assert.Equal("insert-write", inserted.UpdateExcluded);

                    entity.Name = "updated";
                    entity.UpdateExcluded = "update-write";

                    Assert.True(connection.Update(entity));

                    var updated = connection.QuerySingle<ProviderAssignedKeyEntity>(
                        provider.SelectAssignedKeySql(ProviderAssignedKeyEntityMap.MappedTableName),
                        new { entity.Code });

                    Assert.Equal("A-001", updated.Code);
                    Assert.Equal("updated", updated.Name);
                    Assert.Equal("insert-write", updated.UpdateExcluded);
                }
            }
            finally
            {
                PreTest(typeof(ProviderAssignedKeyEntity));
            }
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.EntityMaps.Clear();
            FluentMapper.TypeConventions.Clear();
            FluentMapper.Initialize(_ => { });
            DommelMapper.LogReceived = null;
        }

        public sealed class ProviderCase
        {
            private readonly string connectionString;
            private readonly Func<string, DbConnection> connectionFactory;

            private ProviderCase(
                string name,
                string connectionStringEnvironmentVariable,
                string connectionString,
                Func<string, DbConnection> connectionFactory,
                ProviderDialect dialect,
                bool isAlwaysAvailable = false,
                bool supportsMultipleResults = true,
                bool supportsPersistence = true)
            {
                Name = name;
                ConnectionStringEnvironmentVariable = connectionStringEnvironmentVariable;
                this.connectionString = connectionString;
                this.connectionFactory = connectionFactory;
                Dialect = dialect;
                IsAlwaysAvailable = isAlwaysAvailable;
                SupportsMultipleResults = supportsMultipleResults;
                SupportsPersistence = supportsPersistence;
            }

            public string Name { get; }

            public string ConnectionStringEnvironmentVariable { get; }

            public ProviderDialect Dialect { get; }

            public bool IsAlwaysAvailable { get; }

            public bool SupportsMultipleResults { get; }

            public bool SupportsPersistence { get; }

            public static ProviderCase Sqlite()
            {
                return new ProviderCase(
                    "SQLite",
                    null,
                    "Data Source=:memory:",
                    connectionString => new SqliteConnection(connectionString),
                    ProviderDialect.Sqlite,
                    isAlwaysAvailable: true);
            }

            public static ProviderCase SqlServer()
            {
                const string environmentVariable = "DFM_SQLSERVER_CONNECTION_STRING";
                return new ProviderCase(
                    "SQL Server",
                    environmentVariable,
                    Environment.GetEnvironmentVariable(environmentVariable),
                    connectionString => new SqlConnection(connectionString),
                    ProviderDialect.SqlServer);
            }

            public static ProviderCase PostgreSql()
            {
                const string environmentVariable = "DFM_POSTGRESQL_CONNECTION_STRING";
                return new ProviderCase(
                    "PostgreSQL",
                    environmentVariable,
                    Environment.GetEnvironmentVariable(environmentVariable),
                    connectionString => new NpgsqlConnection(connectionString),
                    ProviderDialect.PostgreSql);
            }

            public override string ToString()
            {
                return Name;
            }

            public void SkipIfUnavailable()
            {
                if (!IsAlwaysAvailable && string.IsNullOrWhiteSpace(connectionString))
                {
                    Assert.Skip(Name + " provider tests require " + ConnectionStringEnvironmentVariable + ".");
                }
            }

            public void SkipIfMultipleResultsUnsupported()
            {
                if (!SupportsMultipleResults)
                {
                    Assert.Skip(Name + " does not expose equivalent multiple-result behavior through this provider.");
                }
            }

            public void SkipIfPersistenceUnsupported()
            {
                if (!SupportsPersistence)
                {
                    Assert.Skip(Name + " persistence is unsupported by the current Dommel/provider combination.");
                }
            }

            public void InitializeNativeProvider()
            {
                if (Dialect == ProviderDialect.Sqlite)
                {
                    SQLitePCL.Batteries_V2.Init();
                }
            }

            public DbConnection CreateClosedConnection()
            {
                InitializeNativeProvider();
                return connectionFactory(connectionString);
            }

            public DbConnection OpenConnection()
            {
                var connection = CreateClosedConnection();
                connection.Open();
                return connection;
            }

            public string CreateTableName(string prefix)
            {
                return "dfm_" + prefix + "_" + Guid.NewGuid().ToString("N");
            }

            public object GuidParameter(Guid value)
            {
                return Dialect == ProviderDialect.Sqlite ? value.ToString() : (object)value;
            }

            public void Execute(IDbConnection connection, string sql)
            {
                connection.Execute(sql);
            }

            public void DropTable(IDbConnection connection, string tableName)
            {
                switch (Dialect)
                {
                    case ProviderDialect.SqlServer:
                        connection.Execute("IF OBJECT_ID(N'" + tableName + "', N'U') IS NOT NULL DROP TABLE " + tableName + ";");
                        break;
                    default:
                        connection.Execute("DROP TABLE IF EXISTS " + tableName + ";");
                        break;
                }
            }

            public string CreateBasicTableSql(string tableName)
            {
                switch (Dialect)
                {
                    case ProviderDialect.SqlServer:
                        return @"CREATE TABLE " + tableName + @" (
    customer_id INT NOT NULL,
    optional_name NVARCHAR(100) NULL,
    external_id UNIQUEIDENTIFIER NOT NULL,
    created_at DATETIME2 NOT NULL,
    balance DECIMAL(18, 2) NOT NULL
);";
                    case ProviderDialect.PostgreSql:
                        return @"CREATE TABLE " + tableName + @" (
    customer_id INTEGER NOT NULL,
    optional_name TEXT NULL,
    external_id UUID NOT NULL,
    created_at TIMESTAMP NOT NULL,
    balance NUMERIC(18, 2) NOT NULL
);";
                    default:
                        return @"CREATE TABLE " + tableName + @" (
    customer_id INTEGER NOT NULL,
    optional_name TEXT NULL,
    external_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    balance TEXT NOT NULL
);";
                }
            }

            public string InsertBasicSql(string tableName)
            {
                return "INSERT INTO " + tableName + @" (
    customer_id,
    optional_name,
    external_id,
    created_at,
    balance
) VALUES (
    @CustomerId,
    @OptionalName,
    @ExternalId,
    @CreatedAt,
    @Balance
);";
            }

            public string SelectBasicSql(string tableName)
            {
                return @"SELECT
    customer_id,
    optional_name,
    external_id,
    created_at,
    balance
FROM " + tableName + ";";
            }

            public string SelectAdvancedSql(
                string idAlias,
                string cityAlias,
                string emailAlias,
                string statusAlias,
                int id,
                string city,
                string email,
                string status)
            {
                return "SELECT " +
                    Literal(id) + " AS " + idAlias + ", " +
                    TextLiteral(city) + " AS " + cityAlias + ", " +
                    TextLiteral(email) + " AS " + emailAlias + ", " +
                    TextLiteral(status) + " AS " + statusAlias + ";";
            }

            public string MultipleResultsSql()
            {
                return "SELECT 11 AS customer_id, " + TextLiteral("Multiple") + " AS customer_name; " +
                    "SELECT 99 AS order_id, " + DecimalLiteral(12.34m) + " AS total;";
            }

            public string StreamingRowsSql()
            {
                return "SELECT 1 AS customer_id, " + TextLiteral("One") + " AS customer_name UNION ALL " +
                    "SELECT 2 AS customer_id, " + TextLiteral("Two") + " AS customer_name;";
            }

            public string CreatePersistenceTableSql(string tableName)
            {
                switch (Dialect)
                {
                    case ProviderDialect.SqlServer:
                        return @"CREATE TABLE " + tableName + @" (
    id INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    normal NVARCHAR(100) NOT NULL,
    read_only NVARCHAR(100) NOT NULL CONSTRAINT DF_" + tableName + @"_read_only DEFAULT N'read-only-default',
    default_value NVARCHAR(100) NOT NULL CONSTRAINT DF_" + tableName + @"_default_value DEFAULT N'default-value-default',
    computed AS (normal + N'-computed')
);";
                    case ProviderDialect.PostgreSql:
                        return @"CREATE TABLE " + tableName + @" (
    id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    normal TEXT NOT NULL,
    read_only TEXT NOT NULL DEFAULT 'read-only-default',
    default_value TEXT NOT NULL DEFAULT 'default-value-default',
    computed TEXT GENERATED ALWAYS AS (normal || '-computed') STORED
);";
                    default:
                        return @"CREATE TABLE " + tableName + @" (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    normal TEXT NOT NULL,
    read_only TEXT NOT NULL DEFAULT 'read-only-default',
    default_value TEXT NOT NULL DEFAULT 'default-value-default',
    computed TEXT GENERATED ALWAYS AS (normal || '-computed') STORED
);";
                }
            }

            public string CreateAssignedKeyTableSql(string tableName)
            {
                switch (Dialect)
                {
                    case ProviderDialect.SqlServer:
                        return @"CREATE TABLE " + tableName + @" (
    code NVARCHAR(32) NOT NULL PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    update_excluded NVARCHAR(100) NULL
);";
                    case ProviderDialect.PostgreSql:
                    case ProviderDialect.Sqlite:
                    default:
                        return @"CREATE TABLE " + tableName + @" (
    code TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    update_excluded TEXT NULL
);";
                }
            }

            public string SelectAssignedKeySql(string tableName)
            {
                return @"SELECT
    code AS Code,
    name AS Name,
    update_excluded AS UpdateExcluded
FROM " + tableName + @"
WHERE code = @Code;";
            }

            private string Literal(int value)
            {
                return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            private string DecimalLiteral(decimal value)
            {
                return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            private string TextLiteral(string value)
            {
                return "'" + value.Replace("'", "''") + "'";
            }
        }

        public enum ProviderDialect
        {
            Sqlite,
            SqlServer,
            PostgreSql
        }

        private sealed class BasicProviderCustomer
        {
            public int Id { get; set; }

            public string OptionalName { get; set; }

            public Guid ExternalId { get; set; }

            public DateTime CreatedAt { get; set; }

            public decimal Balance { get; set; }
        }

        private sealed class BasicProviderCustomerMap : EntityMap<BasicProviderCustomer>
        {
            public BasicProviderCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.OptionalName).ToColumn("optional_name");
                Map(customer => customer.ExternalId).ToColumn("external_id");
                Map(customer => customer.CreatedAt).ToColumn("created_at");
                Map(customer => customer.Balance).ToColumn("balance");
            }
        }

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private enum AccountStatus
        {
            Unknown,
            Active,
            Inactive
        }

        private sealed class AdvancedProviderCustomer
        {
            public AdvancedProviderCustomer(int id, ProviderAddress address, ProviderEmail email, AccountStatus status)
            {
                Id = id;
                Address = address;
                Email = email;
                Status = status;
            }

            public int Id { get; }

            public ProviderAddress Address { get; }

            public ProviderEmail Email { get; }

            public AccountStatus Status { get; }
        }

        private sealed class ProviderAddress
        {
            public ProviderAddress(string city)
            {
                City = city;
            }

            public string City { get; }
        }

        private sealed record ProviderEmail(string Value);

        private sealed class AdvancedProviderCustomerMap : EntityMap<AdvancedProviderCustomer>
        {
            public AdvancedProviderCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("city");
                Map(customer => customer.Email.Value).ToColumn("email");
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<StatusConverter, string>();
            }
        }

        private sealed class LegacyAdvancedProviderCustomerMap :
            EntityMap<AdvancedProviderCustomer>,
            IProfileMap<LegacyProfile>
        {
            public LegacyAdvancedProviderCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Address.City).ToColumn("legacy_city");
                Map(customer => customer.Email.Value).ToColumn("legacy_email");
                Map(customer => customer.Status).ToColumn("legacy_status").ConvertFromDatabaseUsing<StatusConverter, string>();
            }
        }

        private sealed class StatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return value == "A" ? AccountStatus.Active : AccountStatus.Inactive;
            }
        }

        private sealed class MultipleCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class MultipleCustomerMap : EntityMap<MultipleCustomer>
        {
            public MultipleCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class MultipleOrder
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }

        private sealed class MultipleOrderMap : EntityMap<MultipleOrder>
        {
            public MultipleOrderMap()
            {
                Map(order => order.Id).ToColumn("order_id");
                Map(order => order.Total).ToColumn("total");
            }
        }

        private sealed class StreamCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class StreamCustomerMap : EntityMap<StreamCustomer>
        {
            public StreamCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class ProviderPersistenceEntity
        {
            public int Id { get; set; }

            public string Normal { get; set; }

            public string ReadOnly { get; set; }

            public string DefaultValue { get; set; }

            public string Computed { get; set; }
        }

        private sealed class ProviderPersistenceEntityMap : DommelEntityMap<ProviderPersistenceEntity>
        {
            public const string MappedTableName = "dfm_provider_persistence";

            public ProviderPersistenceEntityMap()
            {
                ToTable(MappedTableName);
                Map(entity => entity.Id).ToColumn("id").IsIdentity();
                Map(entity => entity.Normal).ToColumn("normal");
                Map(entity => entity.ReadOnly).ToColumn("read_only").ReadOnly();
                Map(entity => entity.DefaultValue).ToColumn("default_value").DatabaseDefaultOnInsert();
                Map(entity => entity.Computed).ToColumn("computed").Computed();
            }
        }

        private sealed class ProviderAssignedKeyEntity
        {
            public string Code { get; set; }

            public string Name { get; set; }

            public string UpdateExcluded { get; set; }
        }

        private sealed class ProviderAssignedKeyEntityMap : DommelEntityMap<ProviderAssignedKeyEntity>
        {
            public const string MappedTableName = "dfm_provider_assigned_key";

            public ProviderAssignedKeyEntityMap()
            {
                ToTable(MappedTableName);
                Map(entity => entity.Code).ToColumn("code").IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(entity => entity.Name).ToColumn("name");
                Map(entity => entity.UpdateExcluded).ToColumn("update_excluded").ExcludeFromUpdate();
            }
        }
    }
}
