using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Dommel.Resolvers;
using Dommel;

namespace Dapper.FluentMap.Dommel
{
    /// <summary>
    /// Defines methods for configured Dapper.FluentMap.Dommel.
    /// </summary>
    public static class FluentMapConfigurationExtensions
    {
        /// <summary>
        /// Configures the specified configuration for Dapper.FluentMap.Dommel.
        /// </summary>
        /// <param name="config">The Dapper.FluentMap configuration.</param>
        /// <returns>The Dapper.FluentMap configuration.</returns>
        public static FluentMapConfiguration ForDommel(this FluentMapConfiguration config)
        {
            DommelMapper.SetColumnNameResolver(new DommelColumnNameResolver());
            DommelMapper.SetKeyPropertyResolver(new DommelKeyPropertyResolver());
            DommelMapper.SetTableNameResolver(new DommelTableNameResolver());
            DommelMapper.SetPropertyResolver(new DommelPropertyResolver());
            DommelPersistenceSqlBuilder.RegisterDefaults();
            return config;
        }
    }
}
