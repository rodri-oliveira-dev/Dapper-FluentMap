using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MappingProfileTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void DapperQueryShouldContinueUsingDefaultMapping()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new DefaultProfileCustomerMap());
                    c.AddProfile<LegacyProfileCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<ProfileCustomer>(
                        "SELECT 1 AS customer_id, 'Default' AS customer_name;");

                    Assert.Equal(1, customer.Id);
                    Assert.Equal("Default", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseAlternativeProfile()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new DefaultProfileCustomerMap());
                    c.AddProfile<LegacyProfileCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfileCustomer, LegacyProfile>(
                        "SELECT 2 AS id, 'Legacy' AS legal_name;");

                    Assert.Equal(2, customer.Id);
                    Assert.Equal("Legacy", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseDifferentProfilesWithoutLeaking()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddProfile<LegacyProfileCustomerMap>();
                    c.AddProfile<ReportingProfileCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var legacy = connection.QueryMappedSingle<ProfileCustomer, LegacyProfile>(
                        "SELECT 3 AS id, 'Legacy' AS legal_name;");
                    var reporting = connection.QueryMappedSingle<ProfileCustomer, ReportingProfile>(
                        "SELECT 4 AS report_customer_id, 'Reporting' AS report_customer_name;");

                    Assert.Equal(3, legacy.Id);
                    Assert.Equal("Legacy", legacy.Name);
                    Assert.Equal(4, reporting.Id);
                    Assert.Equal("Reporting", reporting.Name);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedDefaultShouldStillUseDefaultAfterProfileQuery()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new DefaultProfileCustomerMap());
                    c.AddProfile<LegacyProfileCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var profile = connection.QueryMappedSingle<ProfileCustomer, LegacyProfile>(
                        "SELECT 5 AS id, 'Legacy' AS legal_name;");
                    var defaultCustomer = connection.QueryMappedSingle<ProfileCustomer>(
                        "SELECT 6 AS customer_id, 'Default' AS customer_name;");

                    Assert.Equal("Legacy", profile.Name);
                    Assert.Equal(6, defaultCustomer.Id);
                    Assert.Equal("Default", defaultCustomer.Name);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldRunParallelProfileQueriesWithoutLeakingMappings()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddProfile<LegacyProfileCustomerMap>();
                    c.AddProfile<ReportingProfileCustomerMap>();
                });

                var results = Enumerable.Range(0, 100)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            if (index % 2 == 0)
                            {
                                var customer = connection.QueryMappedSingle<ProfileCustomer, LegacyProfile>(
                                    $"SELECT {index} AS id, 'legacy-{index}' AS legal_name;");
                                return customer.Id == index && customer.Name == $"legacy-{index}";
                            }

                            var reporting = connection.QueryMappedSingle<ProfileCustomer, ReportingProfile>(
                                $"SELECT {index} AS report_customer_id, 'report-{index}' AS report_customer_name;");
                            return reporting.Id == index && reporting.Name == $"report-{index}";
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task QueryMappedAsyncShouldRunConcurrentProfileQueriesWithoutLeakingMappings()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddProfile<LegacyProfileCustomerMap>();
                    c.AddProfile<ReportingProfileCustomerMap>();
                });

                var tasks = Enumerable.Range(0, 40)
                    .Select(async index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            if (index % 2 == 0)
                            {
                                var customer = await connection.QueryMappedSingleAsync<ProfileCustomer, LegacyProfile>(
                                    $"SELECT {index} AS id, 'legacy-async-{index}' AS legal_name;");
                                return customer.Id == index && customer.Name == $"legacy-async-{index}";
                            }

                            var reporting = await connection.QueryMappedSingleAsync<ProfileCustomer, ReportingProfile>(
                                $"SELECT {index} AS report_customer_id, 'report-async-{index}' AS report_customer_name;");
                            return reporting.Id == index && reporting.Name == $"report-async-{index}";
                        }
                    })
                    .ToArray();

                var results = await Task.WhenAll(tasks);

                Assert.All(results, Assert.True);
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedProfileShouldSupportNestedMappings()
        {
            PreTest(typeof(ProfileCustomerWithAddress));

            try
            {
                FluentMapper.Initialize(c => c.AddProfile<LegacyProfileCustomerWithAddressMap>());

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfileCustomerWithAddress, LegacyProfile>(
                        "SELECT 'Sao Paulo' AS legacy_city;");

                    Assert.NotNull(customer.Address);
                    Assert.Equal("Sao Paulo", customer.Address.City);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomerWithAddress));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedProfileShouldSupportValueObjects()
        {
            PreTest(typeof(ProfileCustomerWithCpf));

            try
            {
                FluentMapper.Initialize(c => c.AddProfile<LegacyProfileCustomerWithCpfMap>());

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfileCustomerWithCpf, LegacyProfile>(
                        "SELECT '12345678909' AS legacy_cpf;");

                    Assert.Equal("12345678909", customer.Cpf.Number);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomerWithCpf));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedProfileShouldUseProfileBaseMappingForInheritance()
        {
            PreTest(typeof(ProfileBaseCustomer), typeof(ProfileDerivedCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddProfile<LegacyProfileBaseCustomerMap>();
                    c.AddProfile<LegacyProfileDerivedCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfileDerivedCustomer, LegacyProfile>(
                        "SELECT 7 AS legacy_id, 'gold' AS legacy_tier;");

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("gold", customer.Tier);
                }
            }
            finally
            {
                PreTest(typeof(ProfileBaseCustomer), typeof(ProfileDerivedCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedProfileShouldApplyEntityNamingPolicy()
        {
            PreTest(typeof(ProfilePolicyCustomer));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false).ForEntity<ProfilePolicyCustomer>();
                    c.AddProfile<LegacyProfilePolicyCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfilePolicyCustomer, LegacyProfile>(
                        "SELECT 8 AS CUSTOMER_ID, 'policy@example.com' AS legacy_email;");

                    Assert.Equal(8, customer.CustomerId);
                    Assert.Equal("policy@example.com", customer.Email.Value);
                }
            }
            finally
            {
                PreTest(typeof(ProfilePolicyCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedProfileShouldSupportConstructorMapping()
        {
            PreTest(typeof(ProfileImmutableCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddProfile<LegacyProfileImmutableCustomerMap>());

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<ProfileImmutableCustomer, LegacyProfile>(
                        "SELECT 9 AS legacy_id, 'Immutable Legacy' AS legacy_name;");

                    Assert.Equal(9, customer.Id);
                    Assert.Equal("Immutable Legacy", customer.Name);
                }
            }
            finally
            {
                PreTest(typeof(ProfileImmutableCustomer));
            }
        }

        [Fact]
        public void QueryMappedProfileShouldRejectMissingProfile()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<ProfileCustomer, LegacyProfile>(
                            "SELECT 1 AS id, 'Legacy' AS legal_name;"));

                    Assert.Contains("does not have a registered mapping profile", exception.Message);
                    Assert.Contains(typeof(LegacyProfile).FullName, exception.Message);
                }
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        public void AddProfileShouldRejectDuplicateProfileForEntity()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c =>
                    {
                        c.AddProfile<LegacyProfileCustomerMap>();
                        c.AddProfile<SecondLegacyProfileCustomerMap>();
                    }));

                Assert.Contains("already has a configured mapping profile", exception.Message);
                Assert.Contains(typeof(LegacyProfile).FullName, exception.Message);
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
            }
        }

        [Fact]
        public void AddProfileShouldRejectProfileBaseMappingWhenSameProfileBaseIsMissing()
        {
            PreTest(typeof(ProfileBaseCustomer), typeof(ProfileDerivedCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c => c.AddProfile<LegacyProfileDerivedCustomerMap>()));

                Assert.Contains(typeof(LegacyProfile).FullName, exception.Message);
                Assert.Contains(typeof(ProfileBaseCustomer).FullName, exception.Message);
            }
            finally
            {
                PreTest(typeof(ProfileBaseCustomer), typeof(ProfileDerivedCustomer));
            }
        }

        [Fact]
        public void ExplainShouldDescribeProfileMappings()
        {
            PreTest(typeof(ProfileCustomer));

            try
            {
                FluentMapper.Initialize(c => c.AddProfile<LegacyProfileCustomerMap>());

                var explanation = FluentMapper.Explain<ProfileCustomer, LegacyProfile>();
                var name = explanation.Members.Single(m => m.MemberPath == nameof(ProfileCustomer.Name));

                Assert.Equal(typeof(LegacyProfile), explanation.ProfileType);
                Assert.Equal(typeof(LegacyProfileCustomerMap), explanation.EntityMapType);
                Assert.Equal("legal_name", name.ColumnName);
                Assert.Equal(MappingSource.Explicit, name.Source);
            }
            finally
            {
                PreTest(typeof(ProfileCustomer));
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

        private sealed class ReportingProfile : IMappingProfile
        {
        }

        private sealed class ProfileCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class DefaultProfileCustomerMap : EntityMap<ProfileCustomer>
        {
            public DefaultProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
            }
        }

        private sealed class LegacyProfileCustomerMap : EntityMap<ProfileCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("id");
                Map(customer => customer.Name).ToColumn("legal_name");
            }
        }

        private sealed class SecondLegacyProfileCustomerMap : EntityMap<ProfileCustomer>, IProfileMap<LegacyProfile>
        {
            public SecondLegacyProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("other_id");
            }
        }

        private sealed class ReportingProfileCustomerMap : EntityMap<ProfileCustomer>, IProfileMap<ReportingProfile>
        {
            public ReportingProfileCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("report_customer_id");
                Map(customer => customer.Name).ToColumn("report_customer_name");
            }
        }

        private sealed class ProfileCustomerWithAddress
        {
            public ProfileAddress Address { get; set; }
        }

        private sealed class ProfileAddress
        {
            public string City { get; set; }
        }

        private sealed class LegacyProfileCustomerWithAddressMap : EntityMap<ProfileCustomerWithAddress>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileCustomerWithAddressMap()
            {
                Map(customer => customer.Address.City).ToColumn("legacy_city");
            }
        }

        private sealed class ProfileCustomerWithCpf
        {
            public ProfileCustomerWithCpf(ProfileCpf cpf)
            {
                Cpf = cpf;
            }

            public ProfileCpf Cpf { get; }
        }

        private sealed class ProfileCpf
        {
            public ProfileCpf(string number)
            {
                Number = number;
            }

            public string Number { get; }
        }

        private sealed class LegacyProfileCustomerWithCpfMap : EntityMap<ProfileCustomerWithCpf>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileCustomerWithCpfMap()
            {
                Map(customer => customer.Cpf.Number).ToColumn("legacy_cpf");
            }
        }

        private class ProfileBaseCustomer
        {
            public int Id { get; set; }
        }

        private sealed class ProfileDerivedCustomer : ProfileBaseCustomer
        {
            public string Tier { get; set; }
        }

        private sealed class LegacyProfileBaseCustomerMap : EntityMap<ProfileBaseCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileBaseCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
            }
        }

        private sealed class LegacyProfileDerivedCustomerMap : EntityMap<ProfileDerivedCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileDerivedCustomerMap()
            {
                IncludeBase<ProfileBaseCustomer>();
                Map(customer => customer.Tier).ToColumn("legacy_tier");
            }
        }

        private sealed class ProfilePolicyCustomer
        {
            public ProfilePolicyCustomer(int customerId, ProfileEmail email)
            {
                CustomerId = customerId;
                Email = email;
            }

            public int CustomerId { get; }

            public ProfileEmail Email { get; }
        }

        private sealed record ProfileEmail(string Value);

        private sealed class LegacyProfilePolicyCustomerMap : EntityMap<ProfilePolicyCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyProfilePolicyCustomerMap()
            {
                Map(customer => customer.Email.Value).ToColumn("legacy_email");
            }
        }

        private sealed class ProfileImmutableCustomer
        {
            public ProfileImmutableCustomer(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; }

            public string Name { get; }
        }

        private sealed class LegacyProfileImmutableCustomerMap : EntityMap<ProfileImmutableCustomer>, IProfileMap<LegacyProfile>
        {
            public LegacyProfileImmutableCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("legacy_id");
                Map(customer => customer.Name).ToColumn("legacy_name");
            }
        }
    }
}
