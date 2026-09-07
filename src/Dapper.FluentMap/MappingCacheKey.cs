using System;

namespace Dapper.FluentMap
{
    internal struct MappingCacheKey : IEquatable<MappingCacheKey>
    {
        private MappingCacheKey(Type type, Type profileType, string columnName, MappingCacheOptions options)
        {
            Type = type;
            ProfileType = profileType;
            ColumnName = columnName;
            Options = options;
        }

        internal Type Type { get; }

        internal Type ProfileType { get; }

        internal string ColumnName { get; }

        internal MappingCacheOptions Options { get; }

        internal static MappingCacheKey FluentMap(Type type, string columnName)
        {
            return new MappingCacheKey(type, null, columnName, MappingCacheOptions.FluentMap);
        }

        internal static MappingCacheKey ProfileMap(Type type, Type profileType, string columnName)
        {
            return new MappingCacheKey(type, profileType, columnName, MappingCacheOptions.ProfileMap);
        }

        internal static MappingCacheKey ConventionOnly(Type type, string columnName)
        {
            return new MappingCacheKey(type, null, columnName, MappingCacheOptions.ConventionOnly);
        }

        public bool Equals(MappingCacheKey other)
        {
            return Type == other.Type &&
                   ProfileType == other.ProfileType &&
                   string.Equals(ColumnName, other.ColumnName, StringComparison.Ordinal) &&
                   Options.Equals(other.Options);
        }

        public override bool Equals(object obj)
        {
            return obj is MappingCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (Type == null ? 0 : Type.GetHashCode());
                hash = (hash * 31) + (ProfileType == null ? 0 : ProfileType.GetHashCode());
                hash = (hash * 31) + (ColumnName == null ? 0 : ColumnName.GetHashCode());
                hash = (hash * 31) + Options.GetHashCode();
                return hash;
            }
        }
    }

    internal struct MappingCacheOptions : IEquatable<MappingCacheOptions>
    {
        private readonly MappingCacheStrategy _strategy;

        private MappingCacheOptions(MappingCacheStrategy strategy)
        {
            _strategy = strategy;
        }

        internal static MappingCacheOptions FluentMap { get; } =
            new MappingCacheOptions(MappingCacheStrategy.FluentMap);

        internal static MappingCacheOptions ConventionOnly { get; } =
            new MappingCacheOptions(MappingCacheStrategy.ConventionOnly);

        internal static MappingCacheOptions ProfileMap { get; } =
            new MappingCacheOptions(MappingCacheStrategy.ProfileMap);

        public bool Equals(MappingCacheOptions other)
        {
            return _strategy == other._strategy;
        }

        public override bool Equals(object obj)
        {
            return obj is MappingCacheOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)_strategy;
        }
    }

    internal enum MappingCacheStrategy
    {
        FluentMap,
        ConventionOnly,
        ProfileMap
    }
}
