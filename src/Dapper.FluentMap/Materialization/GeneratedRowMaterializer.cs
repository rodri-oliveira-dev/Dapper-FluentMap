using System.Data;

namespace Dapper.FluentMap.Materialization
{
    /// <summary>
    /// Represents generated code that materializes the current row from an <see cref="IDataRecord"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type produced by the materializer.</typeparam>
    /// <param name="record">The data record positioned on the row to materialize.</param>
    /// <returns>The materialized entity.</returns>
    public delegate TEntity GeneratedRowMaterializer<out TEntity>(IDataRecord record)
        where TEntity : class;
}
