using System;
using System.Linq;
using Dapper.FluentMap.Mapping;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class PropertyPersistenceMetadataTests
    {
        [Fact]
        public void DefaultPropertyMapShouldParticipateInReadInsertAndUpdate()
        {
            var map = new DefaultPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.True(persistence.ParticipatesInInsert);
            Assert.True(persistence.ParticipatesInUpdate);
            Assert.False(persistence.IgnoredByFluentMap);
            Assert.False(persistence.IsGenerated);
            Assert.False(persistence.IsKey);
            Assert.False(persistence.IsIdentity);
        }

        [Fact]
        public void ReadOnlyShouldPreserveReadAndExcludeInsertAndUpdate()
        {
            var map = new ReadOnlyPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.False(persistence.IgnoredByFluentMap);
            Assert.False(persistence.IsGenerated);
        }

        [Fact]
        public void ExcludeFromInsertShouldOnlyDisableInsertParticipation()
        {
            var map = new ExcludeInsertPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.True(persistence.ParticipatesInUpdate);
            Assert.False(persistence.IsGenerated);
        }

        [Fact]
        public void ExcludeFromUpdateShouldOnlyDisableUpdateParticipation()
        {
            var map = new ExcludeUpdatePersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.True(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
        }

        [Fact]
        public void IgnoreShouldDisableReadInsertAndUpdateParticipation()
        {
            var map = new IgnorePersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(map.PropertyMaps.Single().Ignored);
            Assert.False(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.IgnoredByFluentMap);
        }

        [Fact]
        public void ComputedShouldBeGeneratedReadOnlyPersistenceMetadata()
        {
            var map = new ComputedPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.IsGenerated);
            Assert.True(persistence.IsComputed);
            Assert.False(persistence.HasDatabaseDefaultOnInsert);
        }

        [Fact]
        public void DatabaseDefaultOnInsertShouldBeGeneratedAndExcludeOnlyInsertByDefault()
        {
            var map = new DatabaseDefaultPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.True(persistence.ParticipatesInUpdate);
            Assert.True(persistence.IsGenerated);
            Assert.False(persistence.IsComputed);
            Assert.True(persistence.HasDatabaseDefaultOnInsert);
        }

        [Fact]
        public void DatabaseDefaultCanBeCombinedWithExcludeFromUpdate()
        {
            var map = new DatabaseDefaultReadOnlyPersistenceMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.IsGenerated);
            Assert.True(persistence.HasDatabaseDefaultOnInsert);
        }

        [Fact]
        public void DuplicateCompatibleConfigurationShouldBeIdempotent()
        {
            var map = new DuplicatePersistenceConfigurationMap();

            var persistence = PersistenceOf(map);

            Assert.True(persistence.ParticipatesInMaterialization);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.False(persistence.IsGenerated);
        }

        [Fact]
        public void WritePersistenceConfigurationAfterIgnoreShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new IgnoreThenReadOnlyPersistenceMap());

            Assert.Contains("Ignored properties", exception.Message);
        }

        [Fact]
        public void ComputedAndDatabaseDefaultShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new ComputedThenDefaultPersistenceMap());

            Assert.Contains("computed", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("database default", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DatabaseDefaultAndComputedShouldThrow()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new DefaultThenComputedPersistenceMap());

            Assert.Contains("database-default", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("computed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExplainShouldExposePersistenceMetadata()
        {
            PreTest(typeof(PersistenceEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ComputedPersistenceMap()));

                var explanation = FluentMapper.Explain<PersistenceEntity>();
                var persistence = explanation.Members.Single(m => m.MemberPath == nameof(PersistenceEntity.CreatedAt)).Persistence;

                Assert.True(persistence.IsComputed);
                Assert.False(persistence.ParticipatesInInsert);
                Assert.False(persistence.ParticipatesInUpdate);
            }
            finally
            {
                PreTest(typeof(PersistenceEntity));
            }
        }

        [Fact]
        public void InheritedMappingsShouldPreservePersistenceMetadata()
        {
            PreTest(typeof(PersistenceBaseEntity), typeof(PersistenceDerivedEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new PersistenceBaseMap());
                    c.AddMap(new PersistenceDerivedMap());
                });

                var explanation = FluentMapper.Explain<PersistenceDerivedEntity>();
                var persistence = explanation.Members.Single(m => m.MemberPath == nameof(PersistenceBaseEntity.CreatedAt)).Persistence;

                Assert.Equal(typeof(PersistenceBaseEntity), explanation.Members.Single(m => m.MemberPath == nameof(PersistenceBaseEntity.CreatedAt)).InheritedFrom);
                Assert.True(persistence.ParticipatesInMaterialization);
                Assert.False(persistence.ParticipatesInInsert);
                Assert.False(persistence.ParticipatesInUpdate);
            }
            finally
            {
                PreTest(typeof(PersistenceBaseEntity), typeof(PersistenceDerivedEntity));
            }
        }

        [Fact]
        public void ProfileMappingsShouldExposeProfileSpecificPersistenceMetadata()
        {
            PreTest(typeof(ProfilePersistenceEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddProfile<ProfilePersistenceMap>());

                var explanation = FluentMapper.Explain<ProfilePersistenceEntity, PersistenceProfile>();
                var persistence = explanation.Members.Single(m => m.MemberPath == nameof(ProfilePersistenceEntity.UpdatedAt)).Persistence;

                Assert.False(persistence.ParticipatesInInsert);
                Assert.True(persistence.ParticipatesInUpdate);
                Assert.True(persistence.HasDatabaseDefaultOnInsert);
            }
            finally
            {
                PreTest(typeof(ProfilePersistenceEntity));
            }
        }

        private static PropertyPersistenceMetadata PersistenceOf(IEntityMap map)
        {
            return ((IPropertyMapWithPersistenceMetadata)map.PropertyMaps.Single()).Persistence;
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class PersistenceEntity
        {
            public int Id { get; set; }

            public DateTime CreatedAt { get; set; }
        }

        private sealed class DefaultPersistenceMap : EntityMap<PersistenceEntity>
        {
            public DefaultPersistenceMap()
            {
                Map(e => e.CreatedAt).ToColumn("created_at");
            }
        }

        private sealed class ReadOnlyPersistenceMap : EntityMap<PersistenceEntity>
        {
            public ReadOnlyPersistenceMap()
            {
                Map(e => e.CreatedAt).ToColumn("created_at").ReadOnly();
            }
        }

        private sealed class ExcludeInsertPersistenceMap : EntityMap<PersistenceEntity>
        {
            public ExcludeInsertPersistenceMap()
            {
                Map(e => e.CreatedAt).ExcludeFromInsert();
            }
        }

        private sealed class ExcludeUpdatePersistenceMap : EntityMap<PersistenceEntity>
        {
            public ExcludeUpdatePersistenceMap()
            {
                Map(e => e.CreatedAt).ExcludeFromUpdate();
            }
        }

        private sealed class IgnorePersistenceMap : EntityMap<PersistenceEntity>
        {
            public IgnorePersistenceMap()
            {
                Map(e => e.CreatedAt).Ignore();
            }
        }

        private sealed class ComputedPersistenceMap : EntityMap<PersistenceEntity>
        {
            public ComputedPersistenceMap()
            {
                Map(e => e.CreatedAt).Computed();
            }
        }

        private sealed class DatabaseDefaultPersistenceMap : EntityMap<PersistenceEntity>
        {
            public DatabaseDefaultPersistenceMap()
            {
                Map(e => e.CreatedAt).DatabaseDefaultOnInsert();
            }
        }

        private sealed class DatabaseDefaultReadOnlyPersistenceMap : EntityMap<PersistenceEntity>
        {
            public DatabaseDefaultReadOnlyPersistenceMap()
            {
                Map(e => e.CreatedAt).DatabaseDefaultOnInsert().ExcludeFromUpdate();
            }
        }

        private sealed class DuplicatePersistenceConfigurationMap : EntityMap<PersistenceEntity>
        {
            public DuplicatePersistenceConfigurationMap()
            {
                Map(e => e.CreatedAt).ReadOnly().ExcludeFromInsert().ExcludeFromUpdate().ReadOnly();
            }
        }

        private sealed class IgnoreThenReadOnlyPersistenceMap : EntityMap<PersistenceEntity>
        {
            public IgnoreThenReadOnlyPersistenceMap()
            {
                Map(e => e.CreatedAt).Ignore().ReadOnly();
            }
        }

        private sealed class ComputedThenDefaultPersistenceMap : EntityMap<PersistenceEntity>
        {
            public ComputedThenDefaultPersistenceMap()
            {
                Map(e => e.CreatedAt).Computed().DatabaseDefaultOnInsert();
            }
        }

        private sealed class DefaultThenComputedPersistenceMap : EntityMap<PersistenceEntity>
        {
            public DefaultThenComputedPersistenceMap()
            {
                Map(e => e.CreatedAt).DatabaseDefaultOnInsert().Computed();
            }
        }

        private class PersistenceBaseEntity
        {
            public DateTime CreatedAt { get; set; }
        }

        private sealed class PersistenceDerivedEntity : PersistenceBaseEntity
        {
            public string Name { get; set; }
        }

        private sealed class PersistenceBaseMap : EntityMap<PersistenceBaseEntity>
        {
            public PersistenceBaseMap()
            {
                Map(e => e.CreatedAt).ReadOnly();
            }
        }

        private sealed class PersistenceDerivedMap : EntityMap<PersistenceDerivedEntity>
        {
            public PersistenceDerivedMap()
            {
                IncludeBase<PersistenceBaseEntity>();
            }
        }

        private sealed class PersistenceProfile : IMappingProfile
        {
        }

        private sealed class ProfilePersistenceEntity
        {
            public DateTime UpdatedAt { get; set; }
        }

        private sealed class ProfilePersistenceMap : EntityMap<ProfilePersistenceEntity>, IProfileMap<PersistenceProfile>
        {
            public ProfilePersistenceMap()
            {
                Map(e => e.UpdatedAt).DatabaseDefaultOnInsert();
            }
        }
    }
}
