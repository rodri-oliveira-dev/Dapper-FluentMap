using System;
using Dapper;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests.HistoricalRegression
{
    public class HistoricalCoreRegressionTests
    {
        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void PropertyNamedLikeBclMemberShouldMapExpressionProperty()
        {
            ResetMapper(typeof(MemberCollisionEntity));

            try
            {
                // Historical issue #114.
                FluentMapper.Initialize(configuration => configuration.AddMap(new MemberCollisionMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<MemberCollisionEntity>(
                        "SELECT 'markdown' AS format_text;");

                    Assert.Equal("markdown", entity.Format);
                }
            }
            finally
            {
                ResetMapper(typeof(MemberCollisionEntity));
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void IgnoredPropertySelectedByDapperShouldRemainUnmappedWithoutThrowing()
        {
            ResetMapper(typeof(IgnoredColumnEntity));

            try
            {
                // Historical issue #133.
                FluentMapper.Initialize(configuration => configuration.AddMap(new IgnoredColumnMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<IgnoredColumnEntity>(
                        "SELECT 7 AS id, 'server-secret' AS secret;");

                    Assert.Equal(7, entity.Id);
                    Assert.Equal("initial", entity.Secret);
                }
            }
            finally
            {
                ResetMapper(typeof(IgnoredColumnEntity));
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void NestedMemberPathsWithSameTerminalNameShouldMaterializeDistinctValues()
        {
            ResetMapper(typeof(NestedLevelEntity));

            try
            {
                // Historical issue #126.
                FluentMapper.Initialize(configuration => configuration.AddMap(new NestedLevelMap()));

                using (var connection = OpenConnection())
                {
                    var entity = connection.QueryMappedSingle<NestedLevelEntity>(
                        "SELECT 10 AS rank_level, 20 AS seniority_level, 30 AS completed_profile_level;");

                    Assert.NotNull(entity.Rank);
                    Assert.NotNull(entity.Seniority);
                    Assert.NotNull(entity.CompletedProfile);
                    Assert.Equal(10, entity.Rank.Level);
                    Assert.Equal(20, entity.Seniority.Level);
                    Assert.Equal(30, entity.CompletedProfile.Level);
                }
            }
            finally
            {
                ResetMapper(typeof(NestedLevelEntity));
            }
        }

        [Fact]
        [Trait("Category", "HistoricalRegression")]
        public void GeneratedAndRuntimeMaterializersShouldAgreeForHistoricalReadSemantics()
        {
            // Historical issues #94, #123, #126, #130 and #133.
            var generated = QueryHistoricalReadEntity(useGeneratedMaterializer: true);
            var runtime = QueryHistoricalReadEntity(useGeneratedMaterializer: false);

            Assert.Equal(runtime.Id, generated.Id);
            Assert.Equal(runtime.ReadOnlyValue, generated.ReadOnlyValue);
            Assert.Equal(runtime.ComputedValue, generated.ComputedValue);
            Assert.Equal(runtime.CreatedAt, generated.CreatedAt);
            Assert.Equal(runtime.Rank.Level, generated.Rank.Level);
            Assert.Equal(runtime.Seniority.Level, generated.Seniority.Level);
            Assert.Equal(runtime.Secret, generated.Secret);
        }

        private static HistoricalReadEntity QueryHistoricalReadEntity(bool useGeneratedMaterializer)
        {
            ResetMapper(typeof(HistoricalReadEntity));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new HistoricalReadMap());

                    if (useGeneratedMaterializer)
                    {
                        configuration.AddGeneratedMaterializer(
                            new[]
                            {
                                GeneratedMaterializerColumn.Map("id", nameof(HistoricalReadEntity.Id)),
                                GeneratedMaterializerColumn.Map("read_only_value", nameof(HistoricalReadEntity.ReadOnlyValue)),
                                GeneratedMaterializerColumn.Map("computed_value", nameof(HistoricalReadEntity.ComputedValue)),
                                GeneratedMaterializerColumn.Map("created_at", nameof(HistoricalReadEntity.CreatedAt)),
                                GeneratedMaterializerColumn.Map("rank_level", "Rank.Level"),
                                GeneratedMaterializerColumn.Map("seniority_level", "Seniority.Level"),
                                GeneratedMaterializerColumn.Ignore("secret")
                            },
                            record => new HistoricalReadEntity
                            {
                                Id = Convert.ToInt32(record.GetValue(0)),
                                ReadOnlyValue = Convert.ToString(record.GetValue(1)),
                                ComputedValue = Convert.ToString(record.GetValue(2)),
                                CreatedAt = Convert.ToString(record.GetValue(3)),
                                Rank = new HistoricalLevel { Level = Convert.ToInt32(record.GetValue(4)) },
                                Seniority = new HistoricalLevel { Level = Convert.ToInt32(record.GetValue(5)) }
                            });
                    }
                });

                using (var connection = OpenConnection())
                {
                    return connection.QueryMappedSingle<HistoricalReadEntity>(
                        "SELECT 1 AS id, 'read' AS read_only_value, 'computed' AS computed_value, '2026-07-28' AS created_at, 5 AS rank_level, 9 AS seniority_level, 'server-secret' AS secret;");
                }
            }
            finally
            {
                ResetMapper(typeof(HistoricalReadEntity));
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

        private sealed class MemberCollisionEntity
        {
            public string Format { get; set; }
        }

        private sealed class MemberCollisionMap : EntityMap<MemberCollisionEntity>
        {
            public MemberCollisionMap()
            {
                Map(entity => entity.Format).ToColumn("format_text");
            }
        }

        private sealed class IgnoredColumnEntity
        {
            public int Id { get; set; }

            public string Secret { get; set; } = "initial";
        }

        private sealed class IgnoredColumnMap : EntityMap<IgnoredColumnEntity>
        {
            public IgnoredColumnMap()
            {
                Map(entity => entity.Id).ToColumn("id");
                Map(entity => entity.Secret).ToColumn("secret").Ignore();
            }
        }

        private sealed class NestedLevelEntity
        {
            public HistoricalLevel Rank { get; set; }

            public HistoricalLevel Seniority { get; set; }

            public HistoricalLevel CompletedProfile { get; set; }
        }

        private sealed class NestedLevelMap : EntityMap<NestedLevelEntity>
        {
            public NestedLevelMap()
            {
                Map(entity => entity.Rank.Level).ToColumn("rank_level");
                Map(entity => entity.Seniority.Level).ToColumn("seniority_level");
                Map(entity => entity.CompletedProfile.Level).ToColumn("completed_profile_level");
            }
        }

        private sealed class HistoricalReadEntity
        {
            public int Id { get; set; }

            public string ReadOnlyValue { get; set; }

            public string ComputedValue { get; set; }

            public string CreatedAt { get; set; }

            public HistoricalLevel Rank { get; set; }

            public HistoricalLevel Seniority { get; set; }

            public string Secret { get; set; } = "initial";
        }

        private sealed class HistoricalReadMap : EntityMap<HistoricalReadEntity>
        {
            public HistoricalReadMap()
            {
                Map(entity => entity.Id).ToColumn("id");
                Map(entity => entity.ReadOnlyValue).ToColumn("read_only_value").ReadOnly();
                Map(entity => entity.ComputedValue).ToColumn("computed_value").Computed();
                Map(entity => entity.CreatedAt).ToColumn("created_at").DatabaseDefaultOnInsert();
                Map(entity => entity.Rank.Level).ToColumn("rank_level");
                Map(entity => entity.Seniority.Level).ToColumn("seniority_level");
                Map(entity => entity.Secret).ToColumn("secret").Ignore();
            }
        }

        private sealed class HistoricalLevel
        {
            public int Level { get; set; }
        }
    }
}
