using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Dapper.FluentMap.Naming
{
    /// <summary>
    /// Defines a reusable policy for transforming member names into database column names.
    /// </summary>
    public sealed class NamingPolicy
    {
        private readonly Func<string, string> _transformer;

        private NamingPolicy(Func<string, string> transformer)
        {
            if (transformer == null)
            {
                throw new ArgumentNullException(nameof(transformer));
            }

            _transformer = transformer;
        }

        /// <summary>
        /// Gets a policy that preserves member names unchanged.
        /// </summary>
        public static NamingPolicy Identity { get; } = new NamingPolicy(name => name);

        /// <summary>
        /// Gets a policy that converts PascalCase or camelCase member names to snake_case column names.
        /// </summary>
        public static NamingPolicy SnakeCase { get; } = new NamingPolicy(ToSnakeCase);

        /// <summary>
        /// Creates a policy that prepends the specified prefix to member names.
        /// </summary>
        /// <param name="prefix">The prefix to add to the generated column name.</param>
        /// <returns>A naming policy that adds <paramref name="prefix"/>.</returns>
        public static NamingPolicy Prefix(string prefix)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            return new NamingPolicy(name => prefix + name);
        }

        /// <summary>
        /// Creates a policy that appends the specified suffix to member names.
        /// </summary>
        /// <param name="suffix">The suffix to add to the generated column name.</param>
        /// <returns>A naming policy that adds <paramref name="suffix"/>.</returns>
        public static NamingPolicy Suffix(string suffix)
        {
            if (suffix == null)
            {
                throw new ArgumentNullException(nameof(suffix));
            }

            return new NamingPolicy(name => name + suffix);
        }

        /// <summary>
        /// Creates a policy from a custom member-name transformer.
        /// </summary>
        /// <param name="transformer">A function that receives a member name and returns a column name.</param>
        /// <returns>A naming policy that uses <paramref name="transformer"/>.</returns>
        public static NamingPolicy Custom(Func<string, string> transformer)
        {
            return new NamingPolicy(transformer);
        }

        /// <summary>
        /// Composes the current policy with another policy.
        /// </summary>
        /// <param name="next">The next policy to apply.</param>
        /// <returns>A naming policy that applies this policy and then <paramref name="next"/>.</returns>
        public NamingPolicy Then(NamingPolicy next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            return new NamingPolicy(name => next.GetColumnName(GetColumnName(name)));
        }

        /// <summary>
        /// Composes the current policy with a custom member-name transformer.
        /// </summary>
        /// <param name="transformer">The next transformer to apply.</param>
        /// <returns>A naming policy that applies this policy and then <paramref name="transformer"/>.</returns>
        public NamingPolicy Then(Func<string, string> transformer)
        {
            return Then(Custom(transformer));
        }

        /// <summary>
        /// Creates a policy that applies this policy and prepends the specified prefix.
        /// </summary>
        /// <param name="prefix">The prefix to add to the generated column name.</param>
        /// <returns>A naming policy that adds <paramref name="prefix"/> after applying this policy.</returns>
        public NamingPolicy WithPrefix(string prefix)
        {
            return Then(Prefix(prefix));
        }

        /// <summary>
        /// Creates a policy that applies this policy and appends the specified suffix.
        /// </summary>
        /// <param name="suffix">The suffix to add to the generated column name.</param>
        /// <returns>A naming policy that adds <paramref name="suffix"/> after applying this policy.</returns>
        public NamingPolicy WithSuffix(string suffix)
        {
            return Then(Suffix(suffix));
        }

        /// <summary>
        /// Gets the column name for the specified member name.
        /// </summary>
        /// <param name="memberName">The member name to transform.</param>
        /// <returns>The generated column name.</returns>
        public string GetColumnName(string memberName)
        {
            if (memberName == null)
            {
                throw new ArgumentNullException(nameof(memberName));
            }

            return _transformer(memberName);
        }

        private static string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var builder = new StringBuilder(name.Length + 8);

            for (var i = 0; i < name.Length; i++)
            {
                var current = name[i];
                if (char.IsUpper(current))
                {
                    if (ShouldAddUnderscore(name, i))
                    {
                        builder.Append('_');
                    }

                    builder.Append(char.ToLower(current, CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool ShouldAddUnderscore(string name, int index)
        {
            if (index == 0 || name[index - 1] == '_')
            {
                return false;
            }

            var previous = name[index - 1];
            if (char.IsLower(previous) || char.IsDigit(previous))
            {
                return true;
            }

            return index + 1 < name.Length && char.IsLower(name[index + 1]);
        }

        #region EditorBrowsableStates
        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Type GetType()
        {
            return base.GetType();
        }
        #endregion
    }
}
