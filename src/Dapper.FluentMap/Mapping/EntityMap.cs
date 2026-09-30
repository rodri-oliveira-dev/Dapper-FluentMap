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
        TPropertyMap> : IEntityMap<TEntity>, IEntityMapWithIncludedBaseTypes, IEntityMapWithConstructionStrategy
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

        EntityConstructionStrategy IEntityMapWithConstructionStrategy.ConstructionStrategy => ConstructionStrategy;

        private IList<Type> IncludedBaseTypes { get; }

        private EntityConstructionStrategy ConstructionStrategy { get; set; }

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
        /// Configures an explicit factory that constructs the entity from one explicitly mapped property value.
        /// </summary>
        /// <typeparam name="TValue1">The first mapped value type.</typeparam>
        /// <param name="value1">The mapped property path supplied as the first factory argument.</param>
        /// <param name="factory">The factory invoked for each materialized row.</param>
        protected void ConstructUsing<TValue1>(
            Expression<Func<TEntity, TValue1>> value1,
            Func<TValue1, TEntity> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            SetConstructionStrategy(
                new[] { CreateConstructionBinding(value1) },
                values => factory((TValue1)values[0]));
        }

        /// <summary>
        /// Configures an explicit factory that constructs the entity from two explicitly mapped property values.
        /// </summary>
        /// <typeparam name="TValue1">The first mapped value type.</typeparam>
        /// <typeparam name="TValue2">The second mapped value type.</typeparam>
        /// <param name="value1">The mapped property path supplied as the first factory argument.</param>
        /// <param name="value2">The mapped property path supplied as the second factory argument.</param>
        /// <param name="factory">The factory invoked for each materialized row.</param>
        protected void ConstructUsing<TValue1, TValue2>(
            Expression<Func<TEntity, TValue1>> value1,
            Expression<Func<TEntity, TValue2>> value2,
            Func<TValue1, TValue2, TEntity> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            SetConstructionStrategy(
                new[] { CreateConstructionBinding(value1), CreateConstructionBinding(value2) },
                values => factory((TValue1)values[0], (TValue2)values[1]));
        }

        /// <summary>
        /// Configures an explicit factory that constructs the entity from three explicitly mapped property values.
        /// </summary>
        /// <typeparam name="TValue1">The first mapped value type.</typeparam>
        /// <typeparam name="TValue2">The second mapped value type.</typeparam>
        /// <typeparam name="TValue3">The third mapped value type.</typeparam>
        /// <param name="value1">The mapped property path supplied as the first factory argument.</param>
        /// <param name="value2">The mapped property path supplied as the second factory argument.</param>
        /// <param name="value3">The mapped property path supplied as the third factory argument.</param>
        /// <param name="factory">The factory invoked for each materialized row.</param>
        protected void ConstructUsing<TValue1, TValue2, TValue3>(
            Expression<Func<TEntity, TValue1>> value1,
            Expression<Func<TEntity, TValue2>> value2,
            Expression<Func<TEntity, TValue3>> value3,
            Func<TValue1, TValue2, TValue3, TEntity> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            SetConstructionStrategy(
                new[]
                {
                    CreateConstructionBinding(value1),
                    CreateConstructionBinding(value2),
                    CreateConstructionBinding(value3)
                },
                values => factory((TValue1)values[0], (TValue2)values[1], (TValue3)values[2]));
        }

        /// <summary>
        /// Configures an explicit factory that constructs the entity from four explicitly mapped property values.
        /// </summary>
        /// <typeparam name="TValue1">The first mapped value type.</typeparam>
        /// <typeparam name="TValue2">The second mapped value type.</typeparam>
        /// <typeparam name="TValue3">The third mapped value type.</typeparam>
        /// <typeparam name="TValue4">The fourth mapped value type.</typeparam>
        /// <param name="value1">The mapped property path supplied as the first factory argument.</param>
        /// <param name="value2">The mapped property path supplied as the second factory argument.</param>
        /// <param name="value3">The mapped property path supplied as the third factory argument.</param>
        /// <param name="value4">The mapped property path supplied as the fourth factory argument.</param>
        /// <param name="factory">The factory invoked for each materialized row.</param>
        protected void ConstructUsing<TValue1, TValue2, TValue3, TValue4>(
            Expression<Func<TEntity, TValue1>> value1,
            Expression<Func<TEntity, TValue2>> value2,
            Expression<Func<TEntity, TValue3>> value3,
            Expression<Func<TEntity, TValue4>> value4,
            Func<TValue1, TValue2, TValue3, TValue4, TEntity> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            SetConstructionStrategy(
                new[]
                {
                    CreateConstructionBinding(value1),
                    CreateConstructionBinding(value2),
                    CreateConstructionBinding(value3),
                    CreateConstructionBinding(value4)
                },
                values => factory(
                    (TValue1)values[0],
                    (TValue2)values[1],
                    (TValue3)values[2],
                    (TValue4)values[3]));
        }

        /// <summary>
        /// When overridden in a derived class, gets the property mapping for the specified property.
        /// </summary>
        /// <param name="info">The <see cref="PropertyInfo"/> for the property.</param>
        /// <returns>An instance of <typeparamref name="TPropertyMap"/>.</returns>
        protected abstract TPropertyMap GetPropertyMap(PropertyInfo info);

        private static ConstructionValueBinding CreateConstructionBinding<TValue>(
            Expression<Func<TEntity, TValue>> expression)
        {
            return new ConstructionValueBinding(ReflectionHelper.GetMemberPath(expression), typeof(TValue));
        }

        private void SetConstructionStrategy(
            IEnumerable<ConstructionValueBinding> bindings,
            Func<object[], object> factory)
        {
            if (ConstructionStrategy != null)
            {
                throw new FluentMapConfigurationException(
                    $"Entity '{typeof(TEntity).FullName}' already has an explicit construction strategy.");
            }

            ConstructionStrategy = new EntityConstructionStrategy(typeof(TEntity), bindings, factory);
        }

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
