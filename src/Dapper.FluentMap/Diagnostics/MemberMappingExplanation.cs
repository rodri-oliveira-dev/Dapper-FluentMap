using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Diagnostics
{
    /// <summary>
    /// Describes the effective mapping metadata for one entity member path.
    /// </summary>
    public sealed class MemberMappingExplanation
    {
        internal MemberMappingExplanation(
            string memberPath,
            PropertyInfo propertyInfo,
            string columnName,
            MappingSource source,
            bool caseSensitive,
            bool ignored,
            Type inheritedFrom,
            Type conventionType,
            IEnumerable<ConstructorParameterExplanation> constructorParameters,
            MappingMaterialization materialization,
            PropertyPersistenceMetadata persistence,
            PropertyConversionMetadata conversion)
        {
            if (string.IsNullOrEmpty(memberPath))
            {
                throw new ArgumentException("Member path cannot be null or empty.", nameof(memberPath));
            }

            if (propertyInfo == null)
            {
                throw new ArgumentNullException(nameof(propertyInfo));
            }

            MemberPath = memberPath;
            PropertyInfo = propertyInfo;
            ColumnName = columnName;
            Source = source;
            CaseSensitive = caseSensitive;
            Ignored = ignored;
            InheritedFrom = inheritedFrom;
            ConventionType = conventionType;
            ConstructorParameters = new ReadOnlyCollection<ConstructorParameterExplanation>(
                (constructorParameters ?? Enumerable.Empty<ConstructorParameterExplanation>()).ToList());
            Materialization = materialization;
            Persistence = persistence ?? PropertyPersistenceMetadata.Default;
            Conversion = conversion ?? PropertyConversionMetadata.Default;
        }

        /// <summary>
        /// Gets the member path represented by the mapping.
        /// </summary>
        public string MemberPath { get; }

        /// <summary>
        /// Gets the terminal property represented by the mapping.
        /// </summary>
        public PropertyInfo PropertyInfo { get; }

        /// <summary>
        /// Gets the configured or default column name.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the source that provides the mapping.
        /// </summary>
        public MappingSource Source { get; }

        /// <summary>
        /// Gets a value indicating whether the column name comparison is case-sensitive.
        /// </summary>
        public bool CaseSensitive { get; }

        /// <summary>
        /// Gets a value indicating whether this member is ignored by FluentMap.
        /// </summary>
        public bool Ignored { get; }

        /// <summary>
        /// Gets the base entity type that declared an inherited mapping, when applicable.
        /// </summary>
        public Type InheritedFrom { get; }

        /// <summary>
        /// Gets the convention type that produced the mapping, when applicable.
        /// </summary>
        public Type ConventionType { get; }

        /// <summary>
        /// Gets constructor parameters that can receive this mapped column.
        /// </summary>
        public IReadOnlyList<ConstructorParameterExplanation> ConstructorParameters { get; }

        /// <summary>
        /// Gets how this member is materialized.
        /// </summary>
        public MappingMaterialization Materialization { get; }

        /// <summary>
        /// Gets the persistence metadata associated with this member mapping.
        /// </summary>
        public PropertyPersistenceMetadata Persistence { get; }

        /// <summary>
        /// Gets the conversion metadata associated with this member mapping.
        /// </summary>
        public PropertyConversionMetadata Conversion { get; }
    }
}
