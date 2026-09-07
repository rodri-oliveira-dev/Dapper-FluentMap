using System;

namespace Dapper.FluentMap.Mapping
{
    internal interface IPropertyMapWithMemberPath
    {
        MemberPath MemberPath { get; }

        void SetMemberPath(MemberPath memberPath);
    }

    internal static class PropertyMapIdentity
    {
        internal static MemberPath GetMemberPath(IPropertyMap propertyMap)
        {
            if (propertyMap == null)
            {
                throw new ArgumentNullException(nameof(propertyMap));
            }

            var mapWithPath = propertyMap as IPropertyMapWithMemberPath;
            if (mapWithPath != null && mapWithPath.MemberPath != null)
            {
                return mapWithPath.MemberPath;
            }

            return MemberPath.ForProperty(propertyMap.PropertyInfo);
        }

        internal static void SetMemberPath(IPropertyMap propertyMap, MemberPath memberPath)
        {
            if (propertyMap == null)
            {
                throw new ArgumentNullException(nameof(propertyMap));
            }

            if (memberPath == null)
            {
                throw new ArgumentNullException(nameof(memberPath));
            }

            var mapWithPath = propertyMap as IPropertyMapWithMemberPath;
            mapWithPath?.SetMemberPath(memberPath);
        }
    }
}
