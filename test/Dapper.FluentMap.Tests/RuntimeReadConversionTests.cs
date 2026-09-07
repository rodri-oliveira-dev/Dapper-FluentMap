using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class RuntimeReadConversionTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyScalarNullableAndEnumReadConverters()
        {
            PreTest(typeof(ConversionCustomer));
            CountingStatusConverter.Calls = 0;

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customers = connection.QueryMapped<ConversionCustomer>(
                            @"SELECT 1 AS customer_id, 'A' AS status, '42' AS optional_score
                              UNION ALL
                              SELECT 2 AS customer_id, NULL AS status, NULL AS optional_score;")
                        .ToList();

                    Assert.Collection(
                        customers,
                        first =>
                        {
                            Assert.Equal(1, first.Id);
                            Assert.Equal(AccountStatus.Active, first.Status);
                            Assert.Equal(42, first.OptionalScore);
                        },
                        second =>
                        {
                            Assert.Equal(2, second.Id);
                            Assert.Equal(AccountStatus.Unknown, second.Status);
                            Assert.Null(second.OptionalScore);
                        });
                    Assert.Equal(1, CountingStatusConverter.Calls);
                }
            }
            finally
            {
                PreTest(typeof(ConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyNestedConverterOnlyToConfiguredMemberPath()
        {
            PreTest(typeof(NestedConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new NestedConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<NestedConversionCustomer>(
                        "SELECT '00123' AS billing_zip, '00456' AS shipping_zip;");

                    Assert.NotNull(customer.BillingAddress);
                    Assert.NotNull(customer.ShippingAddress);
                    Assert.Equal("ZIP-00123", customer.BillingAddress.ZipCode);
                    Assert.Equal("00456", customer.ShippingAddress.ZipCode);
                }
            }
            finally
            {
                PreTest(typeof(NestedConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldNotInvokeNestedConverterWhenSubtreeIsNull()
        {
            PreTest(typeof(NestedConversionCustomer));
            CountingZipCodeConverter.Calls = 0;

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new NestedConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<NestedConversionCustomer>(
                        "SELECT NULL AS billing_zip, NULL AS shipping_zip;");

                    Assert.Null(customer.BillingAddress);
                    Assert.Null(customer.ShippingAddress);
                    Assert.Equal(0, CountingZipCodeConverter.Calls);
                }
            }
            finally
            {
                PreTest(typeof(NestedConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyConverterBeforeImmutableConstructor()
        {
            PreTest(typeof(ImmutableConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ImmutableConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ImmutableConversionCustomer>(
                        "SELECT 'I' AS status;");

                    Assert.Equal(AccountStatus.Inactive, customer.Status);
                }
            }
            finally
            {
                PreTest(typeof(ImmutableConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyScalarValueObjectPropertyConverter()
        {
            PreTest(typeof(ValueObjectConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ValueObjectConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ValueObjectConversionCustomer>(
                        "SELECT '12345678909' AS cpf;");

                    Assert.Equal("converted:12345678909", customer.Cpf.Number);
                }
            }
            finally
            {
                PreTest(typeof(ValueObjectConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldRespectProfileScopedReadConverters()
        {
            PreTest(typeof(ProfileConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new DefaultProfileConversionCustomerMap());
                    configuration.AddProfile<LegacyProfileConversionCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var current = connection.QueryMappedSingle<ProfileConversionCustomer>(
                        "SELECT 'A' AS status;");
                    var legacy = connection.QueryMappedSingle<ProfileConversionCustomer, LegacyProfile>(
                        "SELECT '1' AS legacy_status;");

                    Assert.Equal(AccountStatus.Active, current.Status);
                    Assert.Equal(AccountStatus.Inactive, legacy.Status);
                }
            }
            finally
            {
                PreTest(typeof(ProfileConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldKeepConvertersScopedToPropertyWhenPropertyTypesMatch()
        {
            PreTest(typeof(MultipleStatusConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new MultipleStatusConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<MultipleStatusConversionCustomer>(
                        "SELECT 'same' AS primary_status, 'same' AS secondary_status;");

                    Assert.Equal(AccountStatus.Active, customer.PrimaryStatus);
                    Assert.Equal(AccountStatus.Inactive, customer.SecondaryStatus);
                }
            }
            finally
            {
                PreTest(typeof(MultipleStatusConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldIgnoreWriteOnlyConverterDuringReadMaterialization()
        {
            PreTest(typeof(WriteOnlyReadConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new WriteOnlyReadConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<WriteOnlyReadConversionCustomer>(
                        "SELECT 'Active' AS status;");

                    Assert.Equal(AccountStatus.Active, customer.Status);
                }
            }
            finally
            {
                PreTest(typeof(WriteOnlyReadConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldApplyReadConvertersAcrossConcurrentDefaultAndProfileQueries()
        {
            PreTest(typeof(ProfileConversionCustomer));
            CountingStatusConverter.Calls = 0;

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new DefaultProfileConversionCustomerMap());
                    configuration.AddProfile<LegacyProfileConversionCustomerMap>();
                });

                var results = Enumerable.Range(0, 32)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            if (index % 2 == 0)
                            {
                                var current = connection.QueryMappedSingle<ProfileConversionCustomer>(
                                    "SELECT 'A' AS status;");
                                return current.Status == AccountStatus.Active;
                            }

                            var legacy = connection.QueryMappedSingle<ProfileConversionCustomer, LegacyProfile>(
                                "SELECT '1' AS legacy_status;");
                            return legacy.Status == AccountStatus.Inactive;
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
                Assert.Equal(16, CountingStatusConverter.Calls);
            }
            finally
            {
                PreTest(typeof(ProfileConversionCustomer));
            }
        }

        [Fact]
        public void ReadMappedShouldApplyReadConvertersFromCommonMaterializer()
        {
            PreTest(typeof(ConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ConversionCustomerMap()));

                using (var reader = CreateReader(CreateTable(
                    new[] { "customer_id", "status", "optional_score" },
                    new object[] { 9, "A", "17" })))
                using (var multi = new MappedGridReader(reader))
                {
                    var customer = multi.ReadMappedSingle<ConversionCustomer>();

                    Assert.Equal(9, customer.Id);
                    Assert.Equal(AccountStatus.Active, customer.Status);
                    Assert.Equal(17, customer.OptionalScore);
                }
            }
            finally
            {
                PreTest(typeof(ConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedUnbufferedShouldApplyReadConverters()
        {
            PreTest(typeof(ConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ConversionCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = connection.QueryMappedUnbuffered<ConversionCustomer>(
                            "SELECT 10 AS customer_id, 'A' AS status, '22' AS optional_score;")
                        .Single();

                    Assert.Equal(10, customer.Id);
                    Assert.Equal(AccountStatus.Active, customer.Status);
                    Assert.Equal(22, customer.OptionalScore);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(ConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task QueryMappedUnbufferedAsyncShouldApplyReadConverters()
        {
            PreTest(typeof(ConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ConversionCustomerMap()));

                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    var customer = (await ToListAsync(connection.QueryMappedUnbufferedAsync<ConversionCustomer>(
                        "SELECT 11 AS customer_id, 'A' AS status, '23' AS optional_score;",
                        TestContext.Current.CancellationToken))).Single();

                    Assert.Equal(11, customer.Id);
                    Assert.Equal(AccountStatus.Active, customer.Status);
                    Assert.Equal(23, customer.OptionalScore);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
            }
            finally
            {
                PreTest(typeof(ConversionCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUsePropertyConverterInsteadOfDapperTypeHandlerForThatProperty()
        {
            PreTest(typeof(TypeHandlerCoexistenceCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new HandledCodeTypeHandler());
                FluentMapper.Initialize(configuration => configuration.AddMap(new TypeHandlerCoexistenceCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<TypeHandlerCoexistenceCustomer>(
                        "SELECT 'one' AS property_code, 'two' AS handler_code;");

                    Assert.Equal("property:one", customer.PropertyCode.Value);
                    Assert.Equal("handler:two", customer.HandlerCode.Value);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(TypeHandlerCoexistenceCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldWrapConverterExceptionsWithMappingContext()
        {
            PreTest(typeof(ThrowingConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ThrowingConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<ThrowingConversionCustomer>(
                            "SELECT 'bad' AS status;"));

                    Assert.IsType<InvalidOperationException>(exception.InnerException);
                    Assert.Contains(typeof(ThrowingConversionCustomer).FullName, exception.Message);
                    Assert.Contains(nameof(ThrowingConversionCustomer.Status), exception.Message);
                    Assert.Contains("status", exception.Message);
                    Assert.Contains(typeof(ThrowingStatusConverter).FullName, exception.Message);
                    Assert.Contains(typeof(string).FullName, exception.Message);
                    Assert.Contains(typeof(AccountStatus).FullName, exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(ThrowingConversionCustomer));
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

        private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
        {
            var results = new List<T>();

            await foreach (var item in source)
            {
                results.Add(item);
            }

            return results;
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private enum AccountStatus
        {
            Unknown,
            Active,
            Inactive
        }

        private sealed class ConversionCustomer
        {
            public int Id { get; set; }

            public AccountStatus Status { get; set; }

            public int? OptionalScore { get; set; }
        }

        private sealed class ConversionCustomerMap : EntityMap<ConversionCustomer>
        {
            public ConversionCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<CountingStatusConverter, string>();
                Map(customer => customer.OptionalScore).ToColumn("optional_score").ConvertFromDatabaseUsing<ScoreConverter, string>();
            }
        }

        private sealed class CountingStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public static int Calls;

            public AccountStatus ConvertFromDatabase(string value)
            {
                Interlocked.Increment(ref Calls);
                return value == "A" ? AccountStatus.Active : AccountStatus.Unknown;
            }
        }

        private sealed class ScoreConverter : IReadPropertyConverter<string, int>
        {
            public int ConvertFromDatabase(string value)
            {
                return int.Parse(value);
            }
        }

        private sealed class NestedConversionCustomer
        {
            public Address BillingAddress { get; set; }

            public Address ShippingAddress { get; set; }
        }

        private sealed class Address
        {
            public string ZipCode { get; set; }
        }

        private sealed class NestedConversionCustomerMap : EntityMap<NestedConversionCustomer>
        {
            public NestedConversionCustomerMap()
            {
                Map(customer => customer.BillingAddress.ZipCode)
                    .ToColumn("billing_zip")
                    .ConvertFromDatabaseUsing<CountingZipCodeConverter, string>();
                Map(customer => customer.ShippingAddress.ZipCode).ToColumn("shipping_zip");
            }
        }

        private sealed class CountingZipCodeConverter : IReadPropertyConverter<string, string>
        {
            public static int Calls { get; set; }

            public string ConvertFromDatabase(string value)
            {
                Calls++;
                return "ZIP-" + value;
            }
        }

        private sealed class ImmutableConversionCustomer
        {
            public ImmutableConversionCustomer(AccountStatus status)
            {
                Status = status;
            }

            public AccountStatus Status { get; }
        }

        private sealed class ImmutableConversionCustomerMap : EntityMap<ImmutableConversionCustomer>
        {
            public ImmutableConversionCustomerMap()
            {
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<LegacyStatusConverter, string>();
            }
        }

        private sealed class LegacyStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return value == "1" || value == "I" ? AccountStatus.Inactive : AccountStatus.Active;
            }
        }

        private sealed class ValueObjectConversionCustomer
        {
            public Cpf Cpf { get; set; }
        }

        private sealed class ValueObjectConversionCustomerMap : EntityMap<ValueObjectConversionCustomer>
        {
            public ValueObjectConversionCustomerMap()
            {
                Map(customer => customer.Cpf).ToColumn("cpf").ConvertFromDatabaseUsing<CpfConverter, string>();
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

        private sealed class CpfConverter : IReadPropertyConverter<string, Cpf>
        {
            public Cpf ConvertFromDatabase(string value)
            {
                return new Cpf("converted:" + value);
            }
        }

        private sealed class LegacyProfile : IMappingProfile
        {
        }

        private sealed class ProfileConversionCustomer
        {
            public AccountStatus Status { get; set; }
        }

        private sealed class DefaultProfileConversionCustomerMap : EntityMap<ProfileConversionCustomer>
        {
            public DefaultProfileConversionCustomerMap()
            {
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<CountingStatusConverter, string>();
            }
        }

        private sealed class LegacyProfileConversionCustomerMap :
            EntityMap<ProfileConversionCustomer>,
            IProfileMap<LegacyProfile>
        {
            public LegacyProfileConversionCustomerMap()
            {
                Map(customer => customer.Status).ToColumn("legacy_status").ConvertFromDatabaseUsing<LegacyStatusConverter, string>();
            }
        }

        private sealed class MultipleStatusConversionCustomer
        {
            public AccountStatus PrimaryStatus { get; set; }

            public AccountStatus SecondaryStatus { get; set; }
        }

        private sealed class MultipleStatusConversionCustomerMap : EntityMap<MultipleStatusConversionCustomer>
        {
            public MultipleStatusConversionCustomerMap()
            {
                Map(customer => customer.PrimaryStatus)
                    .ToColumn("primary_status")
                    .ConvertFromDatabaseUsing<PrimaryStatusConverter, string>();
                Map(customer => customer.SecondaryStatus)
                    .ToColumn("secondary_status")
                    .ConvertFromDatabaseUsing<SecondaryStatusConverter, string>();
            }
        }

        private sealed class PrimaryStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Active;
            }
        }

        private sealed class SecondaryStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Inactive;
            }
        }

        private sealed class WriteOnlyReadConversionCustomer
        {
            public AccountStatus Status { get; set; }
        }

        private sealed class WriteOnlyReadConversionCustomerMap : EntityMap<WriteOnlyReadConversionCustomer>
        {
            public WriteOnlyReadConversionCustomerMap()
            {
                Map(customer => customer.Status)
                    .ToColumn("status")
                    .ConvertToDatabaseUsing<ThrowingWriteConverter, string>();
            }
        }

        private sealed class ThrowingWriteConverter : IWritePropertyConverter<AccountStatus, string>
        {
            public string ConvertToDatabase(AccountStatus value)
            {
                throw new InvalidOperationException("Write converter should not run during read materialization.");
            }
        }

        private sealed class HandledCode
        {
            public HandledCode(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class TypeHandlerCoexistenceCustomer
        {
            public HandledCode PropertyCode { get; set; }

            public HandledCode HandlerCode { get; set; }
        }

        private sealed class TypeHandlerCoexistenceCustomerMap : EntityMap<TypeHandlerCoexistenceCustomer>
        {
            public TypeHandlerCoexistenceCustomerMap()
            {
                Map(customer => customer.PropertyCode)
                    .ToColumn("property_code")
                    .ConvertFromDatabaseUsing<PropertyCodeConverter, string>();
                Map(customer => customer.HandlerCode).ToColumn("handler_code");
            }
        }

        private sealed class PropertyCodeConverter : IReadPropertyConverter<string, HandledCode>
        {
            public HandledCode ConvertFromDatabase(string value)
            {
                return new HandledCode("property:" + value);
            }
        }

        private sealed class HandledCodeTypeHandler : SqlMapper.TypeHandler<HandledCode>
        {
            public override HandledCode Parse(object value)
            {
                return new HandledCode("handler:" + (string)value);
            }

            public override void SetValue(IDbDataParameter parameter, HandledCode value)
            {
                parameter.Value = value == null ? DBNull.Value : value.Value;
            }
        }

        private sealed class ThrowingConversionCustomer
        {
            public AccountStatus Status { get; set; }
        }

        private sealed class ThrowingConversionCustomerMap : EntityMap<ThrowingConversionCustomer>
        {
            public ThrowingConversionCustomerMap()
            {
                Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<ThrowingStatusConverter, string>();
            }
        }

        private sealed class ThrowingStatusConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                throw new InvalidOperationException("Invalid status.");
            }
        }
    }
}
