using System;
using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ConstructorMappingTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void TraditionalPocoShouldContinueMaterializingConfiguredColumn()
        {
            PreTest(typeof(TraditionalPoco));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new TraditionalPocoMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<TraditionalPoco>(
                        "SELECT 1 AS person_id, 'Ada' AS Name;");

                    Assert.Equal(1, entity.Id);
                    Assert.Equal("Ada", entity.Name);
                }
            }
            finally
            {
                PreTest(typeof(TraditionalPoco));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void PositionalRecordShouldMaterializeExplicitColumns()
        {
            PreTest(typeof(ExplicitRecord));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitRecordMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ExplicitRecord>(
                        "SELECT 2 AS person_id, 'Grace Hopper' AS full_name;");

                    Assert.Equal(2, entity.Id);
                    Assert.Equal("Grace Hopper", entity.FullName);
                }
            }
            finally
            {
                PreTest(typeof(ExplicitRecord));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ImmutableClassShouldMaterializeExplicitColumns()
        {
            PreTest(typeof(ExplicitImmutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitImmutableCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ExplicitImmutableCustomer>(
                        "SELECT 3 AS person_id, 'Katherine Johnson' AS full_name;");

                    Assert.Equal(3, entity.Id);
                    Assert.Equal("Katherine Johnson", entity.FullName);
                }
            }
            finally
            {
                PreTest(typeof(ExplicitImmutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void NamingPolicyShouldMaterializeConstructorParameters()
        {
            PreTest(typeof(PolicyImmutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<PolicyImmutableCustomer>());

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<PolicyImmutableCustomer>(
                        "SELECT 4 AS customer_id, 'Barbara Liskov' AS full_name;");

                    Assert.Equal(4, entity.CustomerId);
                    Assert.Equal("Barbara Liskov", entity.FullName);
                }
            }
            finally
            {
                PreTest(typeof(PolicyImmutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ConventionShouldMaterializeConstructorParameters()
        {
            PreTest(typeof(ConventionImmutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddConvention<PrefixConvention>().ForEntity<ConventionImmutableCustomer>());

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ConventionImmutableCustomer>(
                        "SELECT 5 AS colId, 'Margaret Hamilton' AS colName;");

                    Assert.Equal(5, entity.Id);
                    Assert.Equal("Margaret Hamilton", entity.Name);
                }
            }
            finally
            {
                PreTest(typeof(ConventionImmutableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void MultipleConstructorsShouldUseMappedNamesForDapperSelection()
        {
            PreTest(typeof(MultipleConstructorCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new MultipleConstructorCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<MultipleConstructorCustomer>(
                        "SELECT 6 AS person_id, 'Anita Borg' AS full_name;");

                    Assert.Equal(6, entity.Id);
                    Assert.Equal("Anita Borg", entity.FullName);
                    Assert.Equal("id-name", entity.ConstructorUsed);
                }
            }
            finally
            {
                PreTest(typeof(MultipleConstructorCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ParameterlessConstructorShouldContinueUsingSettableProperties()
        {
            PreTest(typeof(ParameterlessAndSettableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ParameterlessAndSettableCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ParameterlessAndSettableCustomer>(
                        "SELECT 7 AS person_id, 'Joan Clarke' AS full_name;");

                    Assert.Equal(7, entity.Id);
                    Assert.Equal("Joan Clarke", entity.FullName);
                    Assert.Equal("parameterless", entity.ConstructorUsed);
                }
            }
            finally
            {
                PreTest(typeof(ParameterlessAndSettableCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void CaseInsensitiveExplicitMappingShouldMaterializeConstructorParameter()
        {
            PreTest(typeof(CaseInsensitiveConstructorCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CaseInsensitiveConstructorCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<CaseInsensitiveConstructorCustomer>(
                        "SELECT 8 AS PERSON_ID;");

                    Assert.Equal(8, entity.Id);
                }
            }
            finally
            {
                PreTest(typeof(CaseInsensitiveConstructorCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ConstructorParameterMappingShouldFallbackToDapperDefault()
        {
            PreTest(typeof(PartialExplicitConstructorCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new PartialExplicitConstructorCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<PartialExplicitConstructorCustomer>(
                        "SELECT 9 AS Id, 'Radia Perlman' AS full_name;");

                    Assert.Equal(9L, entity.Id);
                    Assert.Equal("Radia Perlman", entity.FullName);
                }
            }
            finally
            {
                PreTest(typeof(PartialExplicitConstructorCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void IncludedBaseMappingShouldMaterializeConstructorParameter()
        {
            PreTest(typeof(ImmutableBaseCustomer), typeof(ImmutableDerivedCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ImmutableBaseCustomerMap());
                    c.AddMap(new ImmutableDerivedCustomerMap());
                });

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ImmutableDerivedCustomer>(
                        "SELECT 10 AS person_id, 'Evelyn Boyd Granville' AS Name;");

                    Assert.Equal(10, entity.Id);
                    Assert.Equal("Evelyn Boyd Granville", entity.Name);
                }
            }
            finally
            {
                PreTest(typeof(ImmutableBaseCustomer), typeof(ImmutableDerivedCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void NestedMemberPathMappingShouldNotActAsConstructorParameterMapping()
        {
            PreTest(typeof(NestedPathConstructorCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new NestedPathConstructorCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        connection.QuerySingle<NestedPathConstructorCustomer>(
                            "SELECT 11 AS rank_level;"));

                    Assert.Contains("constructor", exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(NestedPathConstructorCustomer));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void PreTest(params System.Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class TraditionalPoco
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class TraditionalPocoMap : EntityMap<TraditionalPoco>
        {
            public TraditionalPocoMap()
            {
                Map(e => e.Id).ToColumn("person_id");
            }
        }

        private sealed record ExplicitRecord(int Id, string FullName);

        private class ExplicitRecordMap : EntityMap<ExplicitRecord>
        {
            public ExplicitRecordMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private sealed class ExplicitImmutableCustomer
        {
            public ExplicitImmutableCustomer(int id, string fullName)
            {
                Id = id;
                FullName = fullName;
            }

            public int Id { get; }

            public string FullName { get; }
        }

        private class ExplicitImmutableCustomerMap : EntityMap<ExplicitImmutableCustomer>
        {
            public ExplicitImmutableCustomerMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private sealed class PolicyImmutableCustomer
        {
            public PolicyImmutableCustomer(int customerId, string fullName)
            {
                CustomerId = customerId;
                FullName = fullName;
            }

            public int CustomerId { get; }

            public string FullName { get; }
        }

        private sealed class ConventionImmutableCustomer
        {
            public ConventionImmutableCustomer(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; }

            public string Name { get; }
        }

        private sealed class MultipleConstructorCustomer
        {
            public MultipleConstructorCustomer(int id)
            {
                Id = id;
                ConstructorUsed = "id";
            }

            public MultipleConstructorCustomer(int id, string fullName)
            {
                Id = id;
                FullName = fullName;
                ConstructorUsed = "id-name";
            }

            public int Id { get; }

            public string FullName { get; }

            public string ConstructorUsed { get; }
        }

        private class MultipleConstructorCustomerMap : EntityMap<MultipleConstructorCustomer>
        {
            public MultipleConstructorCustomerMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private sealed class ParameterlessAndSettableCustomer
        {
            public ParameterlessAndSettableCustomer()
            {
                ConstructorUsed = "parameterless";
            }

            public ParameterlessAndSettableCustomer(int id, string fullName)
            {
                Id = id;
                FullName = fullName;
                ConstructorUsed = "id-name";
            }

            public int Id { get; set; }

            public string FullName { get; set; }

            public string ConstructorUsed { get; }
        }

        private class ParameterlessAndSettableCustomerMap : EntityMap<ParameterlessAndSettableCustomer>
        {
            public ParameterlessAndSettableCustomerMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private sealed class CaseInsensitiveConstructorCustomer
        {
            public CaseInsensitiveConstructorCustomer(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }

        private class CaseInsensitiveConstructorCustomerMap : EntityMap<CaseInsensitiveConstructorCustomer>
        {
            public CaseInsensitiveConstructorCustomerMap()
            {
                Map(e => e.Id).ToColumn("person_id", caseSensitive: false);
            }
        }

        private sealed class PartialExplicitConstructorCustomer
        {
            public PartialExplicitConstructorCustomer(long id, string fullName)
            {
                Id = id;
                FullName = fullName;
            }

            public long Id { get; }

            public string FullName { get; }
        }

        private class PartialExplicitConstructorCustomerMap : EntityMap<PartialExplicitConstructorCustomer>
        {
            public PartialExplicitConstructorCustomerMap()
            {
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private class ImmutableBaseCustomer
        {
            public ImmutableBaseCustomer(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }

        private sealed class ImmutableDerivedCustomer : ImmutableBaseCustomer
        {
            public ImmutableDerivedCustomer(int id, string name)
                : base(id)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private class ImmutableBaseCustomerMap : EntityMap<ImmutableBaseCustomer>
        {
            public ImmutableBaseCustomerMap()
            {
                Map(e => e.Id).ToColumn("person_id");
            }
        }

        private class ImmutableDerivedCustomerMap : EntityMap<ImmutableDerivedCustomer>
        {
            public ImmutableDerivedCustomerMap()
            {
                IncludeBase<ImmutableBaseCustomer>();
            }
        }

        private sealed class NestedPathConstructorCustomer
        {
            public NestedPathConstructorCustomer(int level)
            {
                Level = level;
            }

            public int Level { get; }

            public RankInfo Rank { get; set; }
        }

        private sealed class RankInfo
        {
            public int Level { get; set; }
        }

        private class NestedPathConstructorCustomerMap : EntityMap<NestedPathConstructorCustomer>
        {
            public NestedPathConstructorCustomerMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
            }
        }

        private class PrefixConvention : Convention
        {
            public PrefixConvention()
            {
                Properties()
                    .Configure(c => c.HasPrefix("col"));
            }
        }
    }
}
