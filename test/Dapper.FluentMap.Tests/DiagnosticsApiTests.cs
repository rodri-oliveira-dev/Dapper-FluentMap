using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Diagnostics;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Naming;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class DiagnosticsApiTests
    {
        [Fact]
        public void ValidateShouldSucceedForValidConfigurationAndBeRepeatable()
        {
            PreTest(typeof(ExplicitDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitDiagnosticMap()));

                FluentMapper.Validate();
                FluentMapper.Validate();

                Assert.True(FluentMapper.EntityMaps.ContainsKey(typeof(ExplicitDiagnosticEntity)));
                Assert.Equal(0, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                PreTest(typeof(ExplicitDiagnosticEntity));
            }
        }

        [Fact]
        public void ValidateShouldAggregateErrorsFromCurrentConfiguration()
        {
            PreTest(typeof(InvalidEmptyColumnEntity), typeof(InvalidForeignMetadataEntity));

            try
            {
                FluentMapper.EntityMaps.TryAdd(typeof(InvalidEmptyColumnEntity), new EmptyColumnMap());
                FluentMapper.EntityMaps.TryAdd(typeof(InvalidForeignMetadataEntity), new ForeignMetadataMap());

                var exception = Assert.Throws<FluentMapConfigurationException>(() => FluentMapper.Validate());

                Assert.Contains("2 errors", exception.Message);
                Assert.Contains(typeof(InvalidEmptyColumnEntity).FullName, exception.Message);
                Assert.Contains(typeof(InvalidForeignMetadataEntity).FullName, exception.Message);
                Assert.Contains("empty column name", exception.Message);
                Assert.Contains("not compatible", exception.Message);
            }
            finally
            {
                PreTest(typeof(InvalidEmptyColumnEntity), typeof(InvalidForeignMetadataEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeExplicitMappingAndDapperFallback()
        {
            PreTest(typeof(ExplicitDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitDiagnosticMap()));

                var explanation = FluentMapper.Explain<ExplicitDiagnosticEntity>();

                var id = SingleMember(explanation, nameof(ExplicitDiagnosticEntity.Id));
                var name = SingleMember(explanation, nameof(ExplicitDiagnosticEntity.Name));

                Assert.Equal(typeof(ExplicitDiagnosticEntity), explanation.EntityType);
                Assert.Equal(typeof(ExplicitDiagnosticMap), explanation.EntityMapType);
                Assert.Equal("explicit_id", id.ColumnName);
                Assert.Equal(MappingSource.Explicit, id.Source);
                Assert.Equal("Name", name.ColumnName);
                Assert.Equal(MappingSource.DapperDefault, name.Source);
            }
            finally
            {
                PreTest(typeof(ExplicitDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeInheritedMappings()
        {
            PreTest(typeof(DiagnosticBaseEntity), typeof(DiagnosticDerivedEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new DiagnosticBaseMap());
                    c.AddMap(new DiagnosticDerivedMap());
                });

                var explanation = FluentMapper.Explain<DiagnosticDerivedEntity>();
                var id = SingleMember(explanation, nameof(DiagnosticBaseEntity.Id));

                Assert.Equal("base_id", id.ColumnName);
                Assert.Equal(MappingSource.Inherited, id.Source);
                Assert.Equal(typeof(DiagnosticBaseEntity), id.InheritedFrom);
            }
            finally
            {
                PreTest(typeof(DiagnosticBaseEntity), typeof(DiagnosticDerivedEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeConventionMappings()
        {
            PreTest(typeof(ConventionDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddConvention<DiagnosticPrefixConvention>().ForEntity<ConventionDiagnosticEntity>());

                var explanation = FluentMapper.Explain<ConventionDiagnosticEntity>();
                var name = SingleMember(explanation, nameof(ConventionDiagnosticEntity.Name));

                Assert.Equal("colName", name.ColumnName);
                Assert.Equal(MappingSource.Convention, name.Source);
                Assert.Equal(typeof(DiagnosticPrefixConvention), name.ConventionType);
                Assert.Contains(typeof(DiagnosticPrefixConvention), explanation.ConventionTypes);
            }
            finally
            {
                PreTest(typeof(ConventionDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeNamingPolicyMappings()
        {
            PreTest(typeof(PolicyDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false).ForEntity<PolicyDiagnosticEntity>());

                var explanation = FluentMapper.Explain<PolicyDiagnosticEntity>();
                var customerId = SingleMember(explanation, nameof(PolicyDiagnosticEntity.CustomerId));

                Assert.Equal("customer_id", customerId.ColumnName);
                Assert.Equal(MappingSource.NamingPolicy, customerId.Source);
                Assert.False(customerId.CaseSensitive);
                Assert.NotNull(customerId.ConventionType);
            }
            finally
            {
                PreTest(typeof(PolicyDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeConstructorParameterDestinations()
        {
            PreTest(typeof(ImmutableDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ImmutableDiagnosticMap()));

                var explanation = FluentMapper.Explain<ImmutableDiagnosticEntity>();
                var fullName = SingleMember(explanation, nameof(ImmutableDiagnosticEntity.FullName));

                Assert.Equal("full_name", fullName.ColumnName);
                Assert.Equal(MappingSource.Explicit, fullName.Source);
                Assert.Contains(fullName.ConstructorParameters, p => p.Name == "fullName" && p.ParameterType == typeof(string));
            }
            finally
            {
                PreTest(typeof(ImmutableDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldDescribeUnconfiguredEntityWithDapperDefaultFallback()
        {
            PreTest(typeof(UnconfiguredDiagnosticEntity));

            try
            {
                var explanation = FluentMapper.Explain<UnconfiguredDiagnosticEntity>();
                var createdAt = SingleMember(explanation, nameof(UnconfiguredDiagnosticEntity.CreatedAt));

                Assert.Null(explanation.EntityMapType);
                Assert.Empty(explanation.ConventionTypes);
                Assert.Contains("Dapper default mapping", explanation.Diagnostics.Single());
                Assert.Equal("CreatedAt", createdAt.ColumnName);
                Assert.Equal(MappingSource.DapperDefault, createdAt.Source);
            }
            finally
            {
                PreTest(typeof(UnconfiguredDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldDistinguishSameTerminalMemberPath()
        {
            PreTest(typeof(NestedDiagnosticsEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new NestedDiagnosticsMap()));

                var explanation = FluentMapper.Explain<NestedDiagnosticsEntity>();

                Assert.Contains(explanation.Members, m => m.MemberPath == "Rank.Level" && m.ColumnName == "rank_level");
                Assert.Contains(explanation.Members, m => m.MemberPath == "Seniority.Level" && m.ColumnName == "seniority_level");
            }
            finally
            {
                PreTest(typeof(NestedDiagnosticsEntity));
            }
        }

        [Fact]
        public void ExplainMetadataShouldBeReadOnlySnapshots()
        {
            PreTest(typeof(ExplicitDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitDiagnosticMap()));

                var explanation = FluentMapper.Explain<ExplicitDiagnosticEntity>();
                var members = Assert.IsAssignableFrom<ICollection<MemberMappingExplanation>>(explanation.Members);
                var conventionTypes = Assert.IsAssignableFrom<ICollection<Type>>(explanation.ConventionTypes);

                Assert.True(members.IsReadOnly);
                Assert.True(conventionTypes.IsReadOnly);
                Assert.Throws<NotSupportedException>(() => members.Add(explanation.Members[0]));
                Assert.Throws<NotSupportedException>(() => conventionTypes.Add(typeof(DiagnosticPrefixConvention)));
            }
            finally
            {
                PreTest(typeof(ExplicitDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainRepeatedCallsShouldBeConsistentAndAvoidCacheSideEffects()
        {
            PreTest(typeof(ExplicitDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ExplicitDiagnosticMap()));

                var first = FluentMapper.Explain<ExplicitDiagnosticEntity>();
                var second = FluentMapper.Explain<ExplicitDiagnosticEntity>();

                Assert.Equal(
                    first.Members.Select(m => m.MemberPath + ":" + m.ColumnName + ":" + m.Source),
                    second.Members.Select(m => m.MemberPath + ":" + m.ColumnName + ":" + m.Source));
                Assert.Equal(0, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                PreTest(typeof(ExplicitDiagnosticEntity));
            }
        }

        [Fact]
        public void ExplainShouldMentionGeneratedQueryMaterializersWhenDescriptorsAreRegistered()
        {
            PreTest(typeof(ExplicitDiagnosticEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ExplicitDiagnosticMap());
                    c.AddGeneratedMaterializer<ExplicitDiagnosticEntity>(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("explicit_id", nameof(ExplicitDiagnosticEntity.Id))
                        },
                        record => new ExplicitDiagnosticEntity { Id = Convert.ToInt32(record.GetValue(0)) });
                });

                var explanation = FluentMapper.Explain<ExplicitDiagnosticEntity>();

                Assert.Contains(
                    explanation.Diagnostics,
                    diagnostic => diagnostic.Contains("generated QueryMapped materializer descriptor") &&
                                  diagnostic.Contains("runtime materializer fallback"));
            }
            finally
            {
                PreTest(typeof(ExplicitDiagnosticEntity));
            }
        }

        private static MemberMappingExplanation SingleMember(MappingExplanation explanation, string memberPath)
        {
            return explanation.Members.Single(m => m.MemberPath == memberPath);
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class ExplicitDiagnosticEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ExplicitDiagnosticMap : EntityMap<ExplicitDiagnosticEntity>
        {
            public ExplicitDiagnosticMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class DiagnosticBaseEntity
        {
            public int Id { get; set; }
        }

        private class DiagnosticDerivedEntity : DiagnosticBaseEntity
        {
            public string Name { get; set; }
        }

        private class DiagnosticBaseMap : EntityMap<DiagnosticBaseEntity>
        {
            public DiagnosticBaseMap()
            {
                Map(e => e.Id).ToColumn("base_id");
            }
        }

        private class DiagnosticDerivedMap : EntityMap<DiagnosticDerivedEntity>
        {
            public DiagnosticDerivedMap()
            {
                IncludeBase<DiagnosticBaseEntity>();
            }
        }

        private class ConventionDiagnosticEntity
        {
            public string Name { get; set; }
        }

        private class DiagnosticPrefixConvention : Convention
        {
            public DiagnosticPrefixConvention()
            {
                Properties()
                    .Configure(c => c.HasPrefix("col"));
            }
        }

        private class PolicyDiagnosticEntity
        {
            public int CustomerId { get; set; }
        }

        private class ImmutableDiagnosticEntity
        {
            public ImmutableDiagnosticEntity(int id, string fullName)
            {
                Id = id;
                FullName = fullName;
            }

            public int Id { get; }

            public string FullName { get; }
        }

        private class ImmutableDiagnosticMap : EntityMap<ImmutableDiagnosticEntity>
        {
            public ImmutableDiagnosticMap()
            {
                Map(e => e.Id).ToColumn("person_id");
                Map(e => e.FullName).ToColumn("full_name");
            }
        }

        private class UnconfiguredDiagnosticEntity
        {
            public DateTime CreatedAt { get; set; }
        }

        private class NestedDiagnosticsEntity
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

        private class NestedDiagnosticsMap : EntityMap<NestedDiagnosticsEntity>
        {
            public NestedDiagnosticsMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
                Map(e => e.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private class InvalidEmptyColumnEntity
        {
            public int Id { get; set; }
        }

        private class InvalidForeignMetadataEntity
        {
            public int Id { get; set; }
        }

        private class ForeignEntity
        {
            public int Id { get; set; }
        }

        private class EmptyColumnMap : IEntityMap<InvalidEmptyColumnEntity>
        {
            public EmptyColumnMap()
            {
                PropertyMaps = new List<IPropertyMap>
                {
                    new PropertyMap(typeof(InvalidEmptyColumnEntity).GetProperty(nameof(InvalidEmptyColumnEntity.Id)), string.Empty)
                };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }

        private class ForeignMetadataMap : IEntityMap<InvalidForeignMetadataEntity>
        {
            public ForeignMetadataMap()
            {
                PropertyMaps = new List<IPropertyMap>
                {
                    new PropertyMap(typeof(ForeignEntity).GetProperty(nameof(ForeignEntity.Id)), "foreign_id")
                };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }
    }
}
