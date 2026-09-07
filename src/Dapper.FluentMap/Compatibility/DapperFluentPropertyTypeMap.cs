using System;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Compatibility
{
    internal sealed class DapperFluentPropertyTypeMap : SqlMapper.ITypeMap
    {
        private readonly Type _type;
        private readonly Func<Type, string, IPropertyMap> _propertyMapResolver;

        internal DapperFluentPropertyTypeMap(Type type, Func<Type, string, IPropertyMap> propertyMapResolver)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _propertyMapResolver = propertyMapResolver ?? throw new ArgumentNullException(nameof(propertyMapResolver));
        }

        public ConstructorInfo FindConstructor(string[] names, Type[] types)
        {
            return null;
        }

        public ConstructorInfo FindExplicitConstructor()
        {
            return null;
        }

        public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
        {
            return null;
        }

        public SqlMapper.IMemberMap GetMember(string columnName)
        {
            var map = _propertyMapResolver(_type, columnName);
            if (map == null)
            {
                return null;
            }

            var memberPath = PropertyMapIdentity.GetMemberPath(map);
            if (map.Ignored || memberPath.IsNested)
            {
                return new DapperIgnoredMemberMap(columnName);
            }

            return new DapperPropertyMemberMap(columnName, map.PropertyInfo);
        }
    }
}
