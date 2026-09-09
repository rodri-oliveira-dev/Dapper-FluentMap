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
            if (TryResolveConfiguredColumnName(propertyInfo, out var columnName))
            {
                return columnName;
            }

            return DefaultResolver.ResolveColumnName(propertyInfo);
        }

        private static bool TryResolveConfiguredColumnName(PropertyInfo propertyInfo, out string columnName)
        {
            columnName = null;

            if (propertyInfo.DeclaringType == null)
            {
                return false;
            }

            var mappedType = GetMappedType(propertyInfo);
            if (mappedType == null)
            {
                return false;
            }

            if (FluentMapper.EntityMaps.TryGetValue(mappedType, out var entityMap))
            {
                return TryResolveEntityMapColumnName(mappedType, entityMap, propertyInfo, out columnName);
            }

            if (FluentMapper.TypeConventions.TryGetValue(mappedType, out var conventions))
            {
                foreach (var convention in conventions)
                {
                    var propertyMaps = convention.PropertyMaps.Where(m => m.PropertyInfo.Name == propertyInfo.Name).ToList();
                    if (propertyMaps.Count == 1)
                    {
                        columnName = propertyMaps[0].ColumnName;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveEntityMapColumnName(
            System.Type mappedType,
            IEntityMap entityMap,
            PropertyInfo propertyInfo,
            out string columnName)
        {
            columnName = null;

            if (!(entityMap is IDommelEntityMap))
            {
                return false;
            }

            var propertyMaps = DommelPersistenceMetadata
                .ResolvePropertyMaps(mappedType, entityMap)
                .Where(m => m.PropertyInfo.Name == propertyInfo.Name)
                .ToList();

            if (propertyMaps.Count != 1)
            {
                return false;
            }

            columnName = propertyMaps[0].ColumnName;
            return true;
        }

        private static System.Type GetMappedType(PropertyInfo propertyInfo)
        {
#if NETSTANDARD1_3
            return propertyInfo.DeclaringType;
#else
            return propertyInfo.ReflectedType ?? propertyInfo.DeclaringType;
#endif
        }
    }
}
