using System;
using System.Data;
using System.Linq;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class PropertyConversionMetadataTests
    {
        [Fact]
        public void DefaultPropertyMapShouldNotHaveConverters()
        {
            var map = new DefaultConversionMap();

            var conversion = ConversionOf(map);

            Assert.False(conversion.HasReadConverter);
            Assert.False(conversion.HasWriteConverter);
            Assert.Null(conversion.ReadConverter);
            Assert.Null(conversion.WriteConverter);
        }

        [Fact]
        public void ReadOnlyConverterShouldExposeReadMetadata()
        {
            var map = new ReadOnlyConversionMap();

            var conversion = ConversionOf(map);

            Assert.True(conversion.HasReadConverter);
            Assert.False(conversion.HasWriteConverter);
            Assert.Equal(typeof(StatusReadConverter), conversion.ReadConverter.ConverterType);
            Assert.Equal(typeof(string), conversion.ReadConverter.DatabaseType);
            Assert.Equal(typeof(AccountStatus), conversion.ReadConverter.PropertyType);
            Assert.Equal(PropertyConversionDirection.Read, conversion.ReadConverter.Direction);
        }

        [Fact]
        public void WriteOnlyConverterShouldExposeWriteMetadata()
        {
            var map = new WriteOnlyConversionMap();

            var conversion = ConversionOf(map);

            Assert.False(conversion.HasReadConverter);
            Assert.True(conversion.HasWriteConverter);
            Assert.Equal(typeof(StatusWriteConverter), conversion.WriteConverter.ConverterType);
            Assert.Equal(typeof(string), conversion.WriteConverter.DatabaseType);
            Assert.Equal(typeof(AccountStatus), conversion.WriteConverter.PropertyType);
            Assert.Equal(PropertyConversionDirection.Write, conversion.WriteConverter.Direction);
        }

        [Fact]
        public void BidirectionalConverterShouldExposeBothDirections()
        {
            var map = new BidirectionalConversionMap();

            var conversion = ConversionOf(map);

            Assert.True(conversion.HasReadConverter);
            Assert.True(conversion.HasWriteConverter);
            Assert.Equal(typeof(StatusTextConverter), conversion.ReadConverter.ConverterType);
            Assert.Equal(typeof(StatusTextConverter), conversion.WriteConverter.ConverterType);
            Assert.Equal(typeof(string), conversion.ReadConverter.DatabaseType);
            Assert.Equal(typeof(string), conversion.WriteConverter.DatabaseType);
        }

        [Fact]
        public void DelegateConvertersShouldExposeDirectionalMetadata()
        {
            var map = new DelegateConversionMap();

            var conversion = ConversionOf(map);

            Assert.True(conversion.HasReadConverter);
            Assert.True(conversion.HasWriteConverter);
            Assert.Equal(typeof(ReadPropertyConverter<string, AccountStatus>), conversion.ReadConverter.ConverterType);
            Assert.Equal(typeof(WritePropertyConverter<AccountStatus, string>), conversion.WriteConverter.ConverterType);
        }

        [Fact]
        public void ConverterTypeShouldBeInstantiatedOncePerPropertyMap()
        {
            CountingReadConverter.Created = 0;

            var map = new CountingConversionMap();

            Assert.True(ConversionOf(map).HasReadConverter);
            Assert.Equal(1, CountingReadConverter.Created);
        }

        [Fact]
        public void NullablePropertyShouldAcceptNonNullableConverterResult()
        {
            var map = new NullableConversionMap();

            var conversion = ConversionOf(map);

            Assert.True(conversion.HasReadConverter);
            Assert.True(conversion.HasWriteConverter);
            Assert.Equal(typeof(AccountStatus), conversion.ReadConverter.PropertyType);
            Assert.Equal(typeof(AccountStatus), conversion.WriteConverter.PropertyType);
        }

        [Fact]
        public void InheritedMappingsShouldPreserveConversionMetadata()
        {
            PreTest(typeof(ConversionBaseEntity), typeof(ConversionDerivedEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ConversionBaseMap());
                    c.AddMap(new ConversionDerivedMap());
                });

                var explanation = FluentMapper.Explain<ConversionDerivedEntity>();
                var status = explanation.Members.Single(m => m.MemberPath == nameof(ConversionBaseEntity.Status));

                Assert.Equal(typeof(ConversionBaseEntity), status.InheritedFrom);
                Assert.True(status.Conversion.HasReadConverter);
                Assert.Equal(typeof(StatusReadConverter), status.Conversion.ReadConverter.ConverterType);
            }
            finally
            {
                PreTest(typeof(ConversionBaseEntity), typeof(ConversionDerivedEntity));
            }
        }

        [Fact]
        public void DerivedExplicitConverterShouldOverrideInheritedConverter()
        {
            PreTest(typeof(ConversionBaseEntity), typeof(ConversionDerivedEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ConversionBaseMap());
                    c.AddMap(new ConversionDerivedOverrideMap());
                });

                var explanation = FluentMapper.Explain<ConversionDerivedEntity>();
                var status = explanation.Members.Single(m => m.MemberPath == nameof(ConversionBaseEntity.Status));

                Assert.Null(status.InheritedFrom);
                Assert.Equal(typeof(AlternateStatusReadConverter), status.Conversion.ReadConverter.ConverterType);
            }
            finally
            {
                PreTest(typeof(ConversionBaseEntity), typeof(ConversionDerivedEntity));
            }
        }

        [Fact]
        public void ProfileConverterShouldBeScopedToProfileMapping()
        {
            PreTest(typeof(ProfileConversionEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new DefaultProfileConversionMap());
                    c.AddProfile<LegacyProfileConversionMap>();
                });

                var defaultStatus = FluentMapper.Explain<ProfileConversionEntity>()
                    .Members.Single(m => m.MemberPath == nameof(ProfileConversionEntity.Status));
                var profileStatus = FluentMapper.Explain<ProfileConversionEntity, LegacyConversionProfile>()
                    .Members.Single(m => m.MemberPath == nameof(ProfileConversionEntity.Status));

                Assert.False(defaultStatus.Conversion.HasReadConverter);
                Assert.True(profileStatus.Conversion.HasReadConverter);
                Assert.Equal(typeof(StatusReadConverter), profileStatus.Conversion.ReadConverter.ConverterType);
            }
            finally
            {
                PreTest(typeof(ProfileConversionEntity));
            }
        }

        [Fact]
        public void IncompatibleConverterShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => new IncompatibleConverterMap());

            Assert.Contains("not compatible with read conversion", exception.Message);
        }

        [Fact]
        public void SourceTypeMismatchShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => new SourceMismatchConversionMap());

            Assert.Contains(typeof(int).FullName, exception.Message);
            Assert.Contains("read conversion", exception.Message);
        }

        [Fact]
        public void DestinationTypeMismatchShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => new DestinationMismatchConversionMap());

            Assert.Contains("cannot be assigned", exception.Message);
            Assert.Contains(typeof(int).FullName, exception.Message);
        }

        [Fact]
        public void DuplicateReadConverterShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => new DuplicateReadConversionMap());

            Assert.Contains("read converter is already configured", exception.Message);
        }

        [Fact]
        public void DuplicateWriteConverterShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => new DuplicateWriteConversionMap());

            Assert.Contains("write converter is already configured", exception.Message);
        }

        [Fact]
        public void DuplicateProfileRegistrationWithConvertersShouldThrow()
        {
            PreTest(typeof(ProfileConversionEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c =>
                    {
                        c.AddProfile<LegacyProfileConversionMap>();
                        c.AddProfile<SecondLegacyProfileConversionMap>();
                    }));

                Assert.Contains("already has a configured mapping profile", exception.Message);
                Assert.Contains(typeof(LegacyConversionProfile).FullName, exception.Message);
            }
            finally
            {
                PreTest(typeof(ProfileConversionEntity));
            }
        }

        [Fact]
        public void GeneratedMaterializerShouldNotMatchReadConverterMetadata()
        {
            PreTest(typeof(ConversionEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ReadOnlyConversionMap());
                    c.AddGeneratedMaterializer(
                        new[] { GeneratedMaterializerColumn.Map("status", nameof(ConversionEntity.Status)) },
                        ReadGeneratedConversionEntity);
                });

                var found = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(ConversionEntity),
                    profileType: null,
                    columnNames: new[] { "status" },
                    out var materializer);

                Assert.False(found);
                Assert.Null(materializer);
            }
            finally
            {
                PreTest(typeof(ConversionEntity));
            }
        }

        [Fact]
        public void GeneratedMaterializerShouldMatchDeclaredReadConverterMetadata()
        {
            PreTest(typeof(ConversionEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ReadOnlyConversionMap());
                    c.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map(
                                "status",
                                nameof(ConversionEntity.Status),
                                typeof(StatusReadConverter),
                                typeof(string),
                                typeof(AccountStatus))
                        },
                        ReadGeneratedConversionEntity);
                });

                var found = FluentMapper.Registry.TryGetGeneratedMaterializer(
                    typeof(ConversionEntity),
                    profileType: null,
                    columnNames: new[] { "status" },
                    out var materializer);

                Assert.True(found);
                Assert.NotNull(materializer);
            }
            finally
            {
                PreTest(typeof(ConversionEntity));
            }
        }

        private static PropertyConversionMetadata ConversionOf(IEntityMap map)
        {
            return ((IPropertyMapWithConversionMetadata)map.PropertyMaps.Single()).Conversion;
        }

        private static ConversionEntity ReadGeneratedConversionEntity(IDataRecord record)
        {
            return new ConversionEntity
            {
                Status = AccountStatus.Active
            };
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

        private sealed class ConversionEntity
        {
            public AccountStatus Status { get; set; }

            public AccountStatus? OptionalStatus { get; set; }
        }

        private sealed class DefaultConversionMap : EntityMap<ConversionEntity>
        {
            public DefaultConversionMap()
            {
                Map(e => e.Status).ToColumn("status");
            }
        }

        private sealed class ReadOnlyConversionMap : EntityMap<ConversionEntity>
        {
            public ReadOnlyConversionMap()
            {
                Map(e => e.Status)
                    .ToColumn("status")
                    .ConvertFromDatabaseUsing<StatusReadConverter, string>();
            }
        }

        private sealed class WriteOnlyConversionMap : EntityMap<ConversionEntity>
        {
            public WriteOnlyConversionMap()
            {
                Map(e => e.Status)
                    .ToColumn("status")
                    .ConvertToDatabaseUsing<StatusWriteConverter, string>();
            }
        }

        private sealed class BidirectionalConversionMap : EntityMap<ConversionEntity>
        {
            public BidirectionalConversionMap()
            {
                Map(e => e.Status)
                    .ToColumn("status")
                    .ConvertUsing<StatusTextConverter, string>();
            }
        }

        private sealed class DelegateConversionMap : EntityMap<ConversionEntity>
        {
            public DelegateConversionMap()
            {
                Map(e => e.Status)
                    .ConvertFromDatabaseUsing<string, AccountStatus>(value => AccountStatus.Active)
                    .ConvertToDatabaseUsing<AccountStatus, string>(value => value.ToString());
            }
        }

        private sealed class CountingConversionMap : EntityMap<ConversionEntity>
        {
            public CountingConversionMap()
            {
                Map(e => e.Status).ConvertFromDatabaseUsing<CountingReadConverter, string>();
            }
        }

        private sealed class NullableConversionMap : EntityMap<ConversionEntity>
        {
            public NullableConversionMap()
            {
                Map(e => e.OptionalStatus)
                    .ConvertFromDatabaseUsing<StatusReadConverter, string>()
                    .ConvertToDatabaseUsing<StatusWriteConverter, string>();
            }
        }

        private sealed class IncompatibleConverterMap : EntityMap<ConversionEntity>
        {
            public IncompatibleConverterMap()
            {
                Map(e => e.Status).ConvertFromDatabaseUsing<NoDirectionConverter, string>();
            }
        }

        private sealed class SourceMismatchConversionMap : EntityMap<ConversionEntity>
        {
            public SourceMismatchConversionMap()
            {
                Map(e => e.Status).ConvertFromDatabaseUsing<StatusReadConverter, int>();
            }
        }

        private sealed class DestinationMismatchConversionMap : EntityMap<ConversionEntity>
        {
            public DestinationMismatchConversionMap()
            {
                Map(e => e.Status).ConvertFromDatabaseUsing<IntReadConverter, string>();
            }
        }

        private sealed class DuplicateReadConversionMap : EntityMap<ConversionEntity>
        {
            public DuplicateReadConversionMap()
            {
                Map(e => e.Status)
                    .ConvertFromDatabaseUsing<StatusReadConverter, string>()
                    .ConvertFromDatabaseUsing<AlternateStatusReadConverter, string>();
            }
        }

        private sealed class DuplicateWriteConversionMap : EntityMap<ConversionEntity>
        {
            public DuplicateWriteConversionMap()
            {
                Map(e => e.Status)
                    .ConvertToDatabaseUsing<StatusWriteConverter, string>()
                    .ConvertToDatabaseUsing<AlternateStatusWriteConverter, string>();
            }
        }

        private class ConversionBaseEntity
        {
            public AccountStatus Status { get; set; }
        }

        private sealed class ConversionDerivedEntity : ConversionBaseEntity
        {
            public string Name { get; set; }
        }

        private sealed class ConversionBaseMap : EntityMap<ConversionBaseEntity>
        {
            public ConversionBaseMap()
            {
                Map(e => e.Status).ConvertFromDatabaseUsing<StatusReadConverter, string>();
            }
        }

        private sealed class ConversionDerivedMap : EntityMap<ConversionDerivedEntity>
        {
            public ConversionDerivedMap()
            {
                IncludeBase<ConversionBaseEntity>();
                Map(e => e.Name).ToColumn("name");
            }
        }

        private sealed class ConversionDerivedOverrideMap : EntityMap<ConversionDerivedEntity>
        {
            public ConversionDerivedOverrideMap()
            {
                IncludeBase<ConversionBaseEntity>();
                Map(e => e.Status).ConvertFromDatabaseUsing<AlternateStatusReadConverter, string>();
            }
        }

        private sealed class LegacyConversionProfile : IMappingProfile
        {
        }

        private sealed class ProfileConversionEntity
        {
            public AccountStatus Status { get; set; }
        }

        private sealed class DefaultProfileConversionMap : EntityMap<ProfileConversionEntity>
        {
            public DefaultProfileConversionMap()
            {
                Map(e => e.Status).ToColumn("status");
            }
        }

        private sealed class LegacyProfileConversionMap :
            EntityMap<ProfileConversionEntity>,
            IProfileMap<LegacyConversionProfile>
        {
            public LegacyProfileConversionMap()
            {
                Map(e => e.Status)
                    .ToColumn("legacy_status")
                    .ConvertFromDatabaseUsing<StatusReadConverter, string>();
            }
        }

        private sealed class SecondLegacyProfileConversionMap :
            EntityMap<ProfileConversionEntity>,
            IProfileMap<LegacyConversionProfile>
        {
            public SecondLegacyProfileConversionMap()
            {
                Map(e => e.Status)
                    .ToColumn("legacy_status_code")
                    .ConvertFromDatabaseUsing<AlternateStatusReadConverter, string>();
            }
        }

        private sealed class StatusReadConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Active;
            }
        }

        private sealed class AlternateStatusReadConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Inactive;
            }
        }

        private sealed class CountingReadConverter : IReadPropertyConverter<string, AccountStatus>
        {
            public CountingReadConverter()
            {
                Created++;
            }

            public static int Created { get; set; }

            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Active;
            }
        }

        private sealed class IntReadConverter : IReadPropertyConverter<string, int>
        {
            public int ConvertFromDatabase(string value)
            {
                return 1;
            }
        }

        private sealed class StatusWriteConverter : IWritePropertyConverter<AccountStatus, string>
        {
            public string ConvertToDatabase(AccountStatus value)
            {
                return value.ToString();
            }
        }

        private sealed class AlternateStatusWriteConverter : IWritePropertyConverter<AccountStatus, string>
        {
            public string ConvertToDatabase(AccountStatus value)
            {
                return value.ToString();
            }
        }

        private sealed class StatusTextConverter : IPropertyConverter<string, AccountStatus>
        {
            public AccountStatus ConvertFromDatabase(string value)
            {
                return AccountStatus.Active;
            }

            public string ConvertToDatabase(AccountStatus value)
            {
                return value.ToString();
            }
        }

        private sealed class NoDirectionConverter
        {
        }
    }
}
