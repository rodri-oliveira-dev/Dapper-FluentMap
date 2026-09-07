using System;
using System.Linq;
using System.Reflection;

namespace Dapper.FluentMap.Mapping
{
    /// <summary>
    /// Converts database/provider values to property values for one mapped property.
    /// </summary>
    /// <remarks>
    /// Converter instances may be reused by concurrent materialization operations. Implementations should be stateless or otherwise thread-safe.
    /// </remarks>
    /// <typeparam name="TDatabase">The CLR type produced by the database provider.</typeparam>
    /// <typeparam name="TProperty">The mapped property CLR type.</typeparam>
    public interface IReadPropertyConverter<in TDatabase, out TProperty>
    {
        /// <summary>
        /// Converts a non-null database/provider value to a property value.
        /// </summary>
        /// <param name="value">The database/provider value.</param>
        /// <returns>The converted property value.</returns>
        TProperty ConvertFromDatabase(TDatabase value);
    }

    /// <summary>
    /// Converts property values to database/provider values for one mapped property.
    /// </summary>
    /// <remarks>
    /// Converter instances may be reused by concurrent persistence operations when an integration supports write conversion. Implementations should be stateless or otherwise thread-safe.
    /// </remarks>
    /// <typeparam name="TProperty">The mapped property CLR type.</typeparam>
    /// <typeparam name="TDatabase">The CLR type sent to the database provider.</typeparam>
    public interface IWritePropertyConverter<in TProperty, out TDatabase>
    {
        /// <summary>
        /// Converts a non-null property value to a database/provider value.
        /// </summary>
        /// <param name="value">The property value.</param>
        /// <returns>The converted database/provider value.</returns>
        TDatabase ConvertToDatabase(TProperty value);
    }

    /// <summary>
    /// Converts values in both read and write directions for one mapped property.
    /// </summary>
    /// <remarks>
    /// Converter instances may be reused concurrently. Implementations should be stateless or otherwise thread-safe.
    /// </remarks>
    /// <typeparam name="TDatabase">The database/provider CLR type.</typeparam>
    /// <typeparam name="TProperty">The mapped property CLR type.</typeparam>
    public interface IPropertyConverter<TDatabase, TProperty> :
        IReadPropertyConverter<TDatabase, TProperty>,
        IWritePropertyConverter<TProperty, TDatabase>
    {
    }

    /// <summary>
    /// Converts database/provider values to property values using a delegate.
    /// </summary>
    /// <typeparam name="TDatabase">The CLR type produced by the database provider.</typeparam>
    /// <typeparam name="TProperty">The mapped property CLR type.</typeparam>
    /// <param name="value">The database/provider value.</param>
    /// <returns>The converted property value.</returns>
    public delegate TProperty ReadPropertyConverter<in TDatabase, out TProperty>(TDatabase value);

    /// <summary>
    /// Converts property values to database/provider values using a delegate.
    /// </summary>
    /// <typeparam name="TProperty">The mapped property CLR type.</typeparam>
    /// <typeparam name="TDatabase">The CLR type sent to the database provider.</typeparam>
    /// <param name="value">The property value.</param>
    /// <returns>The converted database/provider value.</returns>
    public delegate TDatabase WritePropertyConverter<in TProperty, out TDatabase>(TProperty value);

    /// <summary>
    /// Identifies the direction of a property converter descriptor.
    /// </summary>
    public enum PropertyConversionDirection
    {
        /// <summary>
        /// Database/provider value to property value.
        /// </summary>
        Read,

        /// <summary>
        /// Property value to database/provider value.
        /// </summary>
        Write
    }

    /// <summary>
    /// Describes a configured converter for one property and one conversion direction.
    /// </summary>
    public sealed class PropertyConverterMetadata
    {
        internal PropertyConverterMetadata(
            PropertyConversionDirection direction,
            Type converterType,
            Type databaseType,
            Type propertyType,
            object converter)
        {
            Direction = direction;
            ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
            DatabaseType = databaseType ?? throw new ArgumentNullException(nameof(databaseType));
            PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
            Converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        /// <summary>
        /// Gets the conversion direction represented by this descriptor.
        /// </summary>
        public PropertyConversionDirection Direction { get; }

        /// <summary>
        /// Gets the configured converter type.
        /// </summary>
        public Type ConverterType { get; }

        /// <summary>
        /// Gets the declared database/provider CLR type.
        /// </summary>
        public Type DatabaseType { get; }

        /// <summary>
        /// Gets the declared property CLR type used by the converter.
        /// </summary>
        public Type PropertyType { get; }

        internal object Converter { get; }
    }

    /// <summary>
    /// Describes property converter metadata configured for a mapped property.
    /// </summary>
    public sealed class PropertyConversionMetadata
    {
        /// <summary>
        /// Gets the default conversion metadata for a property without configured converters.
        /// </summary>
        public static readonly PropertyConversionMetadata Default =
            new PropertyConversionMetadata(readConverter: null, writeConverter: null);

