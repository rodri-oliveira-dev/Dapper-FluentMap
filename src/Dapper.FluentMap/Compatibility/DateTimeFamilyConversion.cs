using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Dapper.FluentMap.Compatibility
{
    internal static class DateTimeFamilyConversion
    {
        private const string DateOnlyTypeName = "System.DateOnly";
        private const string TimeOnlyTypeName = "System.TimeOnly";

        private static readonly ConcurrentDictionary<ConversionKey, Func<object, object>> Converters =
            new ConcurrentDictionary<ConversionKey, Func<object, object>>();

        internal static bool TryConvert(object value, Type targetType, out object converted)
        {
            var sourceType = value.GetType();
            var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (!IsSupported(sourceType, conversionType))
            {
                converted = null;
                return false;
            }

            converted = Converters.GetOrAdd(
                new ConversionKey(sourceType, conversionType),
                CreateConverter)(value);
            return true;
        }

        private static bool IsSupported(Type sourceType, Type targetType)
        {
            return (sourceType == typeof(DateTime) &&
                    (targetType.FullName == DateOnlyTypeName || targetType.FullName == TimeOnlyTypeName)) ||
                   (sourceType.FullName == DateOnlyTypeName && targetType == typeof(DateTime)) ||
                   (sourceType == typeof(TimeSpan) && targetType.FullName == TimeOnlyTypeName) ||
                   (sourceType.FullName == TimeOnlyTypeName && targetType == typeof(TimeSpan));
        }

        private static Func<object, object> CreateConverter(ConversionKey key)
        {
            var value = Expression.Parameter(typeof(object), "value");
            Expression conversion;

            if (key.SourceType == typeof(DateTime))
            {
                conversion = Expression.Call(
                    GetRequiredMethod(key.TargetType, "FromDateTime", BindingFlags.Public | BindingFlags.Static, key.SourceType),
                    Expression.Convert(value, key.SourceType));
            }
            else if (key.SourceType == typeof(TimeSpan))
            {
                conversion = Expression.Call(
                    GetRequiredMethod(key.TargetType, "FromTimeSpan", BindingFlags.Public | BindingFlags.Static, key.SourceType),
                    Expression.Convert(value, key.SourceType));
            }
            else if (key.TargetType == typeof(DateTime))
            {
                var timeOnlyType = key.SourceType.Assembly.GetType(TimeOnlyTypeName, throwOnError: true);
                var midnight = Activator.CreateInstance(timeOnlyType);
                conversion = Expression.Call(
                    Expression.Convert(value, key.SourceType),
                    GetRequiredMethod(key.SourceType, "ToDateTime", BindingFlags.Public | BindingFlags.Instance, timeOnlyType),
                    Expression.Convert(Expression.Constant(midnight), timeOnlyType));
            }
            else
            {
                conversion = Expression.Call(
                    Expression.Convert(value, key.SourceType),
                    GetRequiredMethod(key.SourceType, "ToTimeSpan", BindingFlags.Public | BindingFlags.Instance));
            }

            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(conversion, typeof(object)),
                value).Compile();
        }

        private static MethodInfo GetRequiredMethod(
            Type declaringType,
            string name,
            BindingFlags bindingFlags,
            params Type[] parameterTypes)
        {
            var method = declaringType.GetMethod(name, bindingFlags, null, parameterTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(
                    declaringType.FullName,
                    name + "(" + string.Join(", ", (object[])parameterTypes) + ")");
            }

            return method;
        }

        private readonly struct ConversionKey : IEquatable<ConversionKey>
        {
            internal ConversionKey(Type sourceType, Type targetType)
            {
                SourceType = sourceType;
                TargetType = targetType;
            }

            internal Type SourceType { get; }

            internal Type TargetType { get; }

            public bool Equals(ConversionKey other)
            {
                return SourceType == other.SourceType && TargetType == other.TargetType;
            }

            public override bool Equals(object obj)
            {
                return obj is ConversionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceType.GetHashCode() * 397) ^ TargetType.GetHashCode();
                }
            }
        }
    }
}
