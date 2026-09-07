using System;
using System.Reflection;
using Dapper;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class NestedMaterializationSpikeTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldNotTreatNestedMutablePathAsRootProperty()
        {
            PreTest(typeof(NestedMutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new NestedMutableCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<NestedMutableCustomer>("SELECT 'Recife' AS city;");

                    Assert.Null(customer.Address);
                }
            }
            finally
            {
                PreTest(typeof(NestedMutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void NestedPathsWithSameTerminalShouldBeConfiguredButDapperQueryShouldNotMaterializeThem()
        {
            PreTest(typeof(SameTerminalCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new SameTerminalCustomerMap()));

                var explanation = FluentMapper.Explain<SameTerminalCustomer>();
                Assert.Contains(explanation.Members, m => m.MemberPath == "Rank.Level" && m.ColumnName == "rank_level");
                Assert.Contains(explanation.Members, m => m.MemberPath == "Seniority.Level" && m.ColumnName == "seniority_level");

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<SameTerminalCustomer>(
                        "SELECT 'gold' AS rank_level, 'staff' AS seniority_level;");

                    Assert.Null(customer.Rank);
                    Assert.Null(customer.Seniority);
                }
            }
            finally
            {
                PreTest(typeof(SameTerminalCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void TypeHandlerShouldMaterializeScalarValueObjectProperty()
        {
            PreTest(typeof(ScalarValueObjectCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new CpfTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new ScalarValueObjectCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<ScalarValueObjectCustomer>(
                        "SELECT '12345678909' AS cpf;");

                    Assert.NotNull(customer.Cpf);
                    Assert.Equal("12345678909", customer.Cpf.Number);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(ScalarValueObjectCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldNotUseTypeHandlerForNestedValueObjectPath()
        {
            PreTest(typeof(NestedValueObjectCustomer));

            try
            {
                SqlMapper.AddTypeHandler(new CpfTypeHandler());
                FluentMapper.Initialize(c => c.AddMap(new NestedValueObjectCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<NestedValueObjectCustomer>(
                        "SELECT '12345678909' AS cpf;");

                    Assert.Null(customer.Cpf);
                }
            }
            finally
            {
                SqlMapper.ResetTypeHandlers();
                PreTest(typeof(NestedValueObjectCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void NestedRecordShouldNotMaterializeThroughConstructorMapping()
        {
            PreTest(typeof(RecordCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new RecordCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        connection.QuerySingle<RecordCustomer>(
                            "SELECT 42 AS customer_id, 'Olinda' AS city;"));

                    Assert.Contains("constructor", exception.Message, StringComparison.OrdinalIgnoreCase);
                }
            }
            finally
            {
                PreTest(typeof(RecordCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void PureITypeMapReturningNestedLeafPropertyShouldWriteLeafValueIntoRootSlot()
        {
            PreTest(typeof(PureTypeMapCustomer));

            try
            {
                SqlMapper.SetTypeMap(
                    typeof(PureTypeMapCustomer),
                    new LeafPropertyTypeMap(typeof(PureTypeMapAddress).GetProperty(nameof(PureTypeMapAddress.City))));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<PureTypeMapCustomer>("SELECT 'Natal' AS city;");

                    var assignedValue = (object)customer.Address;

                    Assert.IsType<string>(assignedValue);
                    Assert.Equal("Natal", assignedValue);
                }
            }
            finally
            {
                PreTest(typeof(PureTypeMapCustomer));
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

        private sealed class NestedMutableCustomer
        {
            public NestedMutableAddress Address { get; set; }
        }

        private sealed class NestedMutableAddress
        {
            public string City { get; set; }
        }

        private sealed class NestedMutableCustomerMap : EntityMap<NestedMutableCustomer>
        {
            public NestedMutableCustomerMap()
            {
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class SameTerminalCustomer
        {
            public RankInfo Rank { get; set; }

            public SeniorityInfo Seniority { get; set; }
        }

        private sealed class RankInfo
        {
            public string Level { get; set; }
        }

        private sealed class SeniorityInfo
        {
            public string Level { get; set; }
        }

        private sealed class SameTerminalCustomerMap : EntityMap<SameTerminalCustomer>
        {
            public SameTerminalCustomerMap()
            {
                Map(customer => customer.Rank.Level).ToColumn("rank_level");
                Map(customer => customer.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private sealed class ScalarValueObjectCustomer
        {
            public Cpf Cpf { get; set; }
        }

        private sealed class ScalarValueObjectCustomerMap : EntityMap<ScalarValueObjectCustomer>
        {
            public ScalarValueObjectCustomerMap()
            {
                Map(customer => customer.Cpf).ToColumn("cpf");
            }
        }

        private sealed class NestedValueObjectCustomer
        {
            public Cpf Cpf { get; set; }
        }

        private sealed class NestedValueObjectCustomerMap : EntityMap<NestedValueObjectCustomer>
        {
            public NestedValueObjectCustomerMap()
            {
                Map(customer => customer.Cpf.Number).ToColumn("cpf");
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

        private sealed record RecordAddress(string City);

        private sealed record RecordCustomer(int Id, RecordAddress Address);

        private sealed class RecordCustomerMap : EntityMap<RecordCustomer>
        {
            public RecordCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Address.City).ToColumn("city");
            }
        }

        private sealed class PureTypeMapCustomer
        {
            public PureTypeMapAddress Address { get; set; }
        }

        private sealed class PureTypeMapAddress
        {
            public string City { get; set; }
        }

        private sealed class LeafPropertyTypeMap : SqlMapper.ITypeMap
        {
            private readonly PropertyInfo _property;

            public LeafPropertyTypeMap(PropertyInfo property)
            {
                _property = property;
            }

            public ConstructorInfo FindConstructor(string[] names, Type[] types)
            {
                return typeof(PureTypeMapCustomer).GetConstructor(Type.EmptyTypes);
            }

            public ConstructorInfo FindExplicitConstructor()
            {
                return null;
            }

            public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
            {
                return null;
            }

            public SqlMapper.IMemberMap GetMember(string columnName)
            {
                return new LeafMemberMap(columnName, _property);
            }
        }

        private sealed class LeafMemberMap : SqlMapper.IMemberMap
        {
            public LeafMemberMap(string columnName, PropertyInfo property)
            {
                ColumnName = columnName;
                Property = property;
            }

            public string ColumnName { get; }

            public Type MemberType => Property.PropertyType;

            public PropertyInfo Property { get; }

            public FieldInfo Field => null;

            public ParameterInfo Parameter => null;
        }
    }
}
