using System;

namespace Dapper.FluentMap
{
    internal struct MappingProfileKey : IEquatable<MappingProfileKey>
    {
        internal MappingProfileKey(Type entityType, Type profileType)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (profileType == null)
            {
                throw new ArgumentNullException(nameof(profileType));
            }

            EntityType = entityType;
            ProfileType = profileType;
        }

        internal Type EntityType { get; }

        internal Type ProfileType { get; }

        public bool Equals(MappingProfileKey other)
        {
            return EntityType == other.EntityType && ProfileType == other.ProfileType;
        }

        public override bool Equals(object obj)
        {
            return obj is MappingProfileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EntityType == null ? 0 : EntityType.GetHashCode()) * 397) ^
                       (ProfileType == null ? 0 : ProfileType.GetHashCode());
            }
        }
    }
}
