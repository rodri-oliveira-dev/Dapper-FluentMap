using System;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.TypeMaps
{
    internal sealed class FluentConstructorTypeMap : SqlMapper.ITypeMap
    {
        private readonly Type _type;
        private readonly Func<Type, string, IPropertyMap> _propertyMapResolver;
        private readonly DefaultTypeMap _defaultTypeMap;

        internal FluentConstructorTypeMap(Type type, Func<Type, string, IPropertyMap> propertyMapResolver)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (propertyMapResolver == null)
            {
                throw new ArgumentNullException(nameof(propertyMapResolver));
            }

            _type = type;
            _propertyMapResolver = propertyMapResolver;
            _defaultTypeMap = new DefaultTypeMap(type);
        }

        public ConstructorInfo FindConstructor(string[] names, Type[] types)
        {
            var effectiveNames = new string[names.Length];
            var effectiveTypes = new Type[types.Length];
            var hasMappedColumn = false;

            for (var i = 0; i < names.Length; i++)
            {
                var map = GetSimplePropertyMap(names[i]);

                if (map != null && !map.Ignored)
                {
                    effectiveNames[i] = map.PropertyInfo.Name;
                    effectiveTypes[i] = map.PropertyInfo.PropertyType;
                    hasMappedColumn = true;
                    continue;
                }

                effectiveNames[i] = names[i];
                effectiveTypes[i] = types[i];
            }

            return hasMappedColumn
                ? _defaultTypeMap.FindConstructor(effectiveNames, effectiveTypes)
                : null;
        }

        public ConstructorInfo FindExplicitConstructor()
        {
            return null;
        }

        public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
        {
            var map = GetSimplePropertyMap(columnName);
            if (map == null || map.Ignored)
            {
                return null;
            }

            var parameter = MatchParameter(constructor.GetParameters(), map.PropertyInfo.Name);
            return parameter == null
                ? null
                : new ConstructorParameterMap(columnName, parameter);
        }

        public SqlMapper.IMemberMap GetMember(string columnName)
        {
            return null;
        }

        private IPropertyMap GetSimplePropertyMap(string columnName)
        {
            var map = _propertyMapResolver(_type, columnName);
            if (map == null)
            {
                return null;
            }

            var memberPath = PropertyMapIdentity.GetMemberPath(map);
            return memberPath.IsNested ? null : map;
        }

        internal static ParameterInfo MatchParameter(ParameterInfo[] parameters, string memberName)
        {
            return parameters.FirstOrDefault(p => string.Equals(p.Name, memberName, StringComparison.Ordinal))
                ?? parameters.FirstOrDefault(p => string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase))
                ?? MatchParameterWithUnderscores(parameters, memberName);
        }

        private static ParameterInfo MatchParameterWithUnderscores(ParameterInfo[] parameters, string memberName)
        {
            if (!DefaultTypeMap.MatchNamesWithUnderscores)
            {
                return null;
            }

            var effectiveMemberName = memberName.Replace("_", string.Empty);
            return parameters.FirstOrDefault(p => string.Equals(p.Name, effectiveMemberName, StringComparison.Ordinal))
                ?? parameters.FirstOrDefault(p => string.Equals(p.Name, effectiveMemberName, StringComparison.OrdinalIgnoreCase))
                ?? parameters.FirstOrDefault(p => string.Equals(RemoveUnderscores(p.Name), effectiveMemberName, StringComparison.Ordinal))
                ?? parameters.FirstOrDefault(p => string.Equals(RemoveUnderscores(p.Name), effectiveMemberName, StringComparison.OrdinalIgnoreCase));
        }

        private static string RemoveUnderscores(string value)
        {
            return value == null ? null : value.Replace("_", string.Empty);
        }
    }
}
