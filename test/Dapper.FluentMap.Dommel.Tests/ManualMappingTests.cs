using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

[assembly: Parallelization(Mode = ParallelMode.None)]
namespace Dapper.FluentMap.Dommel.Tests
{
    public class ManualMappingTests
    {
        [Fact]
        public void EntityMapsCustomIdAsKey()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapWithCustomIdPropertyMap()));

            var type = typeof(CustomIdEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var columnResolver = new Dommel.Resolvers.DommelColumnNameResolver();

            var keys = keyResolver.ResolveKeyProperties(type);
            var columnName = columnResolver.ResolveColumnName(keys.Single().Property);
            
            Assert.Single(keys);
            Assert.Equal("customid", columnName);
        }

        [Fact]
        public void EntityMapsToSingleCustomId()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapSingleCustomIdPropertyMap()));

            var type = typeof(DoubleIdEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var columnResolver = new Dommel.Resolvers.DommelColumnNameResolver();
            FluentMapper.EntityMaps.TryGetValue(type, out var entityMap);

            var keys = keyResolver.ResolveKeyProperties(type);
            var columnName = columnResolver.ResolveColumnName(keys.Single().Property);

            var idName = columnResolver.ResolveColumnName(entityMap.PropertyMaps.Single(x => x.PropertyInfo.Name == nameof(DoubleIdEntity.Id)).PropertyInfo);

            Assert.Single(keys);
            Assert.Equal("id", columnName);
            Assert.Equal("Id", idName);
        }

        [Fact]
        public void EntityMapsToDefaultSingleKey()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapSingleCustomIdDefaultKey()));

            var type = typeof(DoubleIdEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var keys = keyResolver.ResolveKeyProperties(type);
            Assert.Single(keys);
        }

        [Fact]
        public void KeyPropertyIsGenerated()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapSingleCustomIdPropertyMap()));

            var type = typeof(DoubleIdEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var keys = keyResolver.ResolveKeyProperties(type);

            var key = keys.FirstOrDefault();
            Assert.True(key.IsGenerated);
        }

        [Fact]

        public void PropertyIsGenerated()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapSingleCustomIdPropertyMap()));

            var type = typeof(DoubleIdEntity);
            var propertyResolver = new Dommel.Resolvers.DommelPropertyResolver();
            var properties = propertyResolver.ResolveProperties(type);

            var property = properties.Where(x => x.IsGenerated);
            Assert.NotEmpty(property);

        }

        [Fact]
        public void EntityMapsToMultipleKeys()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapCompositeKeyPropertyMap()));

            var type = typeof(CompositeKeyEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var keys = keyResolver.ResolveKeyProperties(type);

            Assert.True(keys.Count() == 2);
            Assert.All(keys, k => Assert.False(k.IsGenerated));
        }

        [Fact]

        public void CompositeKeysShouldRemainNonGeneratedForKeyResolver()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new MapCompositeKeyPropertyMap()));

            var type = typeof(CompositeKeyEntity);
            var keyResolver = new Dommel.Resolvers.DommelKeyPropertyResolver();
            var properties = keyResolver.ResolveKeyProperties(type);

            Assert.All(properties, p => Assert.False(p.IsGenerated));
        }

        [Fact]
        public void KeyShouldBeRepresentedInPersistenceMetadataWithoutImplyingIdentity()
        {
            PreTest();

            var map = new MapSingleCustomIdPropertyMap();
            var customId = map.PropertyMaps.Single(x => x.PropertyInfo.Name == nameof(DoubleIdEntity.CustomId));
            var persistence = ((IPropertyMapWithPersistenceMetadata)customId).Persistence;

            Assert.True(persistence.IsKey);
            Assert.False(persistence.IsIdentity);
            Assert.False(persistence.IsGenerated);
            Assert.True(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.ParticipatesInMaterialization);
        }

        [Fact]
        public void IdentityShouldBeRepresentedAsGeneratedKeyMetadata()
        {
            PreTest();

            var map = new MapWithCustomIdPropertyMap();
            var customId = map.PropertyMaps.Single();
            var persistence = ((IPropertyMapWithPersistenceMetadata)customId).Persistence;

            Assert.True(persistence.IsKey);
            Assert.True(persistence.IsIdentity);
            Assert.True(persistence.IsGenerated);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.ParticipatesInMaterialization);
        }

        [Fact]
        public void ComputedShouldBeRepresentedAsGeneratedReadOnlyMetadata()
        {
            PreTest();

            var map = new MapComputedProperty();
            var property = map.PropertyMaps.Single();
            var persistence = ((IPropertyMapWithPersistenceMetadata)property).Persistence;

            Assert.True(persistence.IsGenerated);
            Assert.True(persistence.IsComputed);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
            Assert.True(persistence.ParticipatesInMaterialization);
        }

        [Fact]
        public void DirectGeneratedOptionAssignmentShouldUpdatePersistenceMetadata()
        {
            PreTest();

            var map = new MapDirectGeneratedOptionProperty();
            var property = map.PropertyMaps.Single();
            var persistence = ((IPropertyMapWithPersistenceMetadata)property).Persistence;

            Assert.True(persistence.IsGenerated);
            Assert.True(persistence.IsComputed);
            Assert.False(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
        }

        [Fact]
        public void GeneratedOptionNoneShouldKeepNonIdentityKeyInsertable()
        {
            PreTest();

            var map = new MapCompositeKeyPropertyMap();
            var key = map.PropertyMaps.First();
            var persistence = ((IPropertyMapWithPersistenceMetadata)key).Persistence;

            Assert.True(persistence.IsKey);
            Assert.False(persistence.IsIdentity);
            Assert.False(persistence.IsGenerated);
            Assert.True(persistence.ParticipatesInInsert);
            Assert.False(persistence.ParticipatesInUpdate);
        }

        [Fact]
        public void ComputedKeyShouldThrowConfigurationException()
        {
            PreTest();

            var exception = Assert.Throws<FluentMapConfigurationException>(() => new InvalidComputedKeyMap());

            Assert.Contains("computed", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("key", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CoreReadOnlyMetadataShouldBeConsumableByDommelPropertyResolver()
        {
            PreTest();

            FluentMapper.Initialize(c => c.AddMap(new CoreReadOnlyMap()));

            var propertyResolver = new Dommel.Resolvers.DommelPropertyResolver();
            var properties = propertyResolver.ResolveProperties(typeof(DoubleIdEntity));
            var name = properties.Single(p => p.Property.Name == nameof(DoubleIdEntity.Name));

            Assert.True(name.IsGenerated);
        }

        [Fact]
        public void ResolvePropertiesShouldDeferGlobalMapLookupUntilEnumeration()
        {
            PreTest();

            var propertyResolver = new Dommel.Resolvers.DommelPropertyResolver();
            var properties = propertyResolver.ResolveProperties(typeof(DoubleIdEntity));

            FluentMapper.Initialize(c => c.AddMap(new CoreReadOnlyMap()));

            var name = properties.Single(p => p.Property.Name == nameof(DoubleIdEntity.Name));

            Assert.True(name.IsGenerated);
        }

        [Fact]
        public void DommelResolversShouldUseOnlyLegacyProcessWideConfiguration()
        {
            PreTest();

            var isolatedRuntime = new FluentMapConfigurationBuilder()
                .AddMap(new IsolatedDommelTableMap())
                .Build()
                .CreateRuntime();
            var resolver = new Dommel.Resolvers.DommelTableNameResolver();

            var beforeGlobalRegistration = resolver.ResolveTableName(typeof(DommelIsolationEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new LegacyDommelTableMap());
                c.ForDommel();
            });

            var afterGlobalRegistration = resolver.ResolveTableName(typeof(DommelIsolationEntity));

            Assert.NotNull(isolatedRuntime);
            Assert.NotEqual("isolated_dommel_entities", beforeGlobalRegistration);
            Assert.Equal("legacy_dommel_entities", afterGlobalRegistration);
        }

        private static void PreTest()
        {
            FluentMapper.EntityMaps.Clear();
            FluentMapper.TypeConventions.Clear();
            FluentMapper.Initialize(_ => { });
        }

        private class MapWithCustomIdPropertyMap : DommelEntityMap<CustomIdEntity>
        {
            public MapWithCustomIdPropertyMap()
            {
                Map(p => p.CustomId).ToColumn("customid").IsIdentity().IsKey();
            }
        }

        private class MapSingleCustomIdPropertyMap : DommelEntityMap<DoubleIdEntity>
        {
            public MapSingleCustomIdPropertyMap()
            {
                Map(p => p.Id);
                Map(p => p.CustomId).IsKey().ToColumn("id", false);
            }
        }

        private class MapSingleCustomIdDefaultKey : DommelEntityMap<DoubleIdEntity>
        {
            public MapSingleCustomIdDefaultKey()
            {
                Map(p => p.CustomId).ToColumn("customid", false);
            }
        }

        private class MapCompositeKeyPropertyMap : DommelEntityMap<CompositeKeyEntity>
        {
            public MapCompositeKeyPropertyMap()
            {
                Map(p => p.KeyPartOne).IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
                Map(p => p.KeyPartTwo).IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
            }
        }

        private class MapComputedProperty : DommelEntityMap<DoubleIdEntity>
        {
            public MapComputedProperty()
            {
                Map(p => p.Name).Computed();
            }
        }

        private class MapDirectGeneratedOptionProperty : DommelEntityMap<DoubleIdEntity>
        {
            public MapDirectGeneratedOptionProperty()
            {
                Map(p => p.Name).GeneratedOption = DatabaseGeneratedOption.Computed;
            }
        }

        private class InvalidComputedKeyMap : DommelEntityMap<DoubleIdEntity>
        {
            public InvalidComputedKeyMap()
            {
                Map(p => p.CustomId).Computed().IsKey();
            }
        }

        private class CoreReadOnlyMap : EntityMap<DoubleIdEntity>
        {
            public CoreReadOnlyMap()
            {
                Map(p => p.Name).ReadOnly();
            }
        }

        private sealed class DommelIsolationEntity
        {
            public int Id { get; set; }
        }

        private sealed class IsolatedDommelTableMap : DommelEntityMap<DommelIsolationEntity>
        {
            public IsolatedDommelTableMap()
            {
                ToTable("isolated_dommel_entities");
                Map(entity => entity.Id).ToColumn("id").IsKey();
            }
        }

        private sealed class LegacyDommelTableMap : DommelEntityMap<DommelIsolationEntity>
        {
            public LegacyDommelTableMap()
            {
                ToTable("legacy_dommel_entities");
                Map(entity => entity.Id).ToColumn("id").IsKey();
            }
        }
    }
}
