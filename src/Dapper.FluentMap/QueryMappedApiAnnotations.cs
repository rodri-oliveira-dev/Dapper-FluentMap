using System.Diagnostics.CodeAnalysis;

namespace Dapper.FluentMap
{
    internal static class QueryMappedApiAnnotations
    {
        internal const DynamicallyAccessedMemberTypes MaterializedEntityMemberTypes =
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties;

        internal const string RequiresUnreferencedCodeMessage =
            "QueryMapped uses runtime mapping metadata to materialize nested objects. Prefer generated materializers when publishing trimmed or Native AOT applications.";

        internal const string RequiresDynamicCodeMessage =
            "QueryMapped compiles runtime accessors for nested object materialization. Prefer generated materializers when publishing Native AOT applications.";
    }
}
