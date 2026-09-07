using System;
using System.Linq;
using Dapper.FluentMap.Mapping;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Dapper.FluentMap.Tests
{
    public class ManualMappingTests
    {
        [Fact]
        public void DuplicateMappingShouldThrow_Exception()
        {
            // Arrange
            PreTest();

            // Act & Assert
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new MapWithDuplicateMapping());
            Assert.Contains(nameof(TestEntity.Id), exception.Message);
        }

        [Fact]
        public void EntityMapShouldHaveEmptyPropertyMapCollection()
        {
            // Arrange
            PreTest();

            // Act
            var map = new EmptyMap();

            // Assert
            Assert.NotNull(map.PropertyMaps);
            Assert.Empty(map.PropertyMaps);
        }

        [Fact]
        public void EntityMapShouldHavePropertyMap()
        {
            // Arrange
            PreTest();

            // Act
            var map = new MapWithOnePropertyMap();

            // Assert
            Assert.Single(map.PropertyMaps);
        }

        [Fact]
        public void PropertyMapShouldBeCaseInsensitive()
        {
            // Arrange
            PreTest();

            // Act
            var map = new CaseInsensitveMap();
            var propertyMap = map.PropertyMaps.Single();

            // Assert
            Assert.Equal("Test", propertyMap.ColumnName, ignoreCase: true);
            Assert.False(propertyMap.CaseSensitive);
        }

        [Fact]
        public void PropertyShouldBeIgnored()
        {
            // Arrange
            PreTest();

            // Act
            var map = new IgnoreMap();
            var propertyMap = map.PropertyMaps.Single();

            // Assert
            Assert.True(propertyMap.Ignored);
        }

        [Fact]
        public void PropertyInfoNameShouldBeId()
        {
            // Arrange
            PreTest();

            // Act
            var map = new MapWithOnePropertyMap();
            var propertyMap = map.PropertyMaps.Single();

            // Assert
            Assert.Equal("Id", propertyMap.PropertyInfo.Name);
        }

        [Fact]
        public void FluentMapperInitializeShouldAddEntityMap()
        {
            // Arrange
            PreTest();

            // Act
            FluentMapper.Initialize(c => c.AddMap(new MapWithOnePropertyMap()));
            var entityMap = FluentMapper.EntityMaps.Single();

            // Assert
            Assert.Single(FluentMapper.EntityMaps);
            Assert.Equal(typeof(TestEntity), entityMap.Key);
            Assert.IsType<MapWithOnePropertyMap>(entityMap.Value);
        }

        [Fact]
        public void FluentMapperInitializeShouldAddDapperTypeMap()
        {
            // Arrange
            PreTest();

            // Act
            FluentMapper.Initialize(c => c.AddMap(new MapWithOnePropertyMap()));
            var typeMap = SqlMapper.GetTypeMap(typeof(TestEntity));

            // Assert
            Assert.NotNull(typeMap);
            var member = typeMap.GetMember("test");
            Assert.Equal(typeof(TestEntity).GetProperty(nameof(TestEntity.Id)), member.Property);
        }

        [Fact]
        public void PropertyMapShouldMapInheritedProperies()
        {
            // Arrange
            PreTest();

            // Act
            var map = new DerivedMap();
            var idMap = map.PropertyMaps.First();
            var nameMap = map.PropertyMaps.Skip(1).First();

            // Assert
            // todo: should be ReflectedType so the type is DerivedTestEntity
            Assert.Equal(typeof(TestEntity), idMap.PropertyInfo.DeclaringType);
            Assert.Equal(typeof(DerivedTestEntity), nameMap.PropertyInfo.DeclaringType);
        }

        [Fact]
        public void PropertyMapShouldMapValueObjectProperties()
        {
            PreTest();

            var map = new ValueObjectMap();
            var email = map.PropertyMaps.First();
            Assert.Equal(typeof(EmailTestValueObject), email.PropertyInfo.DeclaringType);
        }

        [Fact]
        public void PropertyMapShouldDistinguishNestedPropertiesWithSameTerminalName()
        {
            PreTest();

            var map = new NestedLevelMap();

            Assert.Equal(2, map.PropertyMaps.Count);
        }

        [Fact]
        public void DuplicateNestedPropertyPathShouldThrow_Exception()
        {
            PreTest();

            var exception = Assert.Throws<FluentMapConfigurationException>(() => new DuplicateNestedLevelMap());
            Assert.Contains("Rank.Level", exception.Message);
        }

        private static void PreTest()
        {
            FluentMapper.Reset(typeof(TestEntity), typeof(DerivedTestEntity), typeof(ValueObjectTestEntity));
        }

        private class IgnoreMap : EntityMap<TestEntity>
        {
            public IgnoreMap()
            {
                Map(p => p.Id).Ignore();
            }
        }

        private class CaseInsensitveMap : EntityMap<TestEntity>
        {
            public CaseInsensitveMap()
            {
                Map(p => p.Id).ToColumn("test", caseSensitive: false);
            }
        }

        private class MapWithOnePropertyMap : EntityMap<TestEntity>
        {
            public MapWithOnePropertyMap()
            {
                Map(p => p.Id).ToColumn("test");
            }
        }

        private class DerivedMap : EntityMap<DerivedTestEntity>
        {
            public DerivedMap()
            {
                Map(p => p.Id).ToColumn("intId");
                Map(p => p.Name).ToColumn("strName");
            }
        }

        private class MapWithDuplicateMapping : EntityMap<TestEntity>
        {
            public MapWithDuplicateMapping()
            {
                Map(p => p.Id).ToColumn("id");
                Map(p => p.Id).ToColumn("id2");
            }
        }

        private class EmptyMap : EntityMap<TestEntity>
        {
        }

        private class ValueObjectMap : EntityMap<ValueObjectTestEntity>
        {
            public ValueObjectMap()
            {
                Map(x => x.Email.Address).ToColumn("email");
            }
        }

        private class NestedLevelMap : EntityMap<NestedLevelEntity>
        {
            public NestedLevelMap()
            {
                Map(x => x.Rank.Level).ToColumn("rank_level");
                Map(x => x.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private class DuplicateNestedLevelMap : EntityMap<NestedLevelEntity>
        {
            public DuplicateNestedLevelMap()
            {
                Map(x => x.Rank.Level).ToColumn("rank_level");
                Map(x => x.Rank.Level).ToColumn("rank_level_again");
            }
        }

        private class NestedLevelEntity
        {
            public RankInfo Rank { get; set; }

            public SeniorityInfo Seniority { get; set; }
        }

        private class RankInfo
        {
            public int Level { get; set; }
        }

        private class SeniorityInfo
        {
            public int Level { get; set; }
        }
    }
}