        private PropertyConversionMetadata(
            PropertyConverterMetadata readConverter,
            PropertyConverterMetadata writeConverter)
        {
            ReadConverter = readConverter;
            WriteConverter = writeConverter;
        }

        /// <summary>
        /// Gets a value indicating whether a read converter is configured.
        /// </summary>
        public bool HasReadConverter => ReadConverter != null;

        /// <summary>
        /// Gets a value indicating whether a write converter is configured.
        /// </summary>
        public bool HasWriteConverter => WriteConverter != null;

        /// <summary>
        /// Gets the read converter descriptor, or <see langword="null"/> when no read converter is configured.
        /// </summary>
        public PropertyConverterMetadata ReadConverter { get; }

        /// <summary>
        /// Gets the write converter descriptor, or <see langword="null"/> when no write converter is configured.
        /// </summary>
        public PropertyConverterMetadata WriteConverter { get; }

        internal PropertyConversionMetadata WithReadConverter(PropertyConverterMetadata converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (HasReadConverter)
            {
                throw new FluentMapConfigurationException(
                    $"A read converter is already configured for property type '{ReadConverter.PropertyType.FullName}'.");
            }

            return new PropertyConversionMetadata(converter, WriteConverter);
        }

        internal PropertyConversionMetadata WithWriteConverter(PropertyConverterMetadata converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (HasWriteConverter)
            {
                throw new FluentMapConfigurationException(
                    $"A write converter is already configured for property type '{WriteConverter.PropertyType.FullName}'.");
            }

            return new PropertyConversionMetadata(ReadConverter, converter);
        }
    }

    /// <summary>
    /// Exposes property conversion metadata without changing the original <see cref="IPropertyMap"/> contract.
    /// </summary>
    public interface IPropertyMapWithConversionMetadata
    {
        /// <summary>
        /// Gets the configured conversion metadata for the property.
        /// </summary>
        PropertyConversionMetadata Conversion { get; }
    }

    internal static class PropertyMapConversion
    {
        internal static PropertyConversionMetadata GetConversion(IPropertyMap propertyMap)
        {
            if (propertyMap == null)
            {
                throw new ArgumentNullException(nameof(propertyMap));
            }

            var mapWithConversion = propertyMap as IPropertyMapWithConversionMetadata;
            return mapWithConversion == null
                ? PropertyConversionMetadata.Default
                : mapWithConversion.Conversion;
        }

        internal static PropertyConverterMetadata CreateReadConverter<TDatabase, TProperty>(
            ReadPropertyConverter<TDatabase, TProperty> converter,
            Type mappedPropertyType)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            EnsureReadPropertyType(mappedPropertyType, typeof(TProperty), converter.GetType());
            return new PropertyConverterMetadata(
                PropertyConversionDirection.Read,
                typeof(ReadPropertyConverter<TDatabase, TProperty>),
                typeof(TDatabase),
                typeof(TProperty),
                converter);
        }

        internal static PropertyConverterMetadata CreateReadConverter<TDatabase, TProperty>(
            IReadPropertyConverter<TDatabase, TProperty> converter,
            Type mappedPropertyType)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            EnsureReadPropertyType(mappedPropertyType, typeof(TProperty), converter.GetType());
            return new PropertyConverterMetadata(
                PropertyConversionDirection.Read,
                converter.GetType(),
                typeof(TDatabase),
                typeof(TProperty),
                converter);
        }

        internal static PropertyConverterMetadata CreateReadConverter(
            Type converterType,
            Type databaseType,
            Type mappedPropertyType,
            object converter)
        {
            if (converterType == null)
            {
                throw new ArgumentNullException(nameof(converterType));
            }

            if (databaseType == null)
            {
                throw new ArgumentNullException(nameof(databaseType));
            }

            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            var converterInterface = FindConverterInterface(
                converterType,
                typeof(IReadPropertyConverter<,>),
                databaseType,
                mappedPropertyType,
                readDirection: true);

            if (converterInterface == null)
            {
                throw new FluentMapConfigurationException(
                    $"Converter type '{converterType.FullName}' is not compatible with read conversion from database type '{databaseType.FullName}' to mapped property type '{mappedPropertyType.FullName}'.");
            }

            return new PropertyConverterMetadata(
                PropertyConversionDirection.Read,
                converterType,
                converterInterface.GetGenericArguments()[0],
                converterInterface.GetGenericArguments()[1],
                converter);
        }

