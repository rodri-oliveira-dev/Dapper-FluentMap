using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class DapperIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void DefaultDapperMappingShouldMaterializeProperties()
        {
            ResetMapper(typeof(DefaultDapperEntity));

            try
            {
                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<DefaultDapperEntity>(
                        "SELECT 42 AS Id, 'Ada' AS Name;");

                    Assert.Equal(42, entity.Id);
                    Assert.Equal("Ada", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(DefaultDapperEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExplicitMappingShouldMaterializeConfiguredColumn()
        {
            ResetMapper(typeof(ExplicitMappingEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitMappingMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ExplicitMappingEntity>(
                        "SELECT 7 AS person_id, 'Grace' AS Name;");

                    Assert.Equal(7, entity.Id);
                    Assert.Equal("Grace", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(ExplicitMappingEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void IgnoredExplicitMappingShouldNotMaterializeSelectedColumn()
        {
            ResetMapper(typeof(IgnoredMappingEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new IgnoredMappingMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<IgnoredMappingEntity>(
                        "SELECT 9 AS person_id, 'do-not-map' AS secret;");

                    Assert.Equal(9, entity.Id);
                    Assert.Equal("initial", entity.Secret);
                }
            }
            finally
            {
                ResetMapper(typeof(IgnoredMappingEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ConventionShouldMaterializeConfiguredColumns()
        {
            ResetMapper(typeof(ConventionEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddConvention<PrefixConvention>().ForEntity<ConventionEntity>());

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ConventionEntity>(
                        "SELECT 11 AS colId, 'Linus' AS colName;");

                    Assert.Equal(11, entity.Id);
                    Assert.Equal("Linus", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(ConventionEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExplicitMappingAndConventionShouldMaterializeTogether()
        {
            ResetMapper(typeof(ComposedMappingEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ComposedMappingMap());
                    c.AddConvention<PrefixConvention>().ForEntity<ComposedMappingEntity>();
                });

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ComposedMappingEntity>(
                        "SELECT 13 AS person_id, 'Margaret' AS colName;");

                    Assert.Equal(13, entity.Id);
                    Assert.Equal("Margaret", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(ComposedMappingEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExplicitMappingShouldOverrideConventionDuringMaterialization()
        {
            ResetMapper(typeof(ExplicitOverrideIntegrationEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ExplicitOverrideIntegrationMap());
                    c.AddConvention<PrefixConvention>().ForEntity<ExplicitOverrideIntegrationEntity>();
                });

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<ExplicitOverrideIntegrationEntity>(
                        "SELECT 17 AS person_id, 99 AS colId, 'Barbara' AS colName;");

                    Assert.Equal(17, entity.Id);
                    Assert.Equal("Barbara", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(ExplicitOverrideIntegrationEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExpressionResolvedPropertyShouldMaterializeWhenNameCollidesWithStringMember()
        {
            ResetMapper(typeof(StringMemberNameCollisionEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new StringMemberNameCollisionMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<StringMemberNameCollisionEntity>(
                        "SELECT 'markdown' AS format_text;");

                    Assert.Equal("markdown", entity.Format);
                }
            }
            finally
            {
                ResetMapper(typeof(StringMemberNameCollisionEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ExpressionResolvedPropertyShouldMaterializeWhenNameCollidesWithAnotherStringMember()
        {
            ResetMapper(typeof(AnotherStringMemberNameCollisionEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new AnotherStringMemberNameCollisionMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<AnotherStringMemberNameCollisionEntity>(
                        "SELECT 'joined' AS join_text;");

                    Assert.Equal("joined", entity.Join);
                }
            }
            finally
            {
                ResetMapper(typeof(AnotherStringMemberNameCollisionEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void CaseInsensitiveExplicitMappingShouldMaterializeColumnWithDifferentCase()
        {
            ResetMapper(typeof(CaseInsensitiveEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CaseInsensitiveMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<CaseInsensitiveEntity>(
                        "SELECT 23 AS PERSON_ID;");

                    Assert.Equal(23, entity.Id);
                }
            }
            finally
            {
                ResetMapper(typeof(CaseInsensitiveEntity));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void ResetMapper(params System.Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class DefaultDapperEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ExplicitMappingEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ExplicitMappingMap : EntityMap<ExplicitMappingEntity>
        {
            public ExplicitMappingMap()
            {
                Map(e => e.Id).ToColumn("person_id");
            }
        }

        private class IgnoredMappingEntity
        {
            public int Id { get; set; }

            public string Secret { get; set; } = "initial";
        }

        private class IgnoredMappingMap : EntityMap<IgnoredMappingEntity>
        {
            public IgnoredMappingMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.Secret).ToColumn("secret").Ignore();
            }
        }

        private class ConventionEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ComposedMappingEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ComposedMappingMap : EntityMap<ComposedMappingEntity>
        {
            public ComposedMappingMap()
            {
                Map(e => e.Id).ToColumn("person_id");
            }
        }

        private class ExplicitOverrideIntegrationEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ExplicitOverrideIntegrationMap : EntityMap<ExplicitOverrideIntegrationEntity>
        {
            public ExplicitOverrideIntegrationMap()
            {
                Map(e => e.Id).ToColumn("person_id");
            }
        }

        private class StringMemberNameCollisionEntity
        {
            public string Format { get; set; }
        }

        private class StringMemberNameCollisionMap : EntityMap<StringMemberNameCollisionEntity>
        {
            public StringMemberNameCollisionMap()
            {
                Map(e => e.Format).ToColumn("format_text");
            }
        }

        private class AnotherStringMemberNameCollisionEntity
        {
            public string Join { get; set; }
        }

        private class AnotherStringMemberNameCollisionMap : EntityMap<AnotherStringMemberNameCollisionEntity>
        {
            public AnotherStringMemberNameCollisionMap()
            {
                Map(e => e.Join).ToColumn("join_text");
            }
        }

        private class CaseInsensitiveEntity
        {
            public int Id { get; set; }
        }

        private class CaseInsensitiveMap : EntityMap<CaseInsensitiveEntity>
        {
            public CaseInsensitiveMap()
            {
                Map(e => e.Id).ToColumn("person_id", caseSensitive: false);
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
    }
}
