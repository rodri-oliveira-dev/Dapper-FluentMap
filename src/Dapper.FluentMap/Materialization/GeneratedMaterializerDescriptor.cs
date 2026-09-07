using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Materialization
{
    /// <summary>
    /// Describes one generated materializer for an entity, optional profile and ordered column shape.
    /// </summary>
    /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
    public sealed class GeneratedMaterializerDescriptor<TEntity>
        where TEntity : class
    {
        private readonly GeneratedMaterializerColumn[] _columns;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedMaterializerDescriptor{TEntity}"/> class.
        /// </summary>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        public GeneratedMaterializerDescriptor(
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
            : this(profileType: null, columns, materializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedMaterializerDescriptor{TEntity}"/> class.
        /// </summary>
        /// <param name="profileType">The mapping profile type, or <see langword="null"/> for the default map.</param>
        /// <param name="columns">The ordered column shape and member bindings expected by the materializer.</param>
        /// <param name="materializer">The generated row materializer.</param>
        public GeneratedMaterializerDescriptor(
            Type profileType,
            IEnumerable<GeneratedMaterializerColumn> columns,
            GeneratedRowMaterializer<TEntity> materializer)
        {
            if (profileType != null && !typeof(IMappingProfile).IsAssignableFrom(profileType))
            {
                throw new ArgumentException(
                    $"Profile type '{profileType.FullName}' must implement IMappingProfile.",
                    nameof(profileType));
            }

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            Materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
            ProfileType = profileType;
            _columns = columns.ToArray();

            if (_columns.Any(column => column == null))
            {
                throw new ArgumentException("Column descriptors cannot contain null entries.", nameof(columns));
            }

            if (_columns.Length == 0)
            {
                throw new ArgumentException("At least one column descriptor is required.", nameof(columns));
            }

            Columns = new ReadOnlyCollection<GeneratedMaterializerColumn>(_columns);
        }

        /// <summary>
        /// Gets the entity type produced by the materializer.
        /// </summary>
        public Type EntityType => typeof(TEntity);

        /// <summary>
        /// Gets the mapping profile type, or <see langword="null"/> for the default map.
        /// </summary>
        public Type ProfileType { get; }

        /// <summary>
        /// Gets the ordered column shape and member bindings expected by the materializer.
        /// </summary>
        public IReadOnlyList<GeneratedMaterializerColumn> Columns { get; }

        /// <summary>
        /// Gets the generated row materializer.
        /// </summary>
        public GeneratedRowMaterializer<TEntity> Materializer { get; }
    }
}
