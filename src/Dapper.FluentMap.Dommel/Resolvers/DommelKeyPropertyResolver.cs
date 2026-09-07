using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Mapping;
using Dommel;

namespace Dapper.FluentMap.Dommel.Resolvers
{
    /// <summary>
    /// Implements the <see cref="IKeyPropertyResolver"/> interface by using the configured mapping.
    /// </summary>
    public class DommelKeyPropertyResolver : IKeyPropertyResolver
    {
        private static readonly IKeyPropertyResolver DefaultResolver = new DefaultKeyPropertyResolver();

        /// <inheritdoc/>
        public ColumnPropertyInfo[] ResolveKeyProperties(Type type)
        {
            IEntityMap entityMap;
            if (!FluentMapper.EntityMaps.TryGetValue(type, out entityMap))
            {
                return DefaultResolver.ResolveKeyProperties(type);
            }

            var mapping = entityMap as IDommelEntityMap;
            if (mapping != null)
            {
                var allPropertyMaps = DommelPersistenceMetadata.ResolvePropertyMaps(type, entityMap).OfType<DommelPropertyMap>().ToList();
                var keyPropertyInfos = allPropertyMaps
                     .Where(e => e.Key)
                     .Select(x => new ColumnPropertyInfo(x.PropertyInfo, x.EffectiveKeyGeneratedOption))
                     .ToArray();

                // Now make sure there aren't any missing key properties that weren't explicitly defined in the mapping.
                try
                {
                    // Make sure to exclude any keys that were defined in the dommel entity map and not marked as keys.
                    var defaultKeyPropertyInfos = DefaultResolver.ResolveKeyProperties(type).Where(x => allPropertyMaps.Count(y => y.PropertyInfo.Equals(x.Property)) == 0);
                    keyPropertyInfos = keyPropertyInfos.Union(defaultKeyPropertyInfos).ToArray();
                } 
                catch
                {
                    // There could be no default Ids found. This is okay as long as we found a custom one.
                    if (keyPropertyInfos.Length == 0)
                    {
                        throw new InvalidOperationException($"Could not find the key properties for type '{type.FullName}'.");
                    }
                }

                return keyPropertyInfos;
            }

            // Fall back to the default mapping strategy.
            return DefaultResolver.ResolveKeyProperties(type);
        }
    }
}
