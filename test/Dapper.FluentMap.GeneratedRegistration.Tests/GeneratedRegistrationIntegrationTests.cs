using System;
using System.Linq;
using Dapper;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Naming;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapper.FluentMap.GeneratedRegistration.Tests
{
    public sealed class GeneratedRegistrationIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedRegistrationShouldWorkWithDapperAndExistingMappingFeatures()
        {
            ResetMapper();

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddGeneratedMappings();
                    configuration.UseNamingPolicy(NamingPolicy.SnakeCase).ForEntity<GeneratedNamingCustomer>();
                });

                using (var connection = OpenConnection())
                {
                    var customer = connection.QuerySingle<GeneratedCustomer>(
                        "SELECT 7 AS customer_id, 'Ada' AS Name;");
                    var internalCustomer = connection.QuerySingle<GeneratedInternalCustomer>(
                        "SELECT 8 AS internal_id;");
                    var derived = connection.QuerySingle<GeneratedDerivedCustomer>(
                        "SELECT 9 AS base_id, 'Lovelace' AS derived_name;");
                    var immutable = connection.QuerySingle<GeneratedImmutableCustomer>(
                        "SELECT 10 AS immutable_id, 'Grace' AS name;");
                    var queryMappedImmutable = connection.QueryMappedSingle<GeneratedImmutableCustomer>(
                        "SELECT 12 AS immutable_id, 'Generated Constructor' AS name;");
                    var nullable = connection.QueryMappedSingle<GeneratedNullableCustomer>(
                        "SELECT NULL AS age, NULL AS note;");
                    var named = connection.QuerySingle<GeneratedNamingCustomer>(
                        "SELECT '2026-07-26T10:30:00' AS created_at;");
                    var profiled = connection.QueryMappedSingle<GeneratedProfileCustomer, GeneratedLegacyProfile>(
                        "SELECT 11 AS legacy_id, 'Profiled' AS legacy_name;");
                    var nested = connection.QueryMappedSingle<GeneratedNestedCustomer>(
                        "SELECT 13 AS customer_id, 'Sao Paulo' AS city;");
                    var nullableNested = connection.QueryMappedSingle<GeneratedNestedCustomer>(
                        "SELECT 14 AS customer_id, NULL AS city;");
                    var valueObject = connection.QueryMappedSingle<GeneratedValueObjectCustomer>(
                        "SELECT 15 AS customer_id, '12345678909' AS cpf;");
                    var nullableValueObject = connection.QueryMappedSingle<GeneratedValueObjectCustomer>(
                        "SELECT 16 AS customer_id, NULL AS cpf;");
                    var sameTerminal = connection.QueryMappedSingle<GeneratedSameTerminalCustomer>(
                        "SELECT 5 AS rank_level, 9 AS seniority_level;");
                    var profiledNested = connection.QueryMappedSingle<GeneratedProfileNestedCustomer, GeneratedLegacyProfile>(
                        "SELECT 'Profile City' AS legacy_city;");
                    var ignored = connection.QueryMappedSingle<GeneratedIgnoredCustomer>(
                        "SELECT 17 AS customer_id, 'do-not-map' AS secret;");
                    var readSemantics = connection.QueryMappedSingle<GeneratedReadSemanticsCustomer>(
                        "SELECT 18 AS customer_id, 'Normal' AS normal_name, 'Read' AS read_only_name, 123 AS computed_total, '2026-07-28' AS created_at, 'Insert kept for read' AS insert_excluded, 'Update kept for read' AS update_excluded, 'do-not-map' AS secret;");

                    Assert.Equal(7, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                    Assert.Equal(8, internalCustomer.Id);
                    Assert.Equal(9, derived.Id);
                    Assert.Equal("Lovelace", derived.Name);
                    Assert.Equal(10, immutable.Id);
                    Assert.Equal("Grace", immutable.Name);
                    Assert.Equal(12, queryMappedImmutable.Id);
                    Assert.Equal("Generated Constructor", queryMappedImmutable.Name);
                    Assert.Null(nullable.Age);
                    Assert.Null(nullable.Note);
                    Assert.Equal(new DateTime(2026, 7, 26, 10, 30, 0), named.CreatedAt);
                    Assert.Equal(11, profiled.Id);
                    Assert.Equal("Profiled", profiled.Name);
                    Assert.Equal(13, nested.Id);
                    Assert.NotNull(nested.Address);
                    Assert.Equal("Sao Paulo", nested.Address.City);
                    Assert.Equal(14, nullableNested.Id);
                    Assert.Null(nullableNested.Address);
                    Assert.Equal(15, valueObject.Id);
                    Assert.Equal("12345678909", valueObject.Cpf.Number);
                    Assert.Equal(16, nullableValueObject.Id);
                    Assert.Null(nullableValueObject.Cpf);
                    Assert.Equal(5, sameTerminal.Rank.Level);
                    Assert.Equal(9, sameTerminal.Seniority.Level);
                    Assert.Equal("Profile City", profiledNested.Address.City);
                    Assert.Equal(17, ignored.Id);
                    Assert.Equal("initial", ignored.Secret);
                    Assert.Equal(18, readSemantics.Id);
                    Assert.Equal("Normal", readSemantics.NormalName);
                    Assert.Equal("Read", readSemantics.ReadOnlyName);
                    Assert.Equal(123, readSemantics.ComputedTotal);
                    Assert.Equal("2026-07-28", readSemantics.CreatedAt);
                    Assert.Equal("Insert kept for read", readSemantics.InsertExcluded);
                    Assert.Equal("Update kept for read", readSemantics.UpdateExcluded);
                    Assert.Equal("initial", readSemantics.Secret);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);

                    var fallback = connection.QueryMappedSingle<GeneratedCustomer>(
                        "SELECT 'Fallback' AS Name;");

                    Assert.Equal(0, fallback.Id);
                    Assert.Equal("Fallback", fallback.Name);
                    Assert.Equal(1, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                ResetMapper();
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedRegistrationShouldWorkThroughDependencyInjection()
        {
            using (var provider = new ServiceCollection()
                .AddFluentMap(builder => builder.Configure(configuration => configuration.AddGeneratedMappings()))
                .BuildServiceProvider())
            using (var connection = OpenConnection())
            {
                var configuration = provider.GetRequiredService<ImmutableFluentMapConfiguration>();
                var runtime = provider.GetRequiredService<FluentMapRuntime>();

                var customer = runtime.QueryMappedSingle<GeneratedCustomer>(
                    connection,
                    "SELECT 41 AS customer_id, 'DI' AS Name;");

                Assert.Same(configuration, runtime.Configuration);
                Assert.Contains(configuration.GeneratedMaterializers, materializer => materializer.EntityType == typeof(GeneratedCustomer));
                Assert.Equal(41, customer.Id);
                Assert.Equal("DI", customer.Name);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedQueryMappedShouldMatchRuntimeFallbackForEquivalentComplexShapes()
        {
            ResetMapper();

            try
            {
                GeneratedImmutableCustomer generatedImmutable;
                GeneratedNestedCustomer generatedNested;
                GeneratedValueObjectCustomer generatedValueObject;
                GeneratedSameTerminalCustomer generatedSameTerminal;
                GeneratedProfileNestedCustomer generatedProfileNested;
                GeneratedReadSemanticsCustomer generatedReadSemantics;

                FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

                using (var connection = OpenConnection())
                {
                    generatedImmutable = connection.QueryMappedSingle<GeneratedImmutableCustomer>(
                        "SELECT 21 AS immutable_id, 'Generated Constructor' AS name;");
                    generatedNested = connection.QueryMappedSingle<GeneratedNestedCustomer>(
                        "SELECT 22 AS customer_id, 'Sao Paulo' AS city;");
                    generatedValueObject = connection.QueryMappedSingle<GeneratedValueObjectCustomer>(
                        "SELECT 23 AS customer_id, '12345678909' AS cpf;");
                    generatedSameTerminal = connection.QueryMappedSingle<GeneratedSameTerminalCustomer>(
                        "SELECT 3 AS rank_level, 8 AS seniority_level;");
                    generatedProfileNested = connection.QueryMappedSingle<GeneratedProfileNestedCustomer, GeneratedLegacyProfile>(
                        "SELECT 'Profile City' AS legacy_city;");
                    generatedReadSemantics = connection.QueryMappedSingle<GeneratedReadSemanticsCustomer>(
                        "SELECT 24 AS customer_id, 'Normal' AS normal_name, 'Read' AS read_only_name, 987 AS computed_total, '2026-07-28' AS created_at, 'Insert kept for read' AS insert_excluded, 'Update kept for read' AS update_excluded, 'do-not-map' AS secret;");

                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }

                ResetMapper();

                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap<GeneratedImmutableCustomerMap>();
                    configuration.AddMap<GeneratedNestedCustomerMap>();
                    configuration.AddMap<GeneratedValueObjectCustomerMap>();
                    configuration.AddMap<GeneratedSameTerminalCustomerMap>();
                    configuration.AddMap<GeneratedReadSemanticsCustomerMap>();
                    configuration.AddProfile<GeneratedLegacyProfileNestedCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var runtimeImmutable = connection.QueryMappedSingle<GeneratedImmutableCustomer>(
                        "SELECT 21 AS immutable_id, 'Generated Constructor' AS name;");
                    var runtimeNested = connection.QueryMappedSingle<GeneratedNestedCustomer>(
                        "SELECT 22 AS customer_id, 'Sao Paulo' AS city;");
                    var runtimeValueObject = connection.QueryMappedSingle<GeneratedValueObjectCustomer>(
                        "SELECT 23 AS customer_id, '12345678909' AS cpf;");
                    var runtimeSameTerminal = connection.QueryMappedSingle<GeneratedSameTerminalCustomer>(
                        "SELECT 3 AS rank_level, 8 AS seniority_level;");
                    var runtimeProfileNested = connection.QueryMappedSingle<GeneratedProfileNestedCustomer, GeneratedLegacyProfile>(
                        "SELECT 'Profile City' AS legacy_city;");
                    var runtimeReadSemantics = connection.QueryMappedSingle<GeneratedReadSemanticsCustomer>(
                        "SELECT 24 AS customer_id, 'Normal' AS normal_name, 'Read' AS read_only_name, 987 AS computed_total, '2026-07-28' AS created_at, 'Insert kept for read' AS insert_excluded, 'Update kept for read' AS update_excluded, 'do-not-map' AS secret;");

                    Assert.Equal(generatedImmutable.Id, runtimeImmutable.Id);
                    Assert.Equal(generatedImmutable.Name, runtimeImmutable.Name);
                    Assert.Equal(generatedNested.Id, runtimeNested.Id);
                    Assert.Equal(generatedNested.Address.City, runtimeNested.Address.City);
                    Assert.Equal(generatedValueObject.Id, runtimeValueObject.Id);
                    Assert.Equal(generatedValueObject.Cpf.Number, runtimeValueObject.Cpf.Number);
                    Assert.Equal(generatedSameTerminal.Rank.Level, runtimeSameTerminal.Rank.Level);
                    Assert.Equal(generatedSameTerminal.Seniority.Level, runtimeSameTerminal.Seniority.Level);
                    Assert.Equal(generatedProfileNested.Address.City, runtimeProfileNested.Address.City);
                    Assert.Equal(generatedReadSemantics.Id, runtimeReadSemantics.Id);
                    Assert.Equal(generatedReadSemantics.NormalName, runtimeReadSemantics.NormalName);
                    Assert.Equal(generatedReadSemantics.ReadOnlyName, runtimeReadSemantics.ReadOnlyName);
                    Assert.Equal(generatedReadSemantics.ComputedTotal, runtimeReadSemantics.ComputedTotal);
                    Assert.Equal(generatedReadSemantics.CreatedAt, runtimeReadSemantics.CreatedAt);
                    Assert.Equal(generatedReadSemantics.InsertExcluded, runtimeReadSemantics.InsertExcluded);
                    Assert.Equal(generatedReadSemantics.UpdateExcluded, runtimeReadSemantics.UpdateExcluded);
                    Assert.Equal(generatedReadSemantics.Secret, runtimeReadSemantics.Secret);
                    Assert.Equal(6, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                ResetMapper();
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedQueryMappedShouldMatchRuntimeFallbackForReadConverters()
        {
            ResetMapper();

            try
            {
                GeneratedConvertedCustomer generatedScalar;
                GeneratedConvertedCustomer generatedNull;
                GeneratedConvertedNestedCustomer generatedNested;
                GeneratedConvertedImmutableCustomer generatedImmutable;
                GeneratedConvertedValueObjectCustomer generatedValueObject;
                GeneratedConvertedProfileCustomer generatedProfileDefault;
                GeneratedConvertedProfileCustomer generatedProfileLegacy;

                FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

                using (var connection = OpenConnection())
                {
                    generatedScalar = connection.QueryMappedSingle<GeneratedConvertedCustomer>(
                        "SELECT 31 AS customer_id, 'A' AS status, '42' AS optional_score;");
                    generatedNull = connection.QueryMappedSingle<GeneratedConvertedCustomer>(
                        "SELECT 32 AS customer_id, NULL AS status, NULL AS optional_score;");
                    generatedNested = connection.QueryMappedSingle<GeneratedConvertedNestedCustomer>(
                        "SELECT '00123' AS billing_zip, '00456' AS shipping_zip;");
                    generatedImmutable = connection.QueryMappedSingle<GeneratedConvertedImmutableCustomer>(
                        "SELECT 'I' AS status;");
                    generatedValueObject = connection.QueryMappedSingle<GeneratedConvertedValueObjectCustomer>(
                        "SELECT '12345678909' AS cpf;");
                    generatedProfileDefault = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer>(
                        "SELECT 'A' AS status;");
                    generatedProfileLegacy = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer, GeneratedLegacyProfile>(
                        "SELECT '1' AS legacy_status;");

                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }

                ResetMapper();

                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap<GeneratedConvertedCustomerMap>();
                    configuration.AddMap<GeneratedConvertedNestedCustomerMap>();
                    configuration.AddMap<GeneratedConvertedImmutableCustomerMap>();
                    configuration.AddMap<GeneratedConvertedValueObjectCustomerMap>();
                    configuration.AddMap<GeneratedConvertedProfileCustomerMap>();
                    configuration.AddProfile<GeneratedConvertedLegacyProfileCustomerMap>();
                });

                using (var connection = OpenConnection())
                {
                    var runtimeScalar = connection.QueryMappedSingle<GeneratedConvertedCustomer>(
                        "SELECT 31 AS customer_id, 'A' AS status, '42' AS optional_score;");
                    var runtimeNull = connection.QueryMappedSingle<GeneratedConvertedCustomer>(
                        "SELECT 32 AS customer_id, NULL AS status, NULL AS optional_score;");
                    var runtimeNested = connection.QueryMappedSingle<GeneratedConvertedNestedCustomer>(
                        "SELECT '00123' AS billing_zip, '00456' AS shipping_zip;");
                    var runtimeImmutable = connection.QueryMappedSingle<GeneratedConvertedImmutableCustomer>(
                        "SELECT 'I' AS status;");
                    var runtimeValueObject = connection.QueryMappedSingle<GeneratedConvertedValueObjectCustomer>(
                        "SELECT '12345678909' AS cpf;");
                    var runtimeProfileDefault = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer>(
                        "SELECT 'A' AS status;");
                    var runtimeProfileLegacy = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer, GeneratedLegacyProfile>(
                        "SELECT '1' AS legacy_status;");

                    Assert.Equal(runtimeScalar.Id, generatedScalar.Id);
                    Assert.Equal(runtimeScalar.Status, generatedScalar.Status);
                    Assert.Equal(runtimeScalar.OptionalScore, generatedScalar.OptionalScore);
                    Assert.Equal(runtimeNull.Id, generatedNull.Id);
                    Assert.Equal(runtimeNull.Status, generatedNull.Status);
                    Assert.Equal(runtimeNull.OptionalScore, generatedNull.OptionalScore);
                    Assert.Equal(runtimeNested.BillingAddress.ZipCode, generatedNested.BillingAddress.ZipCode);
                    Assert.Equal(runtimeNested.ShippingAddress.ZipCode, generatedNested.ShippingAddress.ZipCode);
                    Assert.Equal(runtimeImmutable.Status, generatedImmutable.Status);
                    Assert.Equal(runtimeValueObject.Cpf.Number, generatedValueObject.Cpf.Number);
                    Assert.Equal(runtimeProfileDefault.Status, generatedProfileDefault.Status);
                    Assert.Equal(runtimeProfileLegacy.Status, generatedProfileLegacy.Status);
                }
            }
            finally
            {
                ResetMapper();
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedQueryMappedShouldWrapReadConverterExceptions()
        {
            ResetMapper();

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

                using (var connection = OpenConnection())
                {
                    var exception = Assert.Throws<FluentMapConfigurationException>(
                        () => connection.QueryMappedSingle<GeneratedThrowingConvertedCustomer>(
                            "SELECT 'bad' AS status;"));

                    Assert.IsType<InvalidOperationException>(exception.InnerException);
                    Assert.Contains(typeof(GeneratedThrowingConvertedCustomer).FullName, exception.Message);
                    Assert.Contains(nameof(GeneratedThrowingConvertedCustomer.Status), exception.Message);
                    Assert.Contains("status", exception.Message);
                    Assert.Contains(typeof(GeneratedThrowingStatusConverter).FullName, exception.Message);
                    Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
                }
            }
            finally
            {
                ResetMapper();
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GeneratedQueryMappedShouldApplyReadConvertersAcrossConcurrentDefaultAndProfileQueries()
        {
            ResetMapper();

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddGeneratedMappings());

                var results = System.Linq.Enumerable.Range(0, 32)
                    .AsParallel()
                    .Select(index =>
                    {
                        using (var connection = OpenConnection())
                        {
                            if (index % 2 == 0)
                            {
                                var current = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer>(
                                    "SELECT 'A' AS status;");
                                return current.Status == GeneratedAccountStatus.Active;
                            }

                            var legacy = connection.QueryMappedSingle<GeneratedConvertedProfileCustomer, GeneratedLegacyProfile>(
                                "SELECT '1' AS legacy_status;");
                            return legacy.Status == GeneratedAccountStatus.Inactive;
                        }
                    })
                    .ToList();

                Assert.All(results, Assert.True);
                Assert.Equal(0, FluentMapper.Registry.MaterializationPlanCacheEntryCount);
            }
            finally
            {
                ResetMapper();
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void ResetMapper()
        {
            FluentMapper.Reset(
                typeof(GeneratedCustomer),
                typeof(GeneratedInternalCustomer),
                typeof(GeneratedBaseCustomer),
                typeof(GeneratedDerivedCustomer),
                typeof(GeneratedImmutableCustomer),
                typeof(GeneratedNullableCustomer),
                typeof(GeneratedNamingCustomer),
                typeof(GeneratedProfileCustomer),
                typeof(GeneratedNestedCustomer),
                typeof(GeneratedValueObjectCustomer),
                typeof(GeneratedSameTerminalCustomer),
                typeof(GeneratedProfileNestedCustomer),
                typeof(GeneratedIgnoredCustomer),
                typeof(GeneratedReadSemanticsCustomer),
                typeof(GeneratedConvertedCustomer),
                typeof(GeneratedConvertedNestedCustomer),
                typeof(GeneratedConvertedImmutableCustomer),
                typeof(GeneratedConvertedValueObjectCustomer),
                typeof(GeneratedConvertedProfileCustomer),
                typeof(GeneratedThrowingConvertedCustomer));
        }
    }

    public sealed class GeneratedCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public sealed class GeneratedCustomerMap : EntityMap<GeneratedCustomer>
    {
        public GeneratedCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
        }
    }

    internal sealed class GeneratedInternalCustomer
    {
        public int Id { get; set; }
    }

    internal sealed class GeneratedInternalCustomerMap : EntityMap<GeneratedInternalCustomer>
    {
        public GeneratedInternalCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("internal_id");
        }
    }

    public class GeneratedBaseCustomer
    {
        public int Id { get; set; }
    }

    public sealed class GeneratedDerivedCustomer : GeneratedBaseCustomer
    {
        public string Name { get; set; }
    }

    public sealed class GeneratedBaseCustomerMap : EntityMap<GeneratedBaseCustomer>
    {
        public GeneratedBaseCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("base_id");
        }
    }

    public sealed class GeneratedDerivedCustomerMap : EntityMap<GeneratedDerivedCustomer>
    {
        public GeneratedDerivedCustomerMap()
        {
            IncludeBase<GeneratedBaseCustomer>();
            Map(customer => customer.Name).ToColumn("derived_name");
        }
    }

    public sealed class GeneratedImmutableCustomer
    {
        public GeneratedImmutableCustomer(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }

    public sealed class GeneratedImmutableCustomerMap : EntityMap<GeneratedImmutableCustomer>
    {
        public GeneratedImmutableCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("immutable_id");
            Map(customer => customer.Name).ToColumn("name");
        }
    }

    public sealed class GeneratedNullableCustomer
    {
        public int? Age { get; set; }

        public string Note { get; set; }
    }

    public sealed class GeneratedNullableCustomerMap : EntityMap<GeneratedNullableCustomer>
    {
        public GeneratedNullableCustomerMap()
        {
            Map(customer => customer.Age).ToColumn("age");
            Map(customer => customer.Note).ToColumn("note");
        }
    }

    public sealed class GeneratedNamingCustomer
    {
        public DateTime CreatedAt { get; set; }
    }

    public sealed class GeneratedLegacyProfile : IMappingProfile
    {
    }

    public sealed class GeneratedProfileCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public sealed class GeneratedLegacyProfileCustomerMap : EntityMap<GeneratedProfileCustomer>, IProfileMap<GeneratedLegacyProfile>
    {
        public GeneratedLegacyProfileCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("legacy_id");
            Map(customer => customer.Name).ToColumn("legacy_name");
        }
    }

    public sealed class GeneratedNestedCustomer
    {
        public int Id { get; set; }

        public GeneratedAddress Address { get; set; }
    }

    public sealed class GeneratedAddress
    {
        public string City { get; set; }
    }

    public sealed class GeneratedNestedCustomerMap : EntityMap<GeneratedNestedCustomer>
    {
        public GeneratedNestedCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
            Map(customer => customer.Address.City).ToColumn("city");
        }
    }

    public sealed class GeneratedValueObjectCustomer
    {
        public GeneratedValueObjectCustomer(int id, GeneratedCpf cpf)
        {
            Id = id;
            Cpf = cpf;
        }

        public int Id { get; }

        public GeneratedCpf Cpf { get; }
    }

    public sealed class GeneratedCpf
    {
        public GeneratedCpf(string number)
        {
            Number = number;
        }

        public string Number { get; }
    }

    public sealed class GeneratedValueObjectCustomerMap : EntityMap<GeneratedValueObjectCustomer>
    {
        public GeneratedValueObjectCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
            Map(customer => customer.Cpf.Number).ToColumn("cpf");
        }
    }

    public sealed class GeneratedSameTerminalCustomer
    {
        public GeneratedSameTerminalCustomer(GeneratedRank rank, GeneratedSeniority seniority)
        {
            Rank = rank;
            Seniority = seniority;
        }

        public GeneratedRank Rank { get; }

        public GeneratedSeniority Seniority { get; }
    }

    public sealed class GeneratedRank
    {
        public GeneratedRank(int level)
        {
            Level = level;
        }

        public int Level { get; }
    }

    public sealed class GeneratedSeniority
    {
        public GeneratedSeniority(int level)
        {
            Level = level;
        }

        public int Level { get; }
    }

    public sealed class GeneratedSameTerminalCustomerMap : EntityMap<GeneratedSameTerminalCustomer>
    {
        public GeneratedSameTerminalCustomerMap()
        {
            Map(customer => customer.Rank.Level).ToColumn("rank_level");
            Map(customer => customer.Seniority.Level).ToColumn("seniority_level");
        }
    }

    public sealed class GeneratedProfileNestedCustomer
    {
        public GeneratedAddress Address { get; set; }
    }

    public sealed class GeneratedLegacyProfileNestedCustomerMap :
        EntityMap<GeneratedProfileNestedCustomer>,
        IProfileMap<GeneratedLegacyProfile>
    {
        public GeneratedLegacyProfileNestedCustomerMap()
        {
            Map(customer => customer.Address.City).ToColumn("legacy_city");
        }
    }

    public sealed class GeneratedIgnoredCustomer
    {
        public int Id { get; set; }

        public string Secret { get; set; } = "initial";
    }

    public sealed class GeneratedIgnoredCustomerMap : EntityMap<GeneratedIgnoredCustomer>
    {
        public GeneratedIgnoredCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
            Map(customer => customer.Secret).ToColumn("secret").Ignore();
        }
    }

    public sealed class GeneratedReadSemanticsCustomer
    {
        public int Id { get; set; }

        public string NormalName { get; set; }

        public string ReadOnlyName { get; set; }

        public int ComputedTotal { get; set; }

        public string CreatedAt { get; set; }

        public string InsertExcluded { get; set; }

        public string UpdateExcluded { get; set; }

        public string Secret { get; set; } = "initial";
    }

    public sealed class GeneratedReadSemanticsCustomerMap : EntityMap<GeneratedReadSemanticsCustomer>
    {
        public GeneratedReadSemanticsCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
            Map(customer => customer.NormalName).ToColumn("normal_name");
            Map(customer => customer.ReadOnlyName).ToColumn("read_only_name").ReadOnly();
            Map(customer => customer.ComputedTotal).ToColumn("computed_total").Computed();
            Map(customer => customer.CreatedAt).ToColumn("created_at").DatabaseDefaultOnInsert();
            Map(customer => customer.InsertExcluded).ToColumn("insert_excluded").ExcludeFromInsert();
            Map(customer => customer.UpdateExcluded).ToColumn("update_excluded").ExcludeFromUpdate();
            Map(customer => customer.Secret).ToColumn("secret").Ignore();
        }
    }

    public enum GeneratedAccountStatus
    {
        Unknown,
        Active,
        Inactive
    }

    public sealed class GeneratedConvertedCustomer
    {
        public int Id { get; set; }

        public GeneratedAccountStatus Status { get; set; }

        public int? OptionalScore { get; set; }
    }

    public sealed class GeneratedConvertedCustomerMap : EntityMap<GeneratedConvertedCustomer>
    {
        public GeneratedConvertedCustomerMap()
        {
            Map(customer => customer.Id).ToColumn("customer_id");
            Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<GeneratedStatusConverter, string>();
            Map(customer => customer.OptionalScore).ToColumn("optional_score").ConvertFromDatabaseUsing<GeneratedScoreConverter, string>();
        }
    }

    public sealed class GeneratedStatusConverter : IReadPropertyConverter<string, GeneratedAccountStatus>
    {
        public GeneratedAccountStatus ConvertFromDatabase(string value)
        {
            return value == "A" ? GeneratedAccountStatus.Active : GeneratedAccountStatus.Unknown;
        }
    }

    public sealed class GeneratedScoreConverter : IReadPropertyConverter<string, int>
    {
        public int ConvertFromDatabase(string value)
        {
            return int.Parse(value);
        }
    }

    public sealed class GeneratedConvertedNestedCustomer
    {
        public GeneratedConvertedAddress BillingAddress { get; set; }

        public GeneratedConvertedAddress ShippingAddress { get; set; }
    }

    public sealed class GeneratedConvertedAddress
    {
        public string ZipCode { get; set; }
    }

    public sealed class GeneratedConvertedNestedCustomerMap : EntityMap<GeneratedConvertedNestedCustomer>
    {
        public GeneratedConvertedNestedCustomerMap()
        {
            Map(customer => customer.BillingAddress.ZipCode)
                .ToColumn("billing_zip")
                .ConvertFromDatabaseUsing<GeneratedZipCodeConverter, string>();
            Map(customer => customer.ShippingAddress.ZipCode).ToColumn("shipping_zip");
        }
    }

    public sealed class GeneratedZipCodeConverter : IReadPropertyConverter<string, string>
    {
        public string ConvertFromDatabase(string value)
        {
            return "ZIP-" + value;
        }
    }

    public sealed class GeneratedConvertedImmutableCustomer
    {
        public GeneratedConvertedImmutableCustomer(GeneratedAccountStatus status)
        {
            Status = status;
        }

        public GeneratedAccountStatus Status { get; }
    }

    public sealed class GeneratedConvertedImmutableCustomerMap : EntityMap<GeneratedConvertedImmutableCustomer>
    {
        public GeneratedConvertedImmutableCustomerMap()
        {
            Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<GeneratedLegacyStatusConverter, string>();
        }
    }

    public sealed class GeneratedLegacyStatusConverter : IReadPropertyConverter<string, GeneratedAccountStatus>
    {
        public GeneratedAccountStatus ConvertFromDatabase(string value)
        {
            return value == "1" || value == "I"
                ? GeneratedAccountStatus.Inactive
                : GeneratedAccountStatus.Active;
        }
    }

    public sealed class GeneratedConvertedValueObjectCustomer
    {
        public GeneratedConvertedCpf Cpf { get; set; }
    }

    public sealed class GeneratedConvertedCpf
    {
        public GeneratedConvertedCpf(string number)
        {
            Number = number;
        }

        public string Number { get; }
    }

    public sealed class GeneratedConvertedValueObjectCustomerMap : EntityMap<GeneratedConvertedValueObjectCustomer>
    {
        public GeneratedConvertedValueObjectCustomerMap()
        {
            Map(customer => customer.Cpf).ToColumn("cpf").ConvertFromDatabaseUsing<GeneratedCpfConverter, string>();
        }
    }

    public sealed class GeneratedCpfConverter : IReadPropertyConverter<string, GeneratedConvertedCpf>
    {
        public GeneratedConvertedCpf ConvertFromDatabase(string value)
        {
            return new GeneratedConvertedCpf("converted:" + value);
        }
    }

    public sealed class GeneratedConvertedProfileCustomer
    {
        public GeneratedAccountStatus Status { get; set; }
    }

    public sealed class GeneratedConvertedProfileCustomerMap : EntityMap<GeneratedConvertedProfileCustomer>
    {
        public GeneratedConvertedProfileCustomerMap()
        {
            Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<GeneratedStatusConverter, string>();
        }
    }

    public sealed class GeneratedConvertedLegacyProfileCustomerMap :
        EntityMap<GeneratedConvertedProfileCustomer>,
        IProfileMap<GeneratedLegacyProfile>
    {
        public GeneratedConvertedLegacyProfileCustomerMap()
        {
            Map(customer => customer.Status).ToColumn("legacy_status").ConvertFromDatabaseUsing<GeneratedLegacyStatusConverter, string>();
        }
    }

    public sealed class GeneratedThrowingConvertedCustomer
    {
        public GeneratedAccountStatus Status { get; set; }
    }

    public sealed class GeneratedThrowingConvertedCustomerMap : EntityMap<GeneratedThrowingConvertedCustomer>
    {
        public GeneratedThrowingConvertedCustomerMap()
        {
            Map(customer => customer.Status).ToColumn("status").ConvertFromDatabaseUsing<GeneratedThrowingStatusConverter, string>();
        }
    }

    public sealed class GeneratedThrowingStatusConverter : IReadPropertyConverter<string, GeneratedAccountStatus>
    {
        public GeneratedAccountStatus ConvertFromDatabase(string value)
        {
            throw new InvalidOperationException("Invalid status.");
        }
    }
}
