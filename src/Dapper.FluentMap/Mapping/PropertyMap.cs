using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Dapper.FluentMap.Mapping
{
    /// <summary>
    /// Represents the mapping of a property.
    /// </summary>
    public interface IPropertyMap
    {
        /// <summary>
        /// Gets the name of the column in the data store.
        /// </summary>
        string ColumnName { get; }

        /// <summary>
        /// Gets the <see cref="T:System.Reflection.PropertyInfo"/> object for the current property.
        /// </summary>
        PropertyInfo PropertyInfo { get; }

        /// <summary>
        /// Gets or sets a value indicating whether column name mapping should be case sensitive.
        /// </summary>
        bool CaseSensitive { get; }

        /// <summary>
        /// Gets a value indicating wether the property should be ignored when mapping.
        /// </summary>
        bool Ignored { get; }
    }

    /// <summary>
    /// Serves as the base class for all property mapping implementations.
    /// </summary>
    /// <typeparam name="TPropertyMap">The type of the property mapping.</typeparam>
    public abstract class PropertyMapBase<TPropertyMap> :
        IPropertyMapWithMemberPath,
        IPropertyMapWithPersistenceMetadata,
        IPropertyMapWithConversionMetadata
        where TPropertyMap : class, IPropertyMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Dapper.FluentMap.Mapping.PropertyMap"/> using
        /// the specified <see cref="T:System.Reflection.PropertyInfo"/> object representing the property to map.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Reflection.PropertyInfo"/> object representing to the property to map.</param>
        protected PropertyMapBase(PropertyInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            PropertyInfo = info;
            MemberPath = Dapper.FluentMap.Mapping.MemberPath.ForProperty(info);
            ColumnName = info.Name;
            Persistence = PropertyPersistenceMetadata.Default;
            Conversion = PropertyConversionMetadata.Default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Dapper.FluentMap.Mapping.PropertyMap"/> using
        /// the specified <see cref="T:System.Reflection.PropertyInfo"/> object representing the property to map
        /// and column name to map the property to.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Reflection.PropertyInfo"/> object representing to the property to map.</param>
        /// <param name="columnName">The column name in the database to map the property to.</param>
        internal PropertyMapBase(PropertyInfo info, string columnName)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            PropertyInfo = info;
            MemberPath = Dapper.FluentMap.Mapping.MemberPath.ForProperty(info);
            ColumnName = columnName;
            Persistence = PropertyPersistenceMetadata.Default;
            Conversion = PropertyConversionMetadata.Default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Dapper.FluentMap.Mapping.PropertyMap"/> using
        /// the specified <see cref="T:System.Reflection.PropertyInfo"/> object representing the property to map,
        /// column name to map the property to and a value indicating whether the mapping should be case sensitive.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Reflection.PropertyInfo"/> object representing to the property to map.</param>
        /// <param name="columnName">The column name in the database to map the property to.</param>
        /// <param name="caseSensitive">A value indicating whether the mappig should be case sensitive.</param>
        internal PropertyMapBase(PropertyInfo info, string columnName, bool caseSensitive)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            PropertyInfo = info;
            MemberPath = Dapper.FluentMap.Mapping.MemberPath.ForProperty(info);
            ColumnName = columnName;
            CaseSensitive = caseSensitive;
            Persistence = PropertyPersistenceMetadata.Default;
            Conversion = PropertyConversionMetadata.Default;
        }

        /// <summary>
        /// Gets the column name for the mapping.
        /// </summary>
        public string ColumnName { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this mapping is case sensitive.
        /// </summary>
        public bool CaseSensitive { get; private set; }

        /// <summary>
        /// Gets a value indicating the property should be ignored when mapping.
        /// </summary>
        public bool Ignored { get; private set; }

        /// <summary>
        /// Gets a reference to the <see cref="System.Reflection.PropertyInfo"/> of this mapping.
        /// </summary>
        public PropertyInfo PropertyInfo { get; }

        /// <summary>
        /// Gets the persistence metadata configured for this property.
        /// </summary>
        public PropertyPersistenceMetadata Persistence { get; private set; }

        /// <summary>
        /// Gets the conversion metadata configured for this property.
        /// </summary>
        public PropertyConversionMetadata Conversion { get; private set; }

        internal MemberPath MemberPath { get; private set; }

        MemberPath IPropertyMapWithMemberPath.MemberPath => MemberPath;

        void IPropertyMapWithMemberPath.SetMemberPath(MemberPath memberPath)
        {
            MemberPath = memberPath;
        }

        /// <summary>
        /// Maps the current property to the specified column name.
        /// </summary>
        /// <param name="columnName">The name of the column in the data store.</param>
        /// <param name="caseSensitive">A value indicating whether column name mapping should be case sensitive.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ToColumn(string columnName, bool caseSensitive = true)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));
            }

            ColumnName = columnName;
            CaseSensitive = caseSensitive;
            return this as TPropertyMap;
        }

        /// <summary>
        /// Marks the current property as ignored, resulting in the property not being mapped by Dapper.
        /// </summary>
        /// <returns>The current <see cref="T:Dapper.FluentMap.Mapping.PropertyMap"/> instance. This enables a fluent API.</returns>
        public TPropertyMap Ignore()
        {
            Ignored = true;
            Persistence = PropertyPersistenceMetadata.Ignored;
            return this as TPropertyMap;
        }

        /// <summary>
        /// Excludes the current property from generated INSERT operations while preserving read materialization.
        /// </summary>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ExcludeFromInsert()
        {
            Persistence = Persistence.ExcludeFromInsert();
            return this as TPropertyMap;
        }

        /// <summary>
        /// Excludes the current property from generated UPDATE operations while preserving read materialization.
        /// </summary>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ExcludeFromUpdate()
        {
            Persistence = Persistence.ExcludeFromUpdate();
            return this as TPropertyMap;
        }

        /// <summary>
        /// Marks the current property as read-only for generated persistence operations.
        /// </summary>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ReadOnly()
        {
            Persistence = Persistence.ReadOnly();
            return this as TPropertyMap;
        }

        /// <summary>
        /// Marks the current property as computed by the database.
        /// </summary>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap Computed()
        {
            Persistence = Persistence.Computed();
            return this as TPropertyMap;
        }

        /// <summary>
        /// Marks the current property as having a database default value when omitted from INSERT.
        /// </summary>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap DatabaseDefaultOnInsert()
        {
            Persistence = Persistence.DatabaseDefaultOnInsert();
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a read converter type for the current property.
        /// </summary>
        /// <typeparam name="TConverter">The converter type to instantiate for this property map.</typeparam>
        /// <typeparam name="TDatabase">The database/provider CLR type accepted by the converter.</typeparam>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertFromDatabaseUsing<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            TConverter,
            TDatabase>()
            where TConverter : new()
        {
            var converter = new TConverter();
            var readConverter = PropertyMapConversion.CreateReadConverter(
                typeof(TConverter),
                typeof(TDatabase),
                PropertyInfo.PropertyType,
                converter);

            Conversion = Conversion.WithReadConverter(readConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a read converter instance for the current property.
        /// </summary>
        /// <typeparam name="TDatabase">The database/provider CLR type accepted by the converter.</typeparam>
        /// <typeparam name="TProperty">The property CLR type returned by the converter.</typeparam>
        /// <param name="converter">The converter instance to reuse for this property map.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertFromDatabaseUsing<TDatabase, TProperty>(
            IReadPropertyConverter<TDatabase, TProperty> converter)
        {
            var readConverter = PropertyMapConversion.CreateReadConverter(
                converter,
                PropertyInfo.PropertyType);

            Conversion = Conversion.WithReadConverter(readConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a read converter delegate for the current property.
        /// </summary>
        /// <typeparam name="TDatabase">The database/provider CLR type accepted by the converter.</typeparam>
        /// <typeparam name="TProperty">The property CLR type returned by the converter.</typeparam>
        /// <param name="converter">The converter delegate to reuse for this property map.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertFromDatabaseUsing<TDatabase, TProperty>(
            ReadPropertyConverter<TDatabase, TProperty> converter)
        {
            var readConverter = PropertyMapConversion.CreateReadConverter(
                converter,
                PropertyInfo.PropertyType);

            Conversion = Conversion.WithReadConverter(readConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a write converter type for the current property.
        /// </summary>
        /// <typeparam name="TConverter">The converter type to instantiate for this property map.</typeparam>
        /// <typeparam name="TDatabase">The database/provider CLR type returned by the converter.</typeparam>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertToDatabaseUsing<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            TConverter,
            TDatabase>()
            where TConverter : new()
        {
            var converter = new TConverter();
            var writeConverter = PropertyMapConversion.CreateWriteConverter(
                typeof(TConverter),
                typeof(TDatabase),
                PropertyInfo.PropertyType,
                converter);

            Conversion = Conversion.WithWriteConverter(writeConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a write converter instance for the current property.
        /// </summary>
        /// <typeparam name="TProperty">The property CLR type accepted by the converter.</typeparam>
        /// <typeparam name="TDatabase">The database/provider CLR type returned by the converter.</typeparam>
        /// <param name="converter">The converter instance to reuse for this property map.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertToDatabaseUsing<TProperty, TDatabase>(
            IWritePropertyConverter<TProperty, TDatabase> converter)
        {
            var writeConverter = PropertyMapConversion.CreateWriteConverter(
                converter,
                PropertyInfo.PropertyType);

            Conversion = Conversion.WithWriteConverter(writeConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures a write converter delegate for the current property.
        /// </summary>
        /// <typeparam name="TProperty">The property CLR type accepted by the converter.</typeparam>
        /// <typeparam name="TDatabase">The database/provider CLR type returned by the converter.</typeparam>
        /// <param name="converter">The converter delegate to reuse for this property map.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertToDatabaseUsing<TProperty, TDatabase>(
            WritePropertyConverter<TProperty, TDatabase> converter)
        {
            var writeConverter = PropertyMapConversion.CreateWriteConverter(
                converter,
                PropertyInfo.PropertyType);

            Conversion = Conversion.WithWriteConverter(writeConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures one bidirectional converter type for the current property.
        /// </summary>
        /// <typeparam name="TConverter">The converter type to instantiate for this property map.</typeparam>
        /// <typeparam name="TDatabase">The database/provider CLR type used by the converter.</typeparam>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertUsing<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            TConverter,
            TDatabase>()
            where TConverter : new()
        {
            var converter = new TConverter();
            var readConverter = PropertyMapConversion.CreateReadConverter(
                typeof(TConverter),
                typeof(TDatabase),
                PropertyInfo.PropertyType,
                converter);
            var writeConverter = PropertyMapConversion.CreateWriteConverter(
                typeof(TConverter),
                typeof(TDatabase),
                PropertyInfo.PropertyType,
                converter);

            Conversion = Conversion
                .WithReadConverter(readConverter)
                .WithWriteConverter(writeConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Configures one bidirectional converter instance for the current property.
        /// </summary>
        /// <typeparam name="TDatabase">The database/provider CLR type used by the converter.</typeparam>
        /// <typeparam name="TProperty">The property CLR type used by the converter.</typeparam>
        /// <param name="converter">The converter instance to reuse for this property map.</param>
        /// <returns>The current instance of <typeparamref name="TPropertyMap"/>.</returns>
        public TPropertyMap ConvertUsing<TDatabase, TProperty>(
            IPropertyConverter<TDatabase, TProperty> converter)
        {
            var readConverter = PropertyMapConversion.CreateReadConverter<TDatabase, TProperty>(
                converter,
                PropertyInfo.PropertyType);
            var writeConverter = PropertyMapConversion.CreateWriteConverter<TProperty, TDatabase>(
                converter,
                PropertyInfo.PropertyType);

            Conversion = Conversion
                .WithReadConverter(readConverter)
                .WithWriteConverter(writeConverter);
            return this as TPropertyMap;
        }

        /// <summary>
        /// Applies persistence metadata configured by derived mapping types.
        /// </summary>
        /// <param name="persistence">The persistence metadata to apply.</param>
        protected void UsePersistence(PropertyPersistenceMetadata persistence)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            Persistence = persistence;
            Ignored = persistence.IgnoredByFluentMap;
        }

        /// <summary>
        /// Marks the current property as a persistence key.
        /// </summary>
        protected void MarkAsKey()
        {
            UsePersistence(Persistence.Key());
        }

        /// <summary>
        /// Marks the current property as an identity generated by the database.
        /// </summary>
        protected void MarkAsIdentity()
        {
            UsePersistence(Persistence.Identity());
        }

        /// <summary>
        /// Clears database-generated semantics from the current property.
        /// </summary>
        protected void MarkAsNotGenerated()
        {
            UsePersistence(Persistence.GeneratedNone());
        }

        /// <summary>
        /// Marks the current property as computed by the database.
        /// </summary>
        protected void MarkAsComputed()
        {
            UsePersistence(Persistence.Computed());
        }

        #region EditorBrowsableStates
        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }

        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Type GetType()
        {
            return base.GetType();
        }
        #endregion
    }

    /// <summary>
    /// Represents the mapping of a property.
    /// </summary>
    public class PropertyMap : PropertyMapBase<PropertyMap>, IPropertyMap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Dapper.FluentMap.Mapping.PropertyMap"/> class
        /// with the specified <see cref="System.Reflection.PropertyInfo"/> object.
        /// </summary>
        /// <param name="info">The information about the property.</param>
        public PropertyMap(PropertyInfo info)
            : base(info)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dapper.FluentMap.Mapping.PropertyMap"/> class
        /// with the specified <see cref="System.Reflection.PropertyInfo"/> object and column name.
        /// </summary>
        /// <param name="info">The information about the property.</param>
        /// <param name="columnName">The column name.</param>
        public PropertyMap(PropertyInfo info, string columnName)
            : base(info, columnName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dapper.FluentMap.Mapping.PropertyMap"/> class
        /// with the specified <see cref="System.Reflection.PropertyInfo"/> object, column name
        /// and a value indicating whether the mapping should be case sensitive.
        /// </summary>
        /// <param name="info">The information about the property.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="caseSensitive">A value indicating whether the mappig should be case sensitive.</param>
        public PropertyMap(PropertyInfo info, string columnName, bool caseSensitive)
            : base(info, columnName, caseSensitive)
        {
        }
    }
}
