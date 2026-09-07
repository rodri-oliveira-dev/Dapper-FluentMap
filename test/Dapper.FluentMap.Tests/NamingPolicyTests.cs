using System;
using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class NamingPolicyTests
    {
        [Fact]
        public void WithoutNamingPolicyShouldUseDapperDefaultFallback()
        {
            PreTest(typeof(DefaultPolicyEntity));

            try
            {
                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<DefaultPolicyEntity>(
                        "SELECT 3 AS Id, 'Ada' AS Name;");

                    Assert.Equal(3, entity.Id);
                    Assert.Equal("Ada", entity.Name);
                }
            }
            finally
            {
                PreTest(typeof(DefaultPolicyEntity));
            }
        }

        [Fact]
        public void SnakeCaseNamingPolicyShouldResolveColumn()
        {
            PreTest(typeof(SnakeCaseEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<SnakeCaseEntity>());

                var member = SqlMapper.GetTypeMap(typeof(SnakeCaseEntity)).GetMember("customer_id");

                Assert.NotNull(member);
                Assert.Equal(typeof(SnakeCaseEntity).GetProperty(nameof(SnakeCaseEntity.CustomerId)), member.Property);
            }
            finally
            {
                PreTest(typeof(SnakeCaseEntity));
            }
        }

        [Fact]
        public void PrefixNamingPolicyShouldResolveColumn()
        {
            PreTest(typeof(PrefixPolicyEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase.WithPrefix("usr_")).ForEntity<PrefixPolicyEntity>());

                var member = SqlMapper.GetTypeMap(typeof(PrefixPolicyEntity)).GetMember("usr_name");

                Assert.NotNull(member);
                Assert.Equal(typeof(PrefixPolicyEntity).GetProperty(nameof(PrefixPolicyEntity.Name)), member.Property);
            }
            finally
            {
                PreTest(typeof(PrefixPolicyEntity));
            }
        }

        [Fact]
        public void SuffixNamingPolicyShouldResolveColumn()
        {
            PreTest(typeof(SuffixPolicyEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase.WithSuffix("_txt")).ForEntity<SuffixPolicyEntity>());

                var member = SqlMapper.GetTypeMap(typeof(SuffixPolicyEntity)).GetMember("first_name_txt");

                Assert.NotNull(member);
                Assert.Equal(typeof(SuffixPolicyEntity).GetProperty(nameof(SuffixPolicyEntity.FirstName)), member.Property);
            }
            finally
            {
                PreTest(typeof(SuffixPolicyEntity));
            }
        }

        [Fact]
        public void CustomNamingPolicyShouldResolveColumn()
        {
            PreTest(typeof(CustomPolicyEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(name => "x_" + name.ToLowerInvariant()).ForEntity<CustomPolicyEntity>());

                var member = SqlMapper.GetTypeMap(typeof(CustomPolicyEntity)).GetMember("x_code");

                Assert.NotNull(member);
                Assert.Equal(typeof(CustomPolicyEntity).GetProperty(nameof(CustomPolicyEntity.Code)), member.Property);
            }
            finally
            {
                PreTest(typeof(CustomPolicyEntity));
            }
        }

        [Fact]
        public void ExplicitMappingShouldTakePrecedenceOverNamingPolicy()
        {
            PreTest(typeof(ExplicitPolicyEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ExplicitPolicyMap());
                    c.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<ExplicitPolicyEntity>();
                });

                var explicitMember = FluentMapper.Registry.GetFluentPropertyInfo(typeof(ExplicitPolicyEntity), "person_name");
                var policyMember = FluentMapper.Registry.GetFluentPropertyInfo(typeof(ExplicitPolicyEntity), "first_name");

                Assert.Equal(typeof(ExplicitPolicyEntity).GetProperty(nameof(ExplicitPolicyEntity.FirstName)), explicitMember);
                Assert.Null(policyMember);
            }
            finally
            {
                PreTest(typeof(ExplicitPolicyEntity));
            }
        }

        [Fact]
        public void InheritedMappingShouldTakePrecedenceOverNamingPolicy()
        {
            PreTest(typeof(PolicyBaseUser), typeof(PolicyAdminUser));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new PolicyBaseUserMap());
                    c.AddMap(new PolicyAdminUserMap());
                    c.UseNamingPolicy(NamingPolicy.Prefix("col")).ForEntity<PolicyAdminUser>();
                });

                var inheritedMember = FluentMapper.Registry.GetFluentPropertyInfo(typeof(PolicyAdminUser), "user_id");
                var policyMember = FluentMapper.Registry.GetFluentPropertyInfo(typeof(PolicyAdminUser), "colId");

                Assert.Equal(typeof(PolicyBaseUser).GetProperty(nameof(PolicyBaseUser.Id)), inheritedMember);
                Assert.Null(policyMember);
            }
            finally
            {
                PreTest(typeof(PolicyBaseUser), typeof(PolicyAdminUser));
            }
        }

        [Fact]
        public void NamingPolicyAndConventionShouldResolveTogether()
        {
            PreTest(typeof(PolicyWithConventionEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.UseNamingPolicy(NamingPolicy.SnakeCase.WithPrefix("usr_")).ForEntity<PolicyWithConventionEntity>();
                    c.AddConvention<KeyConvention>().ForEntity<PolicyWithConventionEntity>();
                });

                var policyMember = SqlMapper.GetTypeMap(typeof(PolicyWithConventionEntity)).GetMember("usr_name");
                var conventionMember = SqlMapper.GetTypeMap(typeof(PolicyWithConventionEntity)).GetMember("key_id");

                Assert.NotNull(policyMember);
                Assert.NotNull(conventionMember);
                Assert.Equal(typeof(PolicyWithConventionEntity).GetProperty(nameof(PolicyWithConventionEntity.Name)), policyMember.Property);
                Assert.Equal(typeof(PolicyWithConventionEntity).GetProperty(nameof(PolicyWithConventionEntity.Id)), conventionMember.Property);
            }
            finally
            {
                PreTest(typeof(PolicyWithConventionEntity));
            }
        }

        [Fact]
        public void CaseInsensitiveNamingPolicyShouldMatchDifferentCase()
        {
            PreTest(typeof(CaseInsensitivePolicyEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.Prefix("col"), caseSensitive: false).ForEntity<CaseInsensitivePolicyEntity>());

                var member = SqlMapper.GetTypeMap(typeof(CaseInsensitivePolicyEntity)).GetMember("COLName");

                Assert.NotNull(member);
                Assert.Equal(typeof(CaseInsensitivePolicyEntity).GetProperty(nameof(CaseInsensitivePolicyEntity.Name)), member.Property);
            }
            finally
            {
                PreTest(typeof(CaseInsensitivePolicyEntity));
            }
        }

        [Fact]
        public void NamingPolicyShouldApplySameConfigurationToDifferentTypes()
        {
            PreTest(typeof(FirstSharedPolicyEntity), typeof(SecondSharedPolicyEntity));

            try
            {
                FluentMapper.Initialize(c =>
                    c.UseNamingPolicy(NamingPolicy.SnakeCase)
                     .ForEntity<FirstSharedPolicyEntity>()
                     .ForEntity<SecondSharedPolicyEntity>());

                var firstMember = SqlMapper.GetTypeMap(typeof(FirstSharedPolicyEntity)).GetMember("customer_id");
                var secondMember = SqlMapper.GetTypeMap(typeof(SecondSharedPolicyEntity)).GetMember("customer_id");

                Assert.NotNull(firstMember);
                Assert.NotNull(secondMember);
                Assert.Equal(typeof(FirstSharedPolicyEntity).GetProperty(nameof(FirstSharedPolicyEntity.CustomerId)), firstMember.Property);
                Assert.Equal(typeof(SecondSharedPolicyEntity).GetProperty(nameof(SecondSharedPolicyEntity.CustomerId)), secondMember.Property);
            }
            finally
            {
                PreTest(typeof(FirstSharedPolicyEntity), typeof(SecondSharedPolicyEntity));
            }
        }

        [Fact]
        public void InvalidNamingPolicyShouldThrowConfigurationException()
        {
            PreTest(typeof(InvalidPolicyEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(c => c.UseNamingPolicy(_ => null).ForEntity<InvalidPolicyEntity>()));

                Assert.Contains("empty column name", exception.Message);
                Assert.Contains(nameof(InvalidPolicyEntity.Name), exception.Message);
            }
            finally
            {
                PreTest(typeof(InvalidPolicyEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void NamingPolicyShouldMaterializeWithDapper()
        {
            PreTest(typeof(IntegrationPolicyEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<IntegrationPolicyEntity>());

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<IntegrationPolicyEntity>(
                        "SELECT 42 AS customer_id, 'Grace' AS first_name;");

                    Assert.Equal(42, entity.CustomerId);
                    Assert.Equal("Grace", entity.FirstName);
                }
            }
            finally
            {
                PreTest(typeof(IntegrationPolicyEntity));
            }
        }

        [Fact]
        public void NamingPolicyShouldNotChangeDapperMatchNamesWithUnderscores()
        {
            PreTest(typeof(SnakeCaseEntity));
            var original = DefaultTypeMap.MatchNamesWithUnderscores;

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<SnakeCaseEntity>());

                Assert.Equal(original, DefaultTypeMap.MatchNamesWithUnderscores);
            }
            finally
            {
                DefaultTypeMap.MatchNamesWithUnderscores = original;
                PreTest(typeof(SnakeCaseEntity));
            }
        }

        [Fact]
        public void DapperUnderscoreMatchingShouldMapSnakeCaseOnlyWhenGlobalFlagIsEnabled()
        {
            PreTest(typeof(NativeUnderscoreEntity));
            var original = DefaultTypeMap.MatchNamesWithUnderscores;

            try
            {
                DefaultTypeMap.MatchNamesWithUnderscores = false;
                var defaultMember = new DefaultTypeMap(typeof(NativeUnderscoreEntity)).GetMember("customer_id");

                DefaultTypeMap.MatchNamesWithUnderscores = true;
                var underscoreMember = new DefaultTypeMap(typeof(NativeUnderscoreEntity)).GetMember("customer_id");

                Assert.Null(defaultMember);
                Assert.NotNull(underscoreMember);
                Assert.Equal(typeof(NativeUnderscoreEntity).GetProperty(nameof(NativeUnderscoreEntity.CustomerId)), underscoreMember.Property);
            }
            finally
            {
                DefaultTypeMap.MatchNamesWithUnderscores = original;
                PreTest(typeof(NativeUnderscoreEntity));
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

        private class DefaultPolicyEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class SnakeCaseEntity
        {
            public int CustomerId { get; set; }
        }

        private class PrefixPolicyEntity
        {
            public string Name { get; set; }
        }

        private class SuffixPolicyEntity
        {
            public string FirstName { get; set; }
        }

        private class CustomPolicyEntity
        {
            public string Code { get; set; }
        }

        private class ExplicitPolicyEntity
        {
            public string FirstName { get; set; }
        }

        private class ExplicitPolicyMap : EntityMap<ExplicitPolicyEntity>
        {
            public ExplicitPolicyMap()
            {
                Map(e => e.FirstName).ToColumn("person_name");
            }
        }

        private class PolicyBaseUser
        {
            public int Id { get; set; }
        }

        private class PolicyAdminUser : PolicyBaseUser
        {
        }

        private class PolicyBaseUserMap : EntityMap<PolicyBaseUser>
        {
            public PolicyBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class PolicyAdminUserMap : EntityMap<PolicyAdminUser>
        {
            public PolicyAdminUserMap()
            {
                IncludeBase<PolicyBaseUser>();
            }
        }

        private class PolicyWithConventionEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class KeyConvention : Convention
        {
            public KeyConvention()
            {
                Properties<int>()
                    .Where(p => p.Name == "Id")
                    .Configure(c => c.HasColumnName("key_id"));
            }
        }

        private class CaseInsensitivePolicyEntity
        {
            public string Name { get; set; }
        }

        private class FirstSharedPolicyEntity
        {
            public int CustomerId { get; set; }
        }

        private class SecondSharedPolicyEntity
        {
            public int CustomerId { get; set; }
        }

        private class InvalidPolicyEntity
        {
            public string Name { get; set; }
        }

        private class IntegrationPolicyEntity
        {
            public int CustomerId { get; set; }

            public string FirstName { get; set; }
        }

        private class NativeUnderscoreEntity
        {
            public int CustomerId { get; set; }
        }
    }
}
