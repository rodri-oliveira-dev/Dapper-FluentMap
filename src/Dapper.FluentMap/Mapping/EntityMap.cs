using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.FluentMap.Utils;

namespace Dapper.FluentMap.Mapping
{
    /// <summary>
    /// Represents a non-typed mapping of an entity.
    /// </summary>
    public interface IEntityMap
    {
        /// <summary>
        /// Gets the collection of mapped properties.
        /// </summary>
        IList<IPropertyMap> PropertyMaps { get; }
    }

    /// <summary>
    /// Represents a typed mapping of an entity.
    /// This serves as a marker interface for generic type inference.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to configure the mapping for.</typeparam>
    public interface IEntityMap<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        TEntity> : IEntityMap
    {
    }

    /// <summary>
    /// Marker interface for a named mapping profile.
    /// </summary>
    public interface IMappingProfile
    {
    }

    /// <summary>
    /// Marks an entity map as belonging to the specified mapping profile.
    /// </summary>
    /// <typeparam name="TProfile">The profile marker type.</typeparam>
    public interface IProfileMap<TProfile>
        where TProfile : IMappingProfile
    {
    }

    internal interface IEntityMapWithIncludedBaseTypes
    {
        IList<Type> IncludedBaseTypes { get; }
    }

    /// <summary>
    /// Serves as the base class for all entity mapping implementations.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TPropertyMap">The type of the property mapping.</typeparam>
    public abstract class EntityMapBase<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        TEntity,
        TPropertyMap> : IEntityMap<TEntity>, IEntityMapWithIncludedBaseTypes
        where TPropertyMap : IPropertyMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBase{TEntity, TPropertyMap}"/> class.
        /// </summary>
        protected EntityMapBase()
        {
            PropertyMaps = new List<IPropertyMap>();
            IncludedBaseTypes = new List<Type>();
        }

        /// <summary>
        /// Gets the collection of mapped properties.
        /// </summary>
        public IList<IPropertyMap> PropertyMaps { get; }

        IList<Type> IEntityMapWithIncludedBaseTypes.IncludedBaseTypes => IncludedBaseTypes;

        private IList<Type> IncludedBaseTypes { get; }

        /// <summary>
        /// Returns an instance of <typeparamref name="TPropertyMap"/> which can perform custom mapping
        /// for the specified property on <typeparamref name="TEntity"/>.
        /// </summary>
        /// <param name="expression">Expression to the property on <typeparamref name="TEntity"/>.</param>
        /// <returns>The created <see cref="T:Dapper.FluentMap.Mapping.PropertyMap"/> instance. This enables a fluent API.</returns>
        /// <exception cref="T:Dapper.FluentMap.FluentMapConfigurationException">when a duplicate mapping is provided.</exception>
        protected TPropertyMap Map(Expression<Func<TEntity, object>> expression)
        {
            var memberPath = ReflectionHelper.GetMemberPath(expression);
            var propertyMap = GetPropertyMap(memberPath.PropertyInfo);
            PropertyMapIdentity.SetMemberPath(propertyMap, memberPath);
            ThrowIfDuplicateMapping(propertyMap);
            PropertyMaps.Add(propertyMap);
            return propertyMap;
        }

        /// <summary>
        /// Includes the explicit mappings configured for a base entity map.
        /// </summary>
        /// <typeparam name="TBase">The base entity type whose mappings should be included.</typeparam>
        /// <exception cref="T:Dapper.FluentMap.FluentMapConfigurationException">
        /// when <typeparamref name="TBase"/> is not a valid base type for <typeparamref name="TEntity"/>
        /// or the same base type is included more than once.
        /// </exception>
        protected void IncludeBase<TBase>()
            where TBase : class
        {
            var baseType = typeof(TBase);
            var entityType = typeof(TEntity);

            if (baseType == entityType || !baseType.IsClass || !baseType.IsAssignableFrom(entityType))
            {
                throw new FluentMapConfigurationException(
                    $"Type '{baseType.FullName}' cannot be included as a base mapping for entity '{entityType.FullName}'. The included type must be a base class of the entity.");
            }

            if (IncludedBaseTypes.Contains(baseType))
            {
                throw new FluentMapConfigurationException(
                    $"Base mapping for type '{baseType.FullName}' is already included by entity '{entityType.FullName}'.");
            }

            IncludedBaseTypes.Add(baseType);
        }

        /// <summary>
        /// When overridden in a derived class, gets the property mapping for the specified property.
        /// </summary>
        /// <param name="info">The <see cref="PropertyInfo"/> for the property.</param>
        /// <returns>An instance of <typeparamref name="TPropertyMap"/>.</returns>
        protected abstract TPropertyMap GetPropertyMap(PropertyInfo info);

        private void ThrowIfDuplicateMapping(IPropertyMap map)
        {
            var memberPath = PropertyMapIdentity.GetMemberPath(map);

            if (PropertyMaps.Any(p => PropertyMapIdentity.GetMemberPath(p).Equals(memberPath)))
            {
                var existingMap = PropertyMaps.First(p => PropertyMapIdentity.GetMemberPath(p).Equals(memberPath));
                throw new FluentMapConfigurationException($"Property path '{memberPath}' is already mapped for entity '{typeof(TEntity).FullName}'. Existing column: '{existingMap.ColumnName}'; duplicate column: '{map.ColumnName}'.");
            }
        }
    }

    /// <summary>
    /// Represents a typed mapping of an entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to configure the mapping for.</typeparam>
    public abstract class EntityMap<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        TEntity> : EntityMapBase<TEntity, PropertyMap>
        where TEntity : class
    {
        /// <inheritdoc />
        protected override PropertyMap GetPropertyMap(PropertyInfo info)
        {
            return new PropertyMap(info);
        }
    }
}
