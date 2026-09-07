using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Dapper.FluentMap.Compatibility;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.TypeMaps
{
    /// <summary>
    /// Represents a Dapper type mapping strategy which consists of multiple strategies.
    /// </summary>
    public abstract class MultiTypeMap : SqlMapper.ITypeMap
    {
        private readonly IEnumerable<SqlMapper.ITypeMap> _mappers;

        /// <summary>
        /// Initializes an instance of the <see cref="T:Dapper.FluentMap.TypeMaps.MultiTypeMap"/>
        /// class with the specified Dapper type mappers.
        /// </summary>
        /// <param name="mappers">The type mapping strategies to be used when mapping.</param>
        protected MultiTypeMap(params SqlMapper.ITypeMap[] mappers)
        {
            _mappers = mappers;
        }

        /// <inheritdoc />
        public ConstructorInfo FindConstructor(string[] names, Type[] types)
        {
            foreach (var mapper in _mappers)
            {
                try
                {
                    var result = mapper.FindConstructor(names, types);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (NotImplementedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
                catch (NotSupportedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
            }

            return null;
        }

        /// <inheritdoc />
        public ConstructorInfo FindExplicitConstructor()
        {
            foreach (var mapper in _mappers)
            {
                try
                {
                    var result = mapper.FindExplicitConstructor();
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (NotImplementedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
                catch (NotSupportedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
            }

            return null;
        }

        /// <inheritdoc />
        public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
        {
            foreach (var mapper in _mappers)
            {
                try
                {
                    var result = mapper.GetConstructorParameter(constructor, columnName);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (NotImplementedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
                catch (NotSupportedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
            }

            return null;
        }

        /// <inheritdoc />
        public SqlMapper.IMemberMap GetMember(string columnName)
        {
            foreach (var mapper in _mappers)
            {
                try
                {
                    var result = mapper.GetMember(columnName);
                    if (result != null)
                    {
                        if (DapperIgnoredMemberMap.IsIgnored(result))
                        {
                            // The property is explicitly ignored or FluentMap-controlled nested materialization.
                            // return null to prevent falling back to default type map of Dapper.
                            return null;
                        }
                        return result;
                    }
                }
                catch (NotImplementedException)
                {
                    // Ignore unsupported operations from mapper strategies
                    // and continue to the next mapping strategy.
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a cache for columns and properties.
        /// </summary>
        protected static ConcurrentDictionary<string, PropertyInfo> TypePropertyMapCache { get; } = new ConcurrentDictionary<string, PropertyInfo>();

        /// <summary>
        /// Determines whether the configured column name matches the specified column name.
        /// </summary>
        /// <param name="map">The configured property map.</param>
        /// <param name="columnName">The column name Dapper is resolving.</param>
        /// <returns><c>true</c> when the names match; otherwise, <c>false</c>.</returns>
        protected static bool MatchColumnNames(IPropertyMap map, string columnName)
        {
            var comparison = StringComparison.Ordinal;
            if (!map.CaseSensitive)
            {
                comparison = StringComparison.OrdinalIgnoreCase;
            }

            return string.Equals(map.ColumnName, columnName, comparison);
        }
    }
}
