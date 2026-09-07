using System;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Dapper.FluentMap.Compatibility
{
    internal static class DapperTypeHandlerAdapter
    {
        private const string TypeHandlerCacheName = "TypeHandlerCache`1";

        internal static bool HasTypeHandler(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            return SqlMapper.HasTypeHandler(GetHandlerType(targetType));
        }

        internal static Func<object, object> CreateConverter(Type targetType)
        {
            return CreateConverter(targetType, ResolveTypeHandlerCacheDefinition);
        }

        internal static Func<object, object> CreateConverter(Type targetType, Func<Type> cacheDefinitionResolver)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (cacheDefinitionResolver == null)
            {
                throw new ArgumentNullException(nameof(cacheDefinitionResolver));
            }

            var handlerType = GetHandlerType(targetType);
            var cacheTypeDefinition = cacheDefinitionResolver();
            if (cacheTypeDefinition == null)
            {
                throw CreateCompatibilityException(targetType, "nested TypeHandlerCache<T> type was not found");
            }

            MethodInfo parse;
            try
            {
                var cacheType = cacheTypeDefinition.MakeGenericType(handlerType);
                parse = cacheType.GetMethod(
                    "Parse",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(object) },
                    null);
            }
            catch (Exception exception)
            {
                throw CreateCompatibilityException(targetType, "TypeHandlerCache<T>.Parse could not be resolved", exception);
            }

            if (parse == null)
            {
                throw CreateCompatibilityException(targetType, "TypeHandlerCache<T>.Parse(object) was not found");
            }

            return CreateParseDelegate(targetType, parse);
        }

        private static Func<object, object> CreateParseDelegate(Type targetType, MethodInfo parse)
        {
            var value = Expression.Parameter(typeof(object), "value");
            var nullValue = Expression.Constant(GetDefaultValue(targetType), typeof(object));
            var body = Expression.Condition(
                Expression.OrElse(
                    Expression.Equal(value, Expression.Constant(null, typeof(object))),
                    Expression.Equal(value, Expression.Constant(DBNull.Value, typeof(object)))),
                nullValue,
                Expression.Convert(Expression.Call(parse, value), typeof(object)));

            return Expression.Lambda<Func<object, object>>(body, value).Compile();
        }

        private static Type ResolveTypeHandlerCacheDefinition()
        {
            return typeof(SqlMapper).GetNestedType(TypeHandlerCacheName, BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type GetHandlerType(Type targetType)
        {
            return Nullable.GetUnderlyingType(targetType) ?? targetType;
        }

        private static object GetDefaultValue(Type type)
        {
            if (!type.GetTypeInfo().IsValueType || Nullable.GetUnderlyingType(type) != null)
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }

        private static FluentMapConfigurationException CreateCompatibilityException(Type targetType, string reason, Exception innerException = null)
        {
            var message =
                $"Dapper TypeHandler compatibility failed for target type '{targetType.FullName}': {reason}. " +
                "This FluentMap version expects Dapper to expose SqlMapper.TypeHandlerCache<T>.Parse(object). " +
                "Review the Dapper compatibility boundary before upgrading Dapper.";

            return innerException == null
                ? new FluentMapConfigurationException(message)
                : new FluentMapConfigurationException(message, innerException);
        }
    }
}
