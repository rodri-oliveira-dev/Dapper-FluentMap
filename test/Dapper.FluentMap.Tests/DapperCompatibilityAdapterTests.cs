using System;
using Dapper;
using Dapper.FluentMap.Compatibility;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class DapperCompatibilityAdapterTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseRegisteredDapperTypeHandler()
        {
            PreTest(typeof(TypeHandlerCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new CpfTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new TypeHandlerCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<TypeHandlerCustomer>(
                        "SELECT '12345678909' AS cpf;");

                    Assert.NotNull(customer.Cpf);
                    Assert.Equal("12345678909", customer.Cpf.Number);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(TypeHandlerCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseRegisteredDapperTypeHandlerForNullableValue()
        {
            PreTest(typeof(NullableHandlerCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new SmallCodeTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new NullableHandlerCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<NullableHandlerCustomer>(
                        "SELECT 7 AS code;");

                    Assert.True(customer.Code.HasValue);
                    Assert.Equal(7, customer.Code.Value.Value);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(NullableHandlerCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldKeepNullableTypeHandlerValueNullWhenColumnIsDbNull()
        {
            PreTest(typeof(NullableHandlerCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new SmallCodeTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new NullableHandlerCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<NullableHandlerCustomer>(
                        "SELECT NULL AS code;");

                    Assert.False(customer.Code.HasValue);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(NullableHandlerCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseDefaultConversionWhenNoTypeHandlerIsRegistered()
        {
            PreTest(typeof(DefaultConversionCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new DefaultConversionCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<DefaultConversionCustomer>(
                        "SELECT '42' AS customer_id;");

                    Assert.Equal(42, customer.Id);
                }
            }
            finally
            {
                PreTest(typeof(DefaultConversionCustomer));
            }
        }

        [Fact]
        public void TypeHandlerBoundaryShouldFailWithDiagnosticWhenDapperCacheShapeIsMissing()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => DapperTypeHandlerAdapter.CreateConverter(typeof(Cpf), () => null));

            Assert.Contains("Dapper TypeHandler compatibility failed", exception.Message);
            Assert.Contains("TypeHandlerCache", exception.Message);
            Assert.Contains("upgrading Dapper", exception.Message);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldNotMapIgnoredRootPropertyOrFallbackToDefault()
        {
            PreTest(typeof(IgnoredRootCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new IgnoredRootCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<IgnoredRootCustomer>(
                        "SELECT 99 AS Id, 'Ada' AS Name;");

                    Assert.Equal(0, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(IgnoredRootCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldNotMapIgnoredNestedPathOrFallbackToRootProperty()
        {
            PreTest(typeof(IgnoredNestedCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new IgnoredNestedCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<IgnoredNestedCustomer>(
                        "SELECT 'leaked' AS City;");

                    Assert.Null(customer.City);
                    Assert.Null(customer.Address);
                }
            }
            finally
            {
                PreTest(typeof(IgnoredNestedCustomer));
            }
        }

        [Fact]
        public void TypeMapShouldReturnNullForIgnoredMemberWithoutThrowing()
        {
            PreTest(typeof(IgnoredRootCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new IgnoredRootCustomerMap()));

                var typeMap = SqlMapper.GetTypeMap(typeof(IgnoredRootCustomer));
                var member = typeMap.GetMember("Id");

                Assert.Null(member);
            }
            finally
            {
                PreTest(typeof(IgnoredRootCustomer));
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

        private sealed class TypeHandlerCustomer
        {
            public Cpf Cpf { get; set; }
        }

        private sealed class TypeHandlerCustomerMap : EntityMap<TypeHandlerCustomer>
        {
            public TypeHandlerCustomerMap()
            {
                Map(customer => customer.Cpf).ToColumn("cpf");
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

        private sealed class NullableHandlerCustomer
        {
            public SmallCode? Code { get; set; }
        }

        private sealed class NullableHandlerCustomerMap : EntityMap<NullableHandlerCustomer>
        {
            public NullableHandlerCustomerMap()
            {
                Map(customer => customer.Code).ToColumn("code");
            }
        }

        private readonly struct SmallCode
        {
            public SmallCode(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class SmallCodeTypeHandler : SqlMapper.TypeHandler<SmallCode>
        {
            public override SmallCode Parse(object value)
            {
                return new SmallCode(Convert.ToInt32(value));
            }

            public override void SetValue(System.Data.IDbDataParameter parameter, SmallCode value)
            {
                parameter.Value = value.Value;
            }
        }

        private sealed class DefaultConversionCustomer
        {
            public int Id { get; set; }
        }

        private sealed class DefaultConversionCustomerMap : EntityMap<DefaultConversionCustomer>
        {
            public DefaultConversionCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
            }
        }

        private sealed class IgnoredRootCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class IgnoredRootCustomerMap : EntityMap<IgnoredRootCustomer>
        {
            public IgnoredRootCustomerMap()
            {
                Map(customer => customer.Id).Ignore();
            }
        }

        private sealed class IgnoredNestedCustomer
        {
            public string City { get; set; }

            public IgnoredAddress Address { get; set; }
        }

        private sealed class IgnoredAddress
        {
            public string City { get; set; }
        }

        private sealed class IgnoredNestedCustomerMap : EntityMap<IgnoredNestedCustomer>
        {
            public IgnoredNestedCustomerMap()
            {
                Map(customer => customer.Address.City).Ignore();
            }
        }
    }
}
