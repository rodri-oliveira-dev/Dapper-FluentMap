using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Dapper.FluentMap.Diagnostics
{
    /// <summary>
    /// Describes the effective FluentMap diagnostics for an entity type.
    /// </summary>
    public sealed class MappingExplanation
    {
        internal MappingExplanation(
            Type entityType,
            Type profileType,
            Type entityMapType,
            IEnumerable<Type> conventionTypes,
            IEnumerable<MemberMappingExplanation> members,
            IEnumerable<string> diagnostics)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            EntityType = entityType;
            ProfileType = profileType;
            EntityMapType = entityMapType;
            ConventionTypes = new ReadOnlyCollection<Type>(
                (conventionTypes ?? Enumerable.Empty<Type>()).ToList());
            Members = new ReadOnlyCollection<MemberMappingExplanation>(
                (members ?? Enumerable.Empty<MemberMappingExplanation>()).ToList());
            Diagnostics = new ReadOnlyCollection<string>(
                (diagnostics ?? Enumerable.Empty<string>()).ToList());
        }

        /// <summary>
        /// Gets the entity type described by this explanation.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the mapping profile marker type, when this explanation targets a profile.
        /// </summary>
        public Type ProfileType { get; }

        /// <summary>
        /// Gets the registered entity map type, when one exists.
        /// </summary>
        public Type EntityMapType { get; }

        /// <summary>
        /// Gets the registered convention types for the entity.
        /// </summary>
        public IReadOnlyList<Type> ConventionTypes { get; }

        /// <summary>
        /// Gets the effective member mappings.
        /// </summary>
        public IReadOnlyList<MemberMappingExplanation> Members { get; }

        /// <summary>
        /// Gets additional diagnostics that are not tied to a single member.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("Entity: ").Append(EntityType.FullName);
            if (ProfileType != null)
            {
                builder.AppendLine()
                       .Append("Profile: ")
                       .Append(ProfileType.FullName);
            }

            foreach (var member in Members)
            {
                builder.AppendLine()
                       .Append(member.MemberPath)
                       .Append(" -> ")
                       .Append(member.ColumnName)
                       .Append(" (")
                       .Append(member.Source)
                       .Append(")");
            }

            return builder.ToString();
        }
    }
}
