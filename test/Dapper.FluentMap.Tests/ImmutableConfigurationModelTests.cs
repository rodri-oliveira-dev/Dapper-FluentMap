using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Naming;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ImmutableConfigurationModelTests
    {
        [Fact]
        public void BuildShouldCreateEmptyImmutableConfiguration()
        {
            var builder = new FluentMapConfigurationBuilder();

            var configuration = builder.Build();

            Assert.Empty(configuration.EntityMaps);
            Assert.Empty(configuration.ProfileMaps);
            Assert.Empty(configuration.TypeConventions);
            Assert.Empty(configuration.GeneratedMaterializers);
            Assert.Same(configuration, builder.Build());
        }

        [Fact]
        public void BuildShouldCaptureSingleMapMetadata()
        {
            var builder = new FluentMapConfigurationBuilder();

            var configuration = builder
                .AddMap<SingleSnapshotMap>()
                .Build();

            var entityMap = Assert.Single(configuration.EntityMaps).Value;
            var propertyMap = Assert.Single(entityMap.PropertyMaps);

            Assert.Equal(typeof(SingleSnapshotEntity), entityMap.EntityType);
            Assert.Equal(typeof(SingleSnapshotMap), entityMap.MapType);
            Assert.Equal(nameof(SingleSnapshotEntity.Id), propertyMap.MemberPath);
            Assert.Equal("single_id", propertyMap.ColumnName);
            Assert.False(propertyMap.CaseSensitive);
            Assert.True(propertyMap.Persistence.HasDatabaseDefaultOnInsert);
        }

        [Fact]
        public void BuildShouldCaptureMultipleIndependentMaps()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<FirstSnapshotMap>()
                .AddMap<SecondSnapshotMap>()
                .Build();

            Assert.Equal(2, configuration.EntityMaps.Count);
            Assert.Equal("first_id", configuration.EntityMaps[typeof(FirstSnapshotEntity)].PropertyMaps[0].ColumnName);
            Assert.Equal("second_name", configuration.EntityMaps[typeof(SecondSnapshotEntity)].PropertyMaps[0].ColumnName);
        }

        [Fact]
        public void BuildShouldCaptureConventionMetadata()
        {
            var builder = new FluentMapConfigurationBuilder();

            builder.AddConvention<SnapshotPrefixConvention>().ForEntity<ConventionSnapshotEntity>();
            var configuration = builder.Build();

            var convention = Assert.Single(configuration.TypeConventions[typeof(ConventionSnapshotEntity)]);
            var propertyMap = Assert.Single(convention.PropertyMaps);

            Assert.Equal(typeof(SnapshotPrefixConvention), convention.ConventionType);
            Assert.Equal("cfgId", propertyMap.ColumnName);
        }

        [Fact]
        public void BuildShouldCaptureNamingPolicyMetadata()
        {
            var builder = new FluentMapConfigurationBuilder();

            builder.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
                .ForEntity<NamingSnapshotEntity>();
            var configuration = builder.Build();

            var convention = Assert.Single(configuration.TypeConventions[typeof(NamingSnapshotEntity)]);
            var propertyMap = Assert.Single(convention.PropertyMaps);

            Assert.Equal("first_name", propertyMap.ColumnName);
            Assert.False(propertyMap.CaseSensitive);
        }

        [Fact]
        public void BuildShouldCaptureIncludedBaseMappings()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<BaseSnapshotMap>()
                .AddMap<DerivedSnapshotMap>()
                .Build();

            var derivedMap = configuration.EntityMaps[typeof(DerivedSnapshotEntity)];

            Assert.Equal(typeof(BaseSnapshotEntity), Assert.Single(derivedMap.IncludedBaseTypes));
            Assert.Equal("derived_name", Assert.Single(derivedMap.PropertyMaps).ColumnName);
        }

        [Fact]
        public void BuildShouldCaptureProfileMetadata()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<ProfileDefaultMap>()
                .AddProfile<ProfileAlternateMap>()
                .Build();

            var profile = Assert.Single(configuration.ProfileMaps);
            var propertyMap = Assert.Single(profile.PropertyMaps);

            Assert.Equal(typeof(ProfileSnapshotEntity), profile.EntityType);
            Assert.Equal(typeof(AlternateSnapshotProfile), profile.ProfileType);
            Assert.Equal("legacy_id", propertyMap.ColumnName);
        }

        [Fact]
        public void BuildShouldCaptureConverterMetadata()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<ConversionSnapshotMap>()
                .Build();

            var propertyMap = Assert.Single(configuration.EntityMaps[typeof(ConversionSnapshotEntity)].PropertyMaps);

            Assert.True(propertyMap.Conversion.HasReadConverter);
            Assert.Equal(typeof(StatusReadConverter), propertyMap.Conversion.ReadConverter.ConverterType);
            Assert.Equal(typeof(string), propertyMap.Conversion.ReadConverter.DatabaseType);
        }

        [Fact]
        public void BuildShouldCaptureGeneratedMaterializerRegistrations()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<GeneratedSnapshotMap>()
                .AddGeneratedMaterializer(
                    new[] { GeneratedMaterializerColumn.Map("generated_id", nameof(GeneratedSnapshotEntity.Id)) },
                    record => new GeneratedSnapshotEntity { Id = Convert.ToInt32(record.GetValue(0)) })
                .Build();

            var materializer = Assert.Single(configuration.GeneratedMaterializers);
            var column = Assert.Single(materializer.Columns);

            Assert.Equal(typeof(GeneratedSnapshotEntity), materializer.EntityType);
            Assert.Null(materializer.ProfileType);
            Assert.Equal("generated_id", column.ColumnName);
            Assert.Equal(nameof(GeneratedSnapshotEntity.Id), column.MemberPath);
        }

        [Fact]
        public void ConfigureShouldReuseExistingConfigurationDslAgainstBuilderState()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .Configure(config => config.AddMap<SingleSnapshotMap>())
                .Build();

            Assert.True(configuration.EntityMaps.ContainsKey(typeof(SingleSnapshotEntity)));
            Assert.False(FluentMapper.EntityMaps.ContainsKey(typeof(SingleSnapshotEntity)));
        }

        [Fact]
        public void BuildShouldRejectDuplicateMaps()
        {
            var builder = new FluentMapConfigurationBuilder()
                .AddMap<SingleSnapshotMap>();

            var exception = Assert.Throws<FluentMapConfigurationException>(() => builder.AddMap<DuplicateSingleSnapshotMap>());

            Assert.Contains("already has a configured entity map", exception.Message);
        }

        [Fact]
        public void BuildShouldReuseRuntimeValidationForInvalidMutatedMap()
        {
            var map = new InvalidAfterRegistrationMap();
            var builder = new FluentMapConfigurationBuilder()
                .AddMap(map);

            map.PropertyMaps.Add(null);

            var exception = Assert.Throws<FluentMapConfigurationException>(() => builder.Build());

            Assert.Contains("configuration validation found", exception.Message);
        }

        [Fact]
        public void BuildShouldFreezeBuilderMutationBoundary()
        {
            var builder = new FluentMapConfigurationBuilder();
            var convention = builder.AddConvention<SnapshotPrefixConvention>();

            builder.Build();

            Assert.Throws<InvalidOperationException>(() => builder.AddMap<SingleSnapshotMap>());
            Assert.Throws<InvalidOperationException>(() => builder.Configure(configuration => configuration.AddMap<SingleSnapshotMap>()));
            Assert.Throws<InvalidOperationException>(() => convention.ForEntity<ConventionSnapshotEntity>());
        }

        [Fact]
        public void BuildShouldNotExposeMutableEffectiveCollections()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<SingleSnapshotMap>()
                .Build();

            var mutableMaps = Assert.IsAssignableFrom<IDictionary<Type, EntityMappingConfiguration>>(configuration.EntityMaps);
            var mutablePropertyMaps = Assert.IsAssignableFrom<IList<PropertyMappingConfiguration>>(
                configuration.EntityMaps[typeof(SingleSnapshotEntity)].PropertyMaps);

            Assert.Throws<NotSupportedException>(() => mutableMaps.Add(typeof(SecondSnapshotEntity), null));
            Assert.Throws<NotSupportedException>(() => mutablePropertyMaps.Add(null));
        }

        [Fact]
        public void BuildShouldCaptureSnapshotIndependentFromLaterMapMutation()
        {
            var map = new MutableSnapshotMap();
            var builder = new FluentMapConfigurationBuilder()
                .AddMap(map);

            var configuration = builder.Build();

            map.PropertyMaps.Clear();

            var propertyMap = Assert.Single(configuration.EntityMaps[typeof(MutableSnapshotEntity)].PropertyMaps);
            Assert.Equal("before_build", propertyMap.ColumnName);
        }

        [Fact]
        public void BuildersShouldProduceIndependentConfigurationsForSameEntityType()
        {
            var first = new FluentMapConfigurationBuilder()
                .AddMap(new FirstIndependentMap())
                .Build();
            var second = new FluentMapConfigurationBuilder()
                .AddMap(new SecondIndependentMap())
                .Build();

            Assert.Equal("first_id", first.EntityMaps[typeof(IndependentSnapshotEntity)].PropertyMaps[0].ColumnName);
            Assert.Equal("second_id", second.EntityMaps[typeof(IndependentSnapshotEntity)].PropertyMaps[0].ColumnName);
        }

        [Fact]
        public void ImmutableConfigurationShouldSupportConcurrentReads()
        {
            var configuration = new FluentMapConfigurationBuilder()
                .AddMap<SingleSnapshotMap>()
                .Build();
            var columnNames = new string[100];

            Parallel.For(0, columnNames.Length, index =>
            {
                columnNames[index] = configuration.EntityMaps[typeof(SingleSnapshotEntity)]
                    .PropertyMaps[0]
                    .ColumnName;
            });

            Assert.True(columnNames.All(column => column == "single_id"));
        }

        private sealed class SingleSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class SingleSnapshotMap : EntityMap<SingleSnapshotEntity>
        {
            public SingleSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("single_id", caseSensitive: false).DatabaseDefaultOnInsert();
            }
        }

        private sealed class DuplicateSingleSnapshotMap : EntityMap<SingleSnapshotEntity>
        {
            public DuplicateSingleSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("duplicate_id");
            }
        }

        private sealed class FirstSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class FirstSnapshotMap : EntityMap<FirstSnapshotEntity>
        {
            public FirstSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("first_id");
            }
        }

        private sealed class SecondSnapshotEntity
        {
            public string Name { get; set; }
        }

        private sealed class SecondSnapshotMap : EntityMap<SecondSnapshotEntity>
        {
            public SecondSnapshotMap()
            {
                Map(entity => entity.Name).ToColumn("second_name");
            }
        }

        private sealed class SnapshotPrefixConvention : Convention
        {
            public SnapshotPrefixConvention()
            {
                Properties().Configure(configuration => configuration.HasPrefix("cfg"));
            }
        }

        private sealed class ConventionSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class NamingSnapshotEntity
        {
            public string FirstName { get; set; }
        }

        private class BaseSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class DerivedSnapshotEntity : BaseSnapshotEntity
        {
            public string Name { get; set; }
        }

        private sealed class BaseSnapshotMap : EntityMap<BaseSnapshotEntity>
        {
            public BaseSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("base_id");
            }
        }

        private sealed class DerivedSnapshotMap : EntityMap<DerivedSnapshotEntity>
        {
            public DerivedSnapshotMap()
            {
                IncludeBase<BaseSnapshotEntity>();
                Map(entity => entity.Name).ToColumn("derived_name");
            }
        }

        private sealed class AlternateSnapshotProfile : IMappingProfile
        {
        }

        private sealed class ProfileSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class ProfileDefaultMap : EntityMap<ProfileSnapshotEntity>
        {
            public ProfileDefaultMap()
            {
                Map(entity => entity.Id).ToColumn("current_id");
            }
        }

        private sealed class ProfileAlternateMap :
            EntityMap<ProfileSnapshotEntity>,
            IProfileMap<AlternateSnapshotProfile>
        {
            public ProfileAlternateMap()
            {
                Map(entity => entity.Id).ToColumn("legacy_id");
            }
        }

        private enum SnapshotStatus
        {
            Active
        }

        private sealed class ConversionSnapshotEntity
        {
            public SnapshotStatus Status { get; set; }
        }

        private sealed class ConversionSnapshotMap : EntityMap<ConversionSnapshotEntity>
        {
            public ConversionSnapshotMap()
            {
                Map(entity => entity.Status).ToColumn("status").ConvertFromDatabaseUsing<StatusReadConverter, string>();
            }
        }

        private sealed class StatusReadConverter : IReadPropertyConverter<string, SnapshotStatus>
        {
            public SnapshotStatus ConvertFromDatabase(string value)
            {
                return SnapshotStatus.Active;
            }
        }

        private sealed class GeneratedSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class GeneratedSnapshotMap : EntityMap<GeneratedSnapshotEntity>
        {
            public GeneratedSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("generated_id");
            }
        }

        private sealed class InvalidAfterRegistrationEntity
        {
            public int Id { get; set; }
        }

        private sealed class InvalidAfterRegistrationMap : EntityMap<InvalidAfterRegistrationEntity>
        {
            public InvalidAfterRegistrationMap()
            {
                Map(entity => entity.Id).ToColumn("invalid_id");
            }
        }

        private sealed class MutableSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class MutableSnapshotMap : EntityMap<MutableSnapshotEntity>
        {
            public MutableSnapshotMap()
            {
                Map(entity => entity.Id).ToColumn("before_build");
            }
        }

        private sealed class IndependentSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class FirstIndependentMap : EntityMap<IndependentSnapshotEntity>
        {
            public FirstIndependentMap()
            {
                Map(entity => entity.Id).ToColumn("first_id");
            }
        }

        private sealed class SecondIndependentMap : EntityMap<IndependentSnapshotEntity>
        {
            public SecondIndependentMap()
            {
                Map(entity => entity.Id).ToColumn("second_id");
            }
        }
    }
}
