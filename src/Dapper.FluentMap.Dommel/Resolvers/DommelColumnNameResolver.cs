using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Dommel.Mapping;
using Dapper.FluentMap.Mapping;
using Dommel;

namespace Dapper.FluentMap.Dommel.Resolvers
{
    /// <summary>
    /// Implements the <see cref="IColumnNameResolver"/> interface by using the configured mapping.
    /// </summary>
    public class DommelColumnNameResolver : IColumnNameResolver
    {
        private static readonly IColumnNameResolver DefaultResolver = new DefaultColumnNameResolver();

        /// <inheritdoc/>
        public string ResolveColumnName(PropertyInfo propertyInfo)
        {
            if (propertyInfo.DeclaringType != null)
            {
#if NETSTANDARD1_3
                if (FluentMapper.EntityMaps.TryGetValue(propertyInfo.DeclaringType, out var entityMap))

#else
                if (FluentMapper.EntityMaps.TryGetValue(propertyInfo.ReflectedType, out var entityMap))
#endif
                {
                    var mapping = entityMap as IDommelEntityMap;
                    if (mapping != null)
                    {
                        var propertyMaps = DommelPersistenceMetadata
                            .ResolvePropertyMaps(propertyInfo.ReflectedType ?? propertyInfo.DeclaringType, entityMap)
                            .Where(m => m.PropertyInfo.Name == propertyInfo.Name)
                            .ToList();
                        if (propertyMaps.Count == 1)
                        {
                            return propertyMaps[0].ColumnName;
                        }
                    }
                }
#if NETSTANDARD1_3
                else if (FluentMapper.TypeConventions.TryGetValue(propertyInfo.DeclaringType, out var conventions))

#else
                else if (FluentMapper.TypeConventions.TryGetValue(propertyInfo.ReflectedType, out var conventions))
#endif
                {
                    foreach (var convention in conventions)
                    {
                        var propertyMaps = convention.PropertyMaps.Where(m => m.PropertyInfo.Name == propertyInfo.Name).ToList();
                        if (propertyMaps.Count == 1)
                        {
                            return propertyMaps[0].ColumnName;
                        }
                    }
                }
            }

            return DefaultResolver.ResolveColumnName(propertyInfo);
        }
    }
}
