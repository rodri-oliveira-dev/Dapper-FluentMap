using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Dapper.FluentMap.Mapping
{
    internal sealed class MemberPath : IEquatable<MemberPath>
    {
        private readonly PropertyInfo[] _properties;
        private readonly ReadOnlyCollection<PropertyInfo> _readOnlyProperties;
        private readonly int _hashCode;

        private MemberPath(IEnumerable<PropertyInfo> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            _properties = properties.ToArray();
            if (_properties.Length == 0)
            {
                throw new ArgumentException("A member path must contain at least one property.", nameof(properties));
            }

            if (_properties.Any(p => p == null))
            {
                throw new ArgumentException("A member path cannot contain null properties.", nameof(properties));
            }

            _readOnlyProperties = new ReadOnlyCollection<PropertyInfo>(_properties);
            _hashCode = CalculateHashCode(_properties);
        }

        internal IReadOnlyList<PropertyInfo> Properties => _readOnlyProperties;

        internal PropertyInfo PropertyInfo => _properties[_properties.Length - 1];

        internal bool IsNested => _properties.Length > 1;

        internal static MemberPath ForProperty(PropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            return new MemberPath(new[] { property });
        }

        internal static MemberPath FromProperties(IEnumerable<PropertyInfo> properties)
        {
            return new MemberPath(properties);
        }

        public bool Equals(MemberPath other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || _properties.Length != other._properties.Length)
            {
                return false;
            }

            for (var i = 0; i < _properties.Length; i++)
            {
                if (!MemberEquals(_properties[i], other._properties[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is MemberPath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            return string.Join(".", _properties.Select(p => p.Name));
        }

        private static bool MemberEquals(PropertyInfo left, PropertyInfo right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (HasSameMetadataIdentity(left, right))
            {
                return true;
            }

            return left.Equals(right);
        }

        private static bool HasSameMetadataIdentity(PropertyInfo left, PropertyInfo right)
        {
            try
            {
                return Equals(left.Module, right.Module) &&
                       left.MetadataToken == right.MetadataToken &&
                       Equals(left.DeclaringType, right.DeclaringType);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static int CalculateHashCode(PropertyInfo[] properties)
        {
            unchecked
            {
                var hash = 17;

                foreach (var property in properties)
                {
                    hash = (hash * 31) + GetMemberHashCode(property);
                }

                return hash;
            }
        }

        private static int GetMemberHashCode(PropertyInfo property)
        {
            try
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + (property.Module == null ? 0 : property.Module.GetHashCode());
                    hash = (hash * 31) + property.MetadataToken;
                    hash = (hash * 31) + (property.DeclaringType == null ? 0 : property.DeclaringType.GetHashCode());
                    return hash;
                }
            }
            catch (InvalidOperationException)
            {
                return property.GetHashCode();
            }
        }
    }
}
