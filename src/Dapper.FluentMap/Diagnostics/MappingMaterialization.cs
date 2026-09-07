namespace Dapper.FluentMap.Diagnostics
{
    /// <summary>
    /// Describes how a mapped member is materialized.
    /// </summary>
    public enum MappingMaterialization
    {
        /// <summary>
        /// The member is materialized by Dapper's regular root-object mapping.
        /// </summary>
        Dapper,

        /// <summary>
        /// The member is materialized by FluentMap's opt-in nested object materializer.
        /// </summary>
        Nested,

        /// <summary>
        /// The member is materialized by FluentMap's opt-in constructor-based value object materializer.
        /// </summary>
        ValueObject
    }
}
