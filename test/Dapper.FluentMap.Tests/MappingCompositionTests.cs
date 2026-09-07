using System;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MappingCompositionTests
    {
        [Fact]
        public void ExplicitMappingShouldResolveColumn()
        {
            PreTest(typeof(ExplicitOnlyEntity));

            FluentMapper.Initialize(c => c.AddMap(new ExplicitOnlyMap()));

            var member = SqlMapper.GetTypeMap(typeof(ExplicitOnlyEntity)).GetMember("explicit_id");

            Assert.NotNull(member);
            Assert.Equal(typeof(ExplicitOnlyEntity).GetProperty(nameof(ExplicitOnlyEntity.Id)), member.Property);
        }

        [Fact]
        public void IncludedBaseMappingShouldResolveColumnForDerivedEntity()
        {
            PreTest(typeof(BaseUser), typeof(AdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new BaseUserMap());
                c.AddMap(new AdminUserMap());
            });

            var member = SqlMapper.GetTypeMap(typeof(AdminUser)).GetMember("user_id");

            Assert.NotNull(member);
            Assert.Equal(typeof(BaseUser).GetProperty(nameof(BaseUser.Id)), member.Property);
        }

        [Fact]
        public void ConventionShouldResolveColumn()
        {
            PreTest(typeof(ConventionOnlyEntity));

            FluentMapper.Initialize(c => c.AddConvention<PrefixConvention>().ForEntity<ConventionOnlyEntity>());

            var member = SqlMapper.GetTypeMap(typeof(ConventionOnlyEntity)).GetMember("colName");

            Assert.NotNull(member);
            Assert.Equal(typeof(ConventionOnlyEntity).GetProperty(nameof(ConventionOnlyEntity.Name)), member.Property);
        }

        [Fact]
        public void DefaultTypeMapShouldResolveColumnWhenNoExplicitMappingOrConventionMatches()
        {
            PreTest(typeof(DefaultFallbackEntity));

            FluentMapper.Initialize(c => c.AddMap(new DefaultFallbackMap()));

            var member = SqlMapper.GetTypeMap(typeof(DefaultFallbackEntity)).GetMember("Name");

            Assert.NotNull(member);
            Assert.Equal(typeof(DefaultFallbackEntity).GetProperty(nameof(DefaultFallbackEntity.Name)), member.Property);
        }

        [Fact]
        public void ExplicitMappingAndConventionShouldResolveDifferentPropertiesTogether()
        {
            PreTest(typeof(DifferentPropertiesEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new DifferentPropertiesMap());
                c.AddConvention<PrefixConvention>().ForEntity<DifferentPropertiesEntity>();
            });

            var explicitMember = SqlMapper.GetTypeMap(typeof(DifferentPropertiesEntity)).GetMember("explicit_id");
            var conventionMember = SqlMapper.GetTypeMap(typeof(DifferentPropertiesEntity)).GetMember("colName");

            Assert.NotNull(explicitMember);
            Assert.NotNull(conventionMember);
            Assert.Equal(typeof(DifferentPropertiesEntity).GetProperty(nameof(DifferentPropertiesEntity.Id)), explicitMember.Property);
            Assert.Equal(typeof(DifferentPropertiesEntity).GetProperty(nameof(DifferentPropertiesEntity.Name)), conventionMember.Property);
        }

        [Fact]
        public void ExplicitMappingShouldOverrideConventionForSameProperty()
        {
            PreTest(typeof(ExplicitOverrideEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new ExplicitOverrideMap());
                c.AddConvention<PrefixConvention>().ForEntity<ExplicitOverrideEntity>();
            });

            var explicitMember = SqlMapper.GetTypeMap(typeof(ExplicitOverrideEntity)).GetMember("explicit_id");
            var conventionMember = SqlMapper.GetTypeMap(typeof(ExplicitOverrideEntity)).GetMember("colId");

            Assert.NotNull(explicitMember);
            Assert.Null(conventionMember);
            Assert.Equal(typeof(ExplicitOverrideEntity).GetProperty(nameof(ExplicitOverrideEntity.Id)), explicitMember.Property);
        }

        [Fact]
        public void ExplicitNestedMappingShouldNotOverrideConventionForDistinctPropertyWithSameTerminalName()
        {
            PreTest(typeof(NestedExplicitWithConventionEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new NestedExplicitWithConventionMap());
                c.AddConvention<PrefixConvention>().ForEntity<NestedExplicitWithConventionEntity>();
            });

            var conventionMember = SqlMapper.GetTypeMap(typeof(NestedExplicitWithConventionEntity)).GetMember("colLevel");

            Assert.NotNull(conventionMember);
            Assert.Equal(typeof(NestedExplicitWithConventionEntity).GetProperty(nameof(NestedExplicitWithConventionEntity.Level)), conventionMember.Property);
        }

        [Fact]
        public void RegistrationOrderShouldNotMatterWhenExplicitMappingIsAddedFirst()
        {
            PreTest(typeof(MapFirstEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new MapFirstMap());
                c.AddConvention<PrefixConvention>().ForEntity<MapFirstEntity>();
            });

            AssertComposition(typeof(MapFirstEntity), nameof(MapFirstEntity.Id), nameof(MapFirstEntity.Name));
        }

        [Fact]
        public void RegistrationOrderShouldNotMatterWhenConventionIsAddedFirst()
        {
            PreTest(typeof(ConventionFirstEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddConvention<PrefixConvention>().ForEntity<ConventionFirstEntity>();
                c.AddMap(new ConventionFirstMap());
            });

            AssertComposition(typeof(ConventionFirstEntity), nameof(ConventionFirstEntity.Id), nameof(ConventionFirstEntity.Name));
        }

        [Fact]
        public void CompositionShouldPreserveCaseSensitivityForExplicitMappingsAndConventions()
        {
            PreTest(typeof(CaseSensitivityEntity));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new CaseSensitivityMap());
                c.AddConvention<CaseInsensitivePrefixConvention>().ForEntity<CaseSensitivityEntity>();
            });

            var exactExplicitMember = SqlMapper.GetTypeMap(typeof(CaseSensitivityEntity)).GetMember("exact_id");
            var wrongCaseExplicitMember = SqlMapper.GetTypeMap(typeof(CaseSensitivityEntity)).GetMember("EXACT_ID");
            var conventionMember = SqlMapper.GetTypeMap(typeof(CaseSensitivityEntity)).GetMember("COLNAME");

            Assert.NotNull(exactExplicitMember);
            Assert.Null(wrongCaseExplicitMember);
            Assert.NotNull(conventionMember);
            Assert.Equal(typeof(CaseSensitivityEntity).GetProperty(nameof(CaseSensitivityEntity.Id)), exactExplicitMember.Property);
            Assert.Equal(typeof(CaseSensitivityEntity).GetProperty(nameof(CaseSensitivityEntity.Name)), conventionMember.Property);
        }

        private static void AssertComposition(Type entityType, string explicitPropertyName, string conventionPropertyName)
        {
            var explicitMember = SqlMapper.GetTypeMap(entityType).GetMember("explicit_id");
            var conventionMember = SqlMapper.GetTypeMap(entityType).GetMember("colName");

            Assert.NotNull(explicitMember);
            Assert.NotNull(conventionMember);
            Assert.Equal(entityType.GetProperty(explicitPropertyName), explicitMember.Property);
            Assert.Equal(entityType.GetProperty(conventionPropertyName), conventionMember.Property);
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class ExplicitOnlyEntity
        {
            public int Id { get; set; }
        }

        private class ExplicitOnlyMap : EntityMap<ExplicitOnlyEntity>
        {
            public ExplicitOnlyMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class BaseUser
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class AdminUser : BaseUser
        {
            public string Permission { get; set; }
        }

        private class BaseUserMap : EntityMap<BaseUser>
        {
            public BaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class AdminUserMap : EntityMap<AdminUser>
        {
            public AdminUserMap()
            {
                IncludeBase<BaseUser>();
            }
        }

        private class ConventionOnlyEntity
        {
            public string Name { get; set; }
        }

        private class DefaultFallbackEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class DefaultFallbackMap : EntityMap<DefaultFallbackEntity>
        {
            public DefaultFallbackMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class DifferentPropertiesEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class DifferentPropertiesMap : EntityMap<DifferentPropertiesEntity>
        {
            public DifferentPropertiesMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class ExplicitOverrideEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ExplicitOverrideMap : EntityMap<ExplicitOverrideEntity>
        {
            public ExplicitOverrideMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class NestedExplicitWithConventionEntity
        {
            public int Level { get; set; }

            public NestedRankInfo Rank { get; set; }
        }

        private class NestedRankInfo
        {
            public int Level { get; set; }
        }

        private class NestedExplicitWithConventionMap : EntityMap<NestedExplicitWithConventionEntity>
        {
            public NestedExplicitWithConventionMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
            }
        }

        private class MapFirstEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class MapFirstMap : EntityMap<MapFirstEntity>
        {
            public MapFirstMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class ConventionFirstEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ConventionFirstMap : EntityMap<ConventionFirstEntity>
        {
            public ConventionFirstMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class CaseSensitivityEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class CaseSensitivityMap : EntityMap<CaseSensitivityEntity>
        {
            public CaseSensitivityMap()
            {
                Map(e => e.Id).ToColumn("exact_id");
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

        private class CaseInsensitivePrefixConvention : Convention
        {
            public CaseInsensitivePrefixConvention()
            {
                Properties()
                    .Configure(c => c.HasPrefix("col").IsCaseInsensitive());
            }
        }
    }
}