        internal static PropertyConverterMetadata CreateWriteConverter<TProperty, TDatabase>(
            WritePropertyConverter<TProperty, TDatabase> converter,
            Type mappedPropertyType)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            EnsureWritePropertyType(mappedPropertyType, typeof(TProperty), converter.GetType());
            return new PropertyConverterMetadata(
                PropertyConversionDirection.Write,
                typeof(WritePropertyConverter<TProperty, TDatabase>),
                typeof(TDatabase),
                typeof(TProperty),
                converter);
        }

        internal static PropertyConverterMetadata CreateWriteConverter<TProperty, TDatabase>(
            IWritePropertyConverter<TProperty, TDatabase> converter,
            Type mappedPropertyType)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            EnsureWritePropertyType(mappedPropertyType, typeof(TProperty), converter.GetType());
            return new PropertyConverterMetadata(
                PropertyConversionDirection.Write,
                converter.GetType(),
                typeof(TDatabase),
                typeof(TProperty),
                converter);
        }

        internal static PropertyConverterMetadata CreateWriteConverter(
            Type converterType,
            Type databaseType,
            Type mappedPropertyType,
            object converter)
        {
            if (converterType == null)
            {
                throw new ArgumentNullException(nameof(converterType));
            }

            if (databaseType == null)
            {
                throw new ArgumentNullException(nameof(databaseType));
            }

            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            var converterInterface = FindConverterInterface(
                converterType,
                typeof(IWritePropertyConverter<,>),
                databaseType,
                mappedPropertyType,
                readDirection: false);

            if (converterInterface == null)
            {
                throw new FluentMapConfigurationException(
                    $"Converter type '{converterType.FullName}' is not compatible with write conversion from mapped property type '{mappedPropertyType.FullName}' to database type '{databaseType.FullName}'.");
            }

            return new PropertyConverterMetadata(
                PropertyConversionDirection.Write,
                converterType,
                converterInterface.GetGenericArguments()[1],
                converterInterface.GetGenericArguments()[0],
                converter);
        }

        private static Type FindConverterInterface(
            Type converterType,
            Type interfaceDefinition,
            Type databaseType,
            Type mappedPropertyType,
            bool readDirection)
        {
            var databaseMatches = converterType
                .GetTypeInfo()
                .ImplementedInterfaces
                .Where(type => type.GetTypeInfo().IsGenericType &&
                               type.GetGenericTypeDefinition() == interfaceDefinition)
                .Where(type =>
                {
                    var arguments = type.GetGenericArguments();
                    var converterDatabaseType = readDirection ? arguments[0] : arguments[1];

                    return IsSameOrNullableEquivalent(converterDatabaseType, databaseType);
                })
                .ToList();

            if (databaseMatches.Count == 0)
            {
                return null;
            }

            var matches = databaseMatches
                .Where(type =>
                {
                    var arguments = type.GetGenericArguments();
                    var converterPropertyType = readDirection ? arguments[1] : arguments[0];

                    return readDirection
                        ? CanAssignValue(mappedPropertyType, converterPropertyType)
                        : CanAssignValue(converterPropertyType, mappedPropertyType);
                })
                .ToList();

            if (matches.Count == 0)
            {
                var converterPropertyType = databaseMatches[0].GetGenericArguments()[readDirection ? 1 : 0];
                var reason = readDirection
                    ? $"returns '{converterPropertyType.FullName}', which cannot be assigned to mapped property type '{mappedPropertyType.FullName}'"
                    : $"accepts '{converterPropertyType.FullName}', which is not compatible with mapped property type '{mappedPropertyType.FullName}'";

                throw new FluentMapConfigurationException(
                    $"Converter type '{converterType.FullName}' {reason}.");
            }

            if (matches.Count > 1)
            {
                throw new FluentMapConfigurationException(
                    $"Converter type '{converterType.FullName}' matches more than one compatible '{interfaceDefinition.Name}' contract for property type '{mappedPropertyType.FullName}'.");
            }

            return matches[0];
        }

        private static void EnsureReadPropertyType(Type mappedPropertyType, Type converterPropertyType, Type converterType)
        {
            if (!CanAssignValue(mappedPropertyType, converterPropertyType))
            {
                throw new FluentMapConfigurationException(
                    $"Read converter type '{converterType.FullName}' returns '{converterPropertyType.FullName}', which cannot be assigned to mapped property type '{mappedPropertyType.FullName}'.");
            }
        }

        private static void EnsureWritePropertyType(Type mappedPropertyType, Type converterPropertyType, Type converterType)
        {
            if (!CanAssignValue(converterPropertyType, mappedPropertyType))
            {
                throw new FluentMapConfigurationException(
                    $"Write converter type '{converterType.FullName}' accepts '{converterPropertyType.FullName}', which is not compatible with mapped property type '{mappedPropertyType.FullName}'.");
            }
        }

        private static bool CanAssignValue(Type targetType, Type valueType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (valueType == null)
            {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (IsSameOrNullableEquivalent(targetType, valueType))
            {
                return true;
            }

            return targetType.GetTypeInfo().IsAssignableFrom(valueType.GetTypeInfo());
        }

        private static bool IsSameOrNullableEquivalent(Type left, Type right)
        {
            return left == right || Nullable.GetUnderlyingType(left) == right || Nullable.GetUnderlyingType(right) == left;
        }
    }
}
