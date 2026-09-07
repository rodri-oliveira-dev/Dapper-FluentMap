using System;
using System.Collections.Generic;
using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MappingStateEncapsulationTests
    {
        [Fact]
        public void OfficialMapRegistrationShouldInvalidateCachedMissAndInstallDapperTypeMap()
        {
            ResetMapper(typeof(OfficialMapEntity));

            try
            {
                var miss = FluentMapper.Registry.GetFluentPropertyInfo(typeof(OfficialMapEntity), "official_id");

                FluentMapper.Initialize(configuration => configuration.AddMap(new OfficialMap()));

                var hit = FluentMapper.Registry.GetFluentPropertyInfo(typeof(OfficialMapEntity), "official_id");
                var dapperMember = SqlMapper.GetTypeMap(typeof(OfficialMapEntity)).GetMember("official_id");

                Assert.Null(miss);
                Assert.Equal(typeof(OfficialMapEntity).GetProperty(nameof(OfficialMapEntity.Id)), hit);
                Assert.Equal(nameof(OfficialMapEntity.Id), dapperMember.Property.Name);
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                ResetMapper(typeof(OfficialMapEntity));
            }
        }

        [Fact]
        public void OfficialConventionRegistrationShouldInvalidateCachedMissAndInstallDapperTypeMap()
        {
            ResetMapper(typeof(OfficialConventionEntity));

            try
            {
                var miss = FluentMapper.Registry.GetFluentPropertyInfo(typeof(OfficialConventionEntity), "cfgId");

                FluentMapper.Initialize(configuration => configuration
                    .AddConvention<SnapshotPrefixConvention>()
                    .ForEntity<OfficialConventionEntity>());

                var hit = FluentMapper.Registry.GetFluentPropertyInfo(typeof(OfficialConventionEntity), "cfgId");
                var dapperMember = SqlMapper.GetTypeMap(typeof(OfficialConventionEntity)).GetMember("cfgId");

                Assert.Null(miss);
                Assert.Equal(typeof(OfficialConventionEntity).GetProperty(nameof(OfficialConventionEntity.Id)), hit);
                Assert.Equal(nameof(OfficialConventionEntity.Id), dapperMember.Property.Name);
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                ResetMapper(typeof(OfficialConventionEntity));
            }
        }

        [Fact]
        public void EntityMapsSnapshotShouldBeReadOnlyAndNotTrackLaterRegistrations()
        {
            ResetMapper(typeof(ReadOnlyFirstEntity), typeof(ReadOnlySecondEntity));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new ReadOnlyFirstMap()));

                var snapshot = FluentMapper.GetEntityMaps();

                FluentMapper.Initialize(configuration => configuration.AddMap(new ReadOnlySecondMap()));

                Assert.Single(snapshot);
                Assert.True(snapshot.ContainsKey(typeof(ReadOnlyFirstEntity)));
                Assert.False(snapshot.ContainsKey(typeof(ReadOnlySecondEntity)));

                var mutableSnapshot = Assert.IsAssignableFrom<IDictionary<Type, IEntityMap>>(snapshot);
                Assert.Throws<NotSupportedException>(() =>
                    mutableSnapshot.Add(typeof(ReadOnlySecondEntity), new ReadOnlySecondMap()));
            }
            finally
            {
                ResetMapper(typeof(ReadOnlyFirstEntity), typeof(ReadOnlySecondEntity));
            }
        }

        [Fact]
        public void TypeConventionsSnapshotShouldBeReadOnlyAndNotExposeMutableConventionLists()
        {
            ResetMapper(typeof(ReadOnlyConventionEntity), typeof(ReadOnlySecondConventionEntity));

            try
            {
                FluentMapper.Initialize(configuration => configuration
                    .AddConvention<SnapshotPrefixConvention>()
                    .ForEntity<ReadOnlyConventionEntity>());

                var snapshot = FluentMapper.GetTypeConventions();

                FluentMapper.Initialize(configuration => configuration
                    .AddConvention<SnapshotPrefixConvention>()
                    .ForEntity<ReadOnlySecondConventionEntity>());

                Assert.Single(snapshot);
                Assert.True(snapshot.ContainsKey(typeof(ReadOnlyConventionEntity)));
                Assert.False(snapshot.ContainsKey(typeof(ReadOnlySecondConventionEntity)));

                var mutableSnapshot = Assert.IsAssignableFrom<IDictionary<Type, IReadOnlyList<Convention>>>(snapshot);
                Assert.Throws<NotSupportedException>(() =>
                    mutableSnapshot.Add(typeof(ReadOnlySecondConventionEntity), new List<Convention>()));

                var mutableConventions = Assert.IsAssignableFrom<IList<Convention>>(snapshot[typeof(ReadOnlyConventionEntity)]);
                Assert.Throws<NotSupportedException>(() =>
                    mutableConventions.Add(new SnapshotPrefixConvention()));
            }
            finally
            {
                ResetMapper(typeof(ReadOnlyConventionEntity), typeof(ReadOnlySecondConventionEntity));
            }
        }

        [Fact]
        public void LegacyEntityMapReplacementCanBypassCacheInvalidation()
        {
            ResetMapper(typeof(LegacyReplacementEntity));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new LegacyIdMap()));
                var beforeReplacement = FluentMapper.Registry.GetFluentPropertyInfo(typeof(LegacyReplacementEntity), "shared_column");

                FluentMapper.EntityMaps[typeof(LegacyReplacementEntity)] = new LegacyNameMap();
                var afterReplacement = FluentMapper.Registry.GetFluentPropertyInfo(typeof(LegacyReplacementEntity), "shared_column");

                Assert.Equal(typeof(LegacyReplacementEntity).GetProperty(nameof(LegacyReplacementEntity.Id)), beforeReplacement);
                Assert.Equal(typeof(LegacyReplacementEntity).GetProperty(nameof(LegacyReplacementEntity.Id)), afterReplacement);
                Assert.IsType<LegacyNameMap>(FluentMapper.EntityMaps[typeof(LegacyReplacementEntity)]);
            }
            finally
            {
                ResetMapper(typeof(LegacyReplacementEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void EntityMapSnapshotShouldPreserveDefaultMapWhileProfilesRemainQueryScoped()
        {
            ResetMapper(typeof(ProfileSnapshotEntity));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new ProfileSnapshotDefaultMap());
                    configuration.AddProfile<ProfileSnapshotAlternateMap>();
                });

                var snapshot = FluentMapper.GetEntityMaps();

                using (var connection = OpenConnection())
                {
                    var defaultEntity = connection.QuerySingle<ProfileSnapshotEntity>(
                        "SELECT 1 AS default_id;");
                    var profileEntity = connection.QueryMappedSingle<ProfileSnapshotEntity, ProfileSnapshot>(
                        "SELECT 2 AS profile_id;");

                    Assert.IsType<ProfileSnapshotDefaultMap>(snapshot[typeof(ProfileSnapshotEntity)]);
                    Assert.Equal(1, defaultEntity.Id);
                    Assert.Equal(2, profileEntity.Id);
                }
            }
            finally
            {
                ResetMapper(typeof(ProfileSnapshotEntity));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void ResetMapper(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class SnapshotPrefixConvention : Convention
        {
            public SnapshotPrefixConvention()
            {
                Properties()
                    .Configure(configuration => configuration.HasPrefix("cfg"));
            }
        }

        private sealed class OfficialMapEntity
        {
            public int Id { get; set; }
        }

        private sealed class OfficialMap : EntityMap<OfficialMapEntity>
        {
            public OfficialMap()
            {
                Map(entity => entity.Id).ToColumn("official_id");
            }
        }

        private sealed class OfficialConventionEntity
        {
            public int Id { get; set; }
        }

        private sealed class ReadOnlyFirstEntity
        {
            public int Id { get; set; }
        }

        private sealed class ReadOnlyFirstMap : EntityMap<ReadOnlyFirstEntity>
        {
            public ReadOnlyFirstMap()
            {
                Map(entity => entity.Id).ToColumn("first_id");
            }
        }

        private sealed class ReadOnlySecondEntity
        {
            public int Id { get; set; }
        }

        private sealed class ReadOnlySecondMap : EntityMap<ReadOnlySecondEntity>
        {
            public ReadOnlySecondMap()
            {
                Map(entity => entity.Id).ToColumn("second_id");
            }
        }

        private sealed class ReadOnlyConventionEntity
        {
            public int Id { get; set; }
        }

        private sealed class ReadOnlySecondConventionEntity
        {
            public int Id { get; set; }
        }

        private sealed class LegacyReplacementEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class LegacyIdMap : EntityMap<LegacyReplacementEntity>
        {
            public LegacyIdMap()
            {
                Map(entity => entity.Id).ToColumn("shared_column");
            }
        }

        private sealed class LegacyNameMap : EntityMap<LegacyReplacementEntity>
        {
            public LegacyNameMap()
            {
                Map(entity => entity.Name).ToColumn("shared_column");
            }
        }

        private sealed class ProfileSnapshot
            : IMappingProfile
        {
        }

        private sealed class ProfileSnapshotEntity
        {
            public int Id { get; set; }
        }

        private sealed class ProfileSnapshotDefaultMap : EntityMap<ProfileSnapshotEntity>
        {
            public ProfileSnapshotDefaultMap()
            {
                Map(entity => entity.Id).ToColumn("default_id");
            }
        }

        private sealed class ProfileSnapshotAlternateMap :
            EntityMap<ProfileSnapshotEntity>,
            IProfileMap<ProfileSnapshot>
        {
            public ProfileSnapshotAlternateMap()
            {
                Map(entity => entity.Id).ToColumn("profile_id");
            }
        }
    }
}
