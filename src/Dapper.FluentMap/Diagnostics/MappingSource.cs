namespace Dapper.FluentMap.Diagnostics
{
    /// <summary>
    /// Describes the source that provides a mapping in the effective FluentMap configuration.
    /// </summary>
    public enum MappingSource
    {
        /// <summary>
        /// The mapping was configured directly on the entity map.
        /// </summary>
        Explicit,

        /// <summary>
        /// The mapping was included from a registered base entity map.
        /// </summary>
        Inherited,

        /// <summary>
        /// The mapping was produced by a configured convention.
        /// </summary>
        Convention,

        /// <summary>
        /// The mapping was produced by a naming policy.
        /// </summary>
        NamingPolicy,

        /// <summary>
        /// The mapping is left to Dapper's default type map.
        /// </summary>
        DapperDefault
    }
}
