using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Materialization;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Reads multiple result sets using FluentMap-controlled materialization.
    /// </summary>
    public sealed class MappedGridReader : IDisposable, IAsyncDisposable
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
        /// Asynchronously materializes the current result set and advances to the next one.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public Task<IReadOnlyList<TEntity>> ReadMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return ReadMappedAsync<TEntity>(profileType: null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously materializes exactly one row from the current result set and advances to the next one.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public async Task<TEntity> ReadMappedSingleAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var rows = await ReadMappedAsync<TEntity>(cancellationToken).ConfigureAwait(false);
            return rows.Single();
        }

        /// <summary>
        /// Asynchronously materializes the current result set using the specified FluentMap mapping profile and advances to the next one.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public Task<IReadOnlyList<TEntity>> ReadMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            return ReadMappedAsync<TEntity>(typeof(TProfile), cancellationToken);
        }

        /// <summary>
        /// Asynchronously materializes exactly one row from the current result set using the specified FluentMap mapping profile and advances to the next one.
        /// </summary>
        [RequiresUnreferencedCode(QueryMappedApiAnnotations.RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(QueryMappedApiAnnotations.RequiresDynamicCodeMessage)]
        public async Task<TEntity> ReadMappedSingleAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity,
            TProfile>(
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TProfile : IMappingProfile
        {
            var rows = await ReadMappedAsync<TEntity, TProfile>(cancellationToken).ConfigureAwait(false);
            return rows.Single();
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

        /// <summary>
        /// Asynchronously releases the underlying data reader when it supports asynchronous disposal.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isConsumed = true;

            if (_reader is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                return;
            }

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

        private async Task<IReadOnlyList<TEntity>> ReadMappedAsync<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            Type profileType,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            ThrowIfDisposed();

            if (_isConsumed)
            {
                throw new InvalidOperationException("There are no remaining result sets to read.");
            }

            if (!(_reader is DbDataReader dbReader))
            {
                throw new InvalidOperationException("Asynchronous mapped reads require a DbDataReader created by QueryMultipleMappedAsync.");
            }

            try
            {
                var results = new List<TEntity>();
                var materializer = MappedRowMaterializer.CreateMaterializer<TEntity>(_reader, profileType, _runtime);
                while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(materializer(_reader));
                }

                _isConsumed = !await dbReader.NextResultAsync(cancellationToken).ConfigureAwait(false);
                return results;
            }
            catch
            {
                await DisposeAsync().ConfigureAwait(false);
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
