using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Reads multiple result sets using FluentMap-controlled materialization.
    /// </summary>
    public sealed class MappedGridReader : IDisposable
    {
        private readonly IDataReader _reader;
        private readonly FluentMapRuntime _runtime;
        private bool _disposed;
        private bool _isConsumed;

        internal MappedGridReader(IDataReader reader)
            : this(reader, FluentMapper.Runtime)
        {
        }

        internal MappedGridReader(IDataReader reader, FluentMapRuntime runtime)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <summary>
        /// Gets a value indicating whether all result sets have been consumed or the reader has been disposed.
        /// </summary>
        public bool IsConsumed => _isConsumed || _disposed;

        /// <summary>
        /// Materializes the current result set and advances to the next one.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <returns>The buffered materialized rows from the current result set.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> ReadMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>()
            where TEntity : class
        {
            return ReadMapped<TEntity>(profileType: null);
        }

        /// <summary>
        /// Materializes exactly one row from the current result set and advances to the next one.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <returns>The materialized row from the current result set.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public TEntity ReadMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>()
            where TEntity : class
        {
            return ReadMapped<TEntity>().Single();
        }

        /// <summary>
        /// Materializes the current result set using the specified FluentMap mapping profile and advances to the next one.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <returns>The buffered materialized rows from the current result set.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public IEnumerable<TEntity> ReadMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>()
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ReadMapped<TEntity>(typeof(TProfile));
        }

        /// <summary>
        /// Materializes exactly one row from the current result set using the specified FluentMap mapping profile and advances to the next one.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to materialize.</typeparam>
        /// <typeparam name="TProfile">The mapping profile marker type to use.</typeparam>
        /// <returns>The materialized row from the current result set.</returns>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public TEntity ReadMappedSingle<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>()
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ReadMapped<TEntity, TProfile>().Single();
        }

        /// <summary>
        /// Releases the underlying data reader.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isConsumed = true;
            _reader.Dispose();
        }

        private IEnumerable<TEntity> ReadMapped<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            Type profileType)
            where TEntity : class
        {
            ThrowIfDisposed();

            if (_isConsumed)
            {
                throw new InvalidOperationException("There are no remaining result sets to read.");
            }

            try
            {
                var results = MappedRowMaterializer.Materialize<TEntity>(_reader, profileType, _runtime);
                _isConsumed = !_reader.NextResult();
                return results;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MappedGridReader));
            }
        }
    }
}
