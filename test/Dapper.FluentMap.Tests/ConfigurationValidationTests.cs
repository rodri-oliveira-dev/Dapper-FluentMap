using System;
using System.Collections.Generic;
using System.Reflection;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ConfigurationValidationTests
    {
        [Fact]
        public void ValidConfigurationShouldRegisterEntityMap()
        {
            PreTest(typeof(ValidEntity));

            FluentMapper.Initialize(c => c.AddMap(new ValidMap()));

            Assert.True(FluentMapper.EntityMaps.ContainsKey(typeof(ValidEntity)));
        }

        [Fact]
        public void DuplicateMemberPathShouldThrowConfigurationException()
        {
            PreTest(typeof(NestedLevelEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() => new DuplicateNestedLevelMap());

            Assert.Contains("Rank.Level", exception.Message);
            Assert.Contains(typeof(NestedLevelEntity).FullName, exception.Message);
        }

        [Fact]
        public void DistinctPathsWithSameTerminalNameShouldRemainValid()
        {
            PreTest(typeof(NestedLevelEntity));

            var map = new DistinctNestedLevelMap();

            Assert.Equal(2, map.PropertyMaps.Count);
        }

        [Fact]
        public void DuplicateEntityMapRegistrationShouldThrowConfigurationException()
        {
            PreTest(typeof(ValidEntity));

            FluentMapper.Initialize(c => c.AddMap(new ValidMap()));
            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new AlternateValidMap())));

            Assert.Contains(typeof(ValidEntity).FullName, exception.Message);
            Assert.Contains("already has a configured entity map", exception.Message);
        }

        [Fact]
        public void ExplicitColumnConflictShouldThrowConfigurationException()
        {
            PreTest(typeof(ColumnConflictEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new ColumnConflictMap())));

            Assert.Contains("shared_column", exception.Message);
            Assert.Contains(nameof(ColumnConflictEntity.Id), exception.Message);
            Assert.Contains(nameof(ColumnConflictEntity.Name), exception.Message);
            Assert.Contains(typeof(ColumnConflictEntity).FullName, exception.Message);
        }

        [Fact]
        public void CaseSensitivityColumnConflictShouldThrowConfigurationException()
        {
            PreTest(typeof(ColumnConflictEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new CaseSensitivityConflictMap())));

            Assert.Contains("case sensitivity", exception.Message);
            Assert.Contains("shared_column", exception.Message);
            Assert.Contains(typeof(ColumnConflictEntity).FullName, exception.Message);
        }

        [Fact]
        public void AmbiguousConventionShouldThrowConfigurationExceptionDuringConfiguration()
        {
            PreTest(typeof(ColumnConflictEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddConvention<AmbiguousConvention>().ForEntity<ColumnConflictEntity>()));

            Assert.Contains("shared_column", exception.Message);
            Assert.Contains(nameof(ColumnConflictEntity.Id), exception.Message);
            Assert.Contains(nameof(ColumnConflictEntity.Name), exception.Message);
            Assert.Contains(typeof(AmbiguousConvention).FullName, exception.Message);
        }

        [Fact]
        public void InvalidExpressionShouldThrowArgumentExceptionWithUsefulMessage()
        {
            PreTest(typeof(ValidEntity));

            var exception = Assert.Throws<ArgumentException>(() => new InvalidExpressionMap());

            Assert.Contains("property path", exception.Message);
            Assert.Contains("ToString", exception.Message);
        }

        [Fact]
        public void IncompatiblePropertyMetadataShouldThrowConfigurationException()
        {
            PreTest(typeof(ValidEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new IncompatibleMetadataMap())));

            Assert.Contains(typeof(ValidEntity).FullName, exception.Message);
            Assert.Contains(typeof(ForeignMetadataEntity).FullName, exception.Message);
            Assert.Contains("not compatible", exception.Message);
        }

        [Fact]
        public void ExternalPropertyMapsWithSameColumnShouldRemainValid()
        {
            PreTest(typeof(ColumnConflictEntity));

            FluentMapper.Initialize(c => c.AddMap(new ExternalColumnReuseMap()));

            Assert.True(FluentMapper.EntityMaps.ContainsKey(typeof(ColumnConflictEntity)));
        }

        [Fact]
        public void ValidPersistenceCombinationsShouldPassRuntimeValidation()
        {
            PreTest(typeof(PersistenceValidationEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ValidPersistenceValidationMap()));

                FluentMapper.Validate();

                var explanation = FluentMapper.Explain<PersistenceValidationEntity>();
                Assert.Contains(explanation.Members, member =>
                    member.MemberPath == nameof(PersistenceValidationEntity.ReadOnlyValue) &&
                    member.Persistence.ParticipatesInMaterialization &&
                    !member.Persistence.ParticipatesInInsert &&
                    !member.Persistence.ParticipatesInUpdate);
                Assert.Contains(explanation.Members, member =>
                    member.MemberPath == nameof(PersistenceValidationEntity.DefaultValue) &&
                    member.Persistence.HasDatabaseDefaultOnInsert &&
                    !member.Persistence.ParticipatesInInsert &&
                    member.Persistence.ParticipatesInUpdate);
            }
            finally
            {
                PreTest(typeof(PersistenceValidationEntity));
            }
        }

        [Fact]
        public void InvalidPersistenceMetadataShouldThrowUsefulConfigurationException()
        {
            PreTest(typeof(PersistenceValidationEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new InvalidPersistenceValidationMap())));

            Assert.Contains(nameof(PersistenceValidationEntity.ReadOnlyValue), exception.Message);
            Assert.Contains("invalid persistence metadata", exception.Message);
            Assert.Contains("Ignored flag and persistence metadata disagree", exception.Message);
        }

        [Fact]
        public void InvalidConversionMetadataShouldThrowUsefulConfigurationException()
        {
            PreTest(typeof(ConversionValidationEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new InvalidConversionValidationMap())));

            Assert.Contains(nameof(ConversionValidationEntity.Status), exception.Message);
            Assert.Contains("invalid conversion metadata", exception.Message);
            Assert.Contains("Conversion metadata cannot be null", exception.Message);
        }

        [Fact]
        public void WriteConverterForNeverPersistedPropertyShouldThrowUsefulConfigurationException()
        {
            PreTest(typeof(ConversionValidationEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new ReadOnlyWriteConversionValidationMap())));

            Assert.Contains(nameof(ConversionValidationEntity.Status), exception.Message);
            Assert.Contains("write converter", exception.Message);
            Assert.Contains("never participates", exception.Message);
        }

        [Fact]
        public void ConventionWithoutConfigureShouldThrowConfigurationException()
        {
            PreTest(typeof(ValidEntity));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddConvention<MissingConfigureConvention>().ForEntity<ValidEntity>()));

            Assert.Contains(typeof(MissingConfigureConvention).FullName, exception.Message);
            Assert.Contains(typeof(ValidEntity).FullName, exception.Message);
            Assert.Contains("without configuration", exception.Message);
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class ValidEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ValidMap : EntityMap<ValidEntity>
        {
            public ValidMap()
            {
                Map(e => e.Id).ToColumn("valid_id");
            }
        }

        private class AlternateValidMap : EntityMap<ValidEntity>
        {
            public AlternateValidMap()
            {
                Map(e => e.Name).ToColumn("valid_name");
            }
        }

        private class InvalidExpressionMap : EntityMap<ValidEntity>
        {
            public InvalidExpressionMap()
            {
                Map(e => e.Id.ToString()).ToColumn("id_text");
            }
        }

        private class ColumnConflictEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ColumnConflictMap : EntityMap<ColumnConflictEntity>
        {
            public ColumnConflictMap()
            {
                Map(e => e.Id).ToColumn("shared_column");
                Map(e => e.Name).ToColumn("shared_column");
            }
        }

        private class CaseSensitivityConflictMap : EntityMap<ColumnConflictEntity>
        {
            public CaseSensitivityConflictMap()
            {
                Map(e => e.Id).ToColumn("shared_column", caseSensitive: false);
                Map(e => e.Name).ToColumn("SHARED_COLUMN");
            }
        }

        private class ExternalColumnReuseMap : IEntityMap<ColumnConflictEntity>
        {
            public ExternalColumnReuseMap()
            {
                var idMap = new ExternalPropertyMap(typeof(ColumnConflictEntity).GetProperty(nameof(ColumnConflictEntity.Id)))
                    .ToColumn("shared_column");
                var nameMap = new ExternalPropertyMap(typeof(ColumnConflictEntity).GetProperty(nameof(ColumnConflictEntity.Name)))
                    .ToColumn("shared_column");

                PropertyMaps = new List<IPropertyMap> { idMap, nameMap };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }

        private class ExternalPropertyMap : PropertyMapBase<ExternalPropertyMap>, IPropertyMap
        {
            public ExternalPropertyMap(PropertyInfo info)
                : base(info)
            {
            }
        }

        private class AmbiguousConvention : Convention
        {
            public AmbiguousConvention()
            {
                Properties().Configure(c => c.HasColumnName("shared_column"));
            }
        }

        private class MissingConfigureConvention : Convention
        {
            public MissingConfigureConvention()
            {
                Properties();
            }
        }

        private class PersistenceValidationEntity
        {
            public int Id { get; set; }

            public string ReadOnlyValue { get; set; }

            public string InsertExcluded { get; set; }

            public string UpdateExcluded { get; set; }

            public string DefaultValue { get; set; }

            public string ComputedValue { get; set; }
        }

        private class ValidPersistenceValidationMap : EntityMap<PersistenceValidationEntity>
        {
            public ValidPersistenceValidationMap()
            {
                Map(e => e.Id);
                Map(e => e.ReadOnlyValue).ReadOnly();
                Map(e => e.InsertExcluded).ExcludeFromInsert();
                Map(e => e.UpdateExcluded).ExcludeFromUpdate();
                Map(e => e.DefaultValue).DatabaseDefaultOnInsert();
                Map(e => e.ComputedValue).Computed();
            }
        }

        private class InvalidPersistenceValidationMap : IEntityMap<PersistenceValidationEntity>
        {
            public InvalidPersistenceValidationMap()
            {
                PropertyMaps = new List<IPropertyMap>
                {
                    new InvalidPersistencePropertyMap(
                        typeof(PersistenceValidationEntity).GetProperty(nameof(PersistenceValidationEntity.ReadOnlyValue)))
                };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }

        private class InvalidPersistencePropertyMap : IPropertyMap, IPropertyMapWithPersistenceMetadata
        {
            public InvalidPersistencePropertyMap(PropertyInfo propertyInfo)
            {
                PropertyInfo = propertyInfo;
            }

            public string ColumnName => PropertyInfo.Name;

            public PropertyInfo PropertyInfo { get; }

            public bool CaseSensitive => true;

            public bool Ignored => true;

            public PropertyPersistenceMetadata Persistence => PropertyPersistenceMetadata.Default;
        }

        private class ConversionValidationEntity
        {
            public string Status { get; set; }
        }

        private class ReadOnlyWriteConversionValidationMap : EntityMap<ConversionValidationEntity>
        {
            public ReadOnlyWriteConversionValidationMap()
            {
                Map(e => e.Status)
                    .ConvertToDatabaseUsing<StatusWriteConverter, string>()
                    .ReadOnly();
            }
        }

        private class InvalidConversionValidationMap : IEntityMap<ConversionValidationEntity>
        {
            public InvalidConversionValidationMap()
            {
                PropertyMaps = new List<IPropertyMap>
                {
                    new InvalidConversionPropertyMap(
                        typeof(ConversionValidationEntity).GetProperty(nameof(ConversionValidationEntity.Status)))
                };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }

        private class InvalidConversionPropertyMap : IPropertyMap, IPropertyMapWithConversionMetadata
        {
            public InvalidConversionPropertyMap(PropertyInfo propertyInfo)
            {
                PropertyInfo = propertyInfo;
            }

            public string ColumnName => PropertyInfo.Name;

            public PropertyInfo PropertyInfo { get; }

            public bool CaseSensitive => true;

            public bool Ignored => false;

            public PropertyConversionMetadata Conversion => null;
        }

        private sealed class StatusWriteConverter : IWritePropertyConverter<string, string>
        {
            public string ConvertToDatabase(string value)
            {
                return value;
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

        private class DistinctNestedLevelMap : EntityMap<NestedLevelEntity>
        {
            public DistinctNestedLevelMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
                Map(e => e.Seniority.Level).ToColumn("seniority_level");
            }
        }

        private class DuplicateNestedLevelMap : EntityMap<NestedLevelEntity>
        {
            public DuplicateNestedLevelMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
                Map(e => e.Rank.Level).ToColumn("rank_level_again");
            }
        }

        private class ForeignMetadataEntity
        {
            public int Id { get; set; }
        }

        private class IncompatibleMetadataMap : IEntityMap<ValidEntity>
        {
            public IncompatibleMetadataMap()
            {
                var foreignProperty = typeof(ForeignMetadataEntity).GetProperty(nameof(ForeignMetadataEntity.Id));
                PropertyMaps = new List<IPropertyMap> { new PropertyMap(foreignProperty, "foreign_id") };
            }

            public IList<IPropertyMap> PropertyMaps { get; }
        }
    }
}
